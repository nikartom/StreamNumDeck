using StreamNumDeck.Core.Security;
using Windows.Security.Credentials;

namespace StreamNumDeck.Infrastructure.Security;

public sealed class WindowsCredentialStore : IProtectedCredentialStore
{
    private const string ResourceName = "StreamNumDeck.OBS";

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var vault = new PasswordVault();
        try
        {
            var credential = vault.Retrieve(ResourceName, key);
            credential.RetrievePassword();
            return Task.FromResult<string?>(credential.Password);
        }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070490))
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        Guard.NotNullOrWhiteSpace(secret, nameof(secret));
        cancellationToken.ThrowIfCancellationRequested();

        var vault = new PasswordVault();
        try
        {
            vault.Remove(vault.Retrieve(ResourceName, key));
        }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070490))
        {
        }

        vault.Add(new PasswordCredential(ResourceName, key, secret));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var vault = new PasswordVault();
        try
        {
            vault.Remove(vault.Retrieve(ResourceName, key));
        }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070490))
        {
        }

        return Task.CompletedTask;
    }

    private static void ValidateKey(string key) => Guard.NotNullOrWhiteSpace(key, nameof(key));
}
