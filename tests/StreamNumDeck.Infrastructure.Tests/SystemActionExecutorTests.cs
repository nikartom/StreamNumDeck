using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Infrastructure.Execution;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class SystemActionExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_MissingPathProducesUserCorrectableFailure()
    {
        var executor = new SystemActionExecutor();
        var missingPath = Path.Combine(Path.GetTempPath(), $"StreamNumDeck-{Guid.NewGuid():N}");

        var exception = await Assert.ThrowsExactlyAsync<UserActionException>(() =>
            executor.ExecuteAsync(new OpenPathActionDefinition(missingPath)));

        Assert.AreEqual(UserActionError.TargetUnavailable, exception.Error);
        Assert.AreEqual(missingPath, exception.Subject);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenShellReusesRunningApplication_Succeeds()
    {
        var startedTarget = string.Empty;
        var executor = new SystemActionExecutor(startInfo =>
        {
            startedTarget = startInfo.FileName;
            return null;
        });

        await executor.ExecuteAsync(new OpenUriActionDefinition("https://example.com/"));

        Assert.AreEqual("https://example.com/", startedTarget);
    }
}
