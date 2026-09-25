using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Hosts the Vue UI in WebView2 as a full desktop window with its own title bar (maximise, F11 full
/// screen). Closing the window hides it to the tray; the app keeps tracking.
/// </summary>
public partial class MainWindow : Window
{
    private const string DevServerUrl = "http://localhost:5173";
    private const string VirtualHost = "app.local";

    // Space left around WebView2 when not maximised so the window edges can be grabbed for resizing.
    private static readonly Thickness ResizeGrip = new(4);

    private readonly UiBridge _bridge;
    private bool _fullscreen;
    private WindowState _restoreState = WindowState.Maximized;

    public MainWindow(UiBridge bridge)
    {
        _bridge = bridge;
        InitializeComponent();
        _bridge.WindowCommand += OnWindowCommand;
        _bridge.WindowStateProvider = () => (WindowState == WindowState.Maximized, _fullscreen);
        StateChanged += (_, _) => OnStateChanged();
        Loaded += async (_, _) => await InitWebViewAsync();
    }

    /// <summary>The overlay shortcut was pressed (works while Fortnite has focus).</summary>
    public event Action? OverlayHotkey;

    /// <summary>Set before a real exit; otherwise closing only hides the window.</summary>
    public bool AllowClose { get; set; }

    /// <summary>Raised when the close button hid the window instead of exiting.</summary>
    public event Action? HiddenToTray;

    private void OnWindowCommand(string command)
    {
        switch (command)
        {
            case "drag":
                // Hand the pressed mouse button to Windows as if the title bar was grabbed.
                ReleaseCapture();
                SendMessage(new WindowInteropHelper(this).Handle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
                break;
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            case "maximize":
                if (_fullscreen) SetFullscreen(false);
                else WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            case "fullscreen":
                SetFullscreen(!_fullscreen);
                break;
            case "close":
                Close(); // hides to the tray unless AllowClose is set
                break;
        }
    }

    private void SetFullscreen(bool on)
    {
        _fullscreen = on;
        // Re-maximise so Windows asks for the new size (whole monitor vs. work area; see WM_GETMINMAXINFO).
        WindowState = WindowState.Normal;
        WindowState = WindowState.Maximized;
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        if (WindowState != WindowState.Minimized) _restoreState = WindowState;
        if (WindowState != WindowState.Maximized) _fullscreen = false;
        WebView.Margin = WindowState == WindowState.Maximized ? new Thickness(0) : ResizeGrip;
        _bridge.SendWindowState();
    }

    /// <summary>Frame and loading background follow the theme so no dark edge shows around a light one.</summary>
    public void ApplyTheme(ThemeColors colors)
    {
        Background = new SolidColorBrush(colors.Background);
        WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(colors.Background.R, colors.Background.G, colors.Background.B);
    }

    public void ToggleVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
        {
            Hide();
            return;
        }
        ShowAndActivate();
    }

    /// <summary>Creates the native window without showing it, so global shortcuts work from the tray.</summary>
    public void EnsureHandle() => new WindowInteropHelper(this).EnsureHandle();

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = _restoreState;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke();
        }
        base.OnClosing(e);
    }

    private async Task InitWebViewAsync()
    {
        // Velopack installs and updates the app in %LOCALAPPDATA%\FortniteTracker and manages that
        // folder, so WebView2's browser profile lives next to it rather than inside it.
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteTracker.WebView2");
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
        await WebView.EnsureCoreWebView2Async(env);

        var core = WebView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        // Links such as fortnite-api.com/dashboard open in the default browser.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        };
        _bridge.Attach(core, Dispatcher);
        OnStateChanged();

        if (IsDevServerRunning())
        {
            WebView.Source = new Uri(DevServerUrl);
            return;
        }
        core.SetVirtualHostNameToFolderMapping(
            VirtualHost, Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Deny);
        WebView.Source = new Uri($"https://{VirtualHost}/index.html");
    }

    // Debug builds use Vite's dev server (hot reload) when `npm run dev` is running.
    private static bool IsDevServerRunning()
    {
#if DEBUG
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("localhost", 5173).Wait(TimeSpan.FromMilliseconds(300)) && client.Connected;
        }
        catch (Exception ex) when (ex is SocketException or AggregateException)
        {
            return false;
        }
