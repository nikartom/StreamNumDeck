using StreamNumDeck.Core.Deck;

namespace StreamNumDeck.Core.Input;

[Flags]
public enum KeyboardCaptureTargets
{
    None = 0,
    Numpad = 1,
    NavigationBlock = 2,
    All = Numpad | NavigationBlock,
}

public static class KeyboardCaptureTargetsExtensions
{
    public static bool Includes(this KeyboardCaptureTargets targets, KeyboardCaptureTargets target) =>
        (targets & target) == target;

    public static bool Includes(this KeyboardCaptureTargets targets, DeckKey key) => key switch
    {
        DeckKey.Insert or
        DeckKey.Home or
        DeckKey.PageUp or
        DeckKey.Delete or
        DeckKey.End or
        DeckKey.PageDown => targets.Includes(KeyboardCaptureTargets.NavigationBlock),
        DeckKey.NumpadDivide or
        DeckKey.NumpadMultiply or
        DeckKey.NumpadSubtract or
        DeckKey.Numpad7 or
        DeckKey.Numpad8 or
        DeckKey.Numpad9 or
        DeckKey.NumpadAdd or
        DeckKey.Numpad4 or
        DeckKey.Numpad5 or
        DeckKey.Numpad6 or
        DeckKey.Numpad1 or
        DeckKey.Numpad2 or
        DeckKey.Numpad3 or
        DeckKey.NumpadEnter or
        DeckKey.Numpad0 or
        DeckKey.NumpadDecimal => targets.Includes(KeyboardCaptureTargets.Numpad),
        _ => false,
    };
}
