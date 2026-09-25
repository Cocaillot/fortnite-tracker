using DiscordRPC;
using FortniteTracker.Core;
using Microsoft.Extensions.Configuration;

namespace FortniteTracker.Desktop;

/// <summary>
/// Shows your mode, squad and stats on your Discord profile through Discord's local RPC.
/// Needs a Discord application ID in Discord:ClientId; the user can turn it off in settings.
/// </summary>
public sealed class DiscordPresenceService : IDisposable
{
    private readonly string? _clientId;
    private readonly SettingsStore _settings;
    private readonly LobbyTracker _tracker;
    private readonly object _gate = new();
    private DiscordRpcClient? _client;
    private string? _lastSent;

    public DiscordPresenceService(IConfiguration config, SettingsStore settings, LobbyTracker tracker)
    {
        _clientId = config["Discord:ClientId"];
        _settings = settings;
        _tracker = tracker;
        _tracker.Changed += Update;
        _settings.Changed += _ => Update(_tracker.Last);
    }

    public bool Available => !string.IsNullOrWhiteSpace(_clientId);

    private void Update(LobbySnapshot? snapshot)
    {
        lock (_gate)
        {
            if (!Available || !_settings.RichPresenceEnabled || snapshot is null || !snapshot.GameRunning)
            {
                Clear();
                return;
            }

            var presence = Build(snapshot);
            var key = $"{presence.Details}|{presence.State}|{snapshot.MatchStartedUtc}";
            if (key == _lastSent) return; // Discord rate-limits presence updates; skip no-op changes
            _lastSent = key;

            if (_client is null)
            {
                _client = new DiscordRpcClient(_clientId);
                _client.Initialize(); // reconnects on its own if Discord isn't running yet
            }
            _client.SetPresence(presence);
        }
    }

    internal static RichPresence Build(LobbySnapshot s)
    {
        var you = s.Squad.FirstOrDefault(p => p.Status == StatsStatus.Ok && p.EpicName == s.LocalName)
                  ?? s.Squad.FirstOrDefault();
        var squadSize = Math.Max(1, s.Squad.Count);
        var squadLabel = squadSize == 1 ? "solo" : $"squad of {squadSize}";

        var parts = new List<string>();
        if (you is { Status: StatsStatus.Ok, Kd: { } kd, WinRate: { } winRate })
            parts.Add($"K/D {kd:0.00} · {winRate:0.#}% wins");
        var others = s.Squad.Where(p => p != you && p is { Status: StatsStatus.Ok, Kd: not null }).ToList();
        if (others.Count > 0)
            parts.Add($"squad K/D {s.Squad.Where(p => p.Kd is not null).Average(p => p.Kd!.Value):0.00}");

        return new RichPresence
        {
            Details = s.InMatch ? $"{s.Mode} · {squadLabel}" : $"In the lobby · {squadLabel}",
            State = parts.Count > 0 ? string.Join(" · ", parts) : "Tracking stats",
            Timestamps = s is { InMatch: true, MatchStartedUtc: { } started } ? new Timestamps(started) : null,
            Assets = new Assets { LargeImageKey = "logo", LargeImageText = "Fortnite Tracker" },
        };
    }

    private void Clear()
    {
        if (_lastSent is null) return;
        _lastSent = null;
        _client?.ClearPresence();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _client?.ClearPresence();
            _client?.Dispose();
            _client = null;
        }
    }
}
