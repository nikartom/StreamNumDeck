using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Input;

namespace StreamNumDeck.Core.Execution;

public sealed class DeckRuntimeService(
    ConfigurationService configurationService,
    IKeyboardCaptureService keyboardCapture,
    ActionDispatcher actionDispatcher) : IAsyncDisposable
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly IReadOnlyDictionary<DeckKey, SemaphoreSlim> actionGates =
        DeckKeyCatalog.AssignableKeys.ToDictionary(static key => key, static _ => new SemaphoreSlim(1, 1));
    private readonly object runningActionsGate = new();
    private readonly HashSet<Task> runningActions = new();
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
        foreach (var gate in actionGates.Values)
        {
            gate.Dispose();
        }

        lifecycleGate.Dispose();
        disposed = true;
    }

    private async Task ProcessInputAsync(CancellationToken cancellationToken)
    {
        try
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

                TrackAction(ExecuteActionAsync(keyPress.Key, action, cancellationToken));
            }
        }
        finally
        {
            Task[] pendingActions;
            lock (runningActionsGate)
            {
                pendingActions = runningActions.ToArray();
            }

            await Task.WhenAll(pendingActions).ConfigureAwait(false);
        }
    }

    private async Task ExecuteActionAsync(
        DeckKey key,
        ActionDefinition action,
        CancellationToken cancellationToken)
    {
        var gate = actionGates[key];
        var gateEntered = false;
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            gateEntered = true;
            await actionDispatcher.ExecuteAsync(action, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (AutomationStepExecutionException exception)
        {
            ActionExecutionFailed?.Invoke(
                this,
                new ActionExecutionFailedEventArgs(
                    exception.Action,
                    exception.InnerException ?? exception));
        }
        catch (Exception exception)
        {
            ActionExecutionFailed?.Invoke(this, new ActionExecutionFailedEventArgs(action, exception));
        }
        finally
        {
            if (gateEntered)
            {
                gate.Release();
            }
        }
    }

    private void TrackAction(Task actionTask)
    {
        lock (runningActionsGate)
        {
            runningActions.Add(actionTask);
        }

        _ = actionTask.ContinueWith(
            completedTask =>
            {
                lock (runningActionsGate)
                {
                    runningActions.Remove(completedTask);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
