using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Input;

namespace StreamNumDeck.Infrastructure.Input;

public sealed class WindowsKeyboardCaptureService : IKeyboardCaptureService
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmQuit = 0x0012;
    private const uint LlkhfExtended = 0x01;
    private const uint LlkhfInjected = 0x10;
    private const uint LlkhfLowerIlInjected = 0x02;
    private const uint VkControl = 0x11;
    private const uint VkMenu = 0x12;
    private const uint VkF12 = 0x7B;
    private const uint VkNumLock = 0x90;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfExtendedkey = 0x0001;
    private const uint KeyeventfKeyup = 0x0002;

    private readonly object gate = new();
    private readonly Channel<CapturedKeyPress> channel = Channel.CreateBounded<CapturedKeyPress>(
        new BoundedChannelOptions(128)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropWrite,
            AllowSynchronousContinuations = false,
        });
    private readonly HashSet<DeckKey> pressedKeys = [];
    private readonly LowLevelKeyboardProc hookCallback;
    private readonly Timer numLockMonitor;
    private Thread? hookThread;
    private TaskCompletionSource<object?>? startupCompletion;
    private nint hookHandle;
    private uint hookThreadId;
    private volatile bool numLockOn;
    private int captureTargets = (int)KeyboardCaptureTargets.All;
    private long suppressNumLockPollingUntil;
    private bool numLockKeyDown;
    private bool controlDown;
    private bool altDown;
    private bool emergencyChordDown;
    private bool disposed;
    private volatile KeyboardCaptureState state = KeyboardCaptureState.Stopped;

    public WindowsKeyboardCaptureService()
    {
        hookCallback = HookCallback;
        numLockOn = ReadSystemNumLockState();
        numLockMonitor = new Timer(
            static state => ((WindowsKeyboardCaptureService)state!).RefreshNumLockState(),
            this,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100));
    }

    public KeyboardCaptureState State => state;

    public bool IsNumLockOn => numLockOn;

    public KeyboardCaptureTargets CaptureTargets =>
        (KeyboardCaptureTargets)Volatile.Read(ref captureTargets);

    public event EventHandler<KeyboardCaptureStateChangedEventArgs>? StateChanged;

    public event EventHandler<NumLockStateChangedEventArgs>? NumLockStateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);

        Task startupTask;
        lock (gate)
        {
            if (state is not KeyboardCaptureState.Stopped)
            {
                return;
            }

            startupCompletion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            startupTask = startupCompletion.Task;
            hookThread = new Thread(HookThreadMain)
            {
                IsBackground = true,
                Name = "StreamNumDeck keyboard hook",
            };
            hookThread.Start();
        }

        await startupTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Thread? thread;
        uint threadId;
        lock (gate)
        {
            thread = hookThread;
            threadId = hookThreadId;
            if (thread is null)
            {
                return;
            }
        }

        if (threadId != 0 && !PostThreadMessage(threadId, WmQuit, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to stop the keyboard hook message loop.");
        }

        await Task.Run(thread.Join, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<CapturedKeyPress> ReadAllAsync(CancellationToken cancellationToken = default) =>
        channel.Reader.ReadAllAsync(cancellationToken);

    public Task SetCaptureTargetsAsync(
        KeyboardCaptureTargets targets,
        CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if ((targets & ~KeyboardCaptureTargets.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targets), targets, "Unknown keyboard capture target.");
        }

        Volatile.Write(ref captureTargets, (int)targets);
        return Task.CompletedTask;
    }

    public Task SetNumLockAsync(bool isOn, CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        RefreshNumLockState();
        if (numLockOn == isOn)
        {
            return Task.CompletedTask;
        }

        var inputs = new[]
        {
            CreateKeyboardInput(VkNumLock, KeyeventfExtendedkey),
            CreateKeyboardInput(VkNumLock, KeyeventfExtendedkey | KeyeventfKeyup),
        };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to change the Windows NumLock state.");
        }

        Interlocked.Exchange(ref suppressNumLockPollingUntil, PlatformCompatibility.TickCount64 + 100);
        SetNumLockState(isOn);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        numLockMonitor.Dispose();
        disposed = true;
        channel.Writer.TryComplete();
    }

    private void HookThreadMain()
    {
        try
        {
            hookThreadId = GetCurrentThreadId();
            SetNumLockState(ReadSystemNumLockState());
            hookHandle = SetWindowsHookEx(WhKeyboardLl, hookCallback, GetModuleHandle(null), 0);
            if (hookHandle == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to install the low-level keyboard hook.");
            }

            SetState(KeyboardCaptureState.Running);
            startupCompletion?.TrySetResult(null);

            while (true)
            {
                var result = GetMessage(out var message, 0, 0, 0);
                if (result == 0)
                {
                    break;
                }

                if (result < 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The keyboard hook message loop failed.");
                }

                TranslateMessage(in message);
                DispatchMessage(in message);
            }
        }
        catch (Exception exception)
        {
            startupCompletion?.TrySetException(exception);
        }
        finally
        {
            if (hookHandle != 0)
            {
                UnhookWindowsHookEx(hookHandle);
                hookHandle = 0;
            }

            pressedKeys.Clear();
            hookThreadId = 0;
            lock (gate)
            {
                hookThread = null;
            }

            SetState(KeyboardCaptureState.Stopped);
        }
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code < 0)
            {
                return CallNextHookEx(hookHandle, code, wParam, lParam);
            }

            var input = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if ((input.Flags & (LlkhfInjected | LlkhfLowerIlInjected)) != 0)
            {
                return CallNextHookEx(hookHandle, code, wParam, lParam);
            }

            var message = unchecked((int)wParam);
            var isDown = message is WmKeyDown or WmSysKeyDown;
            var isUp = message is WmKeyUp or WmSysKeyUp;
            if (!isDown && !isUp)
            {
                return CallNextHookEx(hookHandle, code, wParam, lParam);
            }

            UpdateModifierState(input.VkCode, isDown);
            if (HandleEmergencyPause(input.VkCode, isDown, isUp))
            {
                return 1;
            }

            if (input.ScanCode == KeyboardScanCodeTranslator.NumLockScanCode
                && (input.Flags & LlkhfExtended) == 0)
            {
                if (isDown && !numLockKeyDown)
                {
                    Interlocked.Exchange(ref suppressNumLockPollingUntil, PlatformCompatibility.TickCount64 + 100);
                    SetNumLockState(!numLockOn);
                    numLockKeyDown = true;
                }
                else if (isUp)
                {
                    numLockKeyDown = false;
                }

                return CallNextHookEx(hookHandle, code, wParam, lParam);
            }

            if (state is KeyboardCaptureState.Paused
                || !KeyboardScanCodeTranslator.TryTranslate(
                    input.ScanCode,
                    (input.Flags & LlkhfExtended) != 0,
                    out var key))
            {
                return CallNextHookEx(hookHandle, code, wParam, lParam);
            }

            if (isUp)
            {
                return pressedKeys.Remove(key)
                    ? 1
                    : CallNextHookEx(hookHandle, code, wParam, lParam);
            }

            if (!CaptureTargets.Includes(key))
            {
                return CallNextHookEx(hookHandle, code, wParam, lParam);
            }

            if (pressedKeys.Add(key))
            {
                channel.Writer.TryWrite(new CapturedKeyPress(
                    key,
                    numLockOn ? NumLockLayer.On : NumLockLayer.Off,
                    DateTimeOffset.UtcNow));
            }

            return 1;
        }
        catch
        {
            return CallNextHookEx(hookHandle, code, wParam, lParam);
        }
    }

    private void UpdateModifierState(uint virtualKey, bool isDown)
    {
        if (virtualKey is VkControl or 0xA2 or 0xA3)
        {
            controlDown = isDown;
        }
        else if (virtualKey is VkMenu or 0xA4 or 0xA5)
        {
            altDown = isDown;
        }
    }

    private bool HandleEmergencyPause(uint virtualKey, bool isDown, bool isUp)
    {
        if (virtualKey != VkF12)
        {
            return false;
        }

        if (isDown && controlDown && altDown && !emergencyChordDown)
        {
            emergencyChordDown = true;
            SetState(state is KeyboardCaptureState.Paused
                ? KeyboardCaptureState.Running
                : KeyboardCaptureState.Paused);
            pressedKeys.Clear();
            return true;
        }

        if (isUp && emergencyChordDown)
        {
            emergencyChordDown = false;
            return true;
        }

        return emergencyChordDown;
    }

    private void SetState(KeyboardCaptureState value)
    {
        if (state == value)
        {
            return;
        }

        state = value;
        StateChanged?.Invoke(this, new KeyboardCaptureStateChangedEventArgs(value));
    }

    private void RefreshNumLockState()
    {
        if (PlatformCompatibility.TickCount64 < Interlocked.Read(ref suppressNumLockPollingUntil))
        {
            return;
        }

        SetNumLockState(ReadSystemNumLockState());
    }

    private void SetNumLockState(bool value)
    {
        if (numLockOn == value)
        {
            return;
        }

        numLockOn = value;
        NumLockStateChanged?.Invoke(this, new NumLockStateChangedEventArgs(value));
    }

    private static bool ReadSystemNumLockState() => (GetKeyState((int)VkNumLock) & 1) != 0;

    private static Input CreateKeyboardInput(uint virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = checked((ushort)virtualKey),
                Flags = flags,
            },
        },
    };

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public readonly uint VkCode;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Message
    {
        public readonly nint Hwnd;
        public readonly uint Value;
        public readonly nuint WParam;
        public readonly nint LParam;
        public readonly uint Time;
        public readonly Point Point;
        public readonly uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Point
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    private static extern int GetMessage(out Message message, nint window, uint minimumMessage, uint maximumMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(in Message message);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern nint DispatchMessage(in Message message);

    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
