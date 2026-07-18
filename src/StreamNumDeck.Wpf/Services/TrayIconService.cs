using System.Drawing;
using System.Windows.Forms;
using System.Windows.Resources;
using StreamNumDeck.Wpf.Localization;

namespace StreamNumDeck.Wpf.Services;

internal sealed record TrayProfileOption(Guid Id, string Name);

internal sealed class TrayIconService : IDisposable
{
    private NotifyIcon? notifyIcon;
    private ToolStripMenuItem? captureItem;
    private ToolStripMenuItem? captureNumpadItem;
    private ToolStripMenuItem? captureNavigationBlockItem;
    private ToolStripMenuItem? profilesItem;
    private Func<Guid, Task>? selectProfile;

    public void Initialize(
        Action showWindow,
        Func<Task> toggleCapture,
        Func<Task> toggleCaptureNumpad,
        Func<Task> toggleCaptureNavigationBlock,
        Func<Guid, Task> selectProfile,
        Action exitApplication)
    {
        this.selectProfile = selectProfile;
        var showItem = new ToolStripMenuItem(AppStrings.Get("Tray_Open", "Open StreamNumDeck"));
        showItem.Click += (_, _) => showWindow();
        captureItem = new ToolStripMenuItem(AppStrings.Get("Tray_Capture", "Capture keys"));
        captureItem.Click += async (_, _) => await ExecuteAsync(toggleCapture, captureItem);
        captureNumpadItem = new ToolStripMenuItem(AppStrings.Get("Capture_NumPadHint", "Intercept numeric keypad keys"));
        captureNumpadItem.Click += async (_, _) => await ExecuteAsync(toggleCaptureNumpad, captureNumpadItem);
        captureNavigationBlockItem = new ToolStripMenuItem(AppStrings.Get("Capture_NavigationBlockHint", "Intercept the six-key navigation block"));
        captureNavigationBlockItem.Click += async (_, _) => await ExecuteAsync(toggleCaptureNavigationBlock, captureNavigationBlockItem);
        profilesItem = new ToolStripMenuItem(AppStrings.Get("Tray_Profiles", "Profiles")) { Enabled = false };
        var exitItem = new ToolStripMenuItem(AppStrings.Get("Tray_Exit", "Exit"));
        exitItem.Click += (_, _) => exitApplication();

        var menu = new ContextMenuStrip();
        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(captureItem);
        menu.Items.Add(captureNumpadItem);
        menu.Items.Add(captureNavigationBlockItem);
        menu.Items.Add(profilesItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = LoadIcon(),
            Text = "StreamNumDeck",
        };
        notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                showWindow();
            }
        };
        notifyIcon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                showWindow();
            }
        };
    }

    public void SetVisible(bool visible)
    {
        if (notifyIcon is not null)
        {
            notifyIcon.Visible = visible;
        }
    }

    public void SetCaptureEnabled(bool enabled)
    {
        if (captureItem is not null)
        {
            captureItem.Checked = enabled;
        }
    }

    public void SetCaptureTargets(bool captureNumpad, bool captureNavigationBlock)
    {
        if (captureNumpadItem is not null)
        {
            captureNumpadItem.Checked = captureNumpad;
        }

        if (captureNavigationBlockItem is not null)
        {
            captureNavigationBlockItem.Checked = captureNavigationBlock;
        }
    }

    public void ShowError(string title, string message)
    {
        if (notifyIcon?.Visible == true)
        {
            notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Error);
        }
    }

    public void SetProfiles(IEnumerable<TrayProfileOption> profiles, Guid activeProfileId)
    {
        if (profilesItem is null || selectProfile is null)
        {
            return;
        }

        profilesItem.DropDownItems.Clear();
        foreach (var profile in profiles)
        {
            var item = new ToolStripMenuItem(profile.Name)
            {
                Checked = profile.Id == activeProfileId,
                Tag = profile.Id,
            };
            item.Click += async (_, _) => await ExecuteAsync(() => selectProfile(profile.Id), item);
            profilesItem.DropDownItems.Add(item);
        }

        profilesItem.Enabled = profilesItem.DropDownItems.Count > 0;
    }

    public void Dispose()
    {
        if (notifyIcon is null)
        {
            return;
        }

        notifyIcon.Visible = false;
        notifyIcon.ContextMenuStrip?.Dispose();
        notifyIcon.Icon?.Dispose();
        notifyIcon.Dispose();
        notifyIcon = null;
    }

    private async Task ExecuteAsync(Func<Task> action, ToolStripItem item)
    {
        item.Enabled = false;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            var title = AppStrings.Get("Tray_ActionFailed", "Action failed");
            AppLogger.Error(title, exception);
            var detail = UserErrorFormatter.GetSafeDetail(exception);
            notifyIcon?.ShowBalloonTip(
                5000,
                title,
                string.IsNullOrWhiteSpace(detail) ? title : detail,
                ToolTipIcon.Error);
        }
        finally
        {
            item.Enabled = true;
        }
    }

    private static Icon LoadIcon()
    {
        StreamResourceInfo resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/AppIcon.ico"));
        using var stream = resource.Stream;
        return new Icon(stream);
    }
}
