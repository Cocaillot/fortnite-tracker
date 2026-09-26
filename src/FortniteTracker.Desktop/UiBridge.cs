using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using FortniteTracker.Core;
using Microsoft.Web.WebView2.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Message protocol between the Vue UI and the .NET services (mirrored in ui/src/bridge.ts).
/// Host → UI: snapshot, ranks, settings, history, sessions, lookupResult, profile, leaderboard, windowState, theme, toast,
/// matchDetail, teammates, notes, statsHistory, goals, recapResult, apiKeyTest, dataResult.
/// UI → host: ready, lookup, setApiKey, setRichPresence, setNotify, setOverlay, applyUpdate, window,
/// profile, follow, leaderboard, setTheme, match, teammates, setNote, setGoals, setDiscordRecap, postRecap,
/// setLanguage, setHotkeys, setLaunchWithFortnite, onboardingDone, seenVersion, testApiKey, data.
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
    private readonly MatchInsights _insights;
    private readonly PlayerNotes _notes;
    private readonly StatsHistory _statsHistory;
    private readonly GoalStore _goals;
    private readonly DiscordRecapPoster _recap;
    private readonly FortniteLogTailer _tailer;
    private readonly DataPaths _paths;
    private CancellationTokenSource? _leaderboardRun;
    private CoreWebView2? _web;
    private Dispatcher? _dispatcher;

    public UiBridge(
        LobbyTracker tracker, FortniteStatsService stats, SettingsStore settings,
        MatchHistoryStore history, UpdateService updates, DiscordPresenceService presence,
        RankBook ranks, SessionStore sessions, PlayerDirectory directory, ThemeStore theme, MatchInsights insights,
        PlayerNotes notes, StatsHistory statsHistory, GoalStore goals, DiscordRecapPoster recap,
        FortniteLogTailer tailer, DataPaths paths)
    {
        _tailer = tailer;
        _paths = paths;
        _recap = recap;
        _notes = notes;
        _statsHistory = statsHistory;
        _goals = goals;
        _statsHistory.Changed += () => Post(SendStatsHistory);
        _notes.Changed += () => Post(SendNotes);
        _insights = insights;
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
        _ranks.Changed += () => Post(() =>
        {
            SendSquadRanks();
            SendHistory(); // rank changes per match
        });
        _sessions.Changed += () => Post(SendSessions);
        _settings.Changed += _ => Post(SendSettings);
        _history.Changed += () => Post(SendHistory);
        _updates.UpdateReady += () => Post(SendSettings);
    }

    /// <summary>Title bar actions of the custom window chrome: drag, minimize, maximize, fullscreen, close.</summary>
    public event Action<string>? WindowCommand;

    /// <summary>Set by the window: whether it is maximised and in full screen, for the title bar buttons.</summary>
    public Func<(bool Maximized, bool Fullscreen)>? WindowStateProvider { get; set; }

    /// <summary>Set by the window: whether each global shortcut is registered.</summary>
    public Func<(bool Window, bool Overlay)>? HotkeyStatusProvider { get; set; }

    /// <summary>Restarts the app (after a restore).</summary>
    public event Action? RestartRequested;

    /// <summary>An in-app pop-up (e.g. a rank change); "good" colours it as good news.</summary>
    public void Toast(string title, string text, bool good) => Post(() => Send("toast", new { title, text, good }));

    /// <summary>Sends the settings again, e.g. once the window knows whether the shortcuts registered.</summary>
    public void RefreshSettings() => Post(SendSettings);

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
                SendNotes();
                SendStatsHistory();
                SendGoals();
                break;
            case "setDiscordRecap":
                if (!string.IsNullOrWhiteSpace(msg.Url) && !DiscordRecapPoster.IsWebhookUrl(msg.Url))
                {
                    Send("recapResult", Loc.T("That isn't a Discord webhook link. It starts with https://discord.com/api/webhooks/"));
                    break;
                }
                _settings.SetDiscordRecap(msg.Url, msg.Enabled ?? true);
                break;
            case "setLanguage" when msg.Lang is "en" or "fr" or "auto":
                _settings.SetLanguage(msg.Lang == "auto" ? null : msg.Lang);
                break;
            case "setHotkeys":
                if (msg.Window is { } w && !HotkeyText.TryParse(w, out _, out _)) break;
                if (msg.Overlay is { } o && !HotkeyText.TryParse(o, out _, out _)) break;
                _settings.SetHotkeys(msg.Window, msg.Overlay);
                break;
            case "setLaunchWithFortnite" when msg.Enabled is { } launch:
                _settings.SetLaunchWithFortnite(launch);
                break;
            case "onboardingDone":
                _settings.SetOnboardingDone();
                _settings.SetLastSeenVersion(_updates.CurrentVersion);
                break;
            case "seenVersion":
                _settings.SetLastSeenVersion(_updates.CurrentVersion);
                break;
            case "testApiKey" when !string.IsNullOrWhiteSpace(msg.Key):
                var test = await _stats.TestKeyAsync(msg.Key, _tracker.Last?.LocalName, CancellationToken.None);
                if (test == "ok") _settings.SetApiKey(msg.Key);
                Send("apiKeyTest", test);
                break;
            case "data" when msg.Action is "csv" or "backup" or "restore":
                Send("dataResult", RunDataAction(msg.Action));
                break;
            case "postRecap":
                Send("recapResult", await _recap.PostLatestAsync(onlyIfNew: false));
                break;
            case "setGoals":
                // The UI owns the goal format; stored as-is.
                _goals.Save(msg.Goals is { ValueKind: JsonValueKind.Array } g ? g.GetRawText() : "[]");
                break;
            case "setNote" when !string.IsNullOrWhiteSpace(msg.Name):
                _notes.Save(msg.AccountId, msg.Name, msg.Tags ?? [], msg.Text ?? "");
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
            case "match" when msg.StartedUtc is { } started:
                Send("matchDetail", await _insights.GetDetailAsync(started.ToUniversalTime(), CancellationToken.None));
                break;
            case "teammates":
                Send("teammates", await _insights.TeammatesWithNamesAsync(CancellationToken.None));
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
            case "setNotifyRanks" when msg.Enabled is { } notifyRanks:
                _settings.SetNotifyRankChanges(notifyRanks);
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

    // Runs on the UI thread (web messages arrive there), so the file dialogs can open directly.
    private string? RunDataAction(string action)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        var stamp = DateTime.Now.ToString("yyyy-MM-dd");
        try
        {
            switch (action)
            {
                case "csv":
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = $"fortnite-matches-{stamp}.csv",
                        Filter = "CSV (*.csv)|*.csv",
                    };
                    if (dialog.ShowDialog(owner) != true) return null;
                    DataExport.WriteCsv(_history.Recent(int.MaxValue), dialog.FileName);
                    return Loc.T("Saved {0}", System.IO.Path.GetFileName(dialog.FileName));
                }
                case "backup":
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = $"fortnite-tracker-backup-{stamp}.zip",
                        Filter = "Backup (*.zip)|*.zip",
                    };
                    if (dialog.ShowDialog(owner) != true) return null;
                    DataExport.CreateBackup(_paths.Directory, dialog.FileName);
                    return Loc.T("Saved {0}", System.IO.Path.GetFileName(dialog.FileName));
                }
                default:
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Backup (*.zip)|*.zip" };
                    if (dialog.ShowDialog(owner) != true) return null;
                    if (!DataExport.QueueRestore(dialog.FileName, _paths.Directory))
                        return Loc.T("That file isn't a Fortnite Tracker backup.");
                    RestartRequested?.Invoke();
                    return null;
                }
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            return Loc.T("Couldn't save the file: {0}", ex.Message);
        }
    }

    private void SendSettings() => Send("settings", new
    {
        hasApiKey = _settings.HasApiKey,
        richPresence = new { available = _presence.Available, enabled = _settings.RichPresenceEnabled },
        notifyOnElimination = _settings.NotifyOnElimination,
        notifyRankChanges = _settings.NotifyRankChanges,
        language = _settings.Language ?? "auto",
        effectiveLanguage = Loc.Language,
        discordRecap = new { hasWebhook = DiscordRecapPoster.IsWebhookUrl(_settings.DiscordWebhookUrl), autoPost = _settings.AutoPostRecap },
        overlay = new { enabled = _settings.OverlayEnabled, corner = _settings.OverlayCorner.ToString() },
        hotkeys = new
        {
            window = _settings.WindowHotkey,
            overlay = _settings.OverlayHotkey,
            windowOk = HotkeyStatusProvider?.Invoke().Window ?? true,
            overlayOk = HotkeyStatusProvider?.Invoke().Overlay ?? true,
        },
        launchWithFortnite = _settings.LaunchWithFortnite,
        onboardingDone = _settings.OnboardingDone,
        lastSeenVersion = _settings.LastSeenVersion,
        fortniteFound = System.IO.File.Exists(_tailer.LogPath),
        version = _updates.CurrentVersion,
        updateVersion = _updates.ReadyVersion,
    });

    // Sent raw: the UI parses its own format.
    private void SendTheme() =>
        _web?.PostWebMessageAsJson($"{{\"type\":\"theme\",\"data\":{_theme.Json ?? "null"}}}");

    private void SendNotes() => Send("notes", _notes.All);

    private void SendStatsHistory() => Send("statsHistory", _statsHistory.All);

    private void SendGoals() =>
        _web?.PostWebMessageAsJson($"{{\"type\":\"goals\",\"data\":{_goals.Json}}}");

    // Each match plus, for ranked ones, how your rank moved ("+32%", like the end-of-match screen).
    private void SendHistory() => Send("history", _history.Recent(HistoryCount).Select(m =>
    {
        var node = JsonSerializer.SerializeToNode(m, Json)!.AsObject();
        // Unreal has a leaderboard position instead of progress, so there's no percentage to show.
        if (_insights.RankMoveFor(m) is { Before.Current: < RankNames.Unreal } move)
            node["rank"] = JsonSerializer.SerializeToNode(new { delta = move.Delta, trackName = move.TrackName, afterName = move.AfterName, matches = move.Matches }, Json);
        return node;
    }).ToList());

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

    private void Send(string type, object? data) =>
        _web?.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, data }, Json));

    private sealed record UiMessage(
        string Type, string? Name, string? Platform, string? Key, bool? Enabled, string? Corner, string? Action, string? AccountId,
        JsonElement? Theme, DateTime? StartedUtc, string[]? Tags, string? Text, JsonElement? Goals, string? Url, string? Lang, string? Window, string? Overlay);
}
