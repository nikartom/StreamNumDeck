using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Core.Obs;

public enum ObsConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Faulted,
}

public sealed class ObsConnectionStateChangedEventArgs(
    ObsConnectionState state,
    string? errorMessage = null) : EventArgs
{
    public ObsConnectionState State { get; } = state;

    public string? ErrorMessage { get; } = errorMessage;
}

public sealed record ObsCatalog(
    IReadOnlyList<string> Scenes,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Sources)
{
    public static ObsCatalog Empty { get; } = new([], [], []);
}

public interface IObsController : IAsyncDisposable
{
    ObsConnectionState State { get; }

    string? LastError { get; }

    event EventHandler<ObsConnectionStateChangedEventArgs>? StateChanged;

    Task ConnectAsync(ObsConnectionSettings settings, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task ExecuteAsync(ObsActionDefinition action, CancellationToken cancellationToken = default);

    Task<ObsCatalog> GetCatalogAsync(CancellationToken cancellationToken = default);
}

public interface IObsConnectionTester
{
    Task<ObsCatalog> TestAsync(
        string host,
        int port,
        string? password,
        CancellationToken cancellationToken = default);
}
