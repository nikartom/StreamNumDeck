using System.Text;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.Infrastructure.Audio;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class WindowsAudioPlaybackServiceTests
{
    private string waveFilePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        waveFilePath = Path.Combine(Path.GetTempPath(), $"StreamNumDeck-{Guid.NewGuid():N}.wav");
        WriteSilentWaveFile(waveFilePath, durationMilliseconds: 100);
    }

    [TestCleanup]
    public void Cleanup()
    {
        File.Delete(waveFilePath);
    }

    [TestMethod]
    public async Task EnumeratesDefaultDeviceAndPlaysWaveFile()
    {
        await using var service = new WindowsAudioPlaybackService();

        var devices = await service.GetOutputDevicesAsync(TestContext.CancellationToken);
        Assert.IsNotEmpty(devices);
        Assert.IsTrue(devices[0].IsSystemDefault);
        Assert.IsNull(devices[0].Id);

        var action = new PlaySoundActionDefinition(waveFilePath, volume: 0);
        await service.PreloadAsync(
            [action],
            GlobalSettings.Default,
            TestContext.CancellationToken);
        await service.PlayAsync(action, GlobalSettings.Default, TestContext.CancellationToken);
        await Task.Delay(25, TestContext.CancellationToken);
        await service.StopAllAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task PreloadAsync_DoesNotStartPlayback()
    {
        await using var service = new WindowsAudioPlaybackService();
        var action = new PlaySoundActionDefinition(waveFilePath, volume: 0);

        await service.PreloadAsync([action], GlobalSettings.Default, TestContext.CancellationToken);
        await Task.Delay(50, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.Zero, service.GetPreloadedPosition(waveFilePath));
    }

    [TestMethod]
    public async Task PlayAsync_ReplenishedNodeDoesNotStartPlayback()
    {
        await using var service = new WindowsAudioPlaybackService();
        var action = new PlaySoundActionDefinition(waveFilePath, volume: 0);

        await service.PlayAsync(action, GlobalSettings.Default, TestContext.CancellationToken);
        while (service.GetPreloadedPosition(waveFilePath) is null)
        {
            await Task.Delay(5, TestContext.CancellationToken);
        }

        await Task.Delay(50, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.Zero, service.GetPreloadedPosition(waveFilePath));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PlayAsync_PlaysSoundToCompletion(bool preloaded)
    {
        await using var service = new WindowsAudioPlaybackService();
        var action = new PlaySoundActionDefinition(waveFilePath, volume: 0);
        if (preloaded)
        {
            await service.PreloadAsync([action], GlobalSettings.Default, TestContext.CancellationToken);
            await Task.Delay(200, TestContext.CancellationToken);
        }

        await service.PlayAsync(action, GlobalSettings.Default, TestContext.CancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (service.ActiveSoundCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.CancellationToken);
        }

        Assert.AreEqual(0, service.ActiveSoundCount);
    }

    public TestContext TestContext { get; set; }

    private static void WriteSilentWaveFile(string path, int durationMilliseconds)
    {
        const int sampleRate = 8_000;
        const short channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = sampleRate * durationMilliseconds / 1_000;
        var dataLength = sampleCount * channels * bitsPerSample / 8;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }
}
