using StreamNumDeck.Core.Actions;

namespace StreamNumDeck.Core.Execution;

public sealed class ActionDispatcher(IEnumerable<IActionExecutor> actionExecutors)
{
    private readonly IReadOnlyList<IActionExecutor> executors = actionExecutors.ToArray();

    public async Task ExecuteAsync(
        ActionDefinition action,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(action, nameof(action));

        if (action is NoActionDefinition)
        {
            return;
        }

        if (action is AutomationActionDefinition automation)
        {
            await ExecuteAutomationAsync(automation, cancellationToken).ConfigureAwait(false);
            return;
        }

        var executor = executors.FirstOrDefault(candidate => candidate.CanExecute(action));
        if (executor is null)
        {
            throw new NotSupportedException($"No executor is registered for {action.GetType().Name}.");
        }

        await executor.ExecuteAsync(action, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAutomationAsync(
        AutomationActionDefinition automation,
        CancellationToken cancellationToken)
    {
        foreach (var step in automation.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (step)
            {
                case ExecuteAutomationStepDefinition actionStep:
                    try
                    {
                        await ExecuteAsync(actionStep.Action, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw new AutomationStepExecutionException(actionStep.Action, exception);
                    }

                    break;

                case PauseAutomationStepDefinition pauseStep:
                    await Task.Delay(pauseStep.DurationMilliseconds, cancellationToken).ConfigureAwait(false);
                    break;

                case FinishAutomationStepDefinition:
                    return;

                default:
                    throw new NotSupportedException($"Unsupported automation step {step.GetType().Name}.");
            }
        }
    }
}

internal sealed class AutomationStepExecutionException(
    ActionDefinition action,
    Exception innerException) : Exception("An automation step failed.", innerException)
{
    public ActionDefinition Action { get; } = action;
}
