using System.Collections.Immutable;

namespace StreamNumDeck.Core.Actions;

[Flags]
public enum MacroModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Windows = 8,
}

public enum MacroKey
{
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    F13,
    F14,
    F15,
    F16,
    F17,
    F18,
    F19,
    F20,
    F21,
    F22,
    F23,
    F24,
    Enter,
    Escape,
    Tab,
    Space,
    Backspace,
    Delete,
    Insert,
    Home,
    End,
    PageUp,
    PageDown,
    Up,
    Down,
    Left,
    Right,
    VolumeUp,
    VolumeDown,
    VolumeMute,
    MediaPlayPause,
    MediaNext,
    MediaPrevious,
}

public sealed record KeyboardChord
{
    public KeyboardChord(MacroKey key, MacroModifiers modifiers = MacroModifiers.None)
    {
        if (!Enum.IsDefined(typeof(MacroKey), key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        const MacroModifiers allModifiers =
            MacroModifiers.Control |
            MacroModifiers.Shift |
            MacroModifiers.Alt |
            MacroModifiers.Windows;
        if ((modifiers & ~allModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modifiers));
        }

        Key = key;
        Modifiers = modifiers;
    }

    public MacroKey Key { get; }

    public MacroModifiers Modifiers { get; }
}

public enum KeyboardMacroStepKind
{
    KeyPress,
    Delay,
}

public sealed record KeyboardMacroStep
{
    public const int MaximumDelayMilliseconds = 60_000;

    public KeyboardMacroStep(
        KeyboardMacroStepKind kind,
        KeyboardChord? chord,
        int delayMilliseconds)
    {
        if (kind is KeyboardMacroStepKind.KeyPress)
        {
            Guard.NotNull(chord, nameof(chord));
            if (delayMilliseconds != 0)
            {
                throw new ArgumentException("A key-press step cannot contain a delay.", nameof(delayMilliseconds));
            }
        }
        else if (kind is KeyboardMacroStepKind.Delay)
        {
            if (chord is not null)
            {
                throw new ArgumentException("A delay step cannot contain a keyboard chord.", nameof(chord));
            }

            if (delayMilliseconds is < 1 or > MaximumDelayMilliseconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(delayMilliseconds),
                    $"A delay must be between 1 and {MaximumDelayMilliseconds} milliseconds.");
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        Chord = chord;
        DelayMilliseconds = delayMilliseconds;
    }

    public KeyboardMacroStepKind Kind { get; }

    public KeyboardChord? Chord { get; }

    public int DelayMilliseconds { get; }

    public static KeyboardMacroStep KeyPress(MacroKey key, MacroModifiers modifiers = MacroModifiers.None) =>
        new(KeyboardMacroStepKind.KeyPress, new KeyboardChord(key, modifiers), 0);

    public static KeyboardMacroStep Delay(int milliseconds) =>
        new(KeyboardMacroStepKind.Delay, null, milliseconds);
}

public sealed record KeyboardMacroActionDefinition : ActionDefinition
{
    public const int MaximumSteps = 64;

    public KeyboardMacroActionDefinition(ImmutableArray<KeyboardMacroStep> steps)
    {
        if (steps.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A keyboard macro must contain at least one step.", nameof(steps));
        }

        if (steps.Length > MaximumSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), $"A macro cannot exceed {MaximumSteps} steps.");
        }

        if (steps.Any(static step => step is null))
        {
            throw new ArgumentException("Macro steps cannot be null.", nameof(steps));
        }

        Steps = steps;
    }

    public override ActionGroup Group => ActionGroup.System;

    public ImmutableArray<KeyboardMacroStep> Steps { get; }
}
