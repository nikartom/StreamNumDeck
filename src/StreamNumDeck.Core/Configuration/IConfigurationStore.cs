namespace StreamNumDeck.Core.Configuration;

public interface IConfigurationStore
{
    Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default);
}
