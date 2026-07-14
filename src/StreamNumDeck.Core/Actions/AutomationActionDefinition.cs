using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace StreamNumDeck.Core.Actions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ExecuteAutomationStepDefinition), "action")]
[JsonDerivedType(typeof(PauseAutomationStepDefinition), "pause")]
[JsonDerivedType(typeof(FinishAutomationStepDefinition), "finish")]
public abstract record AutomationStepDefinition;

public sealed record ExecuteAutomationStepDefinition : AutomationStepDefinition
{
    public ExecuteAutomationStepDefinition(ActionDefinition action)
    {
        Guard.NotNull(action, nameof(action));

        if (action is NoActionDefinition or AutomationActionDefinition)
        {
            throw new ArgumentException(
                "An automation step must contain a configured non-automation action.",
                nameof(action));
        }

        Action = action;
    }

    public ActionDefinition Action { get; }
}

public sealed record PauseAutomationStepDefinition : AutomationStepDefinition
{
    public const int MinimumMilliseconds = 100;
    public const int MaximumMilliseconds = 60 * 60 * 1000;

    public PauseAutomationStepDefinition(int durationMilliseconds)
    {
        if (durationMilliseconds is < MinimumMilliseconds or > MaximumMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMilliseconds),
                $"A pause must be between {MinimumMilliseconds} and {MaximumMilliseconds} milliseconds.");
        }

        DurationMilliseconds = durationMilliseconds;
    }

    public int DurationMilliseconds { get; }
}

public sealed record FinishAutomationStepDefinition : AutomationStepDefinition;

public sealed record AutomationActionDefinition : ActionDefinition
{
    public const int MaximumStepCount = 100;

    public AutomationActionDefinition(ImmutableArray<AutomationStepDefinition> steps)
    {
        if (steps.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An automation must contain at least one step.", nameof(steps));
        }

        if (steps.Length > MaximumStepCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(steps),
                $"An automation cannot contain more than {MaximumStepCount} steps.");
        }

        if (steps.Any(static step => step is null))
        {
            throw new ArgumentException("Automation steps cannot contain null values.", nameof(steps));
        }

        Steps = steps;
    }

    public override ActionGroup Group => ActionGroup.Automation;

    public ImmutableArray<AutomationStepDefinition> Steps { get; }
}
