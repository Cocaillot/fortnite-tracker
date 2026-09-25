using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>Notification-area icon: left click toggles the window, right click opens the menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _overlayItem;
    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _exitItem;
    private string? _updateVersion;
    private bool _hintShown;

    public TrayIcon(Action toggleWindow, Action toggleOverlay, Action applyUpdate, Action exit)
    {
        _updateItem = new ToolStripMenuItem("", null, (_, _) => applyUpdate()) { Visible = false };
        _overlayItem = new ToolStripMenuItem("", null, (_, _) => toggleOverlay());
        _showItem = new ToolStripMenuItem("", null, (_, _) => toggleWindow());
        _exitItem = new ToolStripMenuItem("", null, (_, _) => exit());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_showItem);
        menu.Items.Add(_overlayItem);
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);
        ApplyLanguage();

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

    /// <summary>Menu labels in the current language (see <see cref="Loc"/>).</summary>
    public void ApplyLanguage()
    {
        _showItem.Text = Loc.T("Show / hide  (Ctrl+Shift+F)");
        _overlayItem.Text = Loc.T("In-game overlay  (Ctrl+Shift+O)");
        _exitItem.Text = Loc.T("Exit");
        _updateItem.Text = _updateVersion is null ? Loc.T("Restart to update") : Loc.T("Restart to update to {0}", _updateVersion);
    }

    /// <summary>A Windows notification. Windows may hold it back while a fullscreen game has Do Not Disturb on.</summary>
    public void Notify(string title, string text, int timeoutMs = 6000) =>
        _icon.ShowBalloonTip(timeoutMs, title, text, ToolTipIcon.None);

    /// <summary>Shown the first time the window is closed, so it's clear the app is still running.</summary>
    public void ShowStillRunningHint()
    {
        if (_hintShown) return;
        _hintShown = true;
        Notify(Loc.T("Still running"), Loc.T("Fortnite Tracker keeps tracking from the tray. Right-click the icon to exit."), 3000);
    }

    public void ShowUpdateReady(string version)
    {
        _updateVersion = version;
        ApplyLanguage();
        _updateItem.Visible = true;
        Notify(Loc.T("Update ready"), Loc.T("Version {0} is downloaded. It installs the next time the app restarts.", version));
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
