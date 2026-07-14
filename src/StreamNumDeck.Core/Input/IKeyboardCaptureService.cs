namespace StreamNumDeck.Core.Input;

public interface IKeyboardCaptureService : IAsyncDisposable
{
    KeyboardCaptureState State { get; }

    bool IsNumLockOn { get; }

    event EventHandler<KeyboardCaptureStateChangedEventArgs>? StateChanged;

    event EventHandler<NumLockStateChangedEventArgs>? NumLockStateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SetNumLockAsync(bool isOn, CancellationToken cancellationToken = default);

    IAsyncEnumerable<CapturedKeyPress> ReadAllAsync(CancellationToken cancellationToken = default);
}
