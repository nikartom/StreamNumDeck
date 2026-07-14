using StreamNumDeck.Core.Configuration;

namespace StreamNumDeck.Core.Tests;

[TestClass]
public sealed class ConfigurationServiceTests
{
    [TestMethod]
    public async Task GetAsync_LoadsStoreOnlyOnce()
    {
        var store = new RecordingConfigurationStore(AppConfiguration.CreateDefault());
        using var service = new ConfigurationService(store);

        var first = await service.GetAsync();
        var second = await service.GetAsync();

        Assert.AreSame(first, second);
        Assert.AreEqual(1, store.LoadCount);
    }

    [TestMethod]
    public async Task UpdateAsync_PersistsBeforePublishingSnapshot()
    {
        var initial = AppConfiguration.CreateDefault();
        var store = new RecordingConfigurationStore(initial);
        using var service = new ConfigurationService(store);
        AppConfiguration? publishedConfiguration = null;
        service.Changed += (_, args) => publishedConfiguration = args.Configuration;

        var updated = await service.UpdateAsync(configuration =>
            configuration.WithSettings(new StreamNumDeck.Core.Settings.GlobalSettings(
                configuration.Settings.AudioOutputDeviceId,
                42,
                configuration.Settings.AllowConcurrentSounds,
                configuration.Settings.PreloadShortSounds,
                configuration.Settings.StartWithWindows,
                configuration.Settings.MinimizeToTray,
                configuration.Settings.EnableCaptureOnStartup,
                configuration.Settings.Theme,
                configuration.Settings.Obs)));

        Assert.AreEqual(42, updated.Settings.MasterVolume);
        Assert.AreSame(updated, store.SavedConfiguration);
        Assert.AreSame(updated, publishedConfiguration);
        Assert.AreSame(updated, await service.GetAsync());
    }

    private sealed class RecordingConfigurationStore(AppConfiguration configuration) : IConfigurationStore
    {
        public int LoadCount { get; private set; }

        public AppConfiguration? SavedConfiguration { get; private set; }

        public Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult(configuration);
        }

        public Task SaveAsync(AppConfiguration updatedConfiguration, CancellationToken cancellationToken = default)
        {
            SavedConfiguration = updatedConfiguration;
            return Task.CompletedTask;
        }
    }
}
