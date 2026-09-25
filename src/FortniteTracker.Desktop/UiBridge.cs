using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using FortniteTracker.Core;
using Microsoft.Web.WebView2.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Message protocol between the Vue UI and the .NET services (mirrored in ui/src/bridge.ts).
/// Host → UI: snapshot, ranks, settings, history, sessions, lookupResult, profile, leaderboard, windowState, theme.
/// UI → host: ready, lookup, setApiKey, setRichPresence, setNotify, setOverlay, applyUpdate, window,
/// profile, follow, leaderboard, setTheme.
/// </summary>
public sealed class UiBridge
{
    private const int HistoryCount = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly LobbyTracker _tracker;
    private readonly FortniteStatsService _stats;
    private readonly SettingsStore _settings;
    private readonly MatchHistoryStore _history;
    private readonly UpdateService _updates;
    private readonly DiscordPresenceService _presence;
    private readonly RankBook _ranks;
    private readonly SessionStore _sessions;
    private readonly PlayerDirectory _directory;
    private readonly ThemeStore _theme;
    private CancellationTokenSource? _leaderboardRun;
    private CoreWebView2? _web;
    private Dispatcher? _dispatcher;

    public UiBridge(
        LobbyTracker tracker, FortniteStatsService stats, SettingsStore settings,
        MatchHistoryStore history, UpdateService updates, DiscordPresenceService presence,
        RankBook ranks, SessionStore sessions, PlayerDirectory directory, ThemeStore theme)
    {
        _theme = theme;
        _ranks = ranks;
        _sessions = sessions;
        _directory = directory;
        _tracker = tracker;
        _stats = stats;
        _settings = settings;
        _history = history;
        _updates = updates;
        _presence = presence;

        _tracker.Changed += s => Post(() =>
        {
            Send("snapshot", s);
            SendSquadRanks();
        });
        _ranks.Changed += () => Post(SendSquadRanks);
        _sessions.Changed += () => Post(SendSessions);
        _settings.Changed += _ => Post(SendSettings);
        _history.Changed += () => Post(SendHistory);
        _updates.UpdateReady += () => Post(SendSettings);
    }

    /// <summary>Title bar actions of the custom window chrome: drag, minimize, maximize, fullscreen, close.</summary>
    public event Action<string>? WindowCommand;

    /// <summary>Set by the window: whether it is maximised and in full screen, for the title bar buttons.</summary>
    public Func<(bool Maximized, bool Fullscreen)>? WindowStateProvider { get; set; }

    public void SendWindowState()
    {
        if (WindowStateProvider?.Invoke() is { } state)
            Send("windowState", new { maximized = state.Maximized, fullscreen = state.Fullscreen });
    }

    public void Attach(CoreWebView2 web, Dispatcher dispatcher)
    {
        _web = web;
        _dispatcher = dispatcher;
        web.WebMessageReceived += OnMessage;
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                SendHistory();
                SendSessions();
                if (_tracker.Last is { } last) Send("snapshot", last);
                SendSquadRanks();
                SendWindowState();
                SendTheme();
                break;
            case "setTheme":
                // The UI owns the theme format; stored as-is (null = back to the default look).
                _theme.Save(msg.Theme is { ValueKind: JsonValueKind.Object } t ? t.GetRawText() : null);
                break;
            case "profile" when msg.AccountId is not null || !string.IsNullOrWhiteSpace(msg.Name):
                Send("profile", await _directory.GetProfileAsync(msg.AccountId, msg.Name, CancellationToken.None));
                break;
            case "follow" when !string.IsNullOrWhiteSpace(msg.Name) || msg.AccountId is not null:
                if (msg.Enabled == false) _settings.Unfollow(msg.AccountId, msg.Name);
                else _settings.Follow(msg.AccountId, msg.Name ?? msg.AccountId!);
                Send("profile", await _directory.GetProfileAsync(msg.AccountId, msg.Name, CancellationToken.None));
                break;
            case "leaderboard":
                await RunLeaderboardAsync();
                break;
            case "lookup" when !string.IsNullOrWhiteSpace(msg.Name):
                var result = await _stats.GetByNameAsync(msg.Name.Trim(), msg.Platform ?? "epic", CancellationToken.None);
                Send("lookupResult", result);
                break;
            case "setApiKey" when !string.IsNullOrWhiteSpace(msg.Key):
                _settings.SetApiKey(msg.Key);
                break;
            case "setRichPresence" when msg.Enabled is { } enabled:
                _settings.SetRichPresence(enabled);
                break;
            case "setNotify" when msg.Enabled is { } notify:
                _settings.SetNotifyOnElimination(notify);
                break;
            case "setOverlay":
                _settings.SetOverlay(
                    msg.Enabled ?? _settings.OverlayEnabled,
                    Enum.TryParse<OverlayCorner>(msg.Corner, out var corner) ? corner : _settings.OverlayCorner);
                break;
            case "applyUpdate":
                _updates.ApplyAndRestart();
                break;
            case "window" when msg.Action is "drag" or "minimize" or "maximize" or "fullscreen" or "close":
                WindowCommand?.Invoke(msg.Action);
                break;
        }
    }

    private void SendSettings() => Send("settings", new
    {
        hasApiKey = _settings.HasApiKey,
        richPresence = new { available = _presence.Available, enabled = _settings.RichPresenceEnabled },
        notifyOnElimination = _settings.NotifyOnElimination,
        overlay = new { enabled = _settings.OverlayEnabled, corner = _settings.OverlayCorner.ToString() },
        version = _updates.CurrentVersion,
        updateVersion = _updates.ReadyVersion,
    });

    // Sent raw: the UI parses its own format.
    private void SendTheme() =>
        _web?.PostWebMessageAsJson($"{{\"type\":\"theme\",\"data\":{_theme.Json ?? "null"}}}");

    private void SendHistory() => Send("history", _history.Recent(HistoryCount));

    private void SendSessions() => Send("sessions", _sessions.All.Reverse().Take(50).ToList());

    // Ranks of the squad on screen, keyed by account ID.
    private void SendSquadRanks()
    {
        var ids = _tracker.Last?.Squad.Select(p => p.AccountId).OfType<string>() ?? [];
        Send("ranks", ids.Distinct().ToDictionary(id => id, id => _ranks.For(id)));
    }

    private async Task RunLeaderboardAsync()
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _leaderboardRun, cts)?.Cancel();
        try
        {
            await _directory.BuildLeaderboardAsync(entries => Post(() => Send("leaderboard", entries)), cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Post(Action action) => _dispatcher?.InvokeAsync(action);

    private void Send(string type, object data) =>
        _web?.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, data }, Json));

    private sealed record UiMessage(
        string Type, string? Name, string? Platform, string? Key, bool? Enabled, string? Corner, string? Action, string? AccountId,
        JsonElement? Theme);
}
