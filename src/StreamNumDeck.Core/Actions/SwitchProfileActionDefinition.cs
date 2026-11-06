namespace StreamNumDeck.Core.Actions;

public enum ProfileSwitchMode
{
    Next,
    Specific,
}

public sealed record SwitchProfileActionDefinition : ActionDefinition
{
    public SwitchProfileActionDefinition(ProfileSwitchMode mode, Guid? profileId = null)
    {
        switch (mode)
        {
            case ProfileSwitchMode.Next when profileId is not null:
                throw new ArgumentException(
                    "Switching to the next profile does not use a target profile.",
                    nameof(profileId));

            case ProfileSwitchMode.Specific when profileId is null || profileId == Guid.Empty:
                throw new ArgumentException("A target profile is required.", nameof(profileId));

            case ProfileSwitchMode.Next or ProfileSwitchMode.Specific:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        Mode = mode;
        ProfileId = profileId;
    }

    public override ActionGroup Group => ActionGroup.Profile;

    public ProfileSwitchMode Mode { get; }

    public Guid? ProfileId { get; }
}
