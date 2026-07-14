using System.Collections.Immutable;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Infrastructure.Configuration;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class JsonConfigurationStoreTests
{
    private string testDirectory = null!;
    private ConfigurationPaths paths = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(
            Path.GetTempPath(),
            "StreamNumDeck.Tests",
            Guid.NewGuid().ToString("N"));
        paths = new ConfigurationPaths(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenNoFileExists_CreatesValidDefaultConfiguration()
    {
        using var store = new JsonConfigurationStore(paths);

        var configuration = await store.LoadAsync();

        Assert.IsTrue(File.Exists(paths.ConfigurationFilePath));
        Assert.AreEqual("Основной стрим", configuration.ActiveProfile.Name);
        Assert.HasCount(22, configuration.ActiveProfile.NumLockOff.Assignments);
        Assert.HasCount(22, configuration.ActiveProfile.NumLockOn.Assignments);
    }

    [TestMethod]
    public async Task LoadAsync_WhenNoFileExists_UsesConfiguredDefaultProfileName()
    {
        using var store = new JsonConfigurationStore(
            paths,
            () => AppConfiguration.CreateDefault("Main profile"));

        var configuration = await store.LoadAsync();

        Assert.AreEqual("Main profile", configuration.ActiveProfile.Name);
    }

    [TestMethod]
    public async Task SaveAndLoad_PreservesTypedActionAndIcon()
    {
        using var store = new JsonConfigurationStore(paths);
        var configuration = AppConfiguration.CreateDefault();
        var profileWithIcon = new DeckProfile(
            configuration.ActiveProfile.Id,
            configuration.ActiveProfile.Name,
            configuration.ActiveProfile.NumLockOff,
            configuration.ActiveProfile.NumLockOn,
            IconReference.BuiltIn("star"));
        configuration = configuration.ReplaceProfile(profileWithIcon);
        var assignment = new KeyAssignment(
            "Аплодисменты",
            IconReference.CustomAsset("icons/applause.png"),
            new PlaySoundActionDefinition("C:\\Sounds\\applause.wav", 73, SoundPlaybackBehavior.StopOthers));
        var updatedProfile = configuration.ActiveProfile.WithAssignment(
            NumLockLayer.On,
            DeckKey.NumpadAdd,
            assignment);
        var updatedConfiguration = configuration.ReplaceProfile(updatedProfile);

        await store.SaveAsync(updatedConfiguration);
        var loaded = await store.LoadAsync();

        var loadedAssignment = loaded.ActiveProfile.NumLockOn.GetAssignment(DeckKey.NumpadAdd);
        Assert.AreEqual("Аплодисменты", loadedAssignment.Label);
        Assert.AreEqual(IconKind.CustomAsset, loadedAssignment.Icon.Kind);
        Assert.AreEqual(IconReference.BuiltIn("star"), loaded.ActiveProfile.Icon);
        var soundAction = Assert.IsInstanceOfType<PlaySoundActionDefinition>(loadedAssignment.Action);
        Assert.AreEqual(73, soundAction.Volume);
        Assert.AreEqual(SoundPlaybackBehavior.StopOthers, soundAction.PlaybackBehavior);
    }

    [TestMethod]
    public async Task SaveAndLoad_PreservesKeyboardMacroSteps()
    {
        using var store = new JsonConfigurationStore(paths);
        var configuration = AppConfiguration.CreateDefault();
        var macro = new KeyboardMacroActionDefinition(
            ImmutableArray.Create(
                KeyboardMacroStep.KeyPress(MacroKey.S, MacroModifiers.Control | MacroModifiers.Shift),
                KeyboardMacroStep.Delay(175),
                KeyboardMacroStep.KeyPress(MacroKey.Enter)));
        var profile = configuration.ActiveProfile.WithAssignment(
            NumLockLayer.Off,
            DeckKey.Numpad1,
            new KeyAssignment("Макрос", IconReference.BuiltIn("play"), macro));

        await store.SaveAsync(configuration.ReplaceProfile(profile));
        var loaded = await store.LoadAsync();

        var loadedMacro = Assert.IsInstanceOfType<KeyboardMacroActionDefinition>(
            loaded.ActiveProfile.NumLockOff.GetAssignment(DeckKey.Numpad1).Action);
        Assert.HasCount(3, loadedMacro.Steps);
        Assert.AreEqual(175, loadedMacro.Steps[1].DelayMilliseconds);
        Assert.AreEqual(MacroKey.Enter, loadedMacro.Steps[2].Chord!.Key);
    }

    [TestMethod]
    public async Task SaveAndLoad_PreservesSystemAudioAction()
    {
        using var store = new JsonConfigurationStore(paths);
        var configuration = AppConfiguration.CreateDefault();
        var action = new AdjustApplicationVolumeActionDefinition(
            "msedge.exe",
            VolumeAdjustmentDirection.Decrease,
            8);
        var profile = configuration.ActiveProfile.WithAssignment(
            NumLockLayer.On,
            DeckKey.NumpadSubtract,
            new KeyAssignment("Браузер тише", IconReference.BuiltIn("volume"), action));

        await store.SaveAsync(configuration.ReplaceProfile(profile));
        var loaded = await store.LoadAsync();

        var loadedAction = Assert.IsInstanceOfType<AdjustApplicationVolumeActionDefinition>(
            loaded.ActiveProfile.NumLockOn.GetAssignment(DeckKey.NumpadSubtract).Action);
        Assert.AreEqual("msedge.exe", loadedAction.ApplicationId);
        Assert.AreEqual(VolumeAdjustmentDirection.Decrease, loadedAction.Direction);
        Assert.AreEqual(8, loadedAction.StepPercent);
    }

    [TestMethod]
    public async Task LoadAsync_WhenPrimaryIsCorrupt_RecoversBackupAndRepairsPrimary()
    {
        using var store = new JsonConfigurationStore(paths);
        var original = AppConfiguration.CreateDefault();
        await store.SaveAsync(original);

        var renamedProfile = new DeckProfile(
            original.ActiveProfile.Id,
            "Изменённый профиль",
            original.ActiveProfile.NumLockOff,
            original.ActiveProfile.NumLockOn);
        var renamedConfiguration = original.ReplaceProfile(renamedProfile);
        await store.SaveAsync(renamedConfiguration);

        var latestProfile = new DeckProfile(
            original.ActiveProfile.Id,
            "Последний профиль",
            original.ActiveProfile.NumLockOff,
            original.ActiveProfile.NumLockOn);
        await store.SaveAsync(original.ReplaceProfile(latestProfile));
        await File.WriteAllTextAsync(paths.ConfigurationFilePath, "{ invalid json");

        var recovered = await store.LoadAsync();
        var loadedAgain = await store.LoadAsync();

        Assert.AreEqual("Изменённый профиль", recovered.ActiveProfile.Name);
        Assert.AreEqual("Изменённый профиль", loadedAgain.ActiveProfile.Name);
        Assert.IsTrue(File.Exists(paths.BackupFilePath));
    }

    [TestMethod]
    public async Task LoadAsync_WhenSchemaIsUnsupported_ReportsVersionFailure()
    {
        Directory.CreateDirectory(paths.DirectoryPath);
        await File.WriteAllTextAsync(
            paths.ConfigurationFilePath,
            """
            {
              "schemaVersion": 99,
              "configuration": {}
            }
            """);
        using var store = new JsonConfigurationStore(paths);

        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(() => store.LoadAsync());

        var versionFailure = Assert.IsInstanceOfType<UnsupportedConfigurationVersionException>(exception.PrimaryFailure);
        Assert.AreEqual(99, versionFailure.Version);
        Assert.AreEqual(ConfigurationSchema.CurrentVersion, versionFailure.CurrentVersion);
    }
}
