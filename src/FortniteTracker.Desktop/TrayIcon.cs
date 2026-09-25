using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FortniteTracker.Desktop;

/// <summary>Notification-area icon: left click toggles the window, right click opens the menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _overlayItem;
    private bool _hintShown;

    public TrayIcon(Action toggleWindow, Action toggleOverlay, Action applyUpdate, Action exit)
    {
        _updateItem = new ToolStripMenuItem("Restart to update", null, (_, _) => applyUpdate()) { Visible = false };
        _overlayItem = new ToolStripMenuItem("In-game overlay  (Ctrl+Shift+O)", null, (_, _) => toggleOverlay());

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Show / hide  (Ctrl+Shift+F)", null, (_, _) => toggleWindow()));
        menu.Items.Add(_overlayItem);
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

    public void SetOverlayChecked(bool on) => _overlayItem.Checked = on;

    /// <summary>A Windows notification. Windows may hold it back while a fullscreen game has Do Not Disturb on.</summary>
    public void Notify(string title, string text, int timeoutMs = 6000) =>
        _icon.ShowBalloonTip(timeoutMs, title, text, ToolTipIcon.None);

    /// <summary>Shown the first time the window is closed, so it's clear the app is still running.</summary>
    public void ShowStillRunningHint()
    {
        if (_hintShown) return;
        _hintShown = true;
        Notify("Still running", "Fortnite Tracker keeps tracking from the tray. Right-click the icon to exit.", 3000);
    }

    public void ShowUpdateReady(string version)
    {
        _updateItem.Text = $"Restart to update to {version}";
        _updateItem.Visible = true;
        Notify("Update ready", $"Version {version} is downloaded. It installs the next time the app restarts.");
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
