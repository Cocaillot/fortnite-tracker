using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Hosts the Vue UI in WebView2. Closing the window hides it to the tray; the app keeps tracking.
/// </summary>
public partial class MainWindow : Window
{
    private const string DevServerUrl = "http://localhost:5173";
    private const string VirtualHost = "app.local";

    private readonly UiBridge _bridge;

    public MainWindow(UiBridge bridge)
    {
        _bridge = bridge;
        InitializeComponent();
        Loaded += async (_, _) => await InitWebViewAsync();
    }

    /// <summary>Set before a real exit; otherwise closing only hides the window.</summary>
    public bool AllowClose { get; set; }

    /// <summary>Raised when the close button hid the window instead of exiting.</summary>
    public event Action? HiddenToTray;

    public void ToggleVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
            return;
        }
        ShowAndActivate();
    }

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
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
        _bridge.Attach(core, Dispatcher);

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

    // ---- Global hotkey: Ctrl+Shift+F shows/hides the window, even while Fortnite has focus ----

    private const int HotkeyId = 0x4654;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002, ModShift = 0x0004, ModNoRepeat = 0x4000;
    private const uint VkF = 0x46;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        RegisterHotKey(handle, HotkeyId, ModControl | ModShift | ModNoRepeat, VkF);
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId);
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