#else
        return false;
#endif
    }

    // ---- Win32: global hotkeys, title bar drag, rounded corners, maximised size ----

    private const int HotkeyId = 0x4654;
    private const int OverlayHotkeyId = 0x4655;
    private const int WmHotkey = 0x0312;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmNcLButtonDown = 0xA1, HtCaption = 2;
    private const int DwmwaWindowCornerPreference = 33, DwmwcpRound = 2;
    private const int MonitorDefaultToNearest = 2;
    private const uint ModNoRepeat = 0x4000;

    private (string Window, string Overlay) _hotkeys = (Core.SettingsStore.DefaultWindowHotkey, Core.SettingsStore.DefaultOverlayHotkey);

    /// <summary>Whether each shortcut could be registered (another app may already use it).</summary>
    public (bool Window, bool Overlay) HotkeyStatus { get; private set; }

    /// <summary>Raised after the shortcuts were (re)registered.</summary>
    public event Action? HotkeysApplied;

    /// <summary>Registers the two global shortcuts, e.g. "Ctrl+Shift+F", replacing the previous ones.</summary>
    public void SetHotkeys(string window, string overlay)
    {
        _hotkeys = (window, overlay);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return; // registered once the window exists
        UnregisterHotKey(handle, HotkeyId);
        UnregisterHotKey(handle, OverlayHotkeyId);
        HotkeyStatus = (Register(handle, HotkeyId, window), Register(handle, OverlayHotkeyId, overlay));
        HotkeysApplied?.Invoke();
    }

    private static bool Register(IntPtr handle, int id, string text) =>
        Core.HotkeyText.TryParse(text, out var mods, out var vk) && RegisterHotKey(handle, id, mods | ModNoRepeat, vk);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32 { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public Point32 Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32 { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MonitorInfo
    {
        public int Size = Marshal.SizeOf<MonitorInfo>();
        public Rect32 Monitor, Work;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, MonitorInfo info);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        SetHotkeys(_hotkeys.Window, _hotkeys.Overlay);
        // Rounded corners on Windows 11 (ignored on Windows 10).
        var round = DwmwcpRound;
        DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref round, sizeof(int));
    }

    protected override void OnClosed(EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HotkeyId);
        UnregisterHotKey(handle, OverlayHotkeyId);
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmHotkey when wParam.ToInt32() == HotkeyId:
                ToggleVisibility();
                handled = true;
                break;
            case WmHotkey when wParam.ToInt32() == OverlayHotkeyId:
                OverlayHotkey?.Invoke();
                handled = true;
                break;
            case WmGetMinMaxInfo:
                FitMaximizedToMonitor(hwnd, lParam);
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    // Without the standard frame, a maximised window would cover the taskbar and spill past the
    // screen edges. Maximise to the monitor's work area, or the whole monitor in full screen.
    private void FitMaximizedToMonitor(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var info = new MonitorInfo();
        if (GetMonitorInfo(MonitorFromWindow(hwnd, MonitorDefaultToNearest), info))
        {
            var area = _fullscreen ? info.Monitor : info.Work;
            mmi.MaxPosition.X = area.Left - info.Monitor.Left;
            mmi.MaxPosition.Y = area.Top - info.Monitor.Top;
            mmi.MaxSize.X = area.Right - area.Left;
            mmi.MaxSize.Y = area.Bottom - area.Top;
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        mmi.MinTrackSize.X = (int)(MinWidth * dpi.DpiScaleX);
        mmi.MinTrackSize.Y = (int)(MinHeight * dpi.DpiScaleY);
        Marshal.StructureToPtr(mmi, lParam, true);
    }
}
