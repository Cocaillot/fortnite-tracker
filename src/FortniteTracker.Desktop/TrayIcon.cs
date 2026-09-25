using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FortniteTracker.Desktop;

/// <summary>Notification-area icon: left click toggles the window, right click opens the menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _updateItem;
    private bool _hintShown;

    public TrayIcon(Action toggleWindow, Action applyUpdate, Action exit)
    {
        _updateItem = new ToolStripMenuItem("Restart to update", null, (_, _) => applyUpdate()) { Visible = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Show / hide  (Ctrl+Shift+F)", null, (_, _) => toggleWindow()));
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => exit()));

        _icon = new NotifyIcon
        {
            Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"), SystemInformation.SmallIconSize),
            Text = "Fortnite Tracker",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) toggleWindow();
        };
    }

    /// <summary>Shown the first time the window is closed, so it's clear the app is still running.</summary>
    public void ShowStillRunningHint()
    {
        if (_hintShown) return;
        _hintShown = true;
        _icon.ShowBalloonTip(3000, "Still running", "Fortnite Tracker keeps tracking from the tray. Right-click the icon to exit.", ToolTipIcon.None);
    }

    public void ShowUpdateReady(string version)
    {
        _updateItem.Text = $"Restart to update to {version}";
        _updateItem.Visible = true;
        _icon.ShowBalloonTip(5000, "Update ready", $"Version {version} is downloaded. It installs the next time the app restarts.", ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
