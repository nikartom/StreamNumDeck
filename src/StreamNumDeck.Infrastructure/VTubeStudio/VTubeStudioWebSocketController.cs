using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.Core.VTubeStudio;

namespace StreamNumDeck.Infrastructure.VTubeStudio;

public sealed class VTubeStudioWebSocketController(IProtectedCredentialStore credentialStore)
    : IVTubeStudioController
{
    private const string ApiName = "VTubeStudioPublicAPI";
    private const string ApiVersion = "1.0";
    private const string PluginName = "StreamNumDeck";
    private const string PluginDeveloper = "nikartom";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(2);

    public async Task<VTubeStudioCatalog> GetCatalogAsync(
        VTubeStudioConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(settings, nameof(settings));
        using var socket = await OpenAuthenticatedSocketAsync(settings, cancellationToken).ConfigureAwait(false);

        var currentModel = await SendRequestAsync(
            socket,
            "CurrentModelRequest",
            data: null,
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);
        var availableModels = await SendRequestAsync(
            socket,
            "AvailableModelsRequest",
            data: null,
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);

        var modelLoaded = currentModel.TryGetProperty("modelLoaded", out var loadedElement)
                          && loadedElement.GetBoolean();
        var currentModelName = modelLoaded
            && currentModel.TryGetProperty("modelName", out var modelNameElement)
                ? modelNameElement.GetString()
                : null;

        IReadOnlyList<VTubeStudioHotkey> hotkeys = [];
        if (modelLoaded)
        {
            var response = await SendRequestAsync(
                socket,
                "HotkeysInCurrentModelRequest",
                data: null,
                RequestTimeout,
                cancellationToken).ConfigureAwait(false);
            hotkeys = ReadHotkeys(response);
        }

        return new VTubeStudioCatalog(currentModelName, hotkeys, ReadModels(availableModels));
    }

    public async Task ExecuteAsync(
        VTubeStudioConnectionSettings settings,
        VTubeStudioActionDefinition action,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(settings, nameof(settings));
        Guard.NotNull(action, nameof(action));
        using var socket = await OpenAuthenticatedSocketAsync(settings, cancellationToken).ConfigureAwait(false);

        var (messageType, data) = action.Action switch
        {
            VTubeStudioActionKind.TriggerHotkey => (
                "HotkeyTriggerRequest",
                JsonSerializer.SerializeToElement(new { hotkeyID = action.TargetId })),
            VTubeStudioActionKind.LoadModel => (
                "ModelLoadRequest",
                JsonSerializer.SerializeToElement(new { modelID = action.TargetId })),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Action, "Unsupported VTube Studio action."),
        };

        await SendRequestAsync(
            socket,
            messageType,
            data,
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ClientWebSocket> OpenAuthenticatedSocketAsync(
        VTubeStudioConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);

        try
        {
            await socket.ConnectAsync(
                new Uri($"ws://{settings.Host}:{settings.Port}"),
                timeout.Token).ConfigureAwait(false);
            await AuthenticateAsync(socket, settings, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private async Task AuthenticateAsync(
        ClientWebSocket socket,
        VTubeStudioConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var token = await credentialStore
            .GetAsync(settings.CredentialKey, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token)
            && await TryAuthenticateAsync(socket, token!, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            await credentialStore.DeleteAsync(settings.CredentialKey, cancellationToken).ConfigureAwait(false);
        }

        var tokenResponse = await SendRequestAsync(
            socket,
            "AuthenticationTokenRequest",
            JsonSerializer.SerializeToElement(new
            {
                pluginName = PluginName,
                pluginDeveloper = PluginDeveloper,
            }),
            AuthorizationTimeout,
            cancellationToken).ConfigureAwait(false);
        token = tokenResponse.TryGetProperty("authenticationToken", out var tokenElement)
            ? tokenElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidDataException("VTube Studio did not return an authentication token.");
        }

        await credentialStore.SetAsync(settings.CredentialKey, token!, cancellationToken).ConfigureAwait(false);
        if (!await TryAuthenticateAsync(socket, token!, cancellationToken).ConfigureAwait(false))
        {
            await credentialStore.DeleteAsync(settings.CredentialKey, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("VTube Studio rejected the new authentication token.");
        }
    }

    private static async Task<bool> TryAuthenticateAsync(
        ClientWebSocket socket,
        string token,
        CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync(
            socket,
            "AuthenticationRequest",
            JsonSerializer.SerializeToElement(new
            {
                pluginName = PluginName,
                pluginDeveloper = PluginDeveloper,
                authenticationToken = token,
            }),
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);
        return response.TryGetProperty("authenticated", out var authenticated)
               && authenticated.GetBoolean();
    }

    private static async Task<JsonElement> SendRequestAsync(
        ClientWebSocket socket,
        string messageType,
        JsonElement? data,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var envelope = data.HasValue
            ? JsonSerializer.Serialize(new
            {
                apiName = ApiName,
                apiVersion = ApiVersion,
                requestID = requestId,
                messageType,
                data = data.Value,
            })
            : JsonSerializer.Serialize(new
            {
                apiName = ApiName,
                apiVersion = ApiVersion,
                requestID = requestId,
                messageType,
            });

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(timeout);
        await SendTextAsync(socket, envelope, requestTimeout.Token).ConfigureAwait(false);

        while (true)
        {
            var json = await ReceiveMessageAsync(socket, requestTimeout.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("requestID", out var responseId)
                || !string.Equals(responseId.GetString(), requestId, StringComparison.Ordinal))
            {
                continue;
            }

            var responseType = root.TryGetProperty("messageType", out var typeElement)
                ? typeElement.GetString()
                : null;
            if (string.Equals(responseType, "APIError", StringComparison.Ordinal))
            {
                var errorData = root.GetProperty("data");
                throw new VTubeStudioRequestException(
                    messageType,
                    errorData.TryGetProperty("errorID", out var errorId) ? errorId.GetInt32() : -1,
                    errorData.TryGetProperty("message", out var errorMessage) ? errorMessage.GetString() : null);
            }

            return root.TryGetProperty("data", out var responseData)
                ? responseData.Clone()
                : JsonSerializer.SerializeToElement(new { });
        }
    }

    private static IReadOnlyList<VTubeStudioHotkey> ReadHotkeys(JsonElement data)
    {
        if (!data.TryGetProperty("availableHotkeys", out var array))
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(item => new VTubeStudioHotkey(
                item.TryGetProperty("hotkeyID", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty))
            .Where(static hotkey => hotkey.Id.Length > 0)
            .OrderBy(static hotkey => hotkey.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<VTubeStudioModel> ReadModels(JsonElement data)
    {
        if (!data.TryGetProperty("availableModels", out var array))
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(item => new VTubeStudioModel(
                item.TryGetProperty("modelID", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("modelName", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("modelLoaded", out var loaded) && loaded.GetBoolean()))
            .Where(static model => model.Id.Length > 0)
            .OrderByDescending(static model => model.IsLoaded)
            .ThenBy(static model => model.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static async Task SendTextAsync(
        ClientWebSocket socket,
        string text,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReceiveMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType is WebSocketMessageType.Close)
            {
                throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
            }

            if (result.MessageType is not WebSocketMessageType.Text)
            {
                throw new InvalidDataException("VTube Studio sent a non-text WebSocket message.");
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
    }
}
