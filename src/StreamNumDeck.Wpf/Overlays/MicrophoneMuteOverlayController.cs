using System.Windows.Threading;
using StreamNumDeck.Core.Audio;

namespace StreamNumDeck.Wpf.Overlays;

public sealed class MicrophoneMuteOverlayController : IAsyncDisposable
{
    private readonly ISystemAudioControlService audioControlService;
    private readonly OverlayPositionStore positionStore;
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(250),
    };
    private MicrophoneMuteOverlayWindow? window;
    private bool refreshInProgress;
    private DateTimeOffset previewUntil;

    public MicrophoneMuteOverlayController(
        ISystemAudioControlService audioControlService,
        OverlayPositionStore positionStore)
    {
        this.audioControlService = audioControlService;
        this.positionStore = positionStore;
        timer.Tick += Timer_Tick;
    }

    public async Task StartAsync()
    {
        window ??= new MicrophoneMuteOverlayWindow(positionStore);
        await RefreshAsync();
        timer.Start();
    }

    public async Task ShowPreviewAsync()
    {
        if (window is null)
        {
            await StartAsync();
        }

        previewUntil = DateTimeOffset.UtcNow.AddSeconds(10);
        window?.Show();
    }

    public ValueTask DisposeAsync()
    {
        timer.Stop();
        timer.Tick -= Timer_Tick;
        window?.Close();
        window = null;
        return default;
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await RefreshAsync();
        }
        catch
        {
            window?.Hide();
        }
    }

    private async Task RefreshAsync()
    {
        if (refreshInProgress || window is null)
        {
            return;
        }

        refreshInProgress = true;
        try
        {
            if (DateTimeOffset.UtcNow < previewUntil)
            {
                if (!window.IsVisible)
                {
                    window.Show();
                }

                return;
            }

            if (await audioControlService.GetDefaultMicrophoneMuteAsync())
            {
                if (!window.IsVisible)
                {
                    window.Show();
                }
            }
            else
            {
                window.Hide();
            }
        }
        finally
        {
            refreshInProgress = false;
        }
    }
}
