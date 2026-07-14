namespace StreamNumDeck.Wpf.Services;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\StreamNumDeck.Application";
    private const string ActivationEventName = @"Local\StreamNumDeck.Activate";
    private readonly Mutex instanceMutex;
    private readonly EventWaitHandle? activationEvent;
    private RegisteredWaitHandle? activationRegistration;
    private bool disposed;

    private SingleInstanceCoordinator(
        Mutex instanceMutex,
        EventWaitHandle? activationEvent,
        bool isPrimary)
    {
        this.instanceMutex = instanceMutex;
        this.activationEvent = activationEvent;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static SingleInstanceCoordinator Create()
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        try
        {
            var activationEvent = createdNew
                ? new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName)
                : null;
            return new SingleInstanceCoordinator(mutex, activationEvent, createdNew);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public void StartListening(Action activate)
    {
        if (!IsPrimary || activationEvent is null)
        {
            throw new InvalidOperationException("Only the primary application instance can listen for activation.");
        }

        if (activationRegistration is not null)
        {
            return;
        }

        activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            static (state, timedOut) =>
            {
                if (timedOut)
                {
                    return;
                }

                try
                {
                    ((Action)state!).Invoke();
                }
                catch (Exception exception)
                {
                    AppLogger.Error("Activate primary application instance", exception);
                }
            },
            activate,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public bool SignalPrimaryInstance()
    {
        if (IsPrimary)
        {
            return false;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(ActivationEventName);
                return existingEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        activationRegistration?.Unregister(null);
        activationRegistration = null;
        activationEvent?.Dispose();
        instanceMutex.Dispose();
        disposed = true;
    }
}
