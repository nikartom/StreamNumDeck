using StreamNumDeck.Core.Audio;

namespace StreamNumDeck.App.Overlays;

public sealed class MicrophoneMuteOverlayController(
    ISystemAudioControlService audioControlService,
    OverlayPositionStore positionStore) : IAsyncDisposable
{
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private MicrophoneMuteOverlayWindow? overlayWindow;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? timer;
    private bool refreshInProgress;
    private bool started;
    private DateTimeOffset previewUntil;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (started)
        {
            return;
        }

        overlayWindow = new MicrophoneMuteOverlayWindow(positionStore);
        timer = global::StreamNumDeck.App.App.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.IsRepeating = true;
        timer.Tick += Timer_Tick;

        await RefreshAsync(cancellationToken);
        timer.Start();
        started = true;
    }

    public async Task ShowPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!started)
        {
            await StartAsync(cancellationToken);
        }

        if (overlayWindow is null)
        {
            return;
        }

        previewUntil = DateTimeOffset.UtcNow.AddSeconds(10);
        overlayWindow.SetVisible(true);
    }

    public ValueTask DisposeAsync()
    {
        lifetimeCancellation.Cancel();
        if (timer is not null)
        {
            timer.Stop();
            timer.Tick -= Timer_Tick;
        }

        overlayWindow?.SetVisible(false);
        overlayWindow?.Close();
        lifetimeCancellation.Dispose();
        return ValueTask.CompletedTask;
    }

    private async void Timer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object args)
    {
        try
        {
            await RefreshAsync(lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch
        {
            overlayWindow?.SetVisible(false);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (refreshInProgress)
        {
            return;
        }

        refreshInProgress = true;
        try
        {
            if (DateTimeOffset.UtcNow < previewUntil)
            {
                overlayWindow?.SetVisible(true);
                return;
            }

            var muted = await audioControlService.GetDefaultMicrophoneMuteAsync(cancellationToken);
            overlayWindow?.SetVisible(muted);
        }
        finally
        {
            refreshInProgress = false;
        }
    }
}
