namespace StreamNumDeck.Core.Actions;

public enum VolumeAdjustmentDirection
{
    Increase,
    Decrease,
}

public sealed record ToggleMicrophoneMuteActionDefinition : ActionDefinition
{
    public override ActionGroup Group => ActionGroup.Sound;
}

public sealed record ToggleMasterOutputMuteActionDefinition : ActionDefinition
{
    public override ActionGroup Group => ActionGroup.Sound;
}

public sealed record AdjustMasterVolumeActionDefinition : ActionDefinition
{
    public AdjustMasterVolumeActionDefinition(VolumeAdjustmentDirection direction, int stepPercent)
    {
        ValidateStep(stepPercent);
        Direction = direction;
        StepPercent = stepPercent;
    }

    public override ActionGroup Group => ActionGroup.Sound;

    public VolumeAdjustmentDirection Direction { get; }

    public int StepPercent { get; }

    private static void ValidateStep(int value)
    {
        if (value is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The volume step must be between 1 and 100 percent.");
        }
    }
}

public sealed record AdjustApplicationVolumeActionDefinition : ActionDefinition
{
    public AdjustApplicationVolumeActionDefinition(
        string applicationId,
        VolumeAdjustmentDirection direction,
        int stepPercent)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new ArgumentException("An application identifier is required.", nameof(applicationId));
        }

        if (stepPercent is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(stepPercent), "The volume step must be between 1 and 100 percent.");
        }

        ApplicationId = NormalizeApplicationId(applicationId);
        Direction = direction;
        StepPercent = stepPercent;
    }

    public override ActionGroup Group => ActionGroup.Sound;

    public string ApplicationId { get; }

    public VolumeAdjustmentDirection Direction { get; }

    public int StepPercent { get; }

    public static string NormalizeApplicationId(string value)
    {
        var trimmed = value.Trim();
        var fileName = Path.GetFileName(trimmed);
        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? fileName.ToLowerInvariant()
            : $"{fileName}.exe".ToLowerInvariant();
    }
}
