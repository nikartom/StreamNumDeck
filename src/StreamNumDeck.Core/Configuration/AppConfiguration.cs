using System.Collections.Immutable;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Core.Configuration;

public sealed record AppConfiguration
{
    public AppConfiguration(
        Guid activeProfileId,
        ImmutableArray<DeckProfile> profiles,
        GlobalSettings settings)
    {
        Guard.NotNull(settings, nameof(settings));

        if (profiles.IsDefaultOrEmpty)
        {
            throw new ArgumentException("At least one profile is required.", nameof(profiles));
        }

        if (profiles.Select(static profile => profile.Id).Distinct().Count() != profiles.Length)
        {
            throw new ArgumentException("Profile identifiers must be unique.", nameof(profiles));
        }

        if (profiles.All(profile => profile.Id != activeProfileId))
        {
            throw new ArgumentException("The active profile must exist in the profile collection.", nameof(activeProfileId));
        }

        ActiveProfileId = activeProfileId;
        Profiles = profiles;
        Settings = settings;
    }

    public Guid ActiveProfileId { get; }

    public ImmutableArray<DeckProfile> Profiles { get; }

    public GlobalSettings Settings { get; }

    public DeckProfile ActiveProfile => Profiles.Single(profile => profile.Id == ActiveProfileId);

    public static AppConfiguration CreateDefault(string? profileName = null)
    {
        var profile = profileName is null
            ? DeckProfile.CreateDefault()
            : DeckProfile.CreateDefault(profileName);
        return new AppConfiguration(profile.Id, ImmutableArray.Create(profile), GlobalSettings.Default);
    }

    public AppConfiguration ReplaceProfile(DeckProfile profile)
    {
        Guard.NotNull(profile, nameof(profile));

        var index = -1;
        for (var candidateIndex = 0; candidateIndex < Profiles.Length; candidateIndex++)
        {
            if (Profiles[candidateIndex].Id == profile.Id)
            {
                index = candidateIndex;
                break;
            }
        }

        if (index < 0)
        {
            throw new ArgumentException("The profile does not belong to this configuration.", nameof(profile));
        }

        return new AppConfiguration(ActiveProfileId, Profiles.SetItem(index, profile), Settings);
    }

    public AppConfiguration AddProfile(DeckProfile profile, bool makeActive = true)
    {
        Guard.NotNull(profile, nameof(profile));

        if (Profiles.Any(candidate => candidate.Id == profile.Id))
        {
            throw new ArgumentException("The profile already belongs to this configuration.", nameof(profile));
        }

        return new AppConfiguration(
            makeActive ? profile.Id : ActiveProfileId,
            Profiles.Add(profile),
            Settings);
    }

    public AppConfiguration RemoveProfile(Guid profileId)
    {
        var index = -1;
        for (var candidateIndex = 0; candidateIndex < Profiles.Length; candidateIndex++)
        {
            if (Profiles[candidateIndex].Id == profileId)
            {
                index = candidateIndex;
                break;
            }
        }
        if (index < 0)
        {
            throw new ArgumentException("The profile does not belong to this configuration.", nameof(profileId));
        }

        if (Profiles.Length == 1)
        {
            throw new InvalidOperationException("Нельзя удалить единственный профиль.");
        }

        var profiles = Profiles.RemoveAt(index);
        var activeProfileId = ActiveProfileId == profileId
            ? profiles[Math.Min(index, profiles.Length - 1)].Id
            : ActiveProfileId;
        return new AppConfiguration(activeProfileId, profiles, Settings);
    }

    public AppConfiguration WithActiveProfile(Guid profileId) =>
        new(profileId, Profiles, Settings);

    public AppConfiguration WithSettings(GlobalSettings settings) =>
        new(ActiveProfileId, Profiles, settings);
}
