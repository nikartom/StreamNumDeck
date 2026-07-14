using System.Collections.Immutable;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Core.Tests;

[TestClass]
public sealed class ActionDispatcherTests
{
    [TestMethod]
    public async Task ExecuteAsync_AutomationRunsActionsInOrderAndStopsAtFinish()
    {
        var calls = new List<string>();
        var dispatcher = new ActionDispatcher(new[] { new RecordingExecutor(calls) });
        var automation = new AutomationActionDefinition(
            ImmutableArray.Create<AutomationStepDefinition>(
                new ExecuteAutomationStepDefinition(new OpenPathActionDefinition("first")),
                new PauseAutomationStepDefinition(100),
                new ExecuteAutomationStepDefinition(new OpenPathActionDefinition("second")),
                new FinishAutomationStepDefinition(),
                new ExecuteAutomationStepDefinition(new OpenPathActionDefinition("never"))));

        await dispatcher.ExecuteAsync(automation);

        CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_PauseHonorsCancellation()
    {
        var dispatcher = new ActionDispatcher(Array.Empty<IActionExecutor>());
        var automation = new AutomationActionDefinition(
            ImmutableArray.Create<AutomationStepDefinition>(
                new PauseAutomationStepDefinition(10_000)));
        using var cancellation = new CancellationTokenSource(20);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            dispatcher.ExecuteAsync(automation, cancellation.Token));
    }

    [TestMethod]
    public void Automation_RejectsEmptyNestedAndOversizedDefinitions()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new AutomationActionDefinition(ImmutableArray<AutomationStepDefinition>.Empty));

        var valid = new AutomationActionDefinition(
            ImmutableArray.Create<AutomationStepDefinition>(new FinishAutomationStepDefinition()));
        Assert.ThrowsExactly<ArgumentException>(() => new ExecuteAutomationStepDefinition(valid));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PauseAutomationStepDefinition(99));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new PauseAutomationStepDefinition(PauseAutomationStepDefinition.MaximumMilliseconds + 1));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new AutomationActionDefinition(
                Enumerable.Range(0, AutomationActionDefinition.MaximumStepCount + 1)
                    .Select(_ => (AutomationStepDefinition)new FinishAutomationStepDefinition())
                    .ToImmutableArray()));
    }

    [TestMethod]
    public void EnumerateExecutableActions_StopsAtFinishMarker()
    {
        var automation = new AutomationActionDefinition(
            ImmutableArray.Create<AutomationStepDefinition>(
                new ExecuteAutomationStepDefinition(new OpenPathActionDefinition("before")),
                new FinishAutomationStepDefinition(),
                new ExecuteAutomationStepDefinition(new OpenPathActionDefinition("after"))));

        var actions = automation.EnumerateExecutableActions().OfType<OpenPathActionDefinition>().ToArray();

        Assert.HasCount(1, actions);
        Assert.AreEqual("before", actions[0].Path);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenStepFails_StopsBeforeFollowingActions()
    {
        var calls = new List<string>();
        var dispatcher = new ActionDispatcher(new[] { new FailingExecutor(calls) });
        var automation = new AutomationActionDefinition(
            ImmutableArray.Create<AutomationStepDefinition>(
                new ExecuteAutomationStepDefinition(new OpenPathActionDefinition("failure")),
                new ExecuteAutomationStepDefinition(new OpenPathActionDefinition("never"))));

        await Assert.ThrowsAsync<Exception>(() => dispatcher.ExecuteAsync(automation));

        CollectionAssert.AreEqual(new[] { "failure" }, calls);
    }

    private sealed class RecordingExecutor(List<string> calls) : IActionExecutor
    {
        public bool CanExecute(ActionDefinition action) => action is OpenPathActionDefinition;

        public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
        {
            calls.Add(((OpenPathActionDefinition)action).Path);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingExecutor(List<string> calls) : IActionExecutor
    {
        public bool CanExecute(ActionDefinition action) => action is OpenPathActionDefinition;

        public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
        {
            var path = ((OpenPathActionDefinition)action).Path;
            calls.Add(path);
            if (path == "failure")
            {
                throw new InvalidOperationException("Expected test failure.");
            }

            return Task.CompletedTask;
        }
    }
}
