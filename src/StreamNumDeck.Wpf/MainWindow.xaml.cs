using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Wpf.ViewModels;
using StreamNumDeck.Wpf.Services;
using StreamNumDeck.App.Localization;

namespace StreamNumDeck.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private bool profileSelectionInProgress;
    private bool allowClose;

    public bool MinimizeToTray { get; set; }

    public MainWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await viewModel.InitializeAsync();
            SelectActiveProfileInList();
        }
        catch (Exception exception)
        {
            ShowError("Error_LoadProfiles", "Could not load profiles", exception);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ErrorDismiss_Click(object sender, RoutedEventArgs e) => viewModel.ClearError();

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void ExitFromTray()
    {
        allowClose = true;
        Close();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (MinimizeToTray && WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!allowClose && MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await viewModel.ToggleCaptureAsync();
        }
        catch (Exception exception)
        {
            ShowError("Error_ChangeCapture", "Could not change the key capture state", exception);
        }
    }

    private async void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (profileSelectionInProgress || ProfileList.SelectedValue is not Guid profileId)
        {
            return;
        }

        try
        {
            await viewModel.SelectProfileAsync(profileId);
        }
        catch (Exception exception)
        {
            ShowError("Error_ChangeProfile", "Could not change profile", exception);
            SelectActiveProfileInList();
        }
    }

    private async void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfileEditorWindow(null) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await viewModel.CreateProfileAsync(dialog.ProfileName, dialog.ProfileIcon);
            SelectActiveProfileInList();
        }
        catch (Exception exception)
        {
            ShowError("Error_CreateProfile", "Could not create profile", exception);
        }
    }

    private void ProfileItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private void ProfileContextMenu_Opened(object sender, RoutedEventArgs e) =>
        DeleteProfileMenuItem.IsEnabled = viewModel.Profiles.Count > 1;

    private async void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not DeckProfile profile)
        {
            return;
        }

        var dialog = new ProfileEditorWindow(profile) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteProfileChangeAsync(
            () => viewModel.EditProfileAsync(profile.Id, dialog.ProfileName, dialog.ProfileIcon),
            "Error_EditProfile",
            "Could not edit profile");
    }

    private async void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is DeckProfile profile)
        {
            await ExecuteProfileChangeAsync(
                () => viewModel.DuplicateProfileAsync(profile.Id),
                "Error_DuplicateProfile",
                "Could not duplicate profile");
        }
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not DeckProfile profile)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            AppStrings.Format("Profile_DeleteMessage", profile.Name),
            AppStrings.Get("Profile_DeleteTitle", "Delete profile"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            await ExecuteProfileChangeAsync(
                () => viewModel.DeleteProfileAsync(profile.Id),
                "Error_DeleteProfile",
                "Could not delete profile");
        }
    }

    private async Task ExecuteProfileChangeAsync(Func<Task> action, string errorKey, string fallback)
    {
        try
        {
            await action();
            SelectActiveProfileInList();
        }
        catch (Exception exception)
        {
            ShowError(errorKey, fallback, exception);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(viewModel) { Owner = this };
        settings.ShowDialog();
    }

    private async void DeckKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not string keyName
            || !Enum.TryParse(keyName, out DeckKey key))
        {
            return;
        }

        var editor = new KeyEditorWindow(
            key,
            viewModel.GetCurrentAssignment(key),
            viewModel.GetAssignment(key, viewModel.IsNumLockOn ? NumLockLayer.Off : NumLockLayer.On),
            viewModel.ImportIconAsync,
            viewModel.ResolveIconPath,
            viewModel.GetObsCatalogAsync,
            viewModel.GetAudioApplicationsAsync,
            viewModel.PreviewSoundAsync,
            viewModel.StopSoundsAsync)
        {
            Owner = this,
        };
        if (editor.ShowDialog() == true)
        {
            await SaveAssignmentAsync(key, editor.Assignment);
        }
    }

    private async Task SaveAssignmentAsync(DeckKey key, StreamNumDeck.Core.Deck.KeyAssignment assignment)
    {
        try
        {
            await viewModel.UpdateAssignmentAsync(key, assignment);
        }
        catch (Exception exception)
        {
            ShowError("Error_SaveAssignment", "Could not save assignment", exception);
        }
    }

    private void ShowError(string resourceKey, string fallback, Exception exception) =>
        MessageBox.Show(
            this,
            UserErrorFormatter.Format(AppStrings.Get(resourceKey, fallback), exception),
            "StreamNumDeck",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

    private void SelectActiveProfileInList()
    {
        profileSelectionInProgress = true;
        ProfileList.SelectedValue = viewModel.SelectedProfile?.Id;
        profileSelectionInProgress = false;
    }
}
