using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Interop;
using FortniteTracker.Core;
using Microsoft.Web.WebView2.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Hosts the Vue UI in WebView2 and bridges it to the Core services through web messages.
/// Host → UI: snapshot, settings, lookupResult. UI → host: ready, lookup, setApiKey.
/// </summary>
public partial class MainWindow : Window
{
    private const string DevServerUrl = "http://localhost:5173";
    private const string VirtualHost = "app.local";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly LobbyTracker _tracker;
    private readonly FortniteStatsService _stats;
    private readonly ApiKeyStore _keys;

    public MainWindow(LobbyTracker tracker, FortniteStatsService stats, ApiKeyStore keys)
    {
        _tracker = tracker;
        _stats = stats;
        _keys = keys;
        InitializeComponent();

        _tracker.Changed += snapshot => Dispatcher.InvokeAsync(() => Send("snapshot", snapshot));
        _keys.Changed += () => Dispatcher.InvokeAsync(SendSettings);
        Loaded += async (_, _) => await InitWebViewAsync();
    }

    private async Task InitWebViewAsync()
    {
        // Keep WebView2's profile out of the install folder, which may be read-only.
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteTracker", "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
        await WebView.EnsureCoreWebView2Async(env);

        var core = WebView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.WebMessageReceived += OnWebMessage;

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

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        UiMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<UiMessage>(e.WebMessageAsJson, Json);
        }
        catch (JsonException)
        {
            return;
        }

        switch (msg?.Type)
        {
            case "ready":
                SendSettings();
                if (_tracker.Last is { } last) Send("snapshot", last);
                break;
            case "lookup" when !string.IsNullOrWhiteSpace(msg.Name):
                var result = await _stats.GetByNameAsync(msg.Name.Trim(), msg.Platform ?? "epic", CancellationToken.None);
                Send("lookupResult", result);
                break;
            case "setApiKey" when !string.IsNullOrWhiteSpace(msg.Key):
                _keys.Save(msg.Key);
                break;
        }
    }

    private void SendSettings() => Send("settings", new { hasApiKey = _keys.HasKey });

    private void Send(string type, object data) =>
        WebView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, data }, Json));

    private sealed record UiMessage(string Type, string? Name, string? Platform, string? Key);

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

    private void ToggleVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
            return;
        }
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
