using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Infrastructure.Obs;

public sealed class ObsWebSocketController(IProtectedCredentialStore credentialStore) : IObsController
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
    ];

    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pendingRequests = new();
    private readonly object reconnectSync = new();
    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCancellation;
    private Task? receiveTask;
    private Task? reconnectTask;
    private CancellationTokenSource reconnectCancellation = new();
    private ObsConnectionSettings? settings;
    private volatile bool maintainConnection;
    private bool disposed;
    private ObsConnectionState state = ObsConnectionState.Disconnected;

    public ObsConnectionState State => state;

    public string? LastError { get; private set; }

    public event EventHandler<ObsConnectionStateChangedEventArgs>? StateChanged;

    public async Task ConnectAsync(
        ObsConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        Guard.NotNull(settings, nameof(settings));
        this.settings = settings;
        maintainConnection = true;
        lock (reconnectSync)
        {
            if (reconnectCancellation.IsCancellationRequested)
            {
                reconnectCancellation.Dispose();
                reconnectCancellation = new CancellationTokenSource();
            }
        }

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket?.State is WebSocketState.Open && State is ObsConnectionState.Connected)
            {
                return;
            }

            SetState(ObsConnectionState.Connecting);
            await ConnectCoreAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetState(ObsConnectionState.Faulted, exception.Message);
            if (maintainConnection)
            {
                ScheduleReconnect();
            }
            throw;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        maintainConnection = false;
        Task? activeReconnectTask;
        lock (reconnectSync)
        {
            reconnectCancellation.Cancel();
            activeReconnectTask = reconnectTask;
        }
        Task? activeReceiveTask;

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            activeReceiveTask = receiveTask;
            receiveCancellation?.Cancel();
            if (socket?.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "StreamNumDeck disconnected",
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is WebSocketException or ObjectDisposedException)
                {
                }
            }
        }
        finally
        {
            lifecycleGate.Release();
        }

        if (activeReceiveTask is not null)
        {
            try
            {
                await activeReceiveTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
            }
        }

        if (activeReconnectTask is not null)
        {
            try
            {
                await activeReconnectTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
            }

            lock (reconnectSync)
            {
                if (ReferenceEquals(reconnectTask, activeReconnectTask))
                {
                    reconnectTask = null;
                }
            }
        }

        SetState(ObsConnectionState.Disconnected);
    }

    public async Task ExecuteAsync(ObsActionDefinition action, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(action, nameof(action));
        EnsureConnected();

        if (action.Action is ObsActionKind.ToggleSourceVisibility)
        {
            await ToggleSourceVisibilityAsync(action.TargetName!, cancellationToken).ConfigureAwait(false);
            return;
        }

        var (requestType, requestData) = ObsProtocol.MapActionRequest(action);
        await SendRequestAsync(requestType, requestData, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ObsCatalog> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var sceneList = await SendRequestAsync("GetSceneList", null, cancellationToken).ConfigureAwait(false);
        var inputList = await SendRequestAsync("GetInputList", null, cancellationToken).ConfigureAwait(false);

        var scenes = ReadNameArray(sceneList, "scenes", "sceneName");
        var inputs = ReadNameArray(inputList, "inputs", "inputName");
        var sources = new HashSet<string>(inputs, StringComparer.Ordinal);

        foreach (var sceneName in scenes)
        {
            var data = JsonSerializer.SerializeToElement(new { sceneName });
            var items = await SendRequestAsync("GetSceneItemList", data, cancellationToken).ConfigureAwait(false);
            foreach (var source in ReadNameArray(items, "sceneItems", "sourceName"))
            {
                sources.Add(source);
            }
        }

        return new ObsCatalog(scenes, inputs, sources.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        if (reconnectTask is not null)
        {
            try
            {
                await reconnectTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }
        socket?.Dispose();
        socket = null;
        receiveCancellation?.Dispose();
        receiveCancellation = null;
        reconnectCancellation.Dispose();
        lifecycleGate.Dispose();
        sendGate.Dispose();
    }

    private async Task ConnectCoreAsync(
        ObsConnectionSettings connectionSettings,
        CancellationToken cancellationToken)
    {
        socket?.Dispose();
        socket = null;
        receiveCancellation?.Dispose();
        receiveCancellation = null;

        var candidate = new ClientWebSocket();
        candidate.Options.KeepAliveInterval = KeepAliveInterval;
#if !NETFRAMEWORK
        candidate.Options.KeepAliveTimeout = KeepAliveTimeout;
#endif
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);

        try
        {
            await candidate.ConnectAsync(
                new Uri($"ws://{connectionSettings.Host}:{connectionSettings.Port}"),
                timeout.Token).ConfigureAwait(false);

            var hello = await ReceiveMessageAsync(candidate, timeout.Token).ConfigureAwait(false);
            using var helloDocument = JsonDocument.Parse(hello);
            var helloRoot = helloDocument.RootElement;
            if (helloRoot.GetProperty("op").GetInt32() != 0)
            {
                throw new InvalidDataException("OBS did not send the expected Hello message.");
            }

            var helloData = helloRoot.GetProperty("d");
            string? authentication = null;
            if (helloData.TryGetProperty("authentication", out var authenticationData))
            {
                var password = await credentialStore.GetAsync(
                    connectionSettings.CredentialKey,
                    timeout.Token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException("Для OBS задан пароль, но он не сохранён в настройках StreamNumDeck.");
                }

                authentication = ObsProtocol.CreateAuthentication(
                    password!,
                    authenticationData.GetProperty("salt").GetString()!,
                    authenticationData.GetProperty("challenge").GetString()!);
            }

            var identify = authentication is null
                ? JsonSerializer.Serialize(new { op = 1, d = new { rpcVersion = ObsProtocol.RpcVersion } })
                : JsonSerializer.Serialize(new { op = 1, d = new { rpcVersion = ObsProtocol.RpcVersion, authentication } });
            await SendTextAsync(candidate, identify, timeout.Token).ConfigureAwait(false);

            var identified = await ReceiveMessageAsync(candidate, timeout.Token).ConfigureAwait(false);
            using var identifiedDocument = JsonDocument.Parse(identified);
            if (identifiedDocument.RootElement.GetProperty("op").GetInt32() != 2)
            {
                throw new InvalidDataException("OBS rejected the WebSocket identification.");
            }

            socket = candidate;
            receiveCancellation = new CancellationTokenSource();
            LastError = null;
            SetState(ObsConnectionState.Connected);
            receiveTask = ReceiveLoopAsync(candidate, receiveCancellation.Token);
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    private async Task<JsonElement> SendRequestAsync(
        string requestType,
        JsonElement? requestData,
        CancellationToken cancellationToken)
    {
        var activeSocket = socket;
        if (activeSocket?.State is not WebSocketState.Open)
        {
            throw new InvalidOperationException("OBS не подключён.");
        }

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pendingRequests.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Could not register an OBS request.");
        }

        var envelope = requestData.HasValue
            ? JsonSerializer.Serialize(new { op = 6, d = new { requestType, requestId, requestData = requestData.Value } })
            : JsonSerializer.Serialize(new { op = 6, d = new { requestType, requestId } });

        try
        {
            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await SendTextAsync(activeSocket, envelope, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                sendGate.Release();
            }

            return await completion.Task.WaitAsync(RequestTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            pendingRequests.TryRemove(requestId, out _);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket activeSocket, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested && activeSocket.State is WebSocketState.Open)
            {
                var json = await ReceiveMessageAsync(activeSocket, cancellationToken).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.GetProperty("op").GetInt32() != 7)
                {
                    continue;
                }

                var response = root.GetProperty("d");
                var requestId = response.GetProperty("requestId").GetString();
                if (requestId is null || !pendingRequests.TryRemove(requestId, out var completion))
                {
                    continue;
                }

                var status = response.GetProperty("requestStatus");
                if (!status.GetProperty("result").GetBoolean())
                {
                    completion.TrySetException(new ObsRequestException(
                        response.GetProperty("requestType").GetString() ?? "Unknown",
                        status.GetProperty("code").GetInt32(),
                        status.TryGetProperty("comment", out var comment) ? comment.GetString() : null));
                    continue;
                }

                completion.TrySetResult(
                    response.TryGetProperty("responseData", out var data)
                        ? data.Clone()
                        : JsonSerializer.SerializeToElement(new { }));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            var exception = failure ?? new IOException("Соединение с OBS закрыто.");
            foreach (var pair in pendingRequests)
            {
                pair.Value.TrySetException(exception);
            }

            if (ReferenceEquals(socket, activeSocket))
            {
                socket = null;
                try
                {
                    activeSocket.Abort();
                }
                catch
                {
                }

                activeSocket.Dispose();
            }

            if (maintainConnection && !disposed)
            {
                SetState(ObsConnectionState.Reconnecting, failure?.Message);
                ScheduleReconnect();
            }
            else if (!disposed)
            {
                SetState(ObsConnectionState.Disconnected);
            }
        }
    }

    private void ScheduleReconnect()
    {
        lock (reconnectSync)
        {
            if (reconnectTask is { IsCompleted: false })
            {
                return;
            }

            reconnectTask = ReconnectLoopAsync();
        }
    }

    private async Task ReconnectLoopAsync()
    {
        CancellationToken cancellationToken;
        lock (reconnectSync)
        {
            cancellationToken = reconnectCancellation.Token;
        }

        var attempt = 0;
        while (maintainConnection && !disposed && settings is not null)
        {
            try
            {
                await Task.Delay(
                    ReconnectDelays[Math.Min(attempt, ReconnectDelays.Length - 1)],
                    cancellationToken).ConfigureAwait(false);
                await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (!maintainConnection || disposed)
                    {
                        return;
                    }

                    if (socket?.State is WebSocketState.Open
                        && State is ObsConnectionState.Connected
                        && receiveTask is { IsCompleted: false })
                    {
                        return;
                    }

                    SetState(ObsConnectionState.Reconnecting);
                    await ConnectCoreAsync(settings, cancellationToken).ConfigureAwait(false);
                    return;
                }
                finally
                {
                    lifecycleGate.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                SetState(ObsConnectionState.Reconnecting, exception.Message);
                attempt++;
            }
        }
    }

    private async Task ToggleSourceVisibilityAsync(string sourceName, CancellationToken cancellationToken)
    {
        var currentScene = await SendRequestAsync("GetCurrentProgramScene", null, cancellationToken).ConfigureAwait(false);
        var sceneName = (currentScene.TryGetProperty("currentProgramSceneName", out var currentName)
                ? currentName.GetString()
                : null)
            ?? (currentScene.TryGetProperty("sceneName", out var legacyName)
                ? legacyName.GetString()
                : null)
            ?? throw new InvalidDataException("OBS did not return the current scene name.");

        var item = await SendRequestAsync(
            "GetSceneItemId",
            JsonSerializer.SerializeToElement(new { sceneName, sourceName }),
            cancellationToken).ConfigureAwait(false);
        var sceneItemId = item.GetProperty("sceneItemId").GetInt32();

        var enabledResponse = await SendRequestAsync(
            "GetSceneItemEnabled",
            JsonSerializer.SerializeToElement(new { sceneName, sceneItemId }),
            cancellationToken).ConfigureAwait(false);
        var enabled = enabledResponse.GetProperty("sceneItemEnabled").GetBoolean();

        await SendRequestAsync(
            "SetSceneItemEnabled",
            JsonSerializer.SerializeToElement(new { sceneName, sceneItemId, sceneItemEnabled = !enabled }),
            cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> ReadNameArray(JsonElement root, string arrayName, string valueName)
    {
        if (!root.TryGetProperty(arrayName, out var array))
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(item => item.TryGetProperty(valueName, out var value) ? value.GetString() : null)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task SendTextAsync(
        ClientWebSocket target,
        string text,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await target.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReceiveMessageAsync(
        ClientWebSocket target,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await target.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType is WebSocketMessageType.Close)
            {
                throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
            }

            if (result.MessageType is not WebSocketMessageType.Text)
            {
                throw new InvalidDataException("OBS sent a non-text WebSocket message.");
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
    }

    private void EnsureConnected()
    {
        if (socket?.State is not WebSocketState.Open || State is not ObsConnectionState.Connected)
        {
            throw new InvalidOperationException("OBS не подключён.");
        }
    }

    private void SetState(ObsConnectionState newState, string? error = null)
    {
        state = newState;
        LastError = error;
        StateChanged?.Invoke(this, new ObsConnectionStateChangedEventArgs(newState, error));
    }
}
