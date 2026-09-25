using System.Text.RegularExpressions;

namespace FortniteTracker.Core;

public abstract record GameEvent;
public sealed record LocalPlayerDetected(string AccountId, string DisplayName) : GameEvent;
public sealed record PartyMemberJoined(string AccountId) : GameEvent;
public sealed record PartyMemberLeft(string AccountId) : GameEvent;
public sealed record LocalPartyLeft : GameEvent;
public sealed record MatchStarted(string Level) : GameEvent;
public sealed record MatchEnded : GameEvent;
public sealed record GameRunningChanged(bool Running) : GameEvent;

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

    public static GameEvent? Parse(string line)
    {
        if (Login().Match(line) is { Success: true } l)
            return new LocalPlayerDetected(l.Groups["id"].Value, l.Groups["name"].Value);
        if (PartyMember().Match(line) is { Success: true } p)
            return p.Groups["kind"].Value == "Joined"
                ? new PartyMemberJoined(p.Groups["id"].Value)
                : new PartyMemberLeft(p.Groups["id"].Value);
        if (PartyLeft().IsMatch(line)) return new LocalPartyLeft();
        if (Welcomed().Match(line) is { Success: true } w) return new MatchStarted(w.Groups["level"].Value);
        if (Placement().IsMatch(line)) return new MatchEnded();
        return null;
    }
}
