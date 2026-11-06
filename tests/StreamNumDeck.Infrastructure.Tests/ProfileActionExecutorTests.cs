using System.Collections.Immutable;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.Infrastructure.Execution;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class ProfileActionExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_NextProfileActivatesFollowingProfile()
    {
        var (store, first, second) = CreateStore();
        using var service = new ConfigurationService(store);
        var executor = new ProfileActionExecutor(service);

        await executor.ExecuteAsync(new SwitchProfileActionDefinition(ProfileSwitchMode.Next));
        Assert.AreEqual(second.Id, (await service.GetAsync()).ActiveProfileId);

        await executor.ExecuteAsync(new SwitchProfileActionDefinition(ProfileSwitchMode.Next));
        Assert.AreEqual(first.Id, store.Configuration.ActiveProfileId);
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveTargetDoesNotRewriteConfiguration()
    {
        var (store, first, _) = CreateStore();
        using var service = new ConfigurationService(store);
        var executor = new ProfileActionExecutor(service);

        await executor.ExecuteAsync(new SwitchProfileActionDefinition(ProfileSwitchMode.Specific, first.Id));

        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DeletedTargetReportsUnavailableProfile()
    {
        var (store, _, _) = CreateStore();
        using var service = new ConfigurationService(store);
        var executor = new ProfileActionExecutor(service);

        var exception = await Assert.ThrowsExactlyAsync<UserActionException>(() =>
            executor.ExecuteAsync(new SwitchProfileActionDefinition(ProfileSwitchMode.Specific, Guid.NewGuid())));

        Assert.AreEqual(UserActionError.ProfileUnavailable, exception.Error);
        Assert.AreEqual(0, store.SaveCount);
    }

    private static (InMemoryConfigurationStore Store, DeckProfile First, DeckProfile Second) CreateStore()
    {
        var first = DeckProfile.CreateDefault("First");
        var second = DeckProfile.CreateDefault("Second");
        var store = new InMemoryConfigurationStore(new AppConfiguration(
            first.Id,
            ImmutableArray.Create(first, second),
            GlobalSettings.Default));
        return (store, first, second);
    }

    private sealed class InMemoryConfigurationStore(AppConfiguration configuration) : IConfigurationStore
    {
        public AppConfiguration Configuration { get; private set; } = configuration;

        public int SaveCount { get; private set; }

        public Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Configuration);

        public Task SaveAsync(AppConfiguration updated, CancellationToken cancellationToken = default)
        {
            Configuration = updated;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
