using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace StreamNumDeck.App.Overlays;

public sealed partial class MicrophoneMuteOverlayWindow : Window
{
    private const int BadgeSizeDip = 56;
    private const int MarginDip = 24;
    private const int GwlExstyle = -20;
    private const long WsExToolwindow = 0x00000080L;
    private const long WsExNoactivate = 0x08000000L;
    private const uint MonitorDefaultToPrimary = 1;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmMouseActivate = 0x0021;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const nint HtClient = 1;
    private const nint MaNoActivate = 3;
    private readonly nint windowHandle;
    private readonly OverlayPositionStore positionStore;
    private readonly SubclassProcedure subclassProcedure;
    private readonly ChildEnumerationProcedure childEnumerationProcedure;
    private readonly List<nint> subclassedWindowHandles = [];
    private Point dragStartCursor;
    private PointInt32 dragStartWindow;
    private bool dragging;
    private bool childSubclassConfigurationSucceeded = true;
    private nint foregroundWindowBeforePointer;

    public MicrophoneMuteOverlayWindow(OverlayPositionStore positionStore)
    {
        ArgumentNullException.ThrowIfNull(positionStore);
        this.positionStore = positionStore;
        InitializeComponent();
        windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        subclassProcedure = WindowSubclassProcedure;
        childEnumerationProcedure = ConfigureChildSubclass;

        ConfigurePresenter();
        ConfigureNativeWindow();
        RestoreOrCreatePosition();
        Closed += MicrophoneMuteOverlayWindow_Closed;
    }

