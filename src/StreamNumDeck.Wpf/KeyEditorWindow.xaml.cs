using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.App.Localization;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Wpf.Services;

namespace StreamNumDeck.Wpf;

public partial class KeyEditorWindow : Window
{
    private static readonly IReadOnlyList<ActionGroupOption> ActionGroups = new[]
    {
        new ActionGroupOption("none", AppStrings.Get("ActionGroup_None", "None"), "\uE710"),
        new ActionGroupOption("sound", AppStrings.Get("ActionGroup_Sound", "Sound"), "\uE767"),
        new ActionGroupOption("obs", "OBS Studio", "\uE95A"),
        new ActionGroupOption("system", AppStrings.Get("ActionGroup_System", "System"), "\uE756"),
    };

    private static readonly IReadOnlyList<ActionOption> Actions = new[]
    {
        new ActionOption("none", AppStrings.Get("Action_None", "No action"), null, false, false),
        new ActionOption("sound", AppStrings.Get("Action_PlaySound", "Play sound"), AppStrings.Get("Editor_AudioFile.Header", "Audio file"), true, true),
        new ActionOption(
            "mic-mute",
            AppStrings.Get("Icon_microphone", "Microphone"),
            null,
            false,
            false,
            AppStrings.Get("Action_ToggleMicrophoneMute", "Toggle microphone mute")),
        new ActionOption("master-mute", AppStrings.Get("Action_ToggleMasterMute", "Toggle master mute"), null, false, false),
        new ActionOption("volume-up", AppStrings.Get("Action_IncreaseMasterVolume", "Master volume up"), null, false, true),
        new ActionOption("volume-down", AppStrings.Get("Action_DecreaseMasterVolume", "Master volume down"), null, false, true),
        new ActionOption("app-volume-up", AppStrings.Get("Action_IncreaseApplicationVolume", "Application volume up"), AppStrings.Get("Editor_Application.Header", "Application"), false, true),
        new ActionOption("app-volume-down", AppStrings.Get("Action_DecreaseApplicationVolume", "Application volume down"), AppStrings.Get("Editor_Application.Header", "Application"), false, true),
        new ActionOption("url", AppStrings.Get("Action_OpenUri", "Open web link"), AppStrings.Get("Field_Address", "Address"), false, false),
        new ActionOption("path", AppStrings.Get("Action_OpenPath", "Open file or folder"), AppStrings.Get("Field_ProgramFileFolder", "Path"), true, false),
        new ActionOption("launch", AppStrings.Get("Action_LaunchProcess", "Launch program"), AppStrings.Get("Editor_SystemTarget.Header", "Executable file"), true, false),
        new ActionOption("macro", AppStrings.Get("Action_KeyboardMacro", "Keyboard macro"), AppStrings.Get("Editor_MacroSequence.Header", "Shortcut sequence"), false, false),
        new ActionOption("obs-scene", AppStrings.Get("Action_ObsSwitchScene", "OBS: switch scene"), AppStrings.Get("Field_Scene", "Scene"), false, false),
        new ActionOption("obs-source", AppStrings.Get("Action_ObsToggleSource", "OBS: toggle source"), AppStrings.Get("Field_Source", "Source"), false, false),
        new ActionOption("obs-input-mute", AppStrings.Get("Action_ObsToggleMute", "OBS: toggle input mute"), AppStrings.Get("Editor_ObsTarget.Header", "Input"), false, false),
        new ActionOption("obs-stream-start", AppStrings.Get("Action_ObsStartStream", "OBS: start streaming"), null, false, false),
        new ActionOption("obs-stream-stop", AppStrings.Get("Action_ObsStopStream", "OBS: stop streaming"), null, false, false),
        new ActionOption("obs-record-start", AppStrings.Get("Action_ObsStartRecord", "OBS: start recording"), null, false, false),
        new ActionOption("obs-record-stop", AppStrings.Get("Action_ObsStopRecord", "OBS: stop recording"), null, false, false),
        new ActionOption("obs-replay", AppStrings.Get("Action_ObsSaveReplay", "OBS: save replay buffer"), null, false, false),
        new ActionOption("obs-media-restart", AppStrings.Get("Action_ObsRestartMedia", "OBS: restart media source"), AppStrings.Get("Field_Source", "Source"), false, false),
    };

