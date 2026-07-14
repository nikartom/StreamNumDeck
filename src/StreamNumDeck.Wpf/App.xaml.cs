using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Input;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Infrastructure.Audio;
using StreamNumDeck.Infrastructure.Configuration;
using StreamNumDeck.Infrastructure.Execution;
using StreamNumDeck.Infrastructure.Icons;
using StreamNumDeck.Infrastructure.Input;
using StreamNumDeck.Infrastructure.Obs;
using StreamNumDeck.Infrastructure.Security;
using StreamNumDeck.Wpf.Services;
using StreamNumDeck.Wpf.ViewModels;
using StreamNumDeck.Wpf.Overlays;
using StreamNumDeck.App.Localization;

namespace StreamNumDeck.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;
    private SingleInstanceCoordinator? singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
            AppLogger.Error("Unhandled UI error", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                AppLogger.Error("Unhandled application error", exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Unobserved background task error", args.Exception);
            args.SetObserved();
        };

        singleInstance = SingleInstanceCoordinator.Create();
        if (!singleInstance.IsPrimary)
        {
            if (!singleInstance.SignalPrimaryInstance())
            {
                AppLogger.Error(
                    "Activate existing application instance",
                    new InvalidOperationException("The primary application instance did not accept activation."));
            }

            Shutdown();
            return;
        }

        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StreamNumDeck");
        var configurationRoot = Path.Combine(appDataRoot, "Configuration");
        ConfigurationMigration.TryMigrate(appDataRoot, configurationRoot);

        var collection = new ServiceCollection();
        collection.AddSingleton(new ConfigurationPaths(configurationRoot));
        collection.AddSingleton<IConfigurationStore>(provider => new JsonConfigurationStore(
            provider.GetRequiredService<ConfigurationPaths>(),
            () => AppConfiguration.CreateDefault(GetDefaultProfileName())));
        collection.AddSingleton<IIconAssetStore>(_ => new FileSystemIconAssetStore(
            Path.Combine(appDataRoot, "Assets")));
        collection.AddSingleton<ConfigurationService>();
        collection.AddSingleton<IKeyboardCaptureService, WindowsKeyboardCaptureService>();
        collection.AddSingleton<IActionExecutor, SystemActionExecutor>();
        collection.AddSingleton<IActionExecutor, KeyboardMacroActionExecutor>();
        collection.AddSingleton<IAudioPlaybackService, WindowsAudioPlaybackService>();
        collection.AddSingleton<IActionExecutor, AudioActionExecutor>();
        collection.AddSingleton<ISystemAudioControlService, WindowsSystemAudioControlService>();
        collection.AddSingleton<IActionExecutor, SystemAudioActionExecutor>();
        collection.AddSingleton<IProtectedCredentialStore, WindowsCredentialStore>();
        collection.AddSingleton<IObsController, ObsWebSocketController>();
        collection.AddSingleton<IObsConnectionTester, ObsConnectionTester>();
        collection.AddSingleton<IActionExecutor, ObsActionExecutor>();
        collection.AddSingleton<DeckRuntimeService>();
        collection.AddSingleton<OverlayPositionStore>();
        collection.AddSingleton<MicrophoneMuteOverlayController>();
        collection.AddSingleton<TrayIconService>();
        collection.AddSingleton<MainViewModel>();

        services = collection.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var window = new MainWindow(services.GetRequiredService<MainViewModel>());
        MainWindow = window;
        window.Show();
        singleInstance.StartListening(() =>
            Dispatcher.BeginInvoke(new Action(window.ShowFromTray)));
        InitializeTray(window);
        _ = StartOverlayAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (services is not null)
        {
            var viewModel = services.GetService<MainViewModel>();
            viewModel?.Dispose();

            try
            {
                ((IAsyncDisposable)services).DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                AppLogger.Error("Application shutdown", exception);
            }
        }

        singleInstance?.Dispose();
        singleInstance = null;

        base.OnExit(e);
    }

    private static string GetDefaultProfileName() =>
        AppStrings.Get("Profile_DefaultName", "Main stream");

    private void InitializeTray(MainWindow window)
    {
        var provider = services ?? throw new InvalidOperationException("Application services are not initialized.");
        var tray = provider.GetRequiredService<TrayIconService>();
        var viewModel = provider.GetRequiredService<MainViewModel>();
        tray.Initialize(
            () => Dispatcher.Invoke(window.ShowFromTray),
            () => Dispatcher.InvokeAsync(viewModel.ToggleCaptureAsync).Task.Unwrap(),
            () => Dispatcher.InvokeAsync(() => viewModel.ToggleCaptureTargetAsync(KeyboardCaptureTargets.Numpad)).Task.Unwrap(),
            () => Dispatcher.InvokeAsync(() => viewModel.ToggleCaptureTargetAsync(KeyboardCaptureTargets.NavigationBlock)).Task.Unwrap(),
            profileId => Dispatcher.InvokeAsync(() => viewModel.SelectProfileAsync(profileId)).Task.Unwrap(),
            () => Dispatcher.Invoke(window.ExitFromTray));
        provider.GetRequiredService<ConfigurationService>().Changed += (_, args) =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                tray.SetProfiles(
                    args.Configuration.Profiles.Select(profile => new TrayProfileOption(profile.Id, profile.Name)),
                    args.Configuration.ActiveProfileId);
                tray.SetCaptureTargets(
                    args.Configuration.Settings.CaptureNumpad,
                    args.Configuration.Settings.CaptureNavigationBlock);
            }));
        provider.GetRequiredService<DeckRuntimeService>().CaptureStateChanged += (_, args) =>
            Dispatcher.BeginInvoke(new Action(() => tray.SetCaptureEnabled(args.State == KeyboardCaptureState.Running)));
        viewModel.UserErrorRaised += (title, message) =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!window.IsVisible || window.WindowState == WindowState.Minimized)
                {
                    tray.ShowError(title, message);
                }
            }));
        _ = ApplyTrayConfigurationAsync(window, tray);
    }

    private async Task ApplyTrayConfigurationAsync(MainWindow window, TrayIconService tray)
    {
        try
        {
            var provider = services ?? throw new InvalidOperationException("Application services are not initialized.");
            var configuration = await provider.GetRequiredService<ConfigurationService>().GetAsync();
            await Dispatcher.InvokeAsync(() =>
            {
                window.MinimizeToTray = configuration.Settings.MinimizeToTray;
                tray.SetVisible(configuration.Settings.MinimizeToTray);
                tray.SetCaptureEnabled(provider.GetRequiredService<DeckRuntimeService>().CaptureState == KeyboardCaptureState.Running);
                tray.SetCaptureTargets(
                    configuration.Settings.CaptureNumpad,
                    configuration.Settings.CaptureNavigationBlock);
                tray.SetProfiles(
                    configuration.Profiles.Select(profile => new TrayProfileOption(profile.Id, profile.Name)),
                    configuration.ActiveProfileId);
            });
        }
        catch (Exception exception)
        {
            AppLogger.Error("Apply tray configuration", exception);
        }
    }

    public void ApplySettings(StreamNumDeck.Core.Settings.GlobalSettings settings)
    {
        if (MainWindow is MainWindow window && services is not null)
        {
            window.MinimizeToTray = settings.MinimizeToTray;
            services.GetRequiredService<TrayIconService>().SetVisible(settings.MinimizeToTray);
        }
    }

    private async Task StartOverlayAsync()
    {
        try
        {
            await services!.GetRequiredService<MicrophoneMuteOverlayController>().StartAsync();
        }
        catch (Exception exception)
        {
            AppLogger.Error("Start microphone overlay", exception);
        }
    }
}
