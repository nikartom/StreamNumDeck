using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.App.Overlays;
using StreamNumDeck.App.Localization;
using StreamNumDeck.App.SystemIntegration;

namespace StreamNumDeck.App.ViewModels;

public partial class SettingsPageViewModel(
    ConfigurationService configurationService,
    IAudioPlaybackService audioPlaybackService,
    IProtectedCredentialStore credentialStore,
    IObsController obsController,
    MicrophoneMuteOverlayController microphoneMuteOverlay,
    WindowsStartupService startupService) : ObservableObject
{
    private bool initialized;

    [ObservableProperty]
    public partial double MasterVolume { get; set; }

    public ObservableCollection<AudioOutputDevice> AudioOutputDevices { get; } = [];

    [ObservableProperty]
    public partial AudioOutputDevice? SelectedAudioOutputDevice { get; set; }

    [ObservableProperty]
    public partial bool AllowConcurrentSounds { get; set; }

    [ObservableProperty]
    public partial bool PreloadShortSounds { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool EnableCaptureOnStartup { get; set; }

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial string ObsHost { get; set; } = "127.0.0.1";

    [ObservableProperty]
    public partial double ObsPort { get; set; } = 4455;

    [ObservableProperty]
    public partial bool HasStoredObsPassword { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        var configuration = await configurationService.GetAsync(cancellationToken);
        var settings = configuration.Settings;

        AudioOutputDevices.Clear();
        foreach (var device in await audioPlaybackService.GetOutputDevicesAsync(cancellationToken))
        {
            AudioOutputDevices.Add(device.IsSystemDefault
                ? device with { Name = AppStrings.Get("Audio_SystemDefaultDevice") }
                : device);
        }

        SelectedAudioOutputDevice = AudioOutputDevices.FirstOrDefault(device =>
                string.Equals(device.Id, settings.AudioOutputDeviceId, StringComparison.Ordinal))
            ?? AudioOutputDevices.First();

        MasterVolume = settings.MasterVolume;
        AllowConcurrentSounds = settings.AllowConcurrentSounds;
        PreloadShortSounds = settings.PreloadShortSounds;
        StartWithWindows = await startupService.IsEnabledAsync();
        MinimizeToTray = settings.MinimizeToTray;
        EnableCaptureOnStartup = settings.EnableCaptureOnStartup;
        ThemeIndex = (int)settings.Theme;
        ObsHost = settings.Obs.Host;
        ObsPort = settings.Obs.Port;
        HasStoredObsPassword = !string.IsNullOrEmpty(
            await credentialStore.GetAsync(settings.Obs.CredentialKey, cancellationToken));
        initialized = true;
    }

    public async Task SaveAsync(string? obsPassword, CancellationToken cancellationToken = default)
    {
        await startupService.SetEnabledAsync(StartWithWindows, cancellationToken);
        var configuration = await configurationService.GetAsync(cancellationToken);
        var currentSettings = configuration.Settings;
        var obsSettingsChanged = !string.Equals(currentSettings.Obs.Host, ObsHost, StringComparison.OrdinalIgnoreCase)
            || currentSettings.Obs.Port != checked((int)ObsPort)
            || !string.IsNullOrEmpty(obsPassword);
        var settings = new GlobalSettings(
            SelectedAudioOutputDevice?.Id,
            (int)Math.Round(MasterVolume),
            AllowConcurrentSounds,
            PreloadShortSounds,
            StartWithWindows,
            MinimizeToTray,
            EnableCaptureOnStartup,
            (AppTheme)ThemeIndex,
            new ObsConnectionSettings(
                ObsHost,
                checked((int)ObsPort),
                currentSettings.Obs.CredentialKey));

        var updated = await configurationService.UpdateAsync(
            current => current.WithSettings(settings),
            cancellationToken);

        if (!string.IsNullOrEmpty(obsPassword))
        {
            await credentialStore.SetAsync(
                updated.Settings.Obs.CredentialKey,
                obsPassword,
                cancellationToken);
            HasStoredObsPassword = true;
        }

        global::StreamNumDeck.App.App.ApplySystemSettings(updated.Settings);
        if (obsSettingsChanged)
        {
            _ = ReconnectObsAsync(updated.Settings.Obs);
        }
    }

    public async Task<string> TestObsConnectionAsync(
        string? obsPassword,
        CancellationToken cancellationToken = default)
    {
        var current = await configurationService.GetAsync(cancellationToken);
        var testSettings = new ObsConnectionSettings(
            ObsHost,
            checked((int)ObsPort),
            current.Settings.Obs.CredentialKey);

        if (!string.IsNullOrEmpty(obsPassword))
        {
            await credentialStore.SetAsync(testSettings.CredentialKey, obsPassword, cancellationToken);
            HasStoredObsPassword = true;
        }

        await obsController.DisconnectAsync(cancellationToken);
        await obsController.ConnectAsync(testSettings, cancellationToken);
        var catalog = await obsController.GetCatalogAsync(cancellationToken);
        return AppStrings.Format("Obs_TestResult", catalog.Scenes.Count, catalog.Sources.Count);
    }

    public Task PreviewMicrophoneMuteOverlayAsync(CancellationToken cancellationToken = default) =>
        microphoneMuteOverlay.ShowPreviewAsync(cancellationToken);

    private async Task ReconnectObsAsync(ObsConnectionSettings settings)
    {
        try
        {
            await obsController.DisconnectAsync();
            await obsController.ConnectAsync(settings);
        }
        catch
        {
            // IObsController exposes the fault and continues reconnecting in the background.
        }
    }
}
