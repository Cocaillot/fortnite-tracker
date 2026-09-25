using System.Globalization;
using System.Text.RegularExpressions;

namespace FortniteTracker.Core;

public abstract record GameEvent
{
    /// <summary>UTC time from the log line; for events not read from the log, when they happened.</summary>
    public DateTime At { get; init; }
}

public sealed record LocalPlayerDetected(string AccountId, string DisplayName) : GameEvent;
public sealed record PartyMemberJoined(string AccountId) : GameEvent;
public sealed record PartyMemberLeft(string AccountId) : GameEvent;
public sealed record LocalPartyLeft : GameEvent;
public sealed record MatchStarted(string Level) : GameEvent;
public sealed record MatchEnded : GameEvent;

/// <summary>
/// The game loaded the main menu locally ("Browse: /Game/Maps/Frontend"). Leaving a match before
/// being eliminated writes no placement line, so this is how that shows up. Joining a match also
/// browses to Frontend, but on a server address ("1.2.3.4:9000/Game/Maps/Frontend").
/// </summary>
public sealed record ReturnedToMenu : GameEvent;
public sealed record GameRunningChanged(bool Running) : GameEvent;

/// <summary>
/// A player's presence advertised a playlist. Friends outside the party appear too, and everyone
/// but the local player has a redacted ID, so consumers must only trust the local player's line.
/// </summary>
public sealed record PlaylistSeen(string UserId, string Playlist) : GameEvent;

/// <summary>
/// The camera switched to a player, identified by their in-game display name. This is the only
/// place the log names other players: after your team is eliminated, the camera follows whoever
/// eliminated it, then whoever you spectate next. Streamer Mode players appear as e.g. "Anonyme[272]".
/// </summary>
public sealed record ViewTargetChanged(string PlayerName) : GameEvent;

/// <summary>Ranked progress Fortnite fetched for you, your party or friends (one log line can hold many).</summary>
public sealed record RanksSeen(IReadOnlyList<RankProgress> Ranks) : GameEvent;

/// <summary>
/// Turns FortniteGame.log lines into game events. The log format is undocumented and can change
/// with any Fortnite patch, so every pattern here is covered by tests built from real log lines.
/// Other players in a match appear only as redacted IDs (MCP:xxxxx...xxxxx) and are not resolvable.
/// </summary>
public static partial class FortniteLogParser
{
    [GeneratedRegex(@"\[process_user_login\] Successfully logged in user\. UserId=\[(?<id>[0-9a-f]{32})\] DisplayName=\[(?<name>[^\]]+)\]")]
    private static partial Regex Login();

    [GeneratedRegex(@"OnEpicPartyMember(?<kind>Joined|Left) PartyMember=\[[^(]*\((?<id>[0-9a-f]{32})\)")]
    private static partial Regex PartyMember();

    [GeneratedRegex(@"OnEpicPartyLeft\b")]
    private static partial Regex PartyLeft();

    [GeneratedRegex(@"LogNet: Welcomed by server \(Level: (?<level>[^?)]+)")]
    private static partial Regex Welcomed();

    [GeneratedRegex(@"LocalPlacementChanged We now have placement")]
    private static partial Regex Placement();

    [GeneratedRegex(@"LogNet: Browse: /Game/Maps/Frontend")]
    private static partial Regex MainMenu();

    [GeneratedRegex(@"\[Presence\.Parse\] user=MCP:(?<id>[0-9a-f.]+) .*? Playlist=(?<playlist>Playlist_[A-Za-z0-9_]+)")]
    private static partial Regex Presence();

    [GeneratedRegex(@"LogFortViewTarget: \[[0-9a-f]+\] trying to set view target from .+? to Pawn:(?<name>.+?)\s*$")]
    private static partial Regex ViewTarget();

    [GeneratedRegex(@"\[\d+\]$")]
    private static partial Regex AnonymousName();

    /// <summary>Streamer Mode hides names as "Anonyme[272]" / "Anonymous[272]"; Epic names can't contain brackets.</summary>
    public static bool IsAnonymous(string displayName) => AnonymousName().IsMatch(displayName);

    public static GameEvent? Parse(string line)
    {
        var e = ParseEvent(line);
        return e is null ? null : e with { At = ParseTimestamp(line) };
    }

    private static GameEvent? ParseEvent(string line)
    {
        if (RankParser.Parse(line) is { Count: > 0 } ranks) return new RanksSeen(ranks);
        if (Login().Match(line) is { Success: true } l)
            return new LocalPlayerDetected(l.Groups["id"].Value, l.Groups["name"].Value);
        if (PartyMember().Match(line) is { Success: true } p)
            return p.Groups["kind"].Value == "Joined"
                ? new PartyMemberJoined(p.Groups["id"].Value)
                : new PartyMemberLeft(p.Groups["id"].Value);
        if (PartyLeft().IsMatch(line)) return new LocalPartyLeft();
        if (Welcomed().Match(line) is { Success: true } w) return new MatchStarted(w.Groups["level"].Value);
        if (Placement().IsMatch(line)) return new MatchEnded();
        if (MainMenu().IsMatch(line)) return new ReturnedToMenu();
        if (Presence().Match(line) is { Success: true } pr)
            return new PlaylistSeen(pr.Groups["id"].Value, pr.Groups["playlist"].Value);
        if (ViewTarget().Match(line) is { Success: true } v)
            return new ViewTargetChanged(v.Groups["name"].Value);
        return null;
    }

    // Lines start with "[2026.09.25-02.38.08:865]", in UTC.
    private static DateTime ParseTimestamp(string line) =>
        line.Length >= 25 && line[0] == '[' && DateTime.TryParseExact(
            line.AsSpan(1, 23), "yyyy.MM.dd-HH.mm.ss:fff", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : DateTime.UtcNow;
}
