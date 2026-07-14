using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Input;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.App.Localization;
using StreamNumDeck.Wpf.Overlays;
using StreamNumDeck.Wpf.Services;

namespace StreamNumDeck.Wpf.ViewModels;

using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private static readonly Brush ObsDisconnectedBrush = new SolidColorBrush(Color.FromRgb(138, 136, 134));
    private static readonly Brush ObsConnectingBrush = new SolidColorBrush(Color.FromRgb(247, 187, 0));
    private static readonly Brush ObsConnectedBrush = new SolidColorBrush(Color.FromRgb(16, 124, 16));
    private static readonly Brush ObsFaultedBrush = new SolidColorBrush(Color.FromRgb(209, 52, 56));
    private static readonly Brush NumLockOnBrush = new SolidColorBrush(Color.FromRgb(51, 72, 108));
    private static readonly Brush NumLockOffBrush = new SolidColorBrush(Color.FromRgb(45, 45, 45));
    private static readonly TimeSpan ErrorNotificationCooldown = TimeSpan.FromMinutes(1);

    private readonly ConfigurationService configurationService;
    private readonly IIconAssetStore iconAssetStore;
    private readonly DeckRuntimeService runtimeService;
    private readonly IObsController obsController;
    private readonly IObsConnectionTester obsConnectionTester;
    private readonly IAudioPlaybackService audioPlaybackService;
    private readonly IProtectedCredentialStore credentialStore;
    private readonly ISystemAudioControlService systemAudioControlService;
    private readonly MicrophoneMuteOverlayController microphoneMuteOverlay;
    private readonly Dictionary<string, DateTimeOffset> recentErrorNotifications = new(StringComparer.Ordinal);
    private DeckProfile? selectedProfile;
    private bool isNumLockOn;
    private bool captureNumpad = true;
    private bool captureNavigationBlock = true;
    private string obsStatusText = AppStrings.Get("Obs_Disconnected", "OBS disconnected");
    private Brush obsStatusBrush = ObsDisconnectedBrush;
    private string errorTitle = AppStrings.Get("Deck_ActionError.Title", "Action failed");
    private string errorText = string.Empty;
    private bool initialized;

    public MainViewModel(
        ConfigurationService configurationService,
        IIconAssetStore iconAssetStore,
        DeckRuntimeService runtimeService,
        IObsController obsController,
        IObsConnectionTester obsConnectionTester,
        IAudioPlaybackService audioPlaybackService,
        IProtectedCredentialStore credentialStore,
        ISystemAudioControlService systemAudioControlService,
        MicrophoneMuteOverlayController microphoneMuteOverlay)
    {
        this.configurationService = configurationService;
        this.iconAssetStore = iconAssetStore;
        this.runtimeService = runtimeService;
        this.obsController = obsController;
        this.obsConnectionTester = obsConnectionTester;
        this.audioPlaybackService = audioPlaybackService;
        this.credentialStore = credentialStore;
        this.systemAudioControlService = systemAudioControlService;
        this.microphoneMuteOverlay = microphoneMuteOverlay;

        Tiles = DeckKeyCatalog.AssignableKeys.ToDictionary(
            key => key.ToString(),
            key => new DeckKeyTileViewModel(key),
            StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, DeckKeyTileViewModel> Tiles { get; }
    public ObservableCollection<DeckProfile> Profiles { get; } = new();
    public event Action<string, string>? UserErrorRaised;

    public DeckProfile? SelectedProfile
    {
        get => selectedProfile;
        private set
        {
            if (SetProperty(ref selectedProfile, value))
            {
                RefreshTiles();
            }
        }
    }

    public bool IsNumLockOn
    {
        get => isNumLockOn;
        private set
        {
            if (SetProperty(ref isNumLockOn, value))
            {
                OnPropertyChanged(nameof(NumLockBackground));
                RefreshTiles();
            }
        }
    }

    public Brush NumLockBackground => IsNumLockOn ? NumLockOnBrush : NumLockOffBrush;
    public bool CaptureNumpad { get => captureNumpad; private set => SetProperty(ref captureNumpad, value); }
    public bool CaptureNavigationBlock { get => captureNavigationBlock; private set => SetProperty(ref captureNavigationBlock, value); }
    public string ObsStatusText { get => obsStatusText; private set => SetProperty(ref obsStatusText, value); }
    public Brush ObsStatusBrush { get => obsStatusBrush; private set => SetProperty(ref obsStatusBrush, value); }
    public string ErrorTitle { get => errorTitle; private set => SetProperty(ref errorTitle, value); }
    public string ErrorText
    {
        get => errorText;
        private set
        {
            if (SetProperty(ref errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public void ClearError() => ErrorText = string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(true);
        ReplaceProfiles(configuration, configuration.ActiveProfileId);
        await RefreshPreloadedSoundsAsync(configuration, cancellationToken).ConfigureAwait(true);

        runtimeService.NumLockStateChanged += RuntimeService_NumLockStateChanged;
        runtimeService.ActionExecutionFailed += RuntimeService_ActionExecutionFailed;
        obsController.StateChanged += ObsController_StateChanged;

        IsNumLockOn = runtimeService.IsNumLockOn;
        await ApplyCaptureTargetsAsync(configuration.Settings, cancellationToken).ConfigureAwait(true);
        if (configuration.Settings.EnableCaptureOnStartup)
        {
            await runtimeService.StartAsync(cancellationToken).ConfigureAwait(true);
        }

        if (!string.IsNullOrWhiteSpace(obsController.LastError))
        {
            AppLogger.Error("OBS connection", new InvalidOperationException(obsController.LastError));
        }

        ApplyObsState(obsController.State);
        initialized = true;
        RefreshTiles();
        _ = ConnectObsAsync(configuration.Settings.Obs);
    }

    public async Task ToggleCaptureAsync()
    {
        if (runtimeService.CaptureState == KeyboardCaptureState.Stopped)
        {
            await runtimeService.StartAsync().ConfigureAwait(true);
        }
        else
        {
            await runtimeService.StopAsync().ConfigureAwait(true);
        }

    }

    public async Task ToggleCaptureTargetAsync(KeyboardCaptureTargets target)
    {
        if (target is not KeyboardCaptureTargets.Numpad and not KeyboardCaptureTargets.NavigationBlock)
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "A single capture target is required.");
        }

        var configuration = await configurationService.UpdateAsync(current =>
        {
            var settings = current.Settings;
            return current.WithSettings(settings with
            {
                CaptureNumpad = target == KeyboardCaptureTargets.Numpad
                    ? !settings.CaptureNumpad
                    : settings.CaptureNumpad,
                CaptureNavigationBlock = target == KeyboardCaptureTargets.NavigationBlock
                    ? !settings.CaptureNavigationBlock
                    : settings.CaptureNavigationBlock,
            });
        }).ConfigureAwait(true);

        await ApplyCaptureTargetsAsync(configuration.Settings).ConfigureAwait(true);
    }

    public async Task SelectProfileAsync(Guid profileId)
    {
        var configuration = await configurationService.GetAsync().ConfigureAwait(true);
        if (configuration.ActiveProfileId != profileId)
        {
            configuration = await configurationService
                .UpdateAsync(current => current.WithActiveProfile(profileId))
                .ConfigureAwait(true);
        }

        ReplaceProfiles(configuration, profileId);
    }

    public async Task CreateProfileAsync(string name, IconReference icon)
    {
        var current = await configurationService.GetAsync().ConfigureAwait(true);
        EnsureUniqueProfileName(current, name, null);
        var profile = DeckProfile.CreateDefault(name, icon);
        var configuration = await configurationService
            .UpdateAsync(current => current.AddProfile(profile))
            .ConfigureAwait(true);
        ReplaceProfiles(configuration, profile.Id);
    }

    public async Task EditProfileAsync(Guid profileId, string name, IconReference icon)
    {
        var configuration = await configurationService.GetAsync().ConfigureAwait(true);
        EnsureUniqueProfileName(configuration, name, profileId);
        var profile = configuration.Profiles.Single(candidate => candidate.Id == profileId);
        configuration = await configurationService
            .UpdateAsync(current => current.ReplaceProfile(profile.WithDetails(name, icon)))
            .ConfigureAwait(true);
        ReplaceProfiles(configuration, configuration.ActiveProfileId);
    }

    public async Task DuplicateProfileAsync(Guid profileId)
    {
        var configuration = await configurationService.GetAsync().ConfigureAwait(true);
        var profile = configuration.Profiles.Single(candidate => candidate.Id == profileId);
        var duplicate = profile.Duplicate(CreateCopyName(profile.Name, configuration));
        configuration = await configurationService
            .UpdateAsync(current => current.AddProfile(duplicate))
            .ConfigureAwait(true);
        ReplaceProfiles(configuration, duplicate.Id);
    }

    public async Task DeleteProfileAsync(Guid profileId)
    {
        var configuration = await configurationService
            .UpdateAsync(current => current.RemoveProfile(profileId))
            .ConfigureAwait(true);
        await RefreshPreloadedSoundsAsync(configuration).ConfigureAwait(true);
        ReplaceProfiles(configuration, configuration.ActiveProfileId);
    }

    public KeyAssignment GetCurrentAssignment(DeckKey key) =>
        SelectedProfile?.GetLayer(IsNumLockOn ? NumLockLayer.On : NumLockLayer.Off).GetAssignment(key)
        ?? KeyAssignment.Empty;

    public KeyAssignment GetAssignment(DeckKey key, NumLockLayer layer) =>
        SelectedProfile?.GetLayer(layer).GetAssignment(key) ?? KeyAssignment.Empty;

    public Task<IconReference> ImportIconAsync(string filePath, CancellationToken cancellationToken = default) =>
        iconAssetStore.ImportAsync(filePath, cancellationToken);

    public string? ResolveIconPath(IconReference icon) => iconAssetStore.ResolvePath(icon);

    public Task<ObsCatalog> GetObsCatalogAsync(CancellationToken cancellationToken = default) =>
        obsController.GetCatalogAsync(cancellationToken);

    public Task<IReadOnlyList<AudioApplication>> GetAudioApplicationsAsync(CancellationToken cancellationToken = default) =>
        systemAudioControlService.GetApplicationsAsync(cancellationToken);

    public async Task PreviewSoundAsync(
        string filePath,
        int volume,
        SoundPlaybackBehavior behavior,
        CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(true);
        await audioPlaybackService
            .PlayAsync(new PlaySoundActionDefinition(filePath, volume, behavior), configuration.Settings, cancellationToken)
            .ConfigureAwait(true);
    }

    public Task StopSoundsAsync(CancellationToken cancellationToken = default) =>
        audioPlaybackService.StopAllAsync(cancellationToken);

    public async Task<SettingsSnapshot> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(true);
        var password = await credentialStore.GetAsync(configuration.Settings.Obs.CredentialKey, cancellationToken).ConfigureAwait(true);
        var devices = (await audioPlaybackService.GetOutputDevicesAsync(cancellationToken).ConfigureAwait(true))
            .Select(device => device.IsSystemDefault
                ? device with { Name = AppStrings.Get("Audio_SystemDefaultDevice", "System default device") }
                : device)
            .ToArray();
        return new SettingsSnapshot(configuration.Settings, password, devices);
    }

    public async Task SaveSettingsAsync(
        GlobalSettings settings,
        string? obsPassword,
        CancellationToken cancellationToken = default)
    {
        var passwordChanged = !string.IsNullOrEmpty(obsPassword);
        var previousPassword = passwordChanged
            ? await credentialStore.GetAsync(settings.Obs.CredentialKey, cancellationToken).ConfigureAwait(true)
            : null;

        AppConfiguration updatedConfiguration;
        try
        {
            if (passwordChanged)
            {
                await credentialStore.SetAsync(settings.Obs.CredentialKey, obsPassword!, cancellationToken).ConfigureAwait(true);
            }

            updatedConfiguration = await configurationService
                .UpdateAsync(current => current.WithSettings(settings), cancellationToken)
                .ConfigureAwait(true);
        }
        catch
        {
            if (passwordChanged)
            {
                await RestoreCredentialAsync(settings.Obs.CredentialKey, previousPassword).ConfigureAwait(true);
            }

            throw;
        }

        await RefreshPreloadedSoundsAsync(updatedConfiguration, cancellationToken).ConfigureAwait(true);
        await ApplyCaptureTargetsAsync(updatedConfiguration.Settings, cancellationToken).ConfigureAwait(true);

        try
        {
            await obsController.DisconnectAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            AppLogger.Error("Reconnect OBS after saving settings", exception);
        }

        _ = ConnectObsAsync(settings.Obs);
    }

    public Task PreviewMicrophoneOverlayAsync() => microphoneMuteOverlay.ShowPreviewAsync();

    public async Task<string> TestObsConnectionAsync(
        string host,
        int port,
        string? obsPassword,
        CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(true);
        var password = string.IsNullOrEmpty(obsPassword)
            ? await credentialStore
                .GetAsync(configuration.Settings.Obs.CredentialKey, cancellationToken)
                .ConfigureAwait(true)
            : obsPassword;
        var catalog = await obsConnectionTester
            .TestAsync(host, port, password, cancellationToken)
            .ConfigureAwait(true);
        return AppStrings.Format("Obs_TestResult", catalog.Scenes.Count, catalog.Sources.Count);
    }

    private async Task RestoreCredentialAsync(string credentialKey, string? password)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
            {
                await credentialStore.DeleteAsync(credentialKey).ConfigureAwait(true);
            }
            else
            {
                await credentialStore.SetAsync(credentialKey, password!).ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            AppLogger.Error("Restore OBS credential after failed settings save", exception);
        }
    }

    public async Task UpdateAssignmentAsync(DeckKey key, KeyAssignment assignment)
    {
        var profileId = SelectedProfile?.Id ?? throw new InvalidOperationException("No profile is selected.");
        var layer = IsNumLockOn ? NumLockLayer.On : NumLockLayer.Off;
        var configuration = await configurationService.UpdateAsync(current =>
        {
            var profile = current.Profiles.Single(candidate => candidate.Id == profileId);
            return current.ReplaceProfile(profile.WithAssignment(layer, key, assignment));
        }).ConfigureAwait(true);
        await RefreshPreloadedSoundsAsync(configuration).ConfigureAwait(true);
        ReplaceProfiles(configuration, profileId);
    }

    private async Task RefreshPreloadedSoundsAsync(
        AppConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sounds = configuration.Profiles
                .SelectMany(static profile => new[] { profile.NumLockOff, profile.NumLockOn })
                .SelectMany(static layer => layer.Assignments.Values)
                .Select(static assignment => assignment.Action)
                .OfType<PlaySoundActionDefinition>();
            await audioPlaybackService
                .PreloadAsync(sounds, configuration.Settings, cancellationToken)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AppLogger.Error("Preload configured sounds", exception);
        }
    }

    public void Dispose()
    {
        runtimeService.NumLockStateChanged -= RuntimeService_NumLockStateChanged;
        runtimeService.ActionExecutionFailed -= RuntimeService_ActionExecutionFailed;
        obsController.StateChanged -= ObsController_StateChanged;
    }

    private async Task ConnectObsAsync(StreamNumDeck.Core.Settings.ObsConnectionSettings settings)
    {
        try
        {
            await obsController.ConnectAsync(settings).ConfigureAwait(false);
        }
        catch
        {
            // StateChanged carries the connection error to the UI.
        }
    }

    private void ReplaceProfiles(AppConfiguration configuration, Guid selectedId)
    {
        Profiles.Clear();
        foreach (var profile in configuration.Profiles)
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.Single(profile => profile.Id == selectedId);
    }

    private void RefreshTiles()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var layer = SelectedProfile.GetLayer(IsNumLockOn ? NumLockLayer.On : NumLockLayer.Off);
        foreach (var pair in Tiles)
        {
            pair.Value.Update(layer.GetAssignment((DeckKey)Enum.Parse(typeof(DeckKey), pair.Key)), iconAssetStore);
        }
    }

    private void RuntimeService_NumLockStateChanged(object? sender, NumLockStateChangedEventArgs e) =>
        OnUi(() => IsNumLockOn = e.IsOn);

    private void RuntimeService_ActionExecutionFailed(object? sender, ActionExecutionFailedEventArgs e) =>
        OnUi(() => ReportRuntimeActionFailure(e));

    private void ReportRuntimeActionFailure(ActionExecutionFailedEventArgs failure)
    {
        var actionName = DescribeAction(failure.Action);
        AppLogger.Error(actionName, failure.Exception);

        var detail = UserErrorFormatter.GetSafeDetail(failure.Exception);
        if (failure.Action is NoActionDefinition || string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        var signature = $"{failure.Action.GetType().FullName}|{failure.Exception.GetType().FullName}|{detail}";
        var now = DateTimeOffset.UtcNow;
        if (recentErrorNotifications.TryGetValue(signature, out var previous)
            && now - previous < ErrorNotificationCooldown)
        {
            return;
        }

        recentErrorNotifications[signature] = now;
        if (recentErrorNotifications.Count > 100)
        {
            recentErrorNotifications.Clear();
            recentErrorNotifications[signature] = now;
        }

        ErrorTitle = AppStrings.Get("Deck_ActionError.Title", "Action failed");
        ErrorText = AppStrings.Format("ActionError_Message", actionName, detail);
        UserErrorRaised?.Invoke(ErrorTitle, ErrorText);
    }

    private void ObsController_StateChanged(object? sender, ObsConnectionStateChangedEventArgs e) =>
        OnUi(() =>
        {
            if (!string.IsNullOrWhiteSpace(e.ErrorMessage))
            {
                AppLogger.Error("OBS connection", new InvalidOperationException(e.ErrorMessage));
            }

            ApplyObsState(e.State);
        });

    private async Task ApplyCaptureTargetsAsync(
        GlobalSettings settings,
        CancellationToken cancellationToken = default)
    {
        var targets = KeyboardCaptureTargets.None;
        if (settings.CaptureNumpad)
        {
            targets |= KeyboardCaptureTargets.Numpad;
        }

        if (settings.CaptureNavigationBlock)
        {
            targets |= KeyboardCaptureTargets.NavigationBlock;
        }

        await runtimeService.SetCaptureTargetsAsync(targets, cancellationToken).ConfigureAwait(true);
        CaptureNumpad = settings.CaptureNumpad;
        CaptureNavigationBlock = settings.CaptureNavigationBlock;
    }

    private void ApplyObsState(ObsConnectionState state)
    {
        ObsStatusBrush = state switch
        {
            ObsConnectionState.Connected => ObsConnectedBrush,
            ObsConnectionState.Connecting or ObsConnectionState.Reconnecting => ObsConnectingBrush,
            ObsConnectionState.Faulted => ObsFaultedBrush,
            _ => ObsDisconnectedBrush,
        };
        ObsStatusText = state switch
        {
            ObsConnectionState.Connected => AppStrings.Get("Obs_Connected", "OBS connected"),
            ObsConnectionState.Connecting => AppStrings.Get("Obs_Connecting", "Connecting to OBS"),
            ObsConnectionState.Reconnecting => AppStrings.Get("Obs_Reconnecting", "Reconnecting to OBS"),
            ObsConnectionState.Faulted => AppStrings.Get("Obs_ConnectionFailed", "OBS connection failed"),
            _ => AppStrings.Get("Obs_Disconnected", "OBS disconnected"),
        };
    }

    private static string DescribeAction(ActionDefinition action) => action switch
    {
        PlaySoundActionDefinition => AppStrings.Get("Action_PlaySound", "Sound"),
        ToggleMicrophoneMuteActionDefinition => AppStrings.Get("Action_ToggleMicrophoneMute", "Microphone mute"),
        ToggleMasterOutputMuteActionDefinition => AppStrings.Get("Action_ToggleMasterMute", "Master mute"),
        AdjustMasterVolumeActionDefinition => AppStrings.Get("Action_AdjustMasterVolume", "Master volume"),
        AdjustApplicationVolumeActionDefinition => AppStrings.Get("Action_AdjustApplicationVolume", "Application volume"),
        LaunchProcessActionDefinition => AppStrings.Get("Action_LaunchProcess", "Launch program"),
        OpenPathActionDefinition => AppStrings.Get("Action_OpenPath", "Open path"),
        OpenUriActionDefinition => AppStrings.Get("Action_OpenUri", "Open link"),
        KeyboardMacroActionDefinition => AppStrings.Get("Action_KeyboardMacro", "Keyboard macro"),
        ObsActionDefinition => "OBS",
        _ => AppStrings.Get("Action_Execute", "Action"),
    };

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    private static void EnsureUniqueProfileName(AppConfiguration configuration, string name, Guid? excludedId)
    {
        if (configuration.Profiles.Any(profile =>
                profile.Id != excludedId
                && string.Equals(profile.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException(AppStrings.Get("Profile_NameExists"));
        }
    }

    private static string CreateCopyName(string sourceName, AppConfiguration configuration)
    {
        var suffix = AppStrings.Get("Profile_CopySuffix", " copy");
        var baseName = sourceName.Length + suffix.Length <= DeckProfile.MaxNameLength
            ? sourceName
            : sourceName.Substring(0, DeckProfile.MaxNameLength - suffix.Length);
        var candidate = baseName + suffix;
        var number = 2;
        while (configuration.Profiles.Any(profile =>
                   string.Equals(profile.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
        {
            var numberedSuffix = AppStrings.Format("Profile_NumberedCopySuffix", number++);
            candidate = sourceName.Substring(0, Math.Min(sourceName.Length, DeckProfile.MaxNameLength - numberedSuffix.Length))
                        + numberedSuffix;
        }

        return candidate;
    }
}

public sealed record SettingsSnapshot(
    GlobalSettings Settings,
    string? ObsPassword,
    IReadOnlyList<AudioOutputDevice> AudioDevices);
