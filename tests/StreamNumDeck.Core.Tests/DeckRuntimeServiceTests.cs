using System.Threading.Channels;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Core.Input;

namespace StreamNumDeck.Core.Tests;

[TestClass]
public sealed class DeckRuntimeServiceTests
{
    [TestMethod]
    public async Task DifferentKeys_CanExecuteWhileAutomationIsWaiting()
    {
        var configuration = AppConfiguration.CreateDefault();
        var profile = configuration.ActiveProfile
            .WithAssignment(
                NumLockLayer.Off,
                DeckKey.Numpad1,
                new KeyAssignment("First", IconReference.BuiltIn("play"), new OpenPathActionDefinition("first")))
            .WithAssignment(
                NumLockLayer.Off,
                DeckKey.Numpad2,
                new KeyAssignment("Second", IconReference.BuiltIn("play"), new OpenPathActionDefinition("second")));
        using var configurationService = new ConfigurationService(
            new InMemoryConfigurationStore(configuration.ReplaceProfile(profile)));
        var capture = new TestKeyboardCaptureService();
        var executor = new BlockingExecutor();
        var runtime = new DeckRuntimeService(
            configurationService,
            capture,
            new ActionDispatcher(new[] { executor }));

        try
        {
            await runtime.StartAsync();
            capture.Publish(DeckKey.Numpad1);
            await WaitWithTimeoutAsync(executor.FirstStarted.Task);

            capture.Publish(DeckKey.Numpad2);
            await WaitWithTimeoutAsync(executor.SecondStarted.Task);

            Assert.IsFalse(executor.ReleaseFirst.Task.IsCompleted);
        }
        finally
        {
            executor.ReleaseFirst.TrySetResult(true);
            await runtime.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task SameKey_QueuesRepeatedExecutionsWithoutOverlap()
    {
        var configuration = AppConfiguration.CreateDefault();
        var profile = configuration.ActiveProfile.WithAssignment(
            NumLockLayer.Off,
            DeckKey.Numpad1,
            new KeyAssignment("First", IconReference.BuiltIn("play"), new OpenPathActionDefinition("first")));
        using var configurationService = new ConfigurationService(
            new InMemoryConfigurationStore(configuration.ReplaceProfile(profile)));
        var capture = new TestKeyboardCaptureService();
        var executor = new BlockingExecutor();
        var runtime = new DeckRuntimeService(
            configurationService,
            capture,
            new ActionDispatcher(new[] { executor }));

        try
        {
            await runtime.StartAsync();
            capture.Publish(DeckKey.Numpad1);
            await WaitWithTimeoutAsync(executor.FirstStarted.Task);
            capture.Publish(DeckKey.Numpad1);

            await Task.Delay(50);
            Assert.AreEqual(1, executor.StartCount);

            executor.ReleaseFirst.TrySetResult(true);
            await WaitUntilAsync(() => executor.StartCount == 2);
            Assert.AreEqual(1, executor.MaximumConcurrency);
        }
        finally
        {
            executor.ReleaseFirst.TrySetResult(true);
            await runtime.DisposeAsync();
        }
    }

    private static async Task WaitWithTimeoutAsync(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(3))) != task)
        {
            Assert.Fail("The expected runtime operation did not complete in time.");
        }

        await task;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(condition(), "The expected runtime state was not reached in time.");
    }

    private sealed class BlockingExecutor : IActionExecutor
    {
        private int concurrency;
        private int maximumConcurrency;
        private int startCount;

        public TaskCompletionSource<bool> FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int StartCount => Volatile.Read(ref startCount);

        public int MaximumConcurrency => Volatile.Read(ref maximumConcurrency);

        public bool CanExecute(ActionDefinition action) => action is OpenPathActionDefinition;

        public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
        {
            var currentConcurrency = Interlocked.Increment(ref concurrency);
            UpdateMaximumConcurrency(currentConcurrency);
            var currentStart = Interlocked.Increment(ref startCount);

            try
            {
                if (currentStart == 1)
                {
                    FirstStarted.TrySetResult(true);
                    await ReleaseFirst.Task.WaitAsync(cancellationToken);
                }
                else
                {
                    SecondStarted.TrySetResult(true);
                }
            }
            finally
            {
                Interlocked.Decrement(ref concurrency);
            }
        }

        private void UpdateMaximumConcurrency(int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref maximumConcurrency);
                if (value <= current || Interlocked.CompareExchange(ref maximumConcurrency, value, current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class TestKeyboardCaptureService : IKeyboardCaptureService
    {
        private readonly Channel<CapturedKeyPress> channel = Channel.CreateUnbounded<CapturedKeyPress>();

        public KeyboardCaptureState State { get; private set; }

        public bool IsNumLockOn => false;

        public KeyboardCaptureTargets CaptureTargets { get; private set; } = KeyboardCaptureTargets.All;

        public event EventHandler<KeyboardCaptureStateChangedEventArgs>? StateChanged;

        public event EventHandler<NumLockStateChangedEventArgs>? NumLockStateChanged;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            State = KeyboardCaptureState.Running;
            StateChanged?.Invoke(this, new KeyboardCaptureStateChangedEventArgs(State));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            State = KeyboardCaptureState.Stopped;
            StateChanged?.Invoke(this, new KeyboardCaptureStateChangedEventArgs(State));
            return Task.CompletedTask;
        }

        public Task SetCaptureTargetsAsync(
            KeyboardCaptureTargets targets,
            CancellationToken cancellationToken = default)
        {
            CaptureTargets = targets;
            return Task.CompletedTask;
        }

        public Task SetNumLockAsync(bool isOn, CancellationToken cancellationToken = default)
        {
            NumLockStateChanged?.Invoke(this, new NumLockStateChangedEventArgs(isOn));
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<CapturedKeyPress> ReadAllAsync(CancellationToken cancellationToken = default) =>
            channel.Reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(DeckKey key) => channel.Writer.TryWrite(
            new CapturedKeyPress(key, NumLockLayer.Off, DateTimeOffset.UtcNow));
    }

    private sealed class InMemoryConfigurationStore(AppConfiguration configuration) : IConfigurationStore
    {
        public Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(configuration);

        public Task SaveAsync(AppConfiguration updatedConfiguration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
