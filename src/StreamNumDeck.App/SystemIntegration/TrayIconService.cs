using System.Drawing;
using System.Windows.Forms;
using StreamNumDeck.App.Localization;

namespace StreamNumDeck.App.SystemIntegration;

public sealed record TrayProfileOption(Guid Id, string Name);

public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? notifyIcon;
    private System.Windows.Forms.Timer? activationTimer;
    private ToolStripMenuItem? captureItem;
    private ToolStripMenuItem? profilesItem;
    private Func<Guid, Task>? selectProfile;
    private bool disposed;

    public void Initialize(
        Action showWindow,
        Func<Task> toggleCapture,
        Func<Guid, Task> selectProfile,
        Action exitApplication)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(showWindow);
        ArgumentNullException.ThrowIfNull(toggleCapture);
        ArgumentNullException.ThrowIfNull(selectProfile);
        ArgumentNullException.ThrowIfNull(exitApplication);

        if (notifyIcon is not null)
        {
            return;
        }

        this.selectProfile = selectProfile;
        var timer = new System.Windows.Forms.Timer
        {
            Interval = SystemInformation.DoubleClickTime,
        };
        activationTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            showWindow();
        };

        var showItem = new ToolStripMenuItem(AppStrings.Get("Tray_Open"));
        showItem.Click += (_, _) => showWindow();
        captureItem = new ToolStripMenuItem(AppStrings.Get("Tray_Capture"))
        {
            CheckOnClick = false,
        };
        captureItem.Click += async (_, _) =>
            await ExecuteAsync(toggleCapture, captureItem);
        profilesItem = new ToolStripMenuItem(AppStrings.Get("Tray_Profiles"))
        {
            Enabled = false,
        };
        var exitItem = new ToolStripMenuItem(AppStrings.Get("Tray_Exit"));
        exitItem.Click += (_, _) => exitApplication();

        var menu = new ContextMenuStrip();
        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(captureItem);
        menu.Items.Add(profilesItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = new Icon(ResolveIconPath()),
            Text = "StreamNumDeck",
            Visible = false,
        };
        notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button is MouseButtons.Left)
            {
                timer.Stop();
                timer.Start();
            }
        };
        notifyIcon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button is MouseButtons.Left)
            {
                timer.Stop();
                showWindow();
            }
        };
    }

    public void SetCaptureEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (captureItem is not null)
        {
            captureItem.Checked = enabled;
        }
    }

    public void SetProfiles(
        IReadOnlyCollection<TrayProfileOption> profiles,
        Guid activeProfileId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(profiles);

        var profileSelector = selectProfile;
        if (profilesItem is null || profileSelector is null)
        {
            return;
        }

        var options = profiles.ToArray();
        var existingItems = profilesItem.DropDownItems
            .OfType<ToolStripMenuItem>()
            .ToArray();
        var canUpdateInPlace = existingItems.Length == options.Length
            && existingItems.Zip(options).All(pair =>
                pair.First.Tag is Guid id
                && id == pair.Second.Id
                && string.Equals(pair.First.Text, pair.Second.Name, StringComparison.CurrentCulture));
        if (canUpdateInPlace)
        {
            foreach (var item in existingItems)
            {
                item.Checked = item.Tag is Guid id && id == activeProfileId;
            }

            profilesItem.Enabled = options.Length > 0;
            return;
        }

        profilesItem.DropDownItems.Clear();
        foreach (var item in existingItems)
        {
            item.Dispose();
        }

        foreach (var profile in options)
        {
            var profileItem = new ToolStripMenuItem(profile.Name)
            {
                Checked = profile.Id == activeProfileId,
                Tag = profile.Id,
            };
            profileItem.Click += async (_, _) =>
                await ExecuteAsync(() => profileSelector(profile.Id), profileItem);
            profilesItem.DropDownItems.Add(profileItem);
        }

        profilesItem.Enabled = profiles.Count > 0;
    }

    private static string ResolveIconPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
            Path.Combine(AppContext.BaseDirectory, "AppX", "Assets", "AppIcon.ico"),
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Не найден файл иконки StreamNumDeck для области уведомлений.");
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (notifyIcon is not null)
        {
            notifyIcon.Visible = visible;
        }
    }

    private async Task ExecuteAsync(Func<Task> action, ToolStripItem source)
    {
        source.Enabled = false;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            notifyIcon?.ShowBalloonTip(
                5000,
                AppStrings.Get("Tray_ActionFailed"),
                exception.Message,
                ToolTipIcon.Error);
        }
        finally
        {
            if (!disposed)
            {
                source.Enabled = true;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        activationTimer?.Stop();
        activationTimer?.Dispose();
        activationTimer = null;
        if (notifyIcon is not null)
        {
            notifyIcon.Visible = false;
            notifyIcon.ContextMenuStrip?.Dispose();
            notifyIcon.Icon?.Dispose();
            notifyIcon.Dispose();
            notifyIcon = null;
        }

        captureItem = null;
        profilesItem = null;
        selectProfile = null;
    }
}
