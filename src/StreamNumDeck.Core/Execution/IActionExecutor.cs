using StreamNumDeck.Core.Actions;

namespace StreamNumDeck.Core.Execution;

public interface IActionExecutor
{
    bool CanExecute(ActionDefinition action);

    Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default);
}

public sealed class ActionExecutionFailedEventArgs(
    ActionDefinition action,
    Exception exception) : EventArgs
{
    public ActionDefinition Action { get; } = action;

    public Exception Exception { get; } = exception;
}