    public void SetVisible(bool visible)
    {
        if (visible == AppWindow.IsVisible)
        {
            return;
        }

        if (visible)
        {
            RestoreOrCreatePosition();
            AppWindow.Show(activateWindow: false);
        }
        else if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
        }
    }

    private void ConfigurePresenter()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            throw new InvalidOperationException("The microphone overlay requires an overlapped presenter.");
        }

        presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
    }

    private void ConfigureNativeWindow()
    {
        var extendedStyle = GetWindowLongPtr(windowHandle, GwlExstyle).ToInt64();
        _ = SetWindowLongPtr(
            windowHandle,
            GwlExstyle,
            new nint(extendedStyle | WsExNoactivate | WsExToolwindow));
        if (!SetWindowPos(
                windowHandle,
                0,
                0,
                0,
                0,
                0,
                SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to apply the microphone mute overlay window style.");
        }

        if (!TryConfigureSubclass(windowHandle))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to configure dragging for the microphone mute overlay.");
        }

        _ = EnumChildWindows(windowHandle, childEnumerationProcedure, 0);
        if (!childSubclassConfigurationSucceeded)
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to configure input for the microphone mute overlay.");
        }

        ApplyWindowSizeAndShape();
    }

    private void ApplyWindowSizeAndShape()
    {
        var size = ScaleDip(BadgeSizeDip);
        AppWindow.Resize(new SizeInt32(size, size));
        var radius = ScaleDip(18);
        var region = CreateRoundRectRgn(0, 0, size + 1, size + 1, radius, radius);
        if (region == 0 || SetWindowRgn(windowHandle, region, true) == 0)
        {
            if (region != 0)
            {
                DeleteObject(region);
            }

            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to shape the microphone mute overlay window.");
        }

        // Windows owns the region after a successful SetWindowRgn call.
    }

    private void RestoreOrCreatePosition()
    {
        var size = ScaleDip(BadgeSizeDip);
        var stored = positionStore.Load();
        var workArea = stored.HasValue ? FindWorkAreaForPosition(stored.Value, size) : null;
        if (stored.HasValue && workArea.HasValue)
        {
            var clamped = ClampToWorkArea(stored.Value, size, workArea.Value);
            AppWindow.MoveAndResize(new RectInt32(clamped.X, clamped.Y, size, size));
            if (clamped != stored.Value)
            {
                positionStore.Save(clamped);
            }

            return;
        }

        var margin = ScaleDip(MarginDip);
        var primaryWorkArea = GetMonitorWorkArea(new Point(), MonitorDefaultToPrimary);
        var initial = new PointInt32(
            primaryWorkArea.X + primaryWorkArea.Width - size - margin,
            primaryWorkArea.Y + margin);
        AppWindow.MoveAndResize(new RectInt32(initial.X, initial.Y, size, size));
        positionStore.Save(initial);
    }

    private nint WindowSubclassProcedure(
        nint window,
        uint message,
        nint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WmNcHitTest)
        {
            return HtClient;
        }

        if (message == WmMouseActivate)
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow != 0 && GetAncestor(foregroundWindow, GaRoot) != windowHandle)
            {
                foregroundWindowBeforePointer = foregroundWindow;
            }

            return MaNoActivate;
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void DragSurface_PointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (!args.GetCurrentPoint(DragSurface).Properties.IsLeftButtonPressed
            || !GetCursorPos(out dragStartCursor)
            || !GetWindowRect(windowHandle, out var windowRect))
        {
            return;
        }

        dragStartWindow = new PointInt32(windowRect.Left, windowRect.Top);
        dragging = DragSurface.CapturePointer(args.Pointer);
        args.Handled = dragging;
    }

    private void DragSurface_PointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!dragging)
        {
            return;
        }

        MoveWithCursor(windowHandle);
        args.Handled = true;
    }

    private void DragSurface_PointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!dragging)
        {
            return;
        }

        MoveWithCursor(windowHandle);
        dragging = false;
        DragSurface.ReleasePointerCapture(args.Pointer);
        SavePositionSafely();
        foregroundWindowBeforePointer = 0;
        args.Handled = true;
    }

    private void DragSurface_PointerCanceled(object sender, PointerRoutedEventArgs args)
    {
        CompletePointerDrag(args);
    }

    private void DragSurface_PointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        CompletePointerDrag(args);
    }

    private void MicrophoneMuteOverlayWindow_Closed(object sender, WindowEventArgs args)
    {
        foreach (var handle in subclassedWindowHandles)
        {
            _ = RemoveWindowSubclass(handle, subclassProcedure, 1);
        }

        subclassedWindowHandles.Clear();
        Closed -= MicrophoneMuteOverlayWindow_Closed;
    }

    private bool ConfigureChildSubclass(nint childWindow, nint parameter)
    {
        if (TryConfigureSubclass(childWindow))
        {
            return true;
        }

        childSubclassConfigurationSucceeded = false;
        return false;
    }

    private bool TryConfigureSubclass(nint handle)
    {
        if (!SetWindowSubclass(handle, subclassProcedure, 1, 0))
        {
            return false;
        }

        subclassedWindowHandles.Add(handle);
        return true;
    }

    private void SaveCurrentPosition()
    {
        ApplyWindowSizeAndShape();
        var size = AppWindow.Size.Width;
        var workArea = FindWorkAreaForPosition(AppWindow.Position, size);
        if (!workArea.HasValue)
        {
            RestoreOrCreatePosition();
            return;
        }

        var position = ClampToWorkArea(AppWindow.Position, size, workArea.Value);
        AppWindow.Move(position);
        positionStore.Save(position);
    }

    private void MoveWithCursor(nint window)
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var x = dragStartWindow.X + cursor.X - dragStartCursor.X;
        var y = dragStartWindow.Y + cursor.Y - dragStartCursor.Y;
        _ = SetWindowPos(
            window,
            0,
            x,
            y,
            0,
            0,
            SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private void CompletePointerDrag(PointerRoutedEventArgs args)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        SavePositionSafely();
        foregroundWindowBeforePointer = 0;
        args.Handled = true;
    }

    private void RestoreForegroundWindow()
    {
        if (foregroundWindowBeforePointer != 0)
        {
            _ = SetForegroundWindow(foregroundWindowBeforePointer);
        }
    }

    private void SavePositionSafely()
    {
        try
        {
            SaveCurrentPosition();
        }
        catch
        {
            RestoreOrCreatePosition();
        }
    }

    private static RectInt32? FindWorkAreaForPosition(PointInt32 position, int size)
    {
        var point = new Point
        {
            X = position.X + size / 2,
            Y = position.Y + size / 2,
        };
        var monitor = MonitorFromPoint(point, 0);
        return monitor == 0 ? null : GetMonitorWorkArea(monitor);
    }

    private static RectInt32 GetMonitorWorkArea(Point point, uint fallback) =>
        GetMonitorWorkArea(MonitorFromPoint(point, fallback));

    private static RectInt32 GetMonitorWorkArea(nint monitor)
    {
        var info = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>(),
        };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to read the monitor work area.");
        }

        return new RectInt32(
            info.WorkArea.Left,
            info.WorkArea.Top,
            info.WorkArea.Right - info.WorkArea.Left,
            info.WorkArea.Bottom - info.WorkArea.Top);
    }

    private static PointInt32 ClampToWorkArea(PointInt32 position, int size, RectInt32 workArea)
    {
        return new PointInt32(
            Math.Clamp(position.X, workArea.X, workArea.X + Math.Max(0, workArea.Width - size)),
            Math.Clamp(position.Y, workArea.Y, workArea.Y + Math.Max(0, workArea.Height - size)));
    }

    private int ScaleDip(int value) => checked((int)Math.Round(value * GetDpiForWindow(windowHandle) / 96d));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint window, int index, nint newValue);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    private const uint GaRoot = 2;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProcedure(
        nint window,
        uint message,
        nint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool ChildEnumerationProcedure(nint window, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(
        nint parentWindow,
        ChildEnumerationProcedure callback,
        nint parameter);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint window,
        SubclassProcedure callback,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        nint window,
        SubclassProcedure callback,
        nuint subclassId);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(nint window, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint value);
}
