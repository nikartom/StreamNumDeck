using StreamNumDeck.Core.Obs;
using StreamNumDeck.Core.Security;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Infrastructure.Obs;

public sealed class ObsConnectionTester : IObsConnectionTester
{
    public async Task<ObsCatalog> TestAsync(
        string host,
        int port,
        string? password,
        CancellationToken cancellationToken = default)
    {
        const string credentialKey = "obs.connection-test";
        await using var controller = new ObsWebSocketController(
            new TemporaryCredentialStore(credentialKey, password));
        await controller
            .ConnectAsync(new ObsConnectionSettings(host, port, credentialKey), cancellationToken)
            .ConfigureAwait(false);
        return await controller.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class TemporaryCredentialStore(string key, string? password) : IProtectedCredentialStore
    {
        public Task<string?> GetAsync(string requestedKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(
                string.Equals(requestedKey, key, StringComparison.Ordinal) ? password : null);
        }

        public Task SetAsync(string requestedKey, string secret, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The temporary OBS credential store is read-only.");

        public Task DeleteAsync(string requestedKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The temporary OBS credential store is read-only.");
    }
}
