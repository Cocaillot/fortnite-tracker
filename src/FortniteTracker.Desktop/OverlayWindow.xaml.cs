using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// A small always-on-top bar with squad K/Ds (and your eliminator after a death). It's a native
/// WPF window because WebView2 can't render into a transparent window. Clicks pass through it,
/// so it never gets in the way of the game; it works over Windowed Fullscreen, not exclusive fullscreen.
/// </summary>
public partial class OverlayWindow : Window
{
    private const double ScreenMargin = 16;

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private OverlayCorner _corner = OverlayCorner.TopRight;
    private DateTime? _matchStartedUtc;

    public OverlayWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PlaceInCorner();
        _clock.Tick += (_, _) => UpdateTimer();
        _clock.Start();
    }

    public sealed record Row(string Name, string Kd, Brush KdBrush, string WinRate);

    public void SetCorner(OverlayCorner corner)
    {
        _corner = corner;
        PlaceInCorner();
    }

    public void Update(LobbySnapshot s)
    {
        Header.Text = (s.InMatch ? s.Mode : "In lobby").ToUpperInvariant() + $"  ·  {s.StatsLabel.ToUpperInvariant()}";
        _matchStartedUtc = s.InMatch ? s.MatchStartedUtc : null;
        UpdateTimer();

        Rows.ItemsSource = s.Squad.Select((p, i) =>
        {
            var name = i == 0 && s.LocalName is not null ? s.LocalName : StatText.DisplayName(p);
            return p.For(s.StatsBucket) is { } m && p.Status == StatsStatus.Ok
                ? new Row(name, m.Kd.ToString("0.00"), StatText.Brush(m.KdRarity), $"{m.WinRate:0.#}%")
                : new Row(name, "–", StatText.Brush(Rarity.Common), "");
        }).ToList();

        if (!s.InMatch && s.EliminatedBy is { } e)
        {
            EliminatorPanel.Visibility = Visibility.Visible;
            EliminatorName.Text = StatText.DisplayName(e);
            var m = e.Status == StatsStatus.Ok ? e.For(s.StatsBucket) : null;
            EliminatorKd.Text = m is null ? "" : m.Kd.ToString("0.00");
            EliminatorKd.Foreground = StatText.Brush(m?.KdRarity ?? Rarity.Common);
            var threat = e.Threat is { } t ? StatText.Threat(t).ToUpperInvariant() + "  ·  " : "";
            EliminatorDetail.Text = threat + (StatText.VersusYou(e, s.Squad.FirstOrDefault(), s.StatsBucket) ?? StatText.Summary(e, s.StatsBucket));
        }
        else
        {
            EliminatorPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateTimer()
    {
        Timer.Text = _matchStartedUtc is { } start ? (DateTime.UtcNow - start).ToString(@"m\:ss") : "";
    }

    private void PlaceInCorner()
    {
        var area = SystemParameters.WorkArea;
        var left = _corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft;
        var top = _corner is OverlayCorner.TopLeft or OverlayCorner.TopRight;
        Left = left ? area.Left + ScreenMargin : area.Right - ActualWidth - ScreenMargin;
        Top = top ? area.Top + ScreenMargin : area.Bottom - ActualHeight - ScreenMargin;
    }

    // ---- Click-through, no activation, not in Alt+Tab ----

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExLayered = 0x80000, WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style | WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate));
    }
}
