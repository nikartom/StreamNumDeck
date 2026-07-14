using System.Collections.Immutable;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Infrastructure.Execution;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class KeyboardMacroActionExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_InjectsCompleteHarmlessChordSequence()
    {
        var executor = new KeyboardMacroActionExecutor();
        var action = new KeyboardMacroActionDefinition(
            ImmutableArray.Create(
                KeyboardMacroStep.KeyPress(MacroKey.F24, MacroModifiers.Control | MacroModifiers.Shift),
                KeyboardMacroStep.Delay(1),
                KeyboardMacroStep.KeyPress(MacroKey.F24)));

        Assert.IsTrue(executor.CanExecute(action));
        await executor.ExecuteAsync(action, TestContext.CancellationToken);
    }

    public TestContext TestContext { get; set; }
}
