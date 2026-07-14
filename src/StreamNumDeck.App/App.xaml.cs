using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System.Globalization;
using System.Runtime.InteropServices;
using StreamNumDeck.App.Localization;
using StreamNumDeck.App.ViewModels;
using StreamNumDeck.App.Overlays;
using StreamNumDeck.App.SystemIntegration;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Input;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Infrastructure.Configuration;
using StreamNumDeck.Infrastructure.Audio;
using StreamNumDeck.Infrastructure.Execution;
using StreamNumDeck.Infrastructure.Icons;
using StreamNumDeck.Infrastructure.Input;
using StreamNumDeck.Infrastructure.Obs;
using StreamNumDeck.Infrastructure.Security;
using Windows.Storage;
using Windows.Globalization;
namespace StreamNumDeck.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        ApplySystemUiLanguage();
        InitializeComponent();

        var services = new ServiceCollection();
        services.AddSingleton(new ConfigurationPaths(
            Path.Combine(ApplicationData.Current.LocalFolder.Path, "Configuration")));
        services.AddSingleton<IConfigurationStore>(provider => new JsonConfigurationStore(
            provider.GetRequiredService<ConfigurationPaths>(),
            () => AppConfiguration.CreateDefault(AppStrings.Get("Profile_DefaultName"))));
        services.AddSingleton<IIconAssetStore>(_ => new FileSystemIconAssetStore(
            Path.Combine(ApplicationData.Current.LocalFolder.Path, "Assets")));
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<IKeyboardCaptureService, WindowsKeyboardCaptureService>();
        services.AddSingleton<IActionExecutor, SystemActionExecutor>();
        services.AddSingleton<IActionExecutor, KeyboardMacroActionExecutor>();
        services.AddSingleton<IAudioPlaybackService, WindowsAudioPlaybackService>();
        services.AddSingleton<IActionExecutor, AudioActionExecutor>();
        services.AddSingleton<ISystemAudioControlService, WindowsSystemAudioControlService>();
        services.AddSingleton<IActionExecutor, SystemAudioActionExecutor>();
        services.AddSingleton<MicrophoneMuteOverlayController>();
        services.AddSingleton<OverlayPositionStore>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<WindowsStartupService>();
        services.AddSingleton<IProtectedCredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IObsController, ObsWebSocketController>();
        services.AddSingleton<IActionExecutor, ObsActionExecutor>();
        services.AddSingleton<DeckRuntimeService>();
        services.AddTransient<DeckPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private static void ApplySystemUiLanguage()
    {
        var languageId = GetUserDefaultUILanguage();
        if (languageId == 0)
        {
            return;
        }

        var culture = CultureInfo.GetCultureInfo(languageId);
        ApplicationLanguages.PrimaryLanguageOverride = culture.Name;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        Window.Closed += async (_, _) =>
        {
            GetService<ConfigurationService>().Changed -= ConfigurationService_Changed;
            GetService<DeckRuntimeService>().CaptureStateChanged -= RuntimeService_CaptureStateChanged;
            if (Services is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                (Services as IDisposable)?.Dispose();
            }
        };
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
        var configurationService = GetService<ConfigurationService>();
        var runtimeService = GetService<DeckRuntimeService>();
        var trayIcon = GetService<TrayIconService>();
        configurationService.Changed += ConfigurationService_Changed;
        runtimeService.CaptureStateChanged += RuntimeService_CaptureStateChanged;
        trayIcon.Initialize(
            ShowMainWindow,
            ToggleCaptureFromTrayAsync,
            SelectProfileFromTrayAsync,
            ExitApplication);
        trayIcon.SetCaptureEnabled(runtimeService.CaptureState is not KeyboardCaptureState.Stopped);
        _ = ApplySavedSystemSettingsAsync();
        _ = StartMicrophoneOverlayAsync();
    }

    public static void ApplySystemSettings(StreamNumDeck.Core.Settings.GlobalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (Window.Content is FrameworkElement root)
        {
            root.RequestedTheme = settings.Theme switch
            {
                StreamNumDeck.Core.Settings.AppTheme.Light => ElementTheme.Light,
                StreamNumDeck.Core.Settings.AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }

        if (Window is MainWindow mainWindow)
        {
            mainWindow.MinimizeToTray = settings.MinimizeToTray;
        }

        GetService<TrayIconService>().SetVisible(settings.MinimizeToTray);
    }

    private static async Task ApplySavedSystemSettingsAsync()
    {
        try
        {
            var configuration = await GetService<ConfigurationService>().GetAsync();
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplySystemSettings(configuration.Settings);
                ApplyTrayProfiles(configuration);
                ApplyTrayCaptureState(GetService<DeckRuntimeService>().CaptureState);
            });
        }
        catch
        {
            // Configuration loading errors are surfaced by the main page.
        }
    }

    private static void ShowMainWindow()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (Window is MainWindow mainWindow)
            {
                mainWindow.ShowFromTray();
            }
        });
    }

    private static async Task ToggleCaptureFromTrayAsync()
    {
        var runtimeService = GetService<DeckRuntimeService>();
        if (runtimeService.CaptureState is KeyboardCaptureState.Stopped)
        {
            await runtimeService.StartAsync();
        }
        else
        {
            await runtimeService.StopAsync();
        }
    }

    private static async Task SelectProfileFromTrayAsync(Guid profileId)
    {
        var configurationService = GetService<ConfigurationService>();
        var configuration = await configurationService.GetAsync();
        if (configuration.Profiles.All(profile => profile.Id != profileId))
        {
            throw new InvalidOperationException(AppStrings.Get("Error_ProfileNotFound"));
        }

        if (configuration.ActiveProfileId != profileId)
        {
            await configurationService.UpdateAsync(current => current.WithActiveProfile(profileId));
        }

        await EnqueueOnUiThreadAsync(async () =>
        {
            if (Window is MainWindow mainWindow)
            {
                await mainWindow.ShowProfileFromTrayAsync(profileId);
            }
        });
    }

    private static Task EnqueueOnUiThreadAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("The UI dispatcher is unavailable."));
        }

        return completion.Task;
    }

    private static void ConfigurationService_Changed(object? sender, ConfigurationChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() => ApplyTrayProfiles(args.Configuration));
    }

    private static void RuntimeService_CaptureStateChanged(object? sender, KeyboardCaptureStateChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() => ApplyTrayCaptureState(args.State));
    }

    private static void ApplyTrayProfiles(AppConfiguration configuration)
    {
        var profiles = configuration.Profiles
            .Select(profile => new TrayProfileOption(profile.Id, profile.Name))
            .ToArray();
        GetService<TrayIconService>().SetProfiles(profiles, configuration.ActiveProfileId);
    }

    private static void ApplyTrayCaptureState(KeyboardCaptureState state) =>
        GetService<TrayIconService>().SetCaptureEnabled(state is not KeyboardCaptureState.Stopped);

    private static void ExitApplication()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (Window is MainWindow mainWindow)
            {
                mainWindow.CloseForExit();
            }
        });
    }

    private static async Task StartMicrophoneOverlayAsync()
    {
        try
        {
            await GetService<MicrophoneMuteOverlayController>().StartAsync();
        }
        catch
        {
            // A missing capture endpoint must not prevent the main application from starting.
        }
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
}
