using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Settings;
using StreamNumDeck.Wpf.Services;
using StreamNumDeck.Wpf.ViewModels;

namespace StreamNumDeck.Wpf.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    private static readonly KeyAssignment EditedAssignment = new(
        "Новая подпись",
        IconReference.BuiltIn("play"),
        new OpenUriActionDefinition("https://example.com/"));

    [ClassInitialize]
    public static void InitializeResources(TestContext context)
    {
        // AppStrings reads the localized strings through pack URIs of the application assembly.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        System.Windows.Application.ResourceAssembly ??= typeof(App).Assembly;
    }

    [TestMethod]
    public async Task UpdateAssignmentAsync_WritesToRequestedLayerRegardlessOfCurrentOne()
    {
        var configuration = AppConfiguration.CreateDefault("Основной");
        var viewModel = CreateViewModel(configuration, out var configurationService);
        await viewModel.SelectProfileAsync(configuration.ActiveProfileId);
        Assert.IsFalse(viewModel.IsNumLockOn);

        await viewModel.UpdateAssignmentAsync(
            configuration.ActiveProfileId,
            NumLockLayer.On,
            DeckKey.Numpad7,
            EditedAssignment);

        var profile = (await configurationService.GetAsync()).ActiveProfile;
        Assert.AreEqual(EditedAssignment, profile.NumLockOn.GetAssignment(DeckKey.Numpad7));
        Assert.AreEqual(KeyAssignment.Empty, profile.NumLockOff.GetAssignment(DeckKey.Numpad7));
    }

    [TestMethod]
    public async Task UpdateAssignmentAsync_KeepsActiveProfileWhenAnotherOneIsEdited()
    {
        var configuration = AppConfiguration.CreateDefault("Основной");
        var editedProfileId = configuration.ActiveProfileId;
        var viewModel = CreateViewModel(configuration, out var configurationService);
        await viewModel.CreateProfileAsync("Игры", IconReference.BuiltIn("play"));
        var activeProfileId = viewModel.SelectedProfile!.Id;
        Assert.AreNotEqual(editedProfileId, activeProfileId);

        await viewModel.UpdateAssignmentAsync(
            editedProfileId,
            NumLockLayer.Off,
            DeckKey.Numpad7,
            EditedAssignment);

        var updated = await configurationService.GetAsync();
        Assert.AreEqual(activeProfileId, viewModel.SelectedProfile!.Id);
        Assert.AreEqual(
            EditedAssignment,
            updated.Profiles.Single(profile => profile.Id == editedProfileId).NumLockOff.GetAssignment(DeckKey.Numpad7));
        Assert.AreEqual(KeyAssignment.Empty, updated.ActiveProfile.NumLockOff.GetAssignment(DeckKey.Numpad7));
    }

    [TestMethod]
    public async Task UpdateAssignmentAsync_ProfileSwitchIsPlacedInEveryProfile()
    {
        var viewModel = CreateViewModel(AppConfiguration.CreateDefault("Основной"), out var configurationService);
        await viewModel.CreateProfileAsync("Игры", IconReference.BuiltIn("play"));
        var switchAssignment = new KeyAssignment(
            "Дальше",
            IconReference.BuiltIn("play"),
            new SwitchProfileActionDefinition(ProfileSwitchMode.Next));

        await viewModel.UpdateAssignmentAsync(
            viewModel.SelectedProfile!.Id,
            NumLockLayer.Off,
            DeckKey.Numpad9,
            switchAssignment);

        var profiles = (await configurationService.GetAsync()).Profiles;
        Assert.HasCount(2, profiles);
        foreach (var profile in profiles)
        {
            Assert.AreEqual(switchAssignment, profile.NumLockOff.GetAssignment(DeckKey.Numpad9));
        }
    }

    [TestMethod]
    public async Task GetProfileSwitchConflicts_NamesOtherProfilesWithDifferentAssignments()
    {
        var configuration = AppConfiguration.CreateDefault("Основной");
        var viewModel = CreateViewModel(configuration, out _);
        await viewModel.UpdateAssignmentAsync(configuration.ActiveProfileId, NumLockLayer.Off, DeckKey.Numpad9, EditedAssignment);
        await viewModel.CreateProfileAsync("Игры", IconReference.BuiltIn("play"));
        await viewModel.CreateProfileAsync("Монтаж", IconReference.BuiltIn("play"));
        var switchAssignment = new KeyAssignment(
            "Дальше",
            IconReference.BuiltIn("play"),
            new SwitchProfileActionDefinition(ProfileSwitchMode.Next));

        var conflicts = viewModel.GetProfileSwitchConflicts(
            viewModel.SelectedProfile!.Id,
            NumLockLayer.Off,
            DeckKey.Numpad9,
            switchAssignment);

        CollectionAssert.AreEqual(new[] { "Основной" }, conflicts.ToArray());
    }

    [TestMethod]
    public async Task CreateProfileAsync_CopiesExistingProfileSwitchKeys()
    {
        var configuration = AppConfiguration.CreateDefault("Основной");
        var viewModel = CreateViewModel(configuration, out var configurationService);
        var switchAssignment = new KeyAssignment(
            "Дальше",
            IconReference.BuiltIn("play"),
            new SwitchProfileActionDefinition(ProfileSwitchMode.Next));
        await viewModel.UpdateAssignmentAsync(configuration.ActiveProfileId, NumLockLayer.On, DeckKey.Numpad9, switchAssignment);
        await viewModel.UpdateAssignmentAsync(configuration.ActiveProfileId, NumLockLayer.On, DeckKey.Numpad7, EditedAssignment);

        await viewModel.CreateProfileAsync("Игры", IconReference.BuiltIn("play"));

        var created = (await configurationService.GetAsync()).ActiveProfile;
        Assert.AreEqual("Игры", created.Name);
        Assert.AreEqual(switchAssignment, created.NumLockOn.GetAssignment(DeckKey.Numpad9));
        Assert.AreEqual(KeyAssignment.Empty, created.NumLockOn.GetAssignment(DeckKey.Numpad7));
    }

    [TestMethod]
    public async Task CreateProfileAsync_DuplicateNameProducesVisibleReason()
    {
        var viewModel = CreateViewModel(AppConfiguration.CreateDefault("Основной"), out _);

        var exception = await Assert.ThrowsExactlyAsync<UserFacingException>(() =>
            viewModel.CreateProfileAsync("Основной", IconReference.BuiltIn("play")));

        Assert.AreEqual(exception.Message, UserErrorFormatter.GetSafeDetail(exception));
        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message));
    }

    // The tested paths use only configuration and audio preloading.
    private static MainViewModel CreateViewModel(
        AppConfiguration configuration,
        out ConfigurationService configurationService)
    {
        configurationService = new ConfigurationService(new InMemoryConfigurationStore(configuration));
        return new MainViewModel(
            configurationService,
            null!,
            null!,
            null!,
            null!,
            new NullAudioPlaybackService(),
            null!,
            null!,
            null!,
            null!);
    }

    private sealed class InMemoryConfigurationStore(AppConfiguration configuration) : IConfigurationStore
    {
        public Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(configuration);

        public Task SaveAsync(AppConfiguration updated, CancellationToken cancellationToken = default)
        {
            configuration = updated;
            return Task.CompletedTask;
        }
    }

    private sealed class NullAudioPlaybackService : IAudioPlaybackService
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioOutputDevice>>([]);

        public Task PlayAsync(
            PlaySoundActionDefinition action,
            GlobalSettings settings,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PreloadAsync(
            IEnumerable<PlaySoundActionDefinition> actions,
            GlobalSettings settings,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => default;
    }
}
