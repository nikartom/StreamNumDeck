namespace StreamNumDeck.Core.Actions;

public static class ActionDefinitionExtensions
{
    public static IEnumerable<ActionDefinition> EnumerateExecutableActions(this ActionDefinition action)
    {
        Guard.NotNull(action, nameof(action));

        if (action is not AutomationActionDefinition automation)
        {
            yield return action;
            yield break;
        }

        foreach (var step in automation.Steps)
        {
            if (step is FinishAutomationStepDefinition)
            {
                yield break;
            }

            if (step is ExecuteAutomationStepDefinition actionStep)
            {
                yield return actionStep.Action;
            }
        }
    }
}
