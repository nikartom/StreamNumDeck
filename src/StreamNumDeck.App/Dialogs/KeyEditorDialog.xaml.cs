using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using StreamNumDeck.App.Presentation;
using StreamNumDeck.App.Localization;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Obs;
using Windows.Storage.Pickers;

namespace StreamNumDeck.App.Dialogs;

public sealed partial class KeyEditorDialog : ContentDialog
{
    private static readonly IReadOnlyList<EditorActionGroupOption> ActionGroups =
    [
        new(ActionGroup.None, AppStrings.Get("ActionGroup_None")),
        new(ActionGroup.Sound, AppStrings.Get("ActionGroup_Sound")),
        new(ActionGroup.Obs, "OBS Studio"),
        new(ActionGroup.System, AppStrings.Get("ActionGroup_System")),
    ];

    private static readonly IReadOnlyList<SoundBehaviorOption> SoundBehaviors =
    [
        new(SoundPlaybackBehavior.PlayAlongside, AppStrings.Get("SoundBehavior_Alongside")),
        new(SoundPlaybackBehavior.RestartSameSound, AppStrings.Get("SoundBehavior_Restart")),
        new(SoundPlaybackBehavior.StopOthers, AppStrings.Get("SoundBehavior_StopOthers")),
    ];

    private readonly IIconAssetStore iconAssetStore;
    private readonly IAudioPlaybackService audioPlaybackService;
    private readonly ISystemAudioControlService systemAudioControlService;
    private readonly ConfigurationService configurationService;
    private readonly IObsController obsController;
    private readonly KeyAssignment otherLayerAssignment;
    private IconReference selectedIcon;
    private string? pendingCustomIconPath;
    private ObsCatalog? obsCatalog;
    private bool obsCatalogLoading;
    private bool audioApplicationsLoading;

    public KeyEditorDialog(
        DeckKey key,
        NumLockLayer layer,
        KeyAssignment assignment,
        KeyAssignment otherLayerAssignment,
        IIconAssetStore iconAssetStore,
        IAudioPlaybackService audioPlaybackService,
        ISystemAudioControlService systemAudioControlService,
        ConfigurationService configurationService,
        IObsController obsController)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(otherLayerAssignment);
        ArgumentNullException.ThrowIfNull(iconAssetStore);
        ArgumentNullException.ThrowIfNull(audioPlaybackService);
        ArgumentNullException.ThrowIfNull(systemAudioControlService);
        ArgumentNullException.ThrowIfNull(configurationService);
        ArgumentNullException.ThrowIfNull(obsController);

        this.iconAssetStore = iconAssetStore;
        this.audioPlaybackService = audioPlaybackService;
        this.systemAudioControlService = systemAudioControlService;
        this.configurationService = configurationService;
        this.obsController = obsController;
        this.otherLayerAssignment = otherLayerAssignment;
        selectedIcon = assignment.Icon;

        InitializeComponent();

        Title = AppStrings.Format(
            "Editor_Title",
            DeckKeyPresentation.GetPhysicalLabel(key),
            layer is NumLockLayer.On ? AppStrings.Get("Common_On") : AppStrings.Get("Common_Off"));
        BuiltInIconGrid.ItemsSource = BuiltInIconCatalog.Options;
        ActionGroupComboBox.ItemsSource = ActionGroups;
        SoundBehaviorComboBox.ItemsSource = SoundBehaviors;
        SoundBehaviorComboBox.SelectedIndex = 1;
        LabelTextBox.Text = assignment.Label;

