using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Infrastructure.Audio;
using StreamNumDeck.Infrastructure.Execution;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class SystemAudioActionExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_DispatchesApplicationVolumeAction()
    {
        var service = new RecordingAudioControlService();
        var executor = new SystemAudioActionExecutor(service);
        var action = new AdjustApplicationVolumeActionDefinition(
            "chrome.exe",
            VolumeAdjustmentDirection.Increase,
            6);

        await executor.ExecuteAsync(action);

        Assert.AreEqual("chrome.exe", service.ApplicationId);
        Assert.AreEqual(VolumeAdjustmentDirection.Increase, service.Direction);
        Assert.AreEqual(6, service.StepPercent);
    }

    [TestMethod]
    public async Task GetApplicationsAsync_EnumeratesCurrentWindowsAudioSessions()
    {
        var service = new WindowsSystemAudioControlService();

        var applications = await service.GetApplicationsAsync();

        Assert.HasCount(applications.Count, applications.Select(static application => application.Id).Distinct());
        Assert.IsTrue(applications.All(static application => application.Id.EndsWith(".exe", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task GetDefaultMicrophoneMuteAsync_ReadsCurrentEndpointState()
    {
        var service = new WindowsSystemAudioControlService();

        var muted = await service.GetDefaultMicrophoneMuteAsync();
        TestContext.WriteLine($"Default microphone muted: {muted}");
    }

    public TestContext TestContext { get; set; }

    private sealed class RecordingAudioControlService : ISystemAudioControlService
    {
        public string? ApplicationId { get; private set; }

        public VolumeAdjustmentDirection? Direction { get; private set; }

        public int? StepPercent { get; private set; }

        public Task<IReadOnlyList<AudioApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioApplication>>([]);

        public Task ToggleDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> GetDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task ToggleMasterOutputMuteAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AdjustMasterOutputVolumeAsync(
            VolumeAdjustmentDirection direction,
            int stepPercent,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AdjustApplicationVolumeAsync(
            string applicationId,
            VolumeAdjustmentDirection direction,
            int stepPercent,
            CancellationToken cancellationToken = default)
        {
            ApplicationId = applicationId;
            Direction = direction;
            StepPercent = stepPercent;
            return Task.CompletedTask;
        }
    }
}
