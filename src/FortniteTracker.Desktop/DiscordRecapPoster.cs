using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Posts a session recap to a Discord channel through a webhook link the user pasted in Settings
/// (Channel settings → Integrations → Webhooks). Posts automatically when Fortnite closes, if enabled.
/// </summary>
public sealed class DiscordRecapPoster
{
    private readonly HttpClient _http;
    private readonly SettingsStore _settings;
    private readonly MatchHistoryStore _history;
    private readonly SessionStore _sessions;
    private readonly RankBook _ranks;
    private readonly LobbyTracker _tracker;
    private readonly ThemeStore _theme;
    private bool _wasRunning;
    private bool _playedLive;

    public DiscordRecapPoster(
        IHttpClientFactory http, SettingsStore settings, MatchHistoryStore history, SessionStore sessions,
        RankBook ranks, LobbyTracker tracker, ThemeStore theme)
    {
        _http = http.CreateClient("discord-webhook");
        _settings = settings;
        _history = history;
        _sessions = sessions;
        _ranks = ranks;
        _tracker = tracker;
        _theme = theme;
        tracker.Changed += OnSnapshot;
    }

    public static bool IsWebhookUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        && (u.Host is "discord.com" or "discordapp.com" or "ptb.discord.com" or "canary.discord.com")
        && u.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);

    // Fortnite closed after you played: the session is over. (The app also sees "not running"
    // right after starting while Fortnite is closed; that must not post an old session.)
    private void OnSnapshot(LobbySnapshot s)
    {
        if (s is { InMatch: true, MatchStartedUtc: { } started } && LobbyTracker.IsLive(started)) _playedLive = true;
        var closed = _wasRunning && !s.GameRunning;
        _wasRunning = s.GameRunning;
        if (!closed || !_playedLive || !_settings.AutoPostRecap) return;
        _playedLive = false;
        _ = PostLatestAsync(onlyIfNew: true);
    }

    /// <returns>A message for the user: what was posted, or why nothing was.</returns>
    public async Task<string> PostLatestAsync(bool onlyIfNew)
    {
        var url = _settings.DiscordWebhookUrl;
        if (!IsWebhookUrl(url)) return "Add a Discord webhook link first.";

        var recap = SessionRecaps.Latest(_history.Recent(300), _sessions.All, _ranks, _tracker.SelfId);
        if (recap is null) return "No session to post yet.";
        if (onlyIfNew && _settings.LastRecapPostedUtc is { } last && last >= recap.StartedUtc) return "Already posted.";

        try
        {
            using var res = await _http.PostAsJsonAsync(url, Payload(recap));
            if (!res.IsSuccessStatusCode) return $"Discord refused the post ({(int)res.StatusCode}). Check the webhook link.";
        }
        catch (HttpRequestException)
        {
            return "Couldn't reach Discord. Check your connection.";
        }
        _settings.SetLastRecapPosted(recap.StartedUtc);
        return "Posted to Discord ✓";
    }

    private object Payload(SessionRecap r)
    {
        var name = _tracker.Last?.LocalName ?? "I";
        var hours = r.Minutes >= 60 ? $"{(int)(r.Minutes / 60)}h{(int)(r.Minutes % 60):00}" : $"{(int)r.Minutes} min";
        var fields = new List<object>
        {
            new { name = "Matches", value = r.Matches.ToString(CultureInfo.InvariantCulture), inline = true },
            new { name = "Wins", value = r.Wins?.ToString(CultureInfo.InvariantCulture) ?? "–", inline = true },
            new { name = "Kills", value = r.Kills?.ToString(CultureInfo.InvariantCulture) ?? "–", inline = true },
        };
        if (r.Kd is { } kd) fields.Add(new { name = "K/D", value = kd.ToString("0.00", CultureInfo.InvariantCulture), inline = true });
        if (r.RankChanges.Count > 0) fields.Add(new { name = "Rank", value = string.Join("\n", r.RankChanges), inline = false });
        if (r.Nemesis is { } nemesis) fields.Add(new { name = "Nemesis", value = nemesis, inline = true });

        var accent = _theme.Colors.Accent;
        return new
        {
            username = "Fortnite Tracker",
            embeds = new[]
            {
                new
                {
                    title = $"Session recap · {r.StartedUtc.ToLocalTime().ToString("ddd d MMM", CultureInfo.InvariantCulture)}",
                    description = $"{name} played {r.Matches} {(r.Matches == 1 ? "match" : "matches")} ({hours}), mostly {r.TopMode}.",
                    color = (accent.R << 16) | (accent.G << 8) | accent.B,
                    fields,
                    footer = new { text = "Fortnite Tracker" },
                    timestamp = r.EndedUtc.ToString("o", CultureInfo.InvariantCulture),
                },
            },
        };
    }
}
