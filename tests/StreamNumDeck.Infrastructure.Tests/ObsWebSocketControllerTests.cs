using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.Infrastructure.Obs;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class ObsWebSocketControllerTests
{
    [TestMethod]
    [Timeout(15_000)]
    public async Task ConnectAndExecuteAsync_UsesObsWebSocketRequestProtocol()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serverTask = RunServerAsync(listener);
        await using var controller = new ObsWebSocketController(new NullCredentialStore());

        await controller.ConnectAsync(new ObsConnectionSettings("127.0.0.1", port, "test"));
        await controller.ExecuteAsync(new ObsActionDefinition(ObsActionKind.StartStreaming));

        var receivedRequestType = await serverTask;
        Assert.AreEqual("StartStream", receivedRequestType);
    }

    [TestMethod]
    [Timeout(15_000)]
    public async Task ServerDisconnectAsync_LeavesConnectedState()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serverTask = RunServerAndDisconnectAsync(listener);
        await using var controller = new ObsWebSocketController(new NullCredentialStore());
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionLost = new TaskCompletionSource<ObsConnectionStateChangedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        controller.StateChanged += (_, args) =>
        {
            if (args.State is ObsConnectionState.Connected)
            {
                connected.TrySetResult();
            }
            else if (args.State is ObsConnectionState.Reconnecting or ObsConnectionState.Disconnected)
            {
                connectionLost.TrySetResult(args);
            }
        };

        await controller.ConnectAsync(new ObsConnectionSettings("127.0.0.1", port, "test"));
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await serverTask;
        var state = await connectionLost.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.AreNotEqual(ObsConnectionState.Connected, state.State);
        Assert.AreNotEqual(ObsConnectionState.Connected, controller.State);
    }

    [TestMethod]
    [Timeout(15_000)]
    public async Task ServerDisconnectAsync_ReconnectsAndCanReadCatalog()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serverTask = RunServerDisconnectThenCatalogAsync(listener);
        await using var controller = new ObsWebSocketController(new NullCredentialStore());
        var connectedCount = 0;
        var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.StateChanged += (_, args) =>
        {
            if (args.State is ObsConnectionState.Connected
                && Interlocked.Increment(ref connectedCount) >= 2)
            {
                reconnected.TrySetResult();
            }
        };

        await controller.ConnectAsync(new ObsConnectionSettings("127.0.0.1", port, "test"));
        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var catalog = await controller.GetCatalogAsync();
        await serverTask;

        CollectionAssert.AreEquivalent(new[] { "Gameplay" }, catalog.Scenes.ToArray());
        CollectionAssert.AreEquivalent(new[] { "Microphone" }, catalog.Inputs.ToArray());
        CollectionAssert.AreEquivalent(new[] { "Camera", "Microphone" }, catalog.Sources.ToArray());
    }

    private static async Task<string> RunServerAsync(HttpListener listener)
    {
        var context = await listener.GetContextAsync();
        var webSocketContext = await context.AcceptWebSocketAsync(null);
        using var socket = webSocketContext.WebSocket;

        await SendAsync(socket, new
        {
            op = 0,
            d = new
            {
                obsWebSocketVersion = "5.6.3",
                rpcVersion = 1,
            },
        });

        using var identify = JsonDocument.Parse(await ReceiveAsync(socket));
        Assert.AreEqual(1, identify.RootElement.GetProperty("op").GetInt32());
        Assert.AreEqual(1, identify.RootElement.GetProperty("d").GetProperty("rpcVersion").GetInt32());

        await SendAsync(socket, new { op = 2, d = new { negotiatedRpcVersion = 1 } });

        using var request = JsonDocument.Parse(await ReceiveAsync(socket));
        var requestData = request.RootElement.GetProperty("d");
        var requestType = requestData.GetProperty("requestType").GetString()!;
        var requestId = requestData.GetProperty("requestId").GetString()!;

        await SendAsync(socket, new
        {
            op = 7,
            d = new
            {
                requestType,
                requestId,
                requestStatus = new { result = true, code = 100 },
            },
        });

        return requestType;
    }

    private static async Task RunServerAndDisconnectAsync(HttpListener listener)
    {
        var context = await listener.GetContextAsync();
        var webSocketContext = await context.AcceptWebSocketAsync(null);
        using var socket = webSocketContext.WebSocket;

        await SendAsync(socket, new
        {
            op = 0,
            d = new
            {
                obsWebSocketVersion = "5.6.3",
                rpcVersion = 1,
            },
        });

        _ = await ReceiveAsync(socket);
        await SendAsync(socket, new { op = 2, d = new { negotiatedRpcVersion = 1 } });
        socket.Abort();
    }

    private static async Task RunServerDisconnectThenCatalogAsync(HttpListener listener)
    {
        using (var firstSocket = await AcceptAndIdentifyAsync(listener))
        {
            firstSocket.Abort();
        }

        using var secondSocket = await AcceptAndIdentifyAsync(listener);
        for (var index = 0; index < 3; index++)
        {
            using var request = JsonDocument.Parse(await ReceiveAsync(secondSocket));
            var requestData = request.RootElement.GetProperty("d");
            var requestType = requestData.GetProperty("requestType").GetString()!;
            var requestId = requestData.GetProperty("requestId").GetString()!;
            object responseData = requestType switch
            {
                "GetSceneList" => new { scenes = new[] { new { sceneName = "Gameplay" } } },
                "GetInputList" => new { inputs = new[] { new { inputName = "Microphone" } } },
                "GetSceneItemList" => new { sceneItems = new[] { new { sourceName = "Camera" } } },
                _ => throw new AssertFailedException($"Unexpected OBS request: {requestType}"),
            };

            await SendAsync(secondSocket, new
            {
                op = 7,
                d = new
                {
                    requestType,
                    requestId,
                    requestStatus = new { result = true, code = 100 },
                    responseData,
                },
            });
        }
    }

    private static async Task<WebSocket> AcceptAndIdentifyAsync(HttpListener listener)
    {
        var context = await listener.GetContextAsync();
        var webSocketContext = await context.AcceptWebSocketAsync(null);
        var socket = webSocketContext.WebSocket;

        await SendAsync(socket, new
        {
            op = 0,
            d = new
            {
                obsWebSocketVersion = "5.6.3",
                rpcVersion = 1,
            },
        });
        _ = await ReceiveAsync(socket);
        await SendAsync(socket, new { op = 2, d = new { negotiatedRpcVersion = 1 } });
        return socket;
    }

    private static async Task SendAsync(WebSocket socket, object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveAsync(WebSocket socket)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class NullCredentialStore : IProtectedCredentialStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
