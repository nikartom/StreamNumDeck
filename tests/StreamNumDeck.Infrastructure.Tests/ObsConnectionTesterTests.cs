using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StreamNumDeck.Infrastructure.Obs;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class ObsConnectionTesterTests
{
    [TestMethod]
    [Timeout(15_000)]
    public async Task TestAsync_UsesAnIndependentTemporaryConnection()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serverTask = RunCatalogServerAsync(listener);
        var tester = new ObsConnectionTester();
        var catalog = await tester.TestAsync("127.0.0.1", port, password: null);
        await serverTask;

        Assert.IsEmpty(catalog.Scenes);
        Assert.IsEmpty(catalog.Inputs);
        Assert.IsEmpty(catalog.Sources);
    }

    private static async Task RunCatalogServerAsync(HttpListener listener)
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

        foreach (var expectedRequest in new[] { "GetSceneList", "GetInputList" })
        {
            using var request = JsonDocument.Parse(await ReceiveAsync(socket));
            var requestData = request.RootElement.GetProperty("d");
            var requestType = requestData.GetProperty("requestType").GetString();
            Assert.AreEqual(expectedRequest, requestType);
            var requestId = requestData.GetProperty("requestId").GetString();
            var responseData = expectedRequest == "GetSceneList"
                ? new { scenes = Array.Empty<object>() }
                : (object)new { inputs = Array.Empty<object>() };
            await SendAsync(socket, new
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

    private static async Task SendAsync(WebSocket socket, object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    private static async Task<string> ReceiveAsync(WebSocket socket)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
