using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Infrastructure.Execution;

public sealed class SystemAudioActionExecutor(
    ISystemAudioControlService audioControlService) : IActionExecutor
{
    public bool CanExecute(ActionDefinition action) => action is
        ToggleMicrophoneMuteActionDefinition or
        ToggleMasterOutputMuteActionDefinition or
        AdjustMasterVolumeActionDefinition or
        AdjustApplicationVolumeActionDefinition;

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default) => action switch
    {
        ToggleMicrophoneMuteActionDefinition =>
            audioControlService.ToggleDefaultMicrophoneMuteAsync(cancellationToken),
        ToggleMasterOutputMuteActionDefinition =>
            audioControlService.ToggleMasterOutputMuteAsync(cancellationToken),
        AdjustMasterVolumeActionDefinition master =>
            audioControlService.AdjustMasterOutputVolumeAsync(
                master.Direction,
                master.StepPercent,
                cancellationToken),
        AdjustApplicationVolumeActionDefinition application =>
            audioControlService.AdjustApplicationVolumeAsync(
                application.ApplicationId,
                application.Direction,
                application.StepPercent,
                cancellationToken),
        _ => throw new NotSupportedException($"{action.GetType().Name} is not a system audio action."),
    };
}
