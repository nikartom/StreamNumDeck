using Microsoft.UI.Xaml.Controls;
using StreamNumDeck.App.Localization;
using StreamNumDeck.App.ViewModels;

namespace StreamNumDeck.App.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly Uri SupportUri = new("https://www.donationalerts.com/r/kventinburatino");

    public SettingsPageViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = global::StreamNumDeck.App.App.GetService<SettingsPageViewModel>();
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, AppStrings.Get("Error_LoadSettings"), exception.Message);
        }
    }

    private async void SaveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveAsync(ObsPasswordBox.Password);
            ObsPasswordBox.Password = string.Empty;
            ShowStatus(InfoBarSeverity.Success, AppStrings.Get("Settings_Saved"), string.Empty);
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, AppStrings.Get("Error_SaveSettings"), exception.Message);
        }
    }

    private async void TestObsConnectionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var result = await ViewModel.TestObsConnectionAsync(ObsPasswordBox.Password);
            ObsPasswordBox.Password = string.Empty;
            ShowStatus(InfoBarSeverity.Success, AppStrings.Get("Obs_Connected"), result);
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, AppStrings.Get("Obs_ConnectionFailed"), exception.Message);
        }
    }

    private void PreviewMicrophoneOverlayButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = ShowMicrophoneOverlayPreviewAsync();
    }

    private async void SupportAuthorButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            if (!await Windows.System.Launcher.LaunchUriAsync(SupportUri))
            {
                ShowStatus(
                    InfoBarSeverity.Error,
                    AppStrings.Get("Error_OpenSupportLink"),
                    AppStrings.Get("Error_NoUriHandler"));
            }
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, AppStrings.Get("Error_OpenSupportLink"), exception.Message);
        }
    }

    private async Task ShowMicrophoneOverlayPreviewAsync()
    {
        try
        {
            await ViewModel.PreviewMicrophoneMuteOverlayAsync();
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, AppStrings.Get("Error_ShowOverlay"), exception.Message);
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        SaveStatus.Severity = severity;
        SaveStatus.Title = title;
        SaveStatus.Message = message;
        SaveStatus.IsOpen = true;
    }
}
