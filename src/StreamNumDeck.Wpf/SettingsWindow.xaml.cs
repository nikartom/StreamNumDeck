using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.Wpf.Services;
using StreamNumDeck.Wpf.ViewModels;
using StreamNumDeck.App.Localization;

namespace StreamNumDeck.Wpf;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel viewModel;
    private GlobalSettings? originalSettings;

    public SettingsWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var snapshot = await viewModel.LoadSettingsAsync();
            originalSettings = snapshot.Settings;
            AudioDeviceComboBox.ItemsSource = snapshot.AudioDevices;
            AudioDeviceComboBox.SelectedValue = snapshot.Settings.AudioOutputDeviceId;
            if (AudioDeviceComboBox.SelectedIndex < 0)
            {
                AudioDeviceComboBox.SelectedIndex = 0;
            }

            MasterVolumeSlider.Value = snapshot.Settings.MasterVolume;
            ConcurrentSoundsCheckBox.IsChecked = snapshot.Settings.AllowConcurrentSounds;
            PreloadSoundsCheckBox.IsChecked = snapshot.Settings.PreloadShortSounds;
            StartWithWindowsCheckBox.IsChecked = snapshot.Settings.StartWithWindows;
            MinimizeToTrayCheckBox.IsChecked = snapshot.Settings.MinimizeToTray;
            CaptureOnStartupCheckBox.IsChecked = snapshot.Settings.EnableCaptureOnStartup;
            ObsHostTextBox.Text = snapshot.Settings.Obs.Host;
            ObsPortTextBox.Text = snapshot.Settings.Obs.Port.ToString();
            ObsPasswordBox.Password = string.Empty;
            PasswordStoredText.Visibility = string.IsNullOrEmpty(snapshot.ObsPassword)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        catch (Exception exception)
        {
            ShowErrorStatus("Error_LoadSettings", "Could not load settings", exception);
            SaveButton.IsEnabled = false;
        }
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MasterVolumeText is not null)
        {
            MasterVolumeText.Text = $"{Math.Round(e.NewValue):0}%";
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (originalSettings is null)
        {
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            if (!int.TryParse(ObsPortTextBox.Text, out var port) || port is < 1 or > 65535)
            {
                throw new FormatException($"{AppStrings.Get("Settings_ObsPort.Header", "Port")}: 1–65535");
            }

            var settings = new GlobalSettings(
                AudioDeviceComboBox.SelectedValue as string,
                checked((int)Math.Round(MasterVolumeSlider.Value)),
                ConcurrentSoundsCheckBox.IsChecked == true,
                PreloadSoundsCheckBox.IsChecked == true,
                StartWithWindowsCheckBox.IsChecked == true,
                MinimizeToTrayCheckBox.IsChecked == true,
                CaptureOnStartupCheckBox.IsChecked == true,
                originalSettings.Theme,
                new ObsConnectionSettings(ObsHostTextBox.Text, port, originalSettings.Obs.CredentialKey));

            var startupChanged = settings.StartWithWindows != originalSettings.StartWithWindows;
            if (startupChanged)
            {
                WindowsStartupService.SetEnabled(settings.StartWithWindows);
            }

            try
            {
                await viewModel.SaveSettingsAsync(settings, ObsPasswordBox.Password);
            }
            catch
            {
                if (startupChanged)
                {
                    try
                    {
                        WindowsStartupService.SetEnabled(originalSettings.StartWithWindows);
                    }
                    catch (Exception rollbackException)
                    {
                        AppLogger.Error("Restore Windows startup setting", rollbackException);
                    }
                }

                throw;
            }

            ((App)System.Windows.Application.Current).ApplySettings(settings);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ShowErrorStatus("Error_SaveSettings", "Could not save settings", exception);
            SaveButton.IsEnabled = true;
        }
    }

    private async void TestObs_Click(object sender, RoutedEventArgs e)
    {
        TestObsButton.IsEnabled = false;
        ObsTestResultText.Visibility = Visibility.Collapsed;

        try
        {
            if (!int.TryParse(ObsPortTextBox.Text, out var port) || port is < 1 or > 65535)
            {
                throw new FormatException($"{AppStrings.Get("Settings_ObsPort.Header", "Port")}: 1–65535");
            }

            ObsTestResultText.Text = await viewModel.TestObsConnectionAsync(
                ObsHostTextBox.Text,
                port,
                ObsPasswordBox.Password);
            ObsTestResultText.Visibility = Visibility.Visible;
        }
        catch (Exception exception)
        {
            ShowErrorStatus("Obs_ConnectionFailed", "Could not connect to OBS", exception);
        }
        finally
        {
            TestObsButton.IsEnabled = true;
        }
    }

    private async void PositionOverlay_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await viewModel.PreviewMicrophoneOverlayAsync();
            ShowStatus(AppStrings.Get(
                "Settings_OverlayHint.Text",
                "Drag the indicator to the required monitor. Its position will be saved."), isError: false);
        }
        catch (Exception exception)
        {
            ShowErrorStatus("Error_ShowOverlay", "Could not show the microphone indicator", exception);
        }
    }

    private void Support_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.donationalerts.com/r/kventinburatino",
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowErrorStatus("Error_OpenSupportLink", "Could not open the support page", exception);
        }
    }

    private void ShowErrorStatus(string resourceKey, string fallback, Exception exception) =>
        ShowStatus(
            UserErrorFormatter.Format(AppStrings.Get(resourceKey, fallback), exception),
            isError: true);

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusIcon.Text = isError ? "\uEA39" : "\uE73E";
        StatusIcon.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
            isError ? "#FF99A4" : "#9DDB9F"));
        StatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
            isError ? "#3A2022" : "#243425"));
        StatusBorder.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
            isError ? "#D13438" : "#5B8F5D"));
        StatusBorder.Visibility = Visibility.Visible;
    }
}
