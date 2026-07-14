using System.Collections.Immutable;

namespace StreamNumDeck.Core.Deck;

/// <summary>
/// Physical keys available for assignments. NumLock is intentionally excluded
/// because it selects the active deck layer.
/// </summary>
public enum DeckKey
{
    Insert,
    Home,
    PageUp,
    Delete,
    End,
    PageDown,
    NumpadDivide,
    NumpadMultiply,
    NumpadSubtract,
    Numpad7,
    Numpad8,
    Numpad9,
    NumpadAdd,
    Numpad4,
    Numpad5,
    Numpad6,
    Numpad1,
    Numpad2,
    Numpad3,
    NumpadEnter,
    Numpad0,
    NumpadDecimal,
}

public static class DeckKeyCatalog
{
    public static ImmutableArray<DeckKey> AssignableKeys { get; } = ((DeckKey[])Enum
        .GetValues(typeof(DeckKey)))
        .ToImmutableArray();
}
