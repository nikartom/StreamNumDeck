using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.VTubeStudio;
using StreamNumDeck.Infrastructure.VTubeStudio;

namespace StreamNumDeck.Infrastructure.Execution;

public sealed class VTubeStudioActionExecutor(
    ConfigurationService configurationService,
    IVTubeStudioController controller) : IActionExecutor
{
    public bool CanExecute(ActionDefinition action) => action is VTubeStudioActionDefinition;

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        if (action is not VTubeStudioActionDefinition vTubeStudioAction)
        {
            throw new ArgumentException("The action is not a VTube Studio action.", nameof(action));
        }

        try
        {
            var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(false);
            await controller.ExecuteAsync(
                configuration.Settings.VTubeStudio,
                vTubeStudioAction,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (VTubeStudioRequestException exception)
        {
            throw new UserActionException(
                UserActionError.VTubeStudioActionRejected,
                vTubeStudioAction.TargetName ?? vTubeStudioAction.TargetId,
                exception);
        }
        catch (Exception exception)
        {
            throw new UserActionException(
                UserActionError.VTubeStudioUnavailable,
                innerException: exception);
        }
    }
}
