using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Input;

namespace StreamNumDeck.Core.Execution;

public sealed class DeckRuntimeService(
    ConfigurationService configurationService,
    IKeyboardCaptureService keyboardCapture,
    IEnumerable<IActionExecutor> actionExecutors) : IAsyncDisposable
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly IReadOnlyList<IActionExecutor> executors = actionExecutors.ToArray();
    private CancellationTokenSource? processingCancellation;
    private Task? processingTask;
    private bool disposed;

    public KeyboardCaptureState CaptureState => keyboardCapture.State;

    public bool IsNumLockOn => keyboardCapture.IsNumLockOn;

    public KeyboardCaptureTargets CaptureTargets => keyboardCapture.CaptureTargets;

    public event EventHandler<KeyboardCaptureStateChangedEventArgs>? CaptureStateChanged
    {
        add => keyboardCapture.StateChanged += value;
        remove => keyboardCapture.StateChanged -= value;
    }

    public event EventHandler<NumLockStateChangedEventArgs>? NumLockStateChanged
    {
        add => keyboardCapture.NumLockStateChanged += value;
        remove => keyboardCapture.NumLockStateChanged -= value;
    }

    public event EventHandler<ActionExecutionFailedEventArgs>? ActionExecutionFailed;

    public Task SetNumLockAsync(bool isOn, CancellationToken cancellationToken = default) =>
        keyboardCapture.SetNumLockAsync(isOn, cancellationToken);

    public Task SetCaptureTargetsAsync(
        KeyboardCaptureTargets targets,
        CancellationToken cancellationToken = default) =>
        keyboardCapture.SetCaptureTargetsAsync(targets, cancellationToken);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (processingTask is not null)
            {
                return;
            }

            await keyboardCapture.StartAsync(cancellationToken).ConfigureAwait(false);
            processingCancellation = new CancellationTokenSource();
            processingTask = ProcessInputAsync(processingCancellation.Token);
        }
        catch
        {
            await keyboardCapture.StopAsync(CancellationToken.None).ConfigureAwait(false);
            processingCancellation?.Dispose();
            processingCancellation = null;
            processingTask = null;
            throw;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (processingTask is null)
            {
                return;
            }

            processingCancellation!.Cancel();
            await keyboardCapture.StopAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await processingTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (processingCancellation.IsCancellationRequested)
            {
            }

            processingCancellation.Dispose();
            processingCancellation = null;
            processingTask = null;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        await keyboardCapture.DisposeAsync().ConfigureAwait(false);
        lifecycleGate.Dispose();
        disposed = true;
    }

    private async Task ProcessInputAsync(CancellationToken cancellationToken)
    {
        await foreach (var keyPress in keyboardCapture.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            ActionDefinition action;
            try
            {
                var configuration = await configurationService.GetAsync(cancellationToken).ConfigureAwait(false);
                var profile = configuration.Profiles.Single(profile => profile.Id == configuration.ActiveProfileId);
                action = profile.GetLayer(keyPress.Layer).GetAssignment(keyPress.Key).Action;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                ActionExecutionFailed?.Invoke(this, new ActionExecutionFailedEventArgs(new NoActionDefinition(), exception));
                continue;
            }

            if (action is NoActionDefinition)
            {
                continue;
            }

            var executor = executors.FirstOrDefault(candidate => candidate.CanExecute(action));
            if (executor is null)
            {
                ActionExecutionFailed?.Invoke(
                    this,
                    new ActionExecutionFailedEventArgs(
                        action,
                        new NotSupportedException($"No executor is registered for {action.GetType().Name}.")));
                continue;
            }

            try
            {
                await executor.ExecuteAsync(action, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                ActionExecutionFailed?.Invoke(this, new ActionExecutionFailedEventArgs(action, exception));
            }
        }
    }
}
