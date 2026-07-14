using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace StreamNumDeck.App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int LogicalWindowWidth = 500;
    private const int LogicalWindowHeight = 720;
    private const int SwRestore = 9;
    private bool allowClose;

    public bool MinimizeToTray { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        ConfigureFixedWindow();
        AppWindow.Closing += AppWindow_Closing;
        AppWindow.Changed += AppWindow_Changed;

        RootFrame.Navigate(typeof(MainPage));
    }

    private void ConfigureFixedWindow()
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(windowHandle);
        var scale = dpi == 0 ? 1d : dpi / 96d;
        var width = checked((int)Math.Round(LogicalWindowWidth * scale));
        var height = checked((int)Math.Round(LogicalWindowHeight * scale));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
        }

        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + Math.Max(0, (workArea.Width - width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - height) / 2),
            width,
            height));
    }

    public void ShowFromTray()
    {
        AppWindow.Show();

        if (AppWindow.Presenter is OverlappedPresenter presenter
            && presenter.State is OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ShowWindow(windowHandle, SwRestore);
        Activate();
        SetForegroundWindow(windowHandle);
    }

    public Task ShowProfileFromTrayAsync(Guid profileId) =>
        RootFrame.Content is MainPage mainPage
            ? mainPage.ShowProfileFromTrayAsync(profileId)
            : Task.CompletedTask;

    public void CloseForExit()
    {
        allowClose = true;
        Close();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!allowClose && MinimizeToTray)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!MinimizeToTray
            || !args.DidPresenterChange
            || sender.Presenter is not OverlappedPresenter presenter
            || presenter.State is not OverlappedPresenterState.Minimized)
        {
            return;
        }

        sender.Hide();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
}
