using StreamNumDeck.Core.Deck;

namespace StreamNumDeck.App.Presentation;

public static class DeckKeyPresentation
{
    public static string GetPhysicalLabel(DeckKey key) => key switch
    {
        DeckKey.Insert => "Ins",
        DeckKey.Home => "Home",
        DeckKey.PageUp => "PgUp",
        DeckKey.Delete => "Del",
        DeckKey.End => "End",
        DeckKey.PageDown => "PgDn",
        DeckKey.NumpadDivide => "/",
        DeckKey.NumpadMultiply => "*",
        DeckKey.NumpadSubtract => "−",
        DeckKey.NumpadAdd => "+",
        DeckKey.NumpadEnter => "Enter",
        DeckKey.NumpadDecimal => ".",
        DeckKey.Numpad0 => "0",
        DeckKey.Numpad1 => "1",
        DeckKey.Numpad2 => "2",
        DeckKey.Numpad3 => "3",
        DeckKey.Numpad4 => "4",
        DeckKey.Numpad5 => "5",
        DeckKey.Numpad6 => "6",
        DeckKey.Numpad7 => "7",
        DeckKey.Numpad8 => "8",
        DeckKey.Numpad9 => "9",
        _ => key.ToString(),
    };
}