        ShowIcon(assignment.Icon);
        SelectExistingAction(assignment.Action);
    }

    public KeyAssignment? Result { get; private set; }

    private void SelectExistingAction(ActionDefinition action)
    {
        ActionGroupComboBox.SelectedItem = ActionGroups.Single(option => option.Group == action.Group);
        PopulateActionTypes(action.Group);

        var actionKind = action switch
        {
            NoActionDefinition => EditorActionKind.None,
            PlaySoundActionDefinition => EditorActionKind.PlaySound,
            ToggleMicrophoneMuteActionDefinition => EditorActionKind.ToggleMicrophoneMute,
            ToggleMasterOutputMuteActionDefinition => EditorActionKind.ToggleMasterOutputMute,
            AdjustMasterVolumeActionDefinition master => master.Direction is VolumeAdjustmentDirection.Increase
                ? EditorActionKind.IncreaseMasterVolume
                : EditorActionKind.DecreaseMasterVolume,
            AdjustApplicationVolumeActionDefinition application => application.Direction is VolumeAdjustmentDirection.Increase
                ? EditorActionKind.IncreaseApplicationVolume
                : EditorActionKind.DecreaseApplicationVolume,
            LaunchProcessActionDefinition => EditorActionKind.LaunchProcess,
            OpenPathActionDefinition => EditorActionKind.OpenPath,
            OpenUriActionDefinition => EditorActionKind.OpenUri,
            KeyboardMacroActionDefinition => EditorActionKind.KeyboardMacro,
            ObsActionDefinition obs => MapObsAction(obs.Action),
            _ => EditorActionKind.None,
        };

        ActionTypeComboBox.SelectedItem = ((IEnumerable<EditorActionOption>)ActionTypeComboBox.ItemsSource)
            .Single(option => option.Kind == actionKind);

        switch (action)
        {
            case PlaySoundActionDefinition sound:
                SoundFileTextBox.Text = sound.FilePath;
                SoundVolumeSlider.Value = sound.Volume;
                SoundBehaviorComboBox.SelectedItem = SoundBehaviors.Single(option => option.Behavior == sound.PlaybackBehavior);
                break;

            case AdjustMasterVolumeActionDefinition master:
                VolumeStepNumberBox.Value = master.StepPercent;
                break;

            case AdjustApplicationVolumeActionDefinition application:
                VolumeStepNumberBox.Value = application.StepPercent;
                AudioApplicationComboBox.Text = application.ApplicationId;
                break;

            case LaunchProcessActionDefinition process:
                SystemTargetTextBox.Text = process.ExecutablePath;
                ArgumentsTextBox.Text = process.Arguments ?? string.Empty;
                WorkingDirectoryTextBox.Text = process.WorkingDirectory ?? string.Empty;
                break;

            case OpenPathActionDefinition path:
                SystemTargetTextBox.Text = path.Path;
                break;

            case OpenUriActionDefinition uri:
                SystemTargetTextBox.Text = uri.Uri;
                break;

            case KeyboardMacroActionDefinition macro:
                MacroTextBox.Text = KeyboardMacroTextCodec.Format(macro.Steps);
                break;

            case ObsActionDefinition obs:
                ObsTargetComboBox.Text = obs.TargetName ?? string.Empty;
                break;
        }

        UpdateActionFields();
    }

    private void ActionGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActionGroupComboBox.SelectedItem is not EditorActionGroupOption group)
        {
            return;
        }

        PopulateActionTypes(group.Group);
        ActionTypeComboBox.SelectedIndex = 0;
    }

    private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionFields();

    private void PopulateActionTypes(ActionGroup group)
    {
        ActionTypeComboBox.ItemsSource = group switch
        {
            ActionGroup.Sound => new[]
            {
                new EditorActionOption(EditorActionKind.PlaySound, AppStrings.Get("Action_PlaySound")),
                new EditorActionOption(EditorActionKind.ToggleMicrophoneMute, AppStrings.Get("Action_ToggleMicrophoneMute")),
                new EditorActionOption(EditorActionKind.ToggleMasterOutputMute, AppStrings.Get("Action_ToggleMasterMute")),
                new EditorActionOption(EditorActionKind.IncreaseMasterVolume, AppStrings.Get("Action_IncreaseMasterVolume")),
                new EditorActionOption(EditorActionKind.DecreaseMasterVolume, AppStrings.Get("Action_DecreaseMasterVolume")),
                new EditorActionOption(EditorActionKind.IncreaseApplicationVolume, AppStrings.Get("Action_IncreaseApplicationVolume")),
                new EditorActionOption(EditorActionKind.DecreaseApplicationVolume, AppStrings.Get("Action_DecreaseApplicationVolume")),
            },
            ActionGroup.Obs => new[]
            {
                new EditorActionOption(EditorActionKind.ObsSwitchScene, AppStrings.Get("Action_ObsSwitchScene")),
                new EditorActionOption(EditorActionKind.ObsToggleSource, AppStrings.Get("Action_ObsToggleSource")),
                new EditorActionOption(EditorActionKind.ObsToggleMute, AppStrings.Get("Action_ObsToggleMute")),
                new EditorActionOption(EditorActionKind.ObsStartStream, AppStrings.Get("Action_ObsStartStream")),
                new EditorActionOption(EditorActionKind.ObsStopStream, AppStrings.Get("Action_ObsStopStream")),
                new EditorActionOption(EditorActionKind.ObsStartRecord, AppStrings.Get("Action_ObsStartRecord")),
                new EditorActionOption(EditorActionKind.ObsStopRecord, AppStrings.Get("Action_ObsStopRecord")),
                new EditorActionOption(EditorActionKind.ObsSaveReplay, AppStrings.Get("Action_ObsSaveReplay")),
                new EditorActionOption(EditorActionKind.ObsRestartMedia, AppStrings.Get("Action_ObsRestartMedia")),
            },
            ActionGroup.System => new[]
            {
                new EditorActionOption(EditorActionKind.LaunchProcess, AppStrings.Get("Action_LaunchProcess")),
                new EditorActionOption(EditorActionKind.OpenPath, AppStrings.Get("Action_OpenPath")),
                new EditorActionOption(EditorActionKind.OpenUri, AppStrings.Get("Action_OpenUri")),
                new EditorActionOption(EditorActionKind.KeyboardMacro, AppStrings.Get("Action_KeyboardMacro")),
            },
            _ => new[]
            {
                new EditorActionOption(EditorActionKind.None, AppStrings.Get("Action_None")),
            },
        };
    }

    private void UpdateActionFields()
    {
        if (ActionTypeComboBox.SelectedItem is not EditorActionOption action)
        {
            return;
        }

        SoundFields.Visibility = action.Kind is EditorActionKind.PlaySound ? Visibility.Visible : Visibility.Collapsed;
        var adjustsSystemAudio = action.Kind is
            EditorActionKind.IncreaseMasterVolume or
            EditorActionKind.DecreaseMasterVolume or
            EditorActionKind.IncreaseApplicationVolume or
            EditorActionKind.DecreaseApplicationVolume;
        var adjustsApplication = action.Kind is
            EditorActionKind.IncreaseApplicationVolume or
            EditorActionKind.DecreaseApplicationVolume;
        SystemAudioFields.Visibility = adjustsSystemAudio ? Visibility.Visible : Visibility.Collapsed;
        AudioApplicationComboBox.Visibility = adjustsApplication ? Visibility.Visible : Visibility.Collapsed;
        AudioApplicationHint.Visibility = adjustsApplication ? Visibility.Visible : Visibility.Collapsed;
        if (adjustsApplication)
        {
            _ = LoadAudioApplicationsAsync();
        }
        ObsFields.Visibility = IsObsAction(action.Kind) ? Visibility.Visible : Visibility.Collapsed;
        SystemFields.Visibility = IsSystemAction(action.Kind) ? Visibility.Visible : Visibility.Collapsed;
        MacroFields.Visibility = action.Kind is EditorActionKind.KeyboardMacro ? Visibility.Visible : Visibility.Collapsed;

        var requiresObsTarget = action.Kind is
            EditorActionKind.ObsSwitchScene or
            EditorActionKind.ObsToggleSource or
            EditorActionKind.ObsToggleMute or
            EditorActionKind.ObsRestartMedia;
        ObsTargetComboBox.Visibility = requiresObsTarget ? Visibility.Visible : Visibility.Collapsed;
        if (requiresObsTarget)
        {
            _ = LoadObsTargetsAsync(action.Kind);
        }

        var launchesProcess = action.Kind is EditorActionKind.LaunchProcess;
        ArgumentsTextBox.Visibility = launchesProcess ? Visibility.Visible : Visibility.Collapsed;
        WorkingDirectoryTextBox.Visibility = launchesProcess ? Visibility.Visible : Visibility.Collapsed;
        SystemBrowseButton.Visibility = action.Kind is EditorActionKind.OpenUri ? Visibility.Collapsed : Visibility.Visible;
        SystemTargetTextBox.Header = action.Kind is EditorActionKind.OpenUri
            ? AppStrings.Get("Field_Address")
            : AppStrings.Get("Field_ProgramFileFolder");
    }

    private void BuiltInIconGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BuiltInIconGrid.SelectedItem is not BuiltInIconOption icon)
        {
            return;
        }

        pendingCustomIconPath = null;
        selectedIcon = IconReference.BuiltIn(icon.Id);
        ShowBuiltInIcon(icon);
    }

    private void CopyOtherLayer_Click(object sender, RoutedEventArgs e)
    {
        pendingCustomIconPath = null;
        selectedIcon = otherLayerAssignment.Icon;
        LabelTextBox.Text = otherLayerAssignment.Label;
        ShowIcon(otherLayerAssignment.Icon);
        SelectExistingAction(otherLayerAssignment.Action);
        ValidationMessage.IsOpen = false;
    }

    private async void ChooseCustomIcon_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = CreateFilePicker(".png", ".jpg", ".jpeg", ".webp");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            pendingCustomIconPath = file.Path;
            BuiltInIconGrid.SelectedItem = null;
            IconPreviewGlyph.Visibility = Visibility.Collapsed;
            IconPreviewImage.Source = new BitmapImage(new Uri(file.Path));
            IconPreviewImage.Visibility = Visibility.Visible;
            CustomIconFileName.Text = file.Name;
        }
        catch (Exception exception)
        {
            ShowValidationError(exception.Message);
        }
    }

    private async void ChooseSoundFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = CreateFilePicker(".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac");
        picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            SoundFileTextBox.Text = file.Path;
        }
    }

    private async void PreviewSound_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ValidationMessage.IsOpen = false;
            var sound = new PlaySoundActionDefinition(
                SoundFileTextBox.Text,
                (int)Math.Round(SoundVolumeSlider.Value),
                SoundPlaybackBehavior.RestartSameSound);
            var configuration = await configurationService.GetAsync();
            await audioPlaybackService.PlayAsync(sound, configuration.Settings);
        }
        catch (Exception exception)
        {
            ShowValidationError(exception.Message);
        }
    }

    private async void StopSound_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await audioPlaybackService.StopAllAsync();
        }
        catch (Exception exception)
        {
            ShowValidationError(exception.Message);
        }
    }

    private async void ChooseSystemTarget_Click(object sender, RoutedEventArgs e)
    {
        var picker = CreateFilePicker("*");
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            SystemTargetTextBox.Text = file.Path;
        }
    }

    private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;

        try
        {
            var icon = pendingCustomIconPath is null
                ? selectedIcon
                : await iconAssetStore.ImportAsync(pendingCustomIconPath);
            var action = CreateAction();
            Result = new KeyAssignment(LabelTextBox.Text, icon, action);
            Hide();
        }
        catch (Exception exception)
        {
            ShowValidationError(exception.Message);
        }
    }

    private void SecondaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = KeyAssignment.Empty;
    }

    private ActionDefinition CreateAction()
    {
        var kind = (ActionTypeComboBox.SelectedItem as EditorActionOption)?.Kind
            ?? EditorActionKind.None;

        return kind switch
        {
            EditorActionKind.PlaySound => new PlaySoundActionDefinition(
                SoundFileTextBox.Text,
                (int)Math.Round(SoundVolumeSlider.Value),
                ((SoundBehaviorOption)SoundBehaviorComboBox.SelectedItem).Behavior),
            EditorActionKind.ToggleMicrophoneMute => new ToggleMicrophoneMuteActionDefinition(),
            EditorActionKind.ToggleMasterOutputMute => new ToggleMasterOutputMuteActionDefinition(),
            EditorActionKind.IncreaseMasterVolume => new AdjustMasterVolumeActionDefinition(
                VolumeAdjustmentDirection.Increase,
                GetVolumeStep()),
            EditorActionKind.DecreaseMasterVolume => new AdjustMasterVolumeActionDefinition(
                VolumeAdjustmentDirection.Decrease,
                GetVolumeStep()),
            EditorActionKind.IncreaseApplicationVolume => new AdjustApplicationVolumeActionDefinition(
                GetSelectedApplicationId(),
                VolumeAdjustmentDirection.Increase,
                GetVolumeStep()),
            EditorActionKind.DecreaseApplicationVolume => new AdjustApplicationVolumeActionDefinition(
                GetSelectedApplicationId(),
                VolumeAdjustmentDirection.Decrease,
                GetVolumeStep()),
            EditorActionKind.LaunchProcess => new LaunchProcessActionDefinition(
                SystemTargetTextBox.Text,
                ArgumentsTextBox.Text,
                WorkingDirectoryTextBox.Text),
            EditorActionKind.OpenPath => new OpenPathActionDefinition(SystemTargetTextBox.Text),
            EditorActionKind.OpenUri => new OpenUriActionDefinition(SystemTargetTextBox.Text),
            EditorActionKind.KeyboardMacro => new KeyboardMacroActionDefinition(
                KeyboardMacroTextCodec.Parse(MacroTextBox.Text)),
            EditorActionKind.ObsSwitchScene => new ObsActionDefinition(ObsActionKind.SwitchScene, ObsTargetComboBox.Text),
            EditorActionKind.ObsToggleSource => new ObsActionDefinition(ObsActionKind.ToggleSourceVisibility, ObsTargetComboBox.Text),
            EditorActionKind.ObsToggleMute => new ObsActionDefinition(ObsActionKind.ToggleInputMute, ObsTargetComboBox.Text),
            EditorActionKind.ObsStartStream => new ObsActionDefinition(ObsActionKind.StartStreaming),
            EditorActionKind.ObsStopStream => new ObsActionDefinition(ObsActionKind.StopStreaming),
            EditorActionKind.ObsStartRecord => new ObsActionDefinition(ObsActionKind.StartRecording),
            EditorActionKind.ObsStopRecord => new ObsActionDefinition(ObsActionKind.StopRecording),
            EditorActionKind.ObsSaveReplay => new ObsActionDefinition(ObsActionKind.SaveReplayBuffer),
            EditorActionKind.ObsRestartMedia => new ObsActionDefinition(ObsActionKind.RestartMediaSource, ObsTargetComboBox.Text),
            _ => new NoActionDefinition(),
        };
    }

    private void ShowIcon(IconReference icon)
    {
        if (icon.Kind is IconKind.BuiltIn)
        {
            var option = BuiltInIconCatalog.Get(icon.Value);
            BuiltInIconGrid.SelectedItem = option;
            ShowBuiltInIcon(option);
            return;
        }

        var path = iconAssetStore.ResolvePath(icon);
        if (File.Exists(path))
        {
            BuiltInIconGrid.SelectedItem = null;
            IconPreviewGlyph.Visibility = Visibility.Collapsed;
            IconPreviewImage.Source = new BitmapImage(new Uri(path));
            IconPreviewImage.Visibility = Visibility.Visible;
            CustomIconFileName.Text = Path.GetFileName(path);
        }
    }

    private void ShowBuiltInIcon(BuiltInIconOption icon)
    {
        IconPreviewImage.Source = null;
        IconPreviewImage.Visibility = Visibility.Collapsed;
        IconPreviewGlyph.Glyph = icon.Glyph;
        IconPreviewGlyph.Visibility = Visibility.Visible;
        CustomIconFileName.Text = string.Empty;
    }

    private void ShowValidationError(string message)
    {
        ValidationMessage.Message = message;
        ValidationMessage.IsOpen = true;
    }

    private async Task LoadObsTargetsAsync(EditorActionKind actionKind)
    {
        if (obsController.State is not ObsConnectionState.Connected)
        {
            return;
        }

        try
        {
            if (obsCatalog is null && !obsCatalogLoading)
            {
                obsCatalogLoading = true;
                obsCatalog = await obsController.GetCatalogAsync();
            }

            if (obsCatalog is null)
            {
                return;
            }

            var currentText = ObsTargetComboBox.Text;
            ObsTargetComboBox.Header = actionKind is EditorActionKind.ObsSwitchScene
                ? AppStrings.Get("Field_Scene")
                : AppStrings.Get("Field_Source");
            ObsTargetComboBox.ItemsSource = actionKind switch
            {
                EditorActionKind.ObsSwitchScene => obsCatalog.Scenes,
                EditorActionKind.ObsToggleMute or EditorActionKind.ObsRestartMedia => obsCatalog.Inputs,
                _ => obsCatalog.Sources,
            };
            ObsTargetComboBox.Text = currentText;
        }
        catch
        {
            // Manual entry remains available when the OBS catalog cannot be read.
        }
        finally
        {
            obsCatalogLoading = false;
        }
    }

    private async Task LoadAudioApplicationsAsync()
    {
        if (audioApplicationsLoading)
        {
            return;
        }

        audioApplicationsLoading = true;
        try
        {
            var currentText = AudioApplicationComboBox.Text;
            var applications = await systemAudioControlService.GetApplicationsAsync();
            AudioApplicationComboBox.ItemsSource = applications;
            AudioApplicationComboBox.SelectedItem = applications.FirstOrDefault(application =>
                string.Equals(application.Id, currentText, StringComparison.OrdinalIgnoreCase));
            if (AudioApplicationComboBox.SelectedItem is null)
            {
                AudioApplicationComboBox.Text = currentText;
            }
        }
        catch (Exception exception)
        {
            ShowValidationError(AppStrings.Format("Error_GetAudioSessions", exception.Message));
        }
        finally
        {
            audioApplicationsLoading = false;
        }
    }

    private int GetVolumeStep()
    {
        if (double.IsNaN(VolumeStepNumberBox.Value))
        {
            throw new InvalidOperationException(AppStrings.Get("Error_VolumeStepRequired"));
        }

        return checked((int)Math.Round(VolumeStepNumberBox.Value));
    }

    private string GetSelectedApplicationId()
    {
        if (AudioApplicationComboBox.SelectedItem is AudioApplication application)
        {
            return application.Id;
        }

        return AudioApplicationComboBox.Text;
    }

    private static FileOpenPicker CreateFilePicker(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, global::StreamNumDeck.App.App.WindowHandle);
        foreach (var extension in extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        return picker;
    }

    private static bool IsObsAction(EditorActionKind kind) => kind is >= EditorActionKind.ObsSwitchScene and <= EditorActionKind.ObsRestartMedia;

    private static bool IsSystemAction(EditorActionKind kind) => kind is
        EditorActionKind.LaunchProcess or
        EditorActionKind.OpenPath or
        EditorActionKind.OpenUri;

    private static EditorActionKind MapObsAction(ObsActionKind action) => action switch
    {
        ObsActionKind.SwitchScene => EditorActionKind.ObsSwitchScene,
        ObsActionKind.ToggleSourceVisibility => EditorActionKind.ObsToggleSource,
        ObsActionKind.ToggleInputMute => EditorActionKind.ObsToggleMute,
        ObsActionKind.StartStreaming => EditorActionKind.ObsStartStream,
        ObsActionKind.StopStreaming => EditorActionKind.ObsStopStream,
        ObsActionKind.StartRecording => EditorActionKind.ObsStartRecord,
        ObsActionKind.StopRecording => EditorActionKind.ObsStopRecord,
        ObsActionKind.SaveReplayBuffer => EditorActionKind.ObsSaveReplay,
        ObsActionKind.RestartMediaSource => EditorActionKind.ObsRestartMedia,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private sealed record EditorActionGroupOption(ActionGroup Group, string Name);

    private sealed record EditorActionOption(EditorActionKind Kind, string Name);

    private sealed record SoundBehaviorOption(SoundPlaybackBehavior Behavior, string Name);

    private enum EditorActionKind
    {
        None,
        PlaySound,
        ToggleMicrophoneMute,
        ToggleMasterOutputMute,
        IncreaseMasterVolume,
        DecreaseMasterVolume,
        IncreaseApplicationVolume,
        DecreaseApplicationVolume,
        ObsSwitchScene,
        ObsToggleSource,
        ObsToggleMute,
        ObsStartStream,
        ObsStopStream,
        ObsStartRecord,
        ObsStopRecord,
        ObsSaveReplay,
        ObsRestartMedia,
        LaunchProcess,
        OpenPath,
        OpenUri,
        KeyboardMacro,
    }
}
