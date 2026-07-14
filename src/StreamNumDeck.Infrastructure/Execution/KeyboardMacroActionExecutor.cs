using System.ComponentModel;
using System.Runtime.InteropServices;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Infrastructure.Execution;

public sealed class KeyboardMacroActionExecutor : IActionExecutor
{
    public bool CanExecute(ActionDefinition action) => action is KeyboardMacroActionDefinition;

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        if (action is not KeyboardMacroActionDefinition macro)
        {
            throw new NotSupportedException($"{action.GetType().Name} is not a keyboard macro.");
        }

        foreach (var step in macro.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (step.Kind is KeyboardMacroStepKind.Delay)
            {
                await Task.Delay(step.DelayMilliseconds, cancellationToken).ConfigureAwait(false);
                continue;
            }

            WindowsKeyboardInput.Send(step.Chord!);
        }
    }
}

internal static class WindowsKeyboardInput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const nuint InjectionTag = 0x534E444B;

    public static void Send(KeyboardChord chord)
    {
        var inputs = new List<NativeInput>(10);
        AddModifier(inputs, chord.Modifiers, MacroModifiers.Control, 0x11, keyUp: false);
        AddModifier(inputs, chord.Modifiers, MacroModifiers.Shift, 0x10, keyUp: false);
        AddModifier(inputs, chord.Modifiers, MacroModifiers.Alt, 0x12, keyUp: false);
        AddModifier(inputs, chord.Modifiers, MacroModifiers.Windows, 0x5B, keyUp: false);

        var (virtualKey, extended) = MapKey(chord.Key);
        inputs.Add(CreateInput(virtualKey, extended, keyUp: false));
        inputs.Add(CreateInput(virtualKey, extended, keyUp: true));

        AddModifier(inputs, chord.Modifiers, MacroModifiers.Windows, 0x5B, keyUp: true);
        AddModifier(inputs, chord.Modifiers, MacroModifiers.Alt, 0x12, keyUp: true);
        AddModifier(inputs, chord.Modifiers, MacroModifiers.Shift, 0x10, keyUp: true);
        AddModifier(inputs, chord.Modifiers, MacroModifiers.Control, 0x11, keyUp: true);

        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeInput>());
        if (sent != inputs.Count)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not inject the complete keyboard chord.");
        }
    }

    internal static (ushort VirtualKey, bool Extended) MapKey(MacroKey key)
    {
        if (key is >= MacroKey.A and <= MacroKey.Z)
        {
            return ((ushort)(0x41 + (int)key - (int)MacroKey.A), false);
        }

        if (key is >= MacroKey.Digit0 and <= MacroKey.Digit9)
        {
            return ((ushort)(0x30 + (int)key - (int)MacroKey.Digit0), false);
        }

        if (key is >= MacroKey.F1 and <= MacroKey.F24)
        {
            return ((ushort)(0x70 + (int)key - (int)MacroKey.F1), false);
        }

        return key switch
        {
            MacroKey.Enter => (0x0D, false),
            MacroKey.Escape => (0x1B, false),
            MacroKey.Tab => (0x09, false),
            MacroKey.Space => (0x20, false),
            MacroKey.Backspace => (0x08, false),
            MacroKey.Delete => (0x2E, true),
            MacroKey.Insert => (0x2D, true),
            MacroKey.Home => (0x24, true),
            MacroKey.End => (0x23, true),
            MacroKey.PageUp => (0x21, true),
            MacroKey.PageDown => (0x22, true),
            MacroKey.Up => (0x26, true),
            MacroKey.Down => (0x28, true),
            MacroKey.Left => (0x25, true),
            MacroKey.Right => (0x27, true),
            MacroKey.VolumeUp => (0xAF, true),
            MacroKey.VolumeDown => (0xAE, true),
            MacroKey.VolumeMute => (0xAD, true),
            MacroKey.MediaPlayPause => (0xB3, true),
            MacroKey.MediaNext => (0xB0, true),
            MacroKey.MediaPrevious => (0xB1, true),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
        };
    }

    private static void AddModifier(
        ICollection<NativeInput> inputs,
        MacroModifiers activeModifiers,
        MacroModifiers modifier,
        ushort virtualKey,
        bool keyUp)
    {
        if (activeModifiers.HasFlag(modifier))
        {
            inputs.Add(CreateInput(virtualKey, extended: modifier is MacroModifiers.Windows, keyUp));
        }
    }

    private static NativeInput CreateInput(ushort virtualKey, bool extended, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = (extended ? KeyEventExtendedKey : 0) | (keyUp ? KeyEventKeyUp : 0),
                ExtraInfo = InjectionTag,
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, NativeInput[] inputs, int inputSize);
}
