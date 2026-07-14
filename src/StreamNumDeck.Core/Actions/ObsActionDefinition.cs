namespace StreamNumDeck.Core.Actions;

public enum ObsActionKind
{
    SwitchScene,
    ToggleSourceVisibility,
    ToggleInputMute,
    StartStreaming,
    StopStreaming,
    StartRecording,
    StopRecording,
    SaveReplayBuffer,
    RestartMediaSource,
}

public sealed record ObsActionDefinition : ActionDefinition
{
    private static readonly HashSet<ObsActionKind> TargetedActions =
    [
        ObsActionKind.SwitchScene,
        ObsActionKind.ToggleSourceVisibility,
        ObsActionKind.ToggleInputMute,
        ObsActionKind.RestartMediaSource,
    ];

    public ObsActionDefinition(ObsActionKind action, string? targetName = null)
    {
        if (TargetedActions.Contains(action) && string.IsNullOrWhiteSpace(targetName))
        {
            throw new ArgumentException($"OBS action {action} requires a target name.", nameof(targetName));
        }

        Action = action;
        TargetName = string.IsNullOrWhiteSpace(targetName) ? null : targetName!.Trim();
    }

    public override ActionGroup Group => ActionGroup.Obs;

    public ObsActionKind Action { get; }

    public string? TargetName { get; }
}