    private readonly Func<string, CancellationToken, Task<IconReference>> importIcon;
    private readonly Func<IconReference, string?> resolveIconPath;
    private readonly Func<CancellationToken, Task<ObsCatalog>> getObsCatalog;
    private readonly Func<CancellationToken, Task<IReadOnlyList<AudioApplication>>> getAudioApplications;
    private readonly KeyAssignment otherLayerAssignment;
    private readonly Func<string, int, SoundPlaybackBehavior, CancellationToken, Task> previewSound;
    private readonly Func<CancellationToken, Task> stopSounds;
    private IconReference selectedIcon = IconReference.BuiltIn("plus");

    public KeyEditorWindow(
        DeckKey key,
        KeyAssignment current,
        KeyAssignment otherLayerAssignment,
        Func<string, CancellationToken, Task<IconReference>> importIcon,
        Func<IconReference, string?> resolveIconPath,
        Func<CancellationToken, Task<ObsCatalog>> getObsCatalog,
        Func<CancellationToken, Task<IReadOnlyList<AudioApplication>>> getAudioApplications,
        Func<string, int, SoundPlaybackBehavior, CancellationToken, Task> previewSound,
        Func<CancellationToken, Task> stopSounds)
    {
        this.importIcon = importIcon;
        this.resolveIconPath = resolveIconPath;
        this.otherLayerAssignment = otherLayerAssignment;
        this.getObsCatalog = getObsCatalog;
        this.getAudioApplications = getAudioApplications;
        this.previewSound = previewSound;
        this.stopSounds = stopSounds;
        InitializeComponent();
        var isNumLockOn = System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.NumLock);
        var currentLayerName = AppStrings.Get(isNumLockOn ? "Common_On" : "Common_Off", isNumLockOn ? "on" : "off");
        var otherLayerName = AppStrings.Get(isNumLockOn ? "Common_Off" : "Common_On", isNumLockOn ? "off" : "on");
        Title = AppStrings.Format(
            "Editor_Title",
            StreamNumDeck.Wpf.Presentation.DeckPresentation.GetPhysicalLabel(key),
            currentLayerName);
        CopyOtherLayerButton.Content = $"← NumLock {otherLayerName}";
        CopyOtherLayerButton.ToolTip = $"{AppStrings.Get("Editor_CopyOtherLayer.Content", "Copy assignment from the other layer")} — NumLock {otherLayerName}";
        IconListBox.ItemsSource = new[] { StreamNumDeck.App.Presentation.BuiltInIconCatalog.BlankOption }
            .Concat(StreamNumDeck.App.Presentation.BuiltInIconCatalog.Options.Where(option => option.Id != "square"))
            .ToArray();
        ActionGroupListBox.ItemsSource = ActionGroups;
        SoundBehaviorComboBox.ItemsSource = new[]
        {
            new SoundBehaviorOption(SoundPlaybackBehavior.PlayAlongside, AppStrings.Get("SoundBehavior_Alongside", "Play alongside")),
            new SoundBehaviorOption(SoundPlaybackBehavior.RestartSameSound, AppStrings.Get("SoundBehavior_Restart", "Restart the same sound")),
            new SoundBehaviorOption(SoundPlaybackBehavior.StopOthers, AppStrings.Get("SoundBehavior_StopOthers", "Stop other sounds")),
        };
        ApplyAssignment(current);
    }

    public KeyAssignment Assignment { get; private set; } = KeyAssignment.Empty;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ActionGroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActionGroupListBox.SelectedItem is not ActionGroupOption group)
        {
            return;
        }

        var currentId = (ActionListBox.SelectedItem as ActionOption)?.Id;
        PopulateActions(group.Id, currentId);
    }

    private void ActionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionFields();
        if (ActionListBox.SelectedItem is ActionOption option)
        {
            _ = LoadParameterSuggestionsAsync(option.Id);
        }
    }

    private void NestedActionList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || EditorScrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var scrollLines = SystemParameters.WheelScrollLines;
        var distancePerNotch = scrollLines < 0
            ? EditorScrollViewer.ViewportHeight
            : Math.Max(1, scrollLines) * 16d;
        var notches = e.Delta / 120d;
        EditorScrollViewer.ScrollToVerticalOffset(
            EditorScrollViewer.VerticalOffset - (notches * distancePerNotch));
        e.Handled = true;
    }

    private void IconListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IconListBox.SelectedItem is not StreamNumDeck.App.Presentation.BuiltInIconOption option)
        {
            return;
        }

        selectedIcon = IconReference.BuiltIn(option.Id);
        ShowBuiltInIcon(option);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeValueText is not null)
        {
            VolumeValueText.Text = $"{Math.Round(e.NewValue):0}%";
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var option = ActionListBox.SelectedItem as ActionOption;
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = option?.Id == "sound"
                ? $"{AppStrings.Get("FileFilter_Audio", "Audio files")}|*.wav;*.mp3;*.m4a;*.wma;*.flac;*.aac|{AppStrings.Get("FileFilter_AllFiles", "All files")}|*.*"
                : $"{AppStrings.Get("FileFilter_AllFiles", "All files")}|*.*",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ParameterTextBox.Text = dialog.FileName;
        }
    }

    private async void CustomIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = $"{AppStrings.Get("FileFilter_Images", "Images")}|*.png;*.jpg;*.jpeg;*.webp|{AppStrings.Get("FileFilter_AllFiles", "All files")}|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            selectedIcon = await importIcon(dialog.FileName, CancellationToken.None);
            IconListBox.SelectedItem = null;
            ShowCustomIcon(dialog.FileName);
            CustomIconFileName.Text = Path.GetFileName(dialog.FileName);
            HideValidation();
        }
        catch (Exception exception)
        {
            ShowValidation("Error_ImportIcon", "Could not import the selected icon", exception);
        }
    }

    private async void PreviewSound_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await previewSound(
                GetParameter(),
                checked((int)Math.Round(VolumeSlider.Value)),
                SoundBehaviorComboBox.SelectedValue is SoundPlaybackBehavior behavior
                    ? behavior
                    : SoundPlaybackBehavior.RestartSameSound,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            ShowValidation("Action_PlaySound", "Could not play sound", exception);
        }
    }

    private async void StopSound_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await stopSounds(CancellationToken.None);
            HideValidation();
        }
        catch (Exception exception)
        {
            ShowValidation("Action_PlaySound", "Could not control sound playback", exception);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        Assignment = new KeyAssignment(string.Empty, IconReference.BuiltIn("square"), new NoActionDefinition());
        DialogResult = true;
    }

    private void CopyOtherLayer_Click(object sender, RoutedEventArgs e)
    {
        ApplyAssignment(otherLayerAssignment);
        HideValidation();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var option = ActionListBox.SelectedItem as ActionOption
                ?? throw new InvalidOperationException("Select an action.");
            Assignment = new KeyAssignment(
                LabelTextBox.Text,
                selectedIcon,
                CreateAction(option.Id, GetParameter(), checked((int)Math.Round(VolumeSlider.Value))));
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ShowValidation("Error_SaveAssignment", "Could not save assignment", exception);
        }
    }

    private void UpdateActionFields()
    {
        if (ActionListBox.SelectedItem is not ActionOption option)
        {
            return;
        }

        var hasParameter = option.ParameterLabel is not null;
        var usesSuggestions = option.Id.StartsWith("obs-", StringComparison.Ordinal)
                              && option.Id is not ("obs-stream-start" or "obs-stream-stop" or "obs-record-start" or "obs-record-stop" or "obs-replay")
                              || option.Id.StartsWith("app-volume-", StringComparison.Ordinal);
        ParameterLabel.Visibility = hasParameter ? Visibility.Visible : Visibility.Collapsed;
        ParameterTextBox.Visibility = hasParameter && !usesSuggestions ? Visibility.Visible : Visibility.Collapsed;
        ParameterComboBox.Visibility = hasParameter && usesSuggestions ? Visibility.Visible : Visibility.Collapsed;
        ParameterLabel.Text = option.ParameterLabel ?? string.Empty;
        BrowseButton.Visibility = hasParameter && option.CanBrowse ? Visibility.Visible : Visibility.Collapsed;
        VolumePanel.Visibility = option.ShowValue ? Visibility.Visible : Visibility.Collapsed;
        SoundBehaviorPanel.Visibility = option.Id == "sound" ? Visibility.Visible : Visibility.Collapsed;
        LaunchOptionsPanel.Visibility = option.Id == "launch" ? Visibility.Visible : Visibility.Collapsed;
        ParameterTextBox.AcceptsReturn = option.Id == "macro";
        ParameterTextBox.Height = option.Id == "macro" ? 130 : 34;
        ParameterTextBox.VerticalScrollBarVisibility = option.Id == "macro"
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Hidden;
        var hint = option.Id switch
        {
            "macro" => AppStrings.Get("Editor_MacroHint.Text", "Enter one step per line."),
            "app-volume-up" or "app-volume-down" => AppStrings.Get(
                "Editor_AudioSessionHint.Text",
                "Only applications with an active Windows audio session are listed."),
            _ => string.Empty,
        };
        ParameterHint.Text = hint;
        ParameterHint.Visibility = hint.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        VolumePanel.Margin = hasParameter
            ? new Thickness(0, hint.Length == 0 ? 50 : 86, 0, 0)
            : new Thickness(0);
        ActionParametersCard.Visibility = hasParameter || option.ShowValue || option.Id == "launch"
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyAssignment(KeyAssignment assignment)
    {
        LabelTextBox.Text = assignment.Label;
        ShowIcon(assignment.Icon);

        var state = ReadAction(assignment.Action);
        var action = Actions.Single(option => option.Id == state.Id);
        ActionGroupListBox.SelectedItem = ActionGroups.Single(group => group.Id == action.Group);
        PopulateActions(action.Group, action.Id);

        ParameterTextBox.Text = state.Parameter ?? string.Empty;
        ParameterComboBox.Text = state.Parameter ?? string.Empty;
        VolumeSlider.Value = state.Value;
        SoundBehaviorComboBox.SelectedValue = state.SoundBehavior;
        ArgumentsTextBox.Text = state.Arguments ?? string.Empty;
        WorkingDirectoryTextBox.Text = state.WorkingDirectory ?? string.Empty;
        UpdateActionFields();
    }

    private void PopulateActions(string group, string? preferredActionId)
    {
        var options = Actions.Where(option => option.Group == group).ToArray();
        ActionListBox.ItemsSource = options;
        ActionListBox.SelectedItem = options.FirstOrDefault(option => option.Id == preferredActionId)
                                         ?? options.FirstOrDefault();
    }

    private void ShowIcon(IconReference icon)
    {
        selectedIcon = icon;
        if (icon.Kind == IconKind.BuiltIn)
        {
            var option = StreamNumDeck.App.Presentation.BuiltInIconCatalog.Get(
                string.Equals(icon.Value, "square", StringComparison.Ordinal) ? "plus" : icon.Value);
            IconListBox.SelectedItem = option;
            ShowBuiltInIcon(option);
            return;
        }

        IconListBox.SelectedItem = null;
        var path = resolveIconPath(icon);
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            ShowCustomIcon(path!);
            CustomIconFileName.Text = Path.GetFileName(path);
            return;
        }

        IconPreviewImage.Source = null;
        IconPreviewImage.Visibility = Visibility.Collapsed;
        IconPreviewGlyph.Text = StreamNumDeck.App.Presentation.BuiltInIconCatalog.Get("image").Glyph;
        IconPreviewGlyph.Visibility = Visibility.Visible;
        CustomIconFileName.Text = icon.Value;
    }

    private void ShowBuiltInIcon(StreamNumDeck.App.Presentation.BuiltInIconOption option)
    {
        IconPreviewImage.Source = null;
        IconPreviewImage.Visibility = Visibility.Collapsed;
        IconPreviewGlyph.Text = option.Glyph;
        IconPreviewGlyph.Visibility = Visibility.Visible;
        CustomIconFileName.Text = string.Empty;
    }

    private void ShowCustomIcon(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        IconPreviewImage.Source = image;
        IconPreviewImage.Visibility = Visibility.Visible;
        IconPreviewGlyph.Visibility = Visibility.Collapsed;
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationBorder.Visibility = Visibility.Visible;
    }

    private void ShowValidation(string resourceKey, string fallback, Exception exception) =>
        ShowValidation(UserErrorFormatter.Format(AppStrings.Get(resourceKey, fallback), exception));

    private void HideValidation()
    {
        ValidationText.Text = string.Empty;
        ValidationBorder.Visibility = Visibility.Collapsed;
    }

    private string GetParameter() =>
        ParameterComboBox.Visibility == Visibility.Visible
            ? ParameterComboBox.Text
            : ParameterTextBox.Text;

    private async Task LoadParameterSuggestionsAsync(string actionId)
    {
        try
        {
            IReadOnlyList<string>? values = null;
            if (actionId.StartsWith("app-volume-", StringComparison.Ordinal))
            {
                values = (await getAudioApplications(CancellationToken.None))
                    .Select(application => application.Id)
                    .ToArray();
            }
            else if (actionId is "obs-scene" or "obs-source" or "obs-input-mute" or "obs-media-restart")
            {
                var catalog = await getObsCatalog(CancellationToken.None);
                values = actionId switch
                {
                    "obs-scene" => catalog.Scenes,
                    "obs-input-mute" => catalog.Inputs,
                    _ => catalog.Sources,
                };
            }

            if (values is not null)
            {
                var current = ParameterComboBox.Text;
                ParameterComboBox.ItemsSource = values;
                ParameterComboBox.Text = current;
            }
        }
        catch (Exception exception)
        {
            AppLogger.Error("Load action parameter suggestions", exception);
            ParameterComboBox.ToolTip = null;
        }
    }

    private ActionDefinition CreateAction(string id, string parameter, int value) => id switch
    {
        "none" => new NoActionDefinition(),
        "sound" => new PlaySoundActionDefinition(
            parameter,
            value,
            SoundBehaviorComboBox.SelectedValue is SoundPlaybackBehavior behavior
                ? behavior
                : SoundPlaybackBehavior.RestartSameSound),
        "mic-mute" => new ToggleMicrophoneMuteActionDefinition(),
        "master-mute" => new ToggleMasterOutputMuteActionDefinition(),
        "volume-up" => new AdjustMasterVolumeActionDefinition(VolumeAdjustmentDirection.Increase, value),
        "volume-down" => new AdjustMasterVolumeActionDefinition(VolumeAdjustmentDirection.Decrease, value),
        "app-volume-up" => new AdjustApplicationVolumeActionDefinition(parameter, VolumeAdjustmentDirection.Increase, value),
        "app-volume-down" => new AdjustApplicationVolumeActionDefinition(parameter, VolumeAdjustmentDirection.Decrease, value),
        "url" => new OpenUriActionDefinition(parameter),
        "path" => new OpenPathActionDefinition(parameter),
        "launch" => new LaunchProcessActionDefinition(parameter, ArgumentsTextBox.Text, WorkingDirectoryTextBox.Text),
        "macro" => new KeyboardMacroActionDefinition(KeyboardMacroTextCodec.Parse(parameter)),
        "obs-scene" => new ObsActionDefinition(ObsActionKind.SwitchScene, parameter),
        "obs-source" => new ObsActionDefinition(ObsActionKind.ToggleSourceVisibility, parameter),
        "obs-input-mute" => new ObsActionDefinition(ObsActionKind.ToggleInputMute, parameter),
        "obs-stream-start" => new ObsActionDefinition(ObsActionKind.StartStreaming),
        "obs-stream-stop" => new ObsActionDefinition(ObsActionKind.StopStreaming),
        "obs-record-start" => new ObsActionDefinition(ObsActionKind.StartRecording),
        "obs-record-stop" => new ObsActionDefinition(ObsActionKind.StopRecording),
        "obs-replay" => new ObsActionDefinition(ObsActionKind.SaveReplayBuffer),
        "obs-media-restart" => new ObsActionDefinition(ObsActionKind.RestartMediaSource, parameter),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private static ActionState ReadAction(ActionDefinition action) => action switch
    {
        PlaySoundActionDefinition sound => new ActionState("sound", sound.FilePath, sound.Volume, sound.PlaybackBehavior, null, null),
        ToggleMicrophoneMuteActionDefinition => new ActionState("mic-mute", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ToggleMasterOutputMuteActionDefinition => new ActionState("master-mute", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        AdjustMasterVolumeActionDefinition volume => new ActionState(
            volume.Direction == VolumeAdjustmentDirection.Increase ? "volume-up" : "volume-down",
            null,
            volume.StepPercent, SoundPlaybackBehavior.RestartSameSound, null, null),
        AdjustApplicationVolumeActionDefinition volume => new ActionState(
            volume.Direction == VolumeAdjustmentDirection.Increase ? "app-volume-up" : "app-volume-down",
            volume.ApplicationId,
            volume.StepPercent, SoundPlaybackBehavior.RestartSameSound, null, null),
        OpenUriActionDefinition uri => new ActionState("url", uri.Uri, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        OpenPathActionDefinition path => new ActionState("path", path.Path, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        LaunchProcessActionDefinition launch => new ActionState("launch", launch.ExecutablePath, 10, SoundPlaybackBehavior.RestartSameSound, launch.Arguments, launch.WorkingDirectory),
        KeyboardMacroActionDefinition macro => new ActionState("macro", KeyboardMacroTextCodec.Format(macro.Steps), 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionDefinition obs => ReadObsAction(obs),
        _ => new ActionState("none", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
    };

    private static ActionState ReadObsAction(ObsActionDefinition obs) => obs.Action switch
    {
        ObsActionKind.SwitchScene => new ActionState("obs-scene", obs.TargetName, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.ToggleSourceVisibility => new ActionState("obs-source", obs.TargetName, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.ToggleInputMute => new ActionState("obs-input-mute", obs.TargetName, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.StartStreaming => new ActionState("obs-stream-start", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.StopStreaming => new ActionState("obs-stream-stop", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.StartRecording => new ActionState("obs-record-start", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.StopRecording => new ActionState("obs-record-stop", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.SaveReplayBuffer => new ActionState("obs-replay", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        ObsActionKind.RestartMediaSource => new ActionState("obs-media-restart", obs.TargetName, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
        _ => new ActionState("none", null, 10, SoundPlaybackBehavior.RestartSameSound, null, null),
    };

    private sealed record ActionGroupOption(string Id, string Name, string Glyph);

    private sealed record ActionOption(
        string Id,
        string Name,
        string? ParameterLabel,
        bool CanBrowse,
        bool ShowValue,
        string? Description = null)
    {
        public string ToolTip => Description ?? Name;

        public string Group => Id switch
        {
            "none" => "none",
            "sound" or "mic-mute" or "master-mute" or "volume-up" or "volume-down"
                or "app-volume-up" or "app-volume-down" => "sound",
            "url" or "path" or "launch" or "macro" => "system",
            _ => "obs",
        };

        public string Glyph => Id switch
        {
            "none" => "\uE710",
            "sound" => "\uE8D6",
            "mic-mute" => "\uE720",
            "master-mute" => "\uE74F",
            "volume-up" or "app-volume-up" => "\uE995",
            "volume-down" or "app-volume-down" => "\uE993",
            "url" => "\uE71B",
            "path" => "\uE8B7",
            "launch" => "\uE756",
            "macro" => "\uE765",
            "obs-scene" => "\uE8AB",
            "obs-source" => "\uE890",
            "obs-input-mute" => "\uE74F",
            "obs-stream-start" => "\uE95A",
            "obs-stream-stop" or "obs-record-stop" => "\uE71A",
            "obs-record-start" => "\uE7C8",
            _ => "\uE72C",
        };
    }
    private sealed record SoundBehaviorOption(SoundPlaybackBehavior Value, string Name);
    private sealed record ActionState(
        string Id,
        string? Parameter,
        int Value,
        SoundPlaybackBehavior SoundBehavior,
        string? Arguments,
        string? WorkingDirectory);
}
