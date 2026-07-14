using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using StreamNumDeck.App.Localization;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Input;
using StreamNumDeck.Core.Obs;

namespace StreamNumDeck.App.ViewModels;

public partial class DeckPageViewModel(
    ConfigurationService configurationService,
    IIconAssetStore iconAssetStore,
    DeckRuntimeService runtimeService,
    IAudioPlaybackService audioPlaybackService,
    IObsController obsController) : ObservableObject, IDisposable
{
    private static readonly Brush ObsDisconnectedBrush = CreateStatusBrush(138, 136, 134);
    private static readonly Brush ObsConnectingBrush = CreateStatusBrush(247, 187, 0);
    private static readonly Brush ObsConnectedBrush = CreateStatusBrush(16, 124, 16);
    private static readonly Brush ObsFaultedBrush = CreateStatusBrush(209, 52, 56);
    private bool initialized;

    public IReadOnlyDictionary<string, DeckKeyTileViewModel> Tiles { get; } = DeckKeyCatalog
        .AssignableKeys
        .ToDictionary(
            static key => key.ToString(),
            static key => new DeckKeyTileViewModel(key),
            StringComparer.Ordinal);

    public ObservableCollection<DeckProfile> Profiles { get; } = [];

    [ObservableProperty]
    public partial DeckProfile? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial bool IsNumLockOn { get; set; }

    [ObservableProperty]
    public partial bool IsCaptureEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsCapturePaused { get; private set; }

    [ObservableProperty]
    public partial string ObsStatusText { get; private set; } = AppStrings.Get("Obs_Disconnected");

    [ObservableProperty]
    public partial Brush ObsStatusBrush { get; private set; } = ObsDisconnectedBrush;

    [ObservableProperty]
    public partial bool IsActionErrorOpen { get; set; }

    [ObservableProperty]
    public partial string ActionErrorText { get; private set; } = string.Empty;

    public bool IsInitialized => initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        var configuration = await configurationService.GetAsync(cancellationToken);

        Profiles.Clear();
        foreach (var profile in configuration.Profiles)
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.Single(profile => profile.Id == configuration.ActiveProfileId);
        await audioPlaybackService.PreloadAsync(
            configuration.Profiles
                .SelectMany(static profile => new[] { profile.NumLockOff, profile.NumLockOn })
                .SelectMany(static layer => layer.Assignments.Values)
                .Select(static assignment => assignment.Action)
                .SelectMany(static action => action.EnumerateExecutableActions())
                .OfType<PlaySoundActionDefinition>(),
            configuration.Settings,
            cancellationToken);
        obsController.StateChanged += ObsController_StateChanged;
        ApplyObsState(obsController.State, obsController.LastError);
        _ = TryConnectObsAsync(configuration.Settings.Obs);
        runtimeService.CaptureStateChanged += RuntimeService_StateChanged;
        runtimeService.NumLockStateChanged += RuntimeService_NumLockStateChanged;
        runtimeService.ActionExecutionFailed += RuntimeService_ActionExecutionFailed;
        IsNumLockOn = runtimeService.IsNumLockOn;
        if (configuration.Settings.EnableCaptureOnStartup)
        {
            await runtimeService.StartAsync(cancellationToken);
        }

        ApplyCaptureState(runtimeService.CaptureState);
        initialized = true;
        RefreshTiles();
    }

    public async Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (!initialized)
        {
            return;
        }

        if (enabled)
        {
            await runtimeService.StartAsync(cancellationToken);
        }
        else
        {
            await runtimeService.StopAsync(cancellationToken);
        }

        ApplyCaptureState(runtimeService.CaptureState);
    }

    public Task SetNumLockAsync(bool isOn, CancellationToken cancellationToken = default) =>
        runtimeService.SetNumLockAsync(isOn, cancellationToken);

    public async Task ReloadProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetAsync(cancellationToken);
        var profile = configuration.Profiles.Single(candidate => candidate.Id == profileId);

        Profiles.Clear();
        foreach (var candidate in configuration.Profiles)
        {
            Profiles.Add(candidate);
        }

        SelectedProfile = profile;
        RefreshTiles();
    }

    public KeyAssignment GetCurrentAssignment(DeckKey key)
    {
        var layer = IsNumLockOn ? NumLockLayer.On : NumLockLayer.Off;
        return GetAssignment(key, layer);
    }

    public KeyAssignment GetAssignment(DeckKey key, NumLockLayer layer)
    {
        return SelectedProfile?.GetLayer(layer).GetAssignment(key) ?? KeyAssignment.Empty;
    }

    public async Task UpdateAssignmentAsync(
        DeckKey key,
        KeyAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var selectedProfileId = SelectedProfile?.Id
            ?? throw new InvalidOperationException("No profile is selected.");
        var layer = IsNumLockOn ? NumLockLayer.On : NumLockLayer.Off;

        var updatedConfiguration = await configurationService.UpdateAsync(
            configuration =>
            {
                var profile = configuration.Profiles.Single(candidate => candidate.Id == selectedProfileId);
                return configuration.ReplaceProfile(profile.WithAssignment(layer, key, assignment));
            },
            cancellationToken);

        var updatedProfile = updatedConfiguration.Profiles.Single(profile => profile.Id == selectedProfileId);
        var profileIndex = Profiles.IndexOf(Profiles.Single(profile => profile.Id == selectedProfileId));
        Profiles[profileIndex] = updatedProfile;
        SelectedProfile = updatedProfile;
        RefreshTiles();

        if (assignment.Action is PlaySoundActionDefinition sound)
        {
            await audioPlaybackService.PreloadAsync(
                [sound],
                updatedConfiguration.Settings,
                cancellationToken);
        }
    }

    partial void OnSelectedProfileChanged(DeckProfile? value) => RefreshTiles();

    partial void OnIsNumLockOnChanged(bool value) => RefreshTiles();

    public void Dispose()
    {
        runtimeService.CaptureStateChanged -= RuntimeService_StateChanged;
        runtimeService.NumLockStateChanged -= RuntimeService_NumLockStateChanged;
        runtimeService.ActionExecutionFailed -= RuntimeService_ActionExecutionFailed;
        obsController.StateChanged -= ObsController_StateChanged;
        GC.SuppressFinalize(this);
    }

    private void ObsController_StateChanged(object? sender, ObsConnectionStateChangedEventArgs e)
    {
        global::StreamNumDeck.App.App.DispatcherQueue.TryEnqueue(() => ApplyObsState(e.State, e.ErrorMessage));
    }

    private async Task TryConnectObsAsync(StreamNumDeck.Core.Settings.ObsConnectionSettings settings)
    {
        try
        {
            await obsController.ConnectAsync(settings);
        }
        catch
        {
            // State and the actionable error are exposed by IObsController.
        }
    }

    private void ApplyObsState(ObsConnectionState connectionState, string? error)
    {
        ObsStatusBrush = connectionState switch
        {
            ObsConnectionState.Connected => ObsConnectedBrush,
            ObsConnectionState.Connecting or ObsConnectionState.Reconnecting => ObsConnectingBrush,
            ObsConnectionState.Faulted => ObsFaultedBrush,
            _ => ObsDisconnectedBrush,
        };
        ObsStatusText = connectionState switch
        {
            ObsConnectionState.Connected => AppStrings.Get("Obs_Connected"),
            ObsConnectionState.Connecting => AppStrings.Get("Obs_Connecting"),
            ObsConnectionState.Reconnecting => string.IsNullOrWhiteSpace(error)
                ? AppStrings.Get("Obs_Reconnecting")
                : AppStrings.Format("Obs_ReconnectingError", error),
            ObsConnectionState.Faulted => string.IsNullOrWhiteSpace(error)
                ? AppStrings.Get("Obs_ConnectionFailed")
                : AppStrings.Format("Obs_Error", error),
            _ => AppStrings.Get("Obs_Disconnected"),
        };
    }

    private static Brush CreateStatusBrush(byte red, byte green, byte blue) =>
        new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, red, green, blue));

    private void RuntimeService_StateChanged(object? sender, KeyboardCaptureStateChangedEventArgs e)
    {
        global::StreamNumDeck.App.App.DispatcherQueue.TryEnqueue(() => ApplyCaptureState(e.State));
    }

    private void RuntimeService_NumLockStateChanged(object? sender, NumLockStateChangedEventArgs e)
    {
        global::StreamNumDeck.App.App.DispatcherQueue.TryEnqueue(() => IsNumLockOn = e.IsOn);
    }

    private void RuntimeService_ActionExecutionFailed(object? sender, ActionExecutionFailedEventArgs e)
    {
        global::StreamNumDeck.App.App.DispatcherQueue.TryEnqueue(() =>
        {
            ActionErrorText = AppStrings.Format("ActionError_Message", DescribeAction(e.Action), e.Exception.Message);
            IsActionErrorOpen = true;
        });
    }

    private static string DescribeAction(ActionDefinition action) => action switch
    {
        PlaySoundActionDefinition => AppStrings.Get("Action_PlaySound"),
        ToggleMicrophoneMuteActionDefinition => AppStrings.Get("Action_ToggleMicrophoneMute"),
        ToggleMasterOutputMuteActionDefinition => AppStrings.Get("Action_ToggleMasterMute"),
        AdjustMasterVolumeActionDefinition => AppStrings.Get("Action_AdjustMasterVolume"),
        AdjustApplicationVolumeActionDefinition => AppStrings.Get("Action_AdjustApplicationVolume"),
        LaunchProcessActionDefinition => AppStrings.Get("Action_LaunchProcess"),
        OpenPathActionDefinition => AppStrings.Get("Action_OpenPath"),
        OpenUriActionDefinition => AppStrings.Get("Action_OpenUri"),
        KeyboardMacroActionDefinition => AppStrings.Get("Action_KeyboardMacro"),
        AutomationActionDefinition => AppStrings.Get("Action_Automation"),
        ObsActionDefinition obs => AppStrings.Format("Action_ObsFormat", obs.Action),
        _ => AppStrings.Get("Action_Execute"),
    };

    private void ApplyCaptureState(KeyboardCaptureState state)
    {
        IsCaptureEnabled = state is not KeyboardCaptureState.Stopped;
        IsCapturePaused = state is KeyboardCaptureState.Paused;
    }

    private void RefreshTiles()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var layer = SelectedProfile.GetLayer(IsNumLockOn ? NumLockLayer.On : NumLockLayer.Off);
        foreach (var (keyName, tile) in Tiles)
        {
            var key = Enum.Parse<DeckKey>(keyName);
            tile.Update(layer.GetAssignment(key), iconAssetStore);
        }
    }
}
