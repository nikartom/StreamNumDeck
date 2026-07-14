using System.Text.Json.Serialization;

namespace StreamNumDeck.Core.Actions;

public enum ActionGroup
{
    None,
    Sound,
    Obs,
    System,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoActionDefinition), "none")]
[JsonDerivedType(typeof(PlaySoundActionDefinition), "playSound")]
[JsonDerivedType(typeof(ToggleMicrophoneMuteActionDefinition), "toggleMicrophoneMute")]
[JsonDerivedType(typeof(ToggleMasterOutputMuteActionDefinition), "toggleMasterOutputMute")]
[JsonDerivedType(typeof(AdjustMasterVolumeActionDefinition), "adjustMasterVolume")]
[JsonDerivedType(typeof(AdjustApplicationVolumeActionDefinition), "adjustApplicationVolume")]
[JsonDerivedType(typeof(LaunchProcessActionDefinition), "launchProcess")]
[JsonDerivedType(typeof(OpenPathActionDefinition), "openPath")]
[JsonDerivedType(typeof(OpenUriActionDefinition), "openUri")]
[JsonDerivedType(typeof(KeyboardMacroActionDefinition), "keyboardMacro")]
[JsonDerivedType(typeof(ObsActionDefinition), "obs")]
public abstract record ActionDefinition
{
    [JsonIgnore]
    public abstract ActionGroup Group { get; }
}

public sealed record NoActionDefinition : ActionDefinition
{
    public override ActionGroup Group => ActionGroup.None;
}
