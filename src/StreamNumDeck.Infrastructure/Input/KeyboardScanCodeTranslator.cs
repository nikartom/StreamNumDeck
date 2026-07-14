using StreamNumDeck.Core.Deck;

namespace StreamNumDeck.Infrastructure.Input;

public static class KeyboardScanCodeTranslator
{
    public const uint NumLockScanCode = 0x45;

    public static bool TryTranslate(uint scanCode, bool isExtended, out DeckKey key)
    {
        var mappedKey = (scanCode, isExtended) switch
        {
            (0x52, true) => (DeckKey?)DeckKey.Insert,
            (0x47, true) => DeckKey.Home,
            (0x49, true) => DeckKey.PageUp,
            (0x53, true) => DeckKey.Delete,
            (0x4F, true) => DeckKey.End,
            (0x51, true) => DeckKey.PageDown,
            (0x35, true) => DeckKey.NumpadDivide,
            (0x37, false) => DeckKey.NumpadMultiply,
            (0x4A, false) => DeckKey.NumpadSubtract,
            (0x47, false) => DeckKey.Numpad7,
            (0x48, false) => DeckKey.Numpad8,
            (0x49, false) => DeckKey.Numpad9,
            (0x4E, false) => DeckKey.NumpadAdd,
            (0x4B, false) => DeckKey.Numpad4,
            (0x4C, false) => DeckKey.Numpad5,
            (0x4D, false) => DeckKey.Numpad6,
            (0x4F, false) => DeckKey.Numpad1,
            (0x50, false) => DeckKey.Numpad2,
            (0x51, false) => DeckKey.Numpad3,
            (0x1C, true) => DeckKey.NumpadEnter,
            (0x52, false) => DeckKey.Numpad0,
            (0x53, false) => DeckKey.NumpadDecimal,
            _ => null,
        };

        key = mappedKey.GetValueOrDefault();
        return mappedKey.HasValue;
    }
}
