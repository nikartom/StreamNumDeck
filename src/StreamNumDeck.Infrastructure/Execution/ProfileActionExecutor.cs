using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Infrastructure.Execution;

public sealed class ProfileActionExecutor(ConfigurationService configurationService) : IActionExecutor
{
    public bool CanExecute(ActionDefinition action) => action is SwitchProfileActionDefinition;

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        if (action is not SwitchProfileActionDefinition switchAction)
        {
            throw new ArgumentException("The action is not a profile switch.", nameof(action));
        }

        var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (ResolveTarget(configuration, switchAction) == configuration.ActiveProfileId)
        {
            return;
        }

        await configurationService
            .UpdateAsync(current => current.WithActiveProfile(ResolveTarget(current, switchAction)), cancellationToken)
            .ConfigureAwait(false);
    }

    private static Guid ResolveTarget(AppConfiguration configuration, SwitchProfileActionDefinition action) =>
        configuration.ResolveProfileSwitchTarget(action)
        ?? throw new UserActionException(UserActionError.ProfileUnavailable, action.ProfileId?.ToString());
}
