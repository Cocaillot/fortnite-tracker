using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using FortniteTracker.Core;
using Microsoft.Web.WebView2.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Message protocol between the Vue UI and the .NET services (mirrored in ui/src/bridge.ts).
/// Host → UI: snapshot, settings, history, lookupResult.
/// UI → host: ready, lookup, setApiKey, setRichPresence, setNotify, setOverlay, applyUpdate, window.
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
    private CoreWebView2? _web;
    private Dispatcher? _dispatcher;

    public UiBridge(
        LobbyTracker tracker, FortniteStatsService stats, SettingsStore settings,
        MatchHistoryStore history, UpdateService updates, DiscordPresenceService presence)
    {
        _tracker = tracker;
        _stats = stats;
        _settings = settings;
        _history = history;
        _updates = updates;
        _presence = presence;

        _tracker.Changed += s => Post(() => Send("snapshot", s));
        _settings.Changed += _ => Post(SendSettings);
        _history.Changed += () => Post(SendHistory);
        _updates.UpdateReady += () => Post(SendSettings);
    }

    /// <summary>Title bar buttons of the custom window chrome: "drag", "minimize", "close".</summary>
    public event Action<string>? WindowCommand;

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
                if (_tracker.Last is { } last) Send("snapshot", last);
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
            case "window" when msg.Action is "drag" or "minimize" or "close":
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

    private void SendHistory() => Send("history", _history.Recent(HistoryCount));

    private void Post(Action action) => _dispatcher?.InvokeAsync(action);

    private void Send(string type, object data) =>
        _web?.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, data }, Json));

    private sealed record UiMessage(
        string Type, string? Name, string? Platform, string? Key, bool? Enabled, string? Corner, string? Action);
}
