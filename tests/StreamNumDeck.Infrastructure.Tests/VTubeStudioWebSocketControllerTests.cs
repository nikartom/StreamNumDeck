using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.Infrastructure.VTubeStudio;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
[DoNotParallelize]
public sealed class VTubeStudioWebSocketControllerTests
{
    [TestMethod]
    [Timeout(15_000)]
    public async Task GetCatalogAsync_RequestsTokenAndReadsModelsAndHotkeys()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serverTask = RunCatalogServerAsync(listener);
        var credentials = new MemoryCredentialStore();
        var controller = new VTubeStudioWebSocketController(credentials);

        var catalog = await controller.GetCatalogAsync(
            new VTubeStudioConnectionSettings("127.0.0.1", port, "vts.test"));
        await serverTask;

        Assert.AreEqual("Akari", catalog.CurrentModelName);
        Assert.HasCount(1, catalog.Hotkeys);
        Assert.AreEqual("Wave", catalog.Hotkeys[0].Name);
        Assert.HasCount(2, catalog.Models);
        Assert.AreEqual("token-123", await credentials.GetAsync("vts.test"));
    }

    [TestMethod]
    [Timeout(15_000)]
    public async Task ExecuteAsync_AuthenticatesWithStoredTokenAndTriggersHotkey()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serverTask = RunActionServerAsync(listener);
        var credentials = new MemoryCredentialStore();
        await credentials.SetAsync("vts.test", "stored-token");
        var controller = new VTubeStudioWebSocketController(credentials);

        await controller.ExecuteAsync(
            new VTubeStudioConnectionSettings("127.0.0.1", port, "vts.test"),
            new VTubeStudioActionDefinition(VTubeStudioActionKind.TriggerHotkey, "hotkey-wave"));
        await serverTask;
    }

    private static async Task RunCatalogServerAsync(HttpListener listener)
    {
        var socket = await AcceptAsync(listener);
        using (socket)
        {
            await RespondAsync(socket, "AuthenticationTokenRequest", new { authenticationToken = "token-123" });
            await RespondAsync(socket, "AuthenticationRequest", new { authenticated = true, reason = "Token valid" });
            await RespondAsync(socket, "CurrentModelRequest", new
            {
                modelLoaded = true,
                modelName = "Akari",
                modelID = "model-akari",
            });
            await RespondAsync(socket, "AvailableModelsRequest", new
            {
                availableModels = new[]
                {
                    new { modelLoaded = true, modelName = "Akari", modelID = "model-akari" },
                    new { modelLoaded = false, modelName = "Haru", modelID = "model-haru" },
                },
            });
            await RespondAsync(socket, "HotkeysInCurrentModelRequest", new
            {
                modelLoaded = true,
                availableHotkeys = new[]
                {
                    new { name = "Wave", type = "TriggerAnimation", hotkeyID = "hotkey-wave" },
                },
            });
        }
    }

    private static async Task RunActionServerAsync(HttpListener listener)
    {
        var socket = await AcceptAsync(listener);
        using (socket)
        {
            await RespondAsync(socket, "AuthenticationRequest", new { authenticated = true, reason = "Token valid" });
            using var request = JsonDocument.Parse(await ReceiveAsync(socket));
            var root = request.RootElement;
            Assert.AreEqual("HotkeyTriggerRequest", root.GetProperty("messageType").GetString());
            Assert.AreEqual("hotkey-wave", root.GetProperty("data").GetProperty("hotkeyID").GetString());
            await SendResponseAsync(socket, root, new { hotkeyID = "hotkey-wave" });
        }
    }

    private static async Task<WebSocket> AcceptAsync(HttpListener listener)
    {
        var context = await listener.GetContextAsync();
        var webSocketContext = await context.AcceptWebSocketAsync(null);
        return webSocketContext.WebSocket;
    }

    private static async Task RespondAsync(WebSocket socket, string expectedType, object data)
    {
        using var request = JsonDocument.Parse(await ReceiveAsync(socket));
        var root = request.RootElement;
        Assert.AreEqual(expectedType, root.GetProperty("messageType").GetString());
        await SendResponseAsync(socket, root, data);
    }

    private static Task SendResponseAsync(WebSocket socket, JsonElement request, object data) =>
        SendAsync(socket, new
        {
            apiName = "VTubeStudioPublicAPI",
            apiVersion = "1.0",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            messageType = request.GetProperty("messageType").GetString()!.Replace("Request", "Response"),
            requestID = request.GetProperty("requestID").GetString(),
            data,
        });

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

    private sealed class MemoryCredentialStore : IProtectedCredentialStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default)
        {
            values[key] = secret;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
