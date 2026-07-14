using Microsoft.UI.Xaml.Controls;
using StreamNumDeck.App.Dialogs;
using StreamNumDeck.App.Localization;
using StreamNumDeck.App.ViewModels;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Obs;

namespace StreamNumDeck.App.Views;

public sealed partial class DeckPage : Page
{
    private bool captureToggleChangeInProgress;

    public DeckPageViewModel ViewModel { get; }

    public DeckPage()
    {
        ViewModel = global::StreamNumDeck.App.App.GetService<DeckPageViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public Task ShowProfileAsync(Guid profileId) => ViewModel.ReloadProfileAsync(profileId);

    private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            await ShowConfigurationErrorAsync(AppStrings.Get("Error_LoadSettings"), exception);
        }
    }

    private void Page_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        ViewModel.Dispose();

    private async void DeckKey_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var keyName = (sender as Button)?.Tag?.ToString();
        if (!Enum.TryParse<DeckKey>(keyName, out var key) || ViewModel.SelectedProfile is null)
        {
            return;
        }

        await OpenKeyEditorAsync(key);
    }

    private async Task OpenKeyEditorAsync(DeckKey key)
    {
        try
        {
            var layer = ViewModel.IsNumLockOn ? NumLockLayer.On : NumLockLayer.Off;
            var dialog = new KeyEditorDialog(
                key,
                layer,
                ViewModel.GetCurrentAssignment(key),
                ViewModel.GetAssignment(key, layer is NumLockLayer.On ? NumLockLayer.Off : NumLockLayer.On),
                global::StreamNumDeck.App.App.GetService<IIconAssetStore>(),
                global::StreamNumDeck.App.App.GetService<IAudioPlaybackService>(),
                global::StreamNumDeck.App.App.GetService<ISystemAudioControlService>(),
                global::StreamNumDeck.App.App.GetService<ConfigurationService>(),
                global::StreamNumDeck.App.App.GetService<IObsController>())
            {
                XamlRoot = XamlRoot,
            };

            await dialog.ShowAsync();
            if (dialog.Result is null)
            {
                return;
            }

            await ViewModel.UpdateAssignmentAsync(key, dialog.Result);
        }
        catch (Exception exception)
        {
            await ShowConfigurationErrorAsync(AppStrings.Get("Error_SaveAssignment"), exception);
        }
    }

    private async void NumLockKey_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await SetNumLockAsync(!ViewModel.IsNumLockOn);
    }

    private async Task SetNumLockAsync(bool isOn)
    {
        try
        {
            await ViewModel.SetNumLockAsync(isOn);
        }
        catch (Exception exception)
        {
            await ShowConfigurationErrorAsync(AppStrings.Get("Error_ToggleNumLock"), exception);
        }
    }

    private async void Capture_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!ViewModel.IsInitialized
            || captureToggleChangeInProgress
            || sender is not ToggleSwitch toggle)
        {
            return;
        }

        captureToggleChangeInProgress = true;
        try
        {
            await ViewModel.SetCaptureEnabledAsync(toggle.IsOn);
        }
        catch (Exception exception)
        {
            toggle.IsOn = ViewModel.IsCaptureEnabled;
            await ShowConfigurationErrorAsync(AppStrings.Get("Error_ChangeCapture"), exception);
        }
        finally
        {
            captureToggleChangeInProgress = false;
        }
    }

    private async Task ShowConfigurationErrorAsync(string title, Exception exception)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = exception.Message,
            CloseButtonText = AppStrings.Get("Common_Close"),
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }
}
