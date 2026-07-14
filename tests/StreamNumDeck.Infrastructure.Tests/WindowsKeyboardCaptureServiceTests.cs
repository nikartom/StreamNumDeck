using StreamNumDeck.Core.Input;
using StreamNumDeck.Infrastructure.Input;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class WindowsKeyboardCaptureServiceTests
{
    [TestMethod]
    public async Task StartsAndStopsDedicatedHookThread()
    {
        await using var service = new WindowsKeyboardCaptureService();

        await service.StartAsync(TestContext.CancellationToken);
        Assert.AreEqual(KeyboardCaptureState.Running, service.State);

        await service.StopAsync(TestContext.CancellationToken);
        Assert.AreEqual(KeyboardCaptureState.Stopped, service.State);
    }

    public TestContext TestContext { get; set; }
}
