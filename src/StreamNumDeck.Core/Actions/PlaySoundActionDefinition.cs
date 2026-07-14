namespace StreamNumDeck.Core.Actions;

public enum SoundPlaybackBehavior
{
    PlayAlongside,
    RestartSameSound,
    StopOthers,
}

public sealed record PlaySoundActionDefinition : ActionDefinition
{
    public PlaySoundActionDefinition(
        string filePath,
        int volume,
        SoundPlaybackBehavior playbackBehavior = SoundPlaybackBehavior.RestartSameSound)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("An audio file path is required.", nameof(filePath));
        }

        if (volume is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 100.");
        }

        FilePath = filePath.Trim();
        Volume = volume;
        PlaybackBehavior = playbackBehavior;
    }

    public override ActionGroup Group => ActionGroup.Sound;

    public string FilePath { get; }

    public int Volume { get; }

    public SoundPlaybackBehavior PlaybackBehavior { get; }
}
