namespace StreamNumDeck.Core.Configuration;

public sealed class ConfigurationChangedEventArgs(AppConfiguration configuration) : EventArgs
{
    public AppConfiguration Configuration { get; } = configuration;
}

/// <summary>
/// Serializes configuration access so UI and background services observe one
/// validated snapshot and every accepted change is persisted before publication.
/// </summary>
public sealed class ConfigurationService : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IConfigurationStore store;
    private AppConfiguration? current;
    private bool disposed;

    public ConfigurationService(IConfigurationStore store)
    {
        this.store = Guard.NotNull(store, nameof(store));
    }

    public event EventHandler<ConfigurationChangedEventArgs>? Changed;

    public async Task<AppConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            current ??= await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            return current;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AppConfiguration> UpdateAsync(
        Func<AppConfiguration, AppConfiguration> update,
        CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        Guard.NotNull(update, nameof(update));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        AppConfiguration updated;

        try
        {
            current ??= await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            updated = update(current) ?? throw new InvalidOperationException("A configuration update cannot return null.");
            await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            current = updated;
        }
        finally
        {
            gate.Release();
        }

        Changed?.Invoke(this, new ConfigurationChangedEventArgs(updated));
        return updated;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        gate.Dispose();
        disposed = true;
    }
}
