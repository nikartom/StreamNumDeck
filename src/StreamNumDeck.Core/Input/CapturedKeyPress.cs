using StreamNumDeck.Core.Deck;

namespace StreamNumDeck.Core.Input;

public readonly record struct CapturedKeyPress(
    DeckKey Key,
    NumLockLayer Layer,
    DateTimeOffset Timestamp);

public enum KeyboardCaptureState
{
    Stopped,
    Running,
    Paused,
}

public sealed class KeyboardCaptureStateChangedEventArgs(KeyboardCaptureState state) : EventArgs
{
    public KeyboardCaptureState State { get; } = state;
}

public sealed class NumLockStateChangedEventArgs(bool isOn) : EventArgs
{
    public bool IsOn { get; } = isOn;
}
