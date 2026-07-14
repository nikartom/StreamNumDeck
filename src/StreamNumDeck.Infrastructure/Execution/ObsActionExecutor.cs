using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Obs;
using StreamNumDeck.Infrastructure.Obs;

namespace StreamNumDeck.Infrastructure.Execution;

public sealed class ObsActionExecutor(
    ConfigurationService configurationService,
    IObsController obsController) : IActionExecutor
{
    public bool CanExecute(ActionDefinition action) => action is ObsActionDefinition;

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        if (action is not ObsActionDefinition obsAction)
        {
            throw new ArgumentException("The action is not an OBS action.", nameof(action));
        }

        if (obsController.State is not ObsConnectionState.Connected)
        {
            try
            {
                var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(false);
                await obsController.ConnectAsync(configuration.Settings.Obs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new UserActionException(UserActionError.ObsUnavailable, innerException: exception);
            }
        }

        try
        {
            await obsController.ExecuteAsync(obsAction, cancellationToken).ConfigureAwait(false);
        }
        catch (ObsRequestException exception)
        {
            throw new UserActionException(
                UserActionError.ObsActionRejected,
                obsAction.TargetName,
                exception);
        }
    }
}
