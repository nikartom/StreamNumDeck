using System.Collections.Immutable;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Core.Tests;

[TestClass]
public sealed class ProfileSwitchTests
{
    private static readonly SwitchProfileActionDefinition NextProfile = new(ProfileSwitchMode.Next);

    [TestMethod]
    public void ResolveProfileSwitchTarget_NextCyclesInListOrderAndWraps()
    {
        var (configuration, first, second, third) = CreateThreeProfiles();

        Assert.AreEqual(second.Id, configuration.ResolveProfileSwitchTarget(NextProfile));
        Assert.AreEqual(third.Id, configuration.WithActiveProfile(second.Id).ResolveProfileSwitchTarget(NextProfile));
        Assert.AreEqual(first.Id, configuration.WithActiveProfile(third.Id).ResolveProfileSwitchTarget(NextProfile));
    }

    [TestMethod]
    public void ResolveProfileSwitchTarget_SpecificProfileOrNullWhenItWasDeleted()
    {
        var (configuration, _, second, _) = CreateThreeProfiles();
        var action = new SwitchProfileActionDefinition(ProfileSwitchMode.Specific, second.Id);

        Assert.AreEqual(second.Id, configuration.ResolveProfileSwitchTarget(action));
        Assert.IsNull(configuration.RemoveProfile(second.Id).ResolveProfileSwitchTarget(action));
    }

    [TestMethod]
    public void Constructor_RequiresTargetOnlyForSpecificProfile()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SwitchProfileActionDefinition(ProfileSwitchMode.Specific));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SwitchProfileActionDefinition(ProfileSwitchMode.Specific, Guid.Empty));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SwitchProfileActionDefinition(ProfileSwitchMode.Next, Guid.NewGuid()));
    }

    [TestMethod]
    public void WithAssignmentInAllProfiles_SetsSameKeyAndLayerEverywhere()
    {
        var (configuration, _, _, _) = CreateThreeProfiles();
        var assignment = new KeyAssignment("Next", IconReference.BuiltIn("play"), NextProfile);

        var updated = configuration.WithAssignmentInAllProfiles(NumLockLayer.On, DeckKey.Numpad9, assignment);

        Assert.AreEqual(configuration.ActiveProfileId, updated.ActiveProfileId);
        foreach (var profile in updated.Profiles)
        {
            Assert.AreEqual(assignment, profile.NumLockOn.GetAssignment(DeckKey.Numpad9));
            Assert.AreEqual(KeyAssignment.Empty, profile.NumLockOff.GetAssignment(DeckKey.Numpad9));
        }
    }

    [TestMethod]
    public void WithProfileSwitchAssignmentsFrom_CopiesOnlyProfileSwitches()
    {
        var switchAssignment = new KeyAssignment("Next", IconReference.BuiltIn("play"), NextProfile);
        var otherAssignment = new KeyAssignment(
            "Site",
            IconReference.BuiltIn("play"),
            new OpenUriActionDefinition("https://example.com/"));
        var source = DeckProfile.CreateDefault("Source")
            .WithAssignment(NumLockLayer.Off, DeckKey.Numpad1, switchAssignment)
            .WithAssignment(NumLockLayer.On, DeckKey.Numpad2, otherAssignment);

        var copy = DeckProfile.CreateDefault("New").WithProfileSwitchAssignmentsFrom(source);

        Assert.AreEqual(switchAssignment, copy.NumLockOff.GetAssignment(DeckKey.Numpad1));
        Assert.AreEqual(KeyAssignment.Empty, copy.NumLockOn.GetAssignment(DeckKey.Numpad2));
        Assert.AreEqual("New", copy.Name);
    }

    private static (AppConfiguration Configuration, DeckProfile First, DeckProfile Second, DeckProfile Third)
        CreateThreeProfiles()
    {
        var first = DeckProfile.CreateDefault("First");
        var second = DeckProfile.CreateDefault("Second");
        var third = DeckProfile.CreateDefault("Third");
        var configuration = new AppConfiguration(
            first.Id,
            ImmutableArray.Create(first, second, third),
            GlobalSettings.Default);
        return (configuration, first, second, third);
    }
}
