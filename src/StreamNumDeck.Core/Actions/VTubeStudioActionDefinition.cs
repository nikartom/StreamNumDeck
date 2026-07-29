namespace StreamNumDeck.Core.Actions;

public enum VTubeStudioActionKind
{
    TriggerHotkey,
    LoadModel,
}

public sealed record VTubeStudioActionDefinition : ActionDefinition
{
    public VTubeStudioActionDefinition(
        VTubeStudioActionKind action,
        string targetId,
        string? targetName = null)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("A VTube Studio target is required.", nameof(targetId));
        }

        if (targetId.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(targetId), "VTube Studio target identifiers cannot exceed 128 characters.");
        }

        Action = action;
        TargetId = targetId.Trim();
        TargetName = string.IsNullOrWhiteSpace(targetName) ? null : targetName!.Trim();
    }

    public override ActionGroup Group => ActionGroup.VTubeStudio;

    public VTubeStudioActionKind Action { get; }

    public string TargetId { get; }

    public string? TargetName { get; }
}
