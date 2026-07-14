using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Infrastructure.Execution;

public sealed class AudioActionExecutor(
    IAudioPlaybackService audioPlaybackService,
    ConfigurationService configurationService) : IActionExecutor
{
    public bool CanExecute(ActionDefinition action) => action is PlaySoundActionDefinition;

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        if (action is not PlaySoundActionDefinition sound)
        {
            throw new NotSupportedException($"{action.GetType().Name} is not a sound action.");
        }

        var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(false);
        await audioPlaybackService
            .PlayAsync(sound, configuration.Settings, cancellationToken)
            .ConfigureAwait(false);
    }
}
