namespace FortniteTracker.Core;

/// <summary>How a player relates to you; decides what we can know about them.</summary>
public enum Relation { You, Party, Friend, Followed, Opponent }

/// <summary>Your history with a player, from match history (matched by the name seen in-game).</summary>
public sealed record Encounters(int EliminatedYou, DateTime? LastEliminatedYouUtc);

public sealed record PlayerProfile(
    string? AccountId,
    string Name,
    Relation Relation,
    bool Followed,
    PlayerStats Season,
    PlayerStats Lifetime,
    IReadOnlyList<RankProgress> Ranks,
    Encounters Encounters);

public sealed record LeaderboardEntry(
    string? AccountId,
    string? Name,
    Relation Relation,
    PlayerStats Stats,
    IReadOnlyList<RankProgress> Ranks);

/// <summary>
/// Builds player profiles and the friends leaderboard from stats lookups, the rank book, match
/// history and your followed list.
/// </summary>
public sealed class PlayerDirectory(
    FortniteStatsService stats, RankBook ranks, MatchHistoryStore history, SettingsStore settings, LobbyTracker tracker)
{
    // Friends are listed when Fortnite fetched a current-season rank for them; this caps lookups.
    private const int MaxFriends = 60;
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromMilliseconds(700);

    public Relation RelationOf(string? accountId, string? name)
    {
        if (accountId is not null && accountId == tracker.SelfId) return Relation.You;
        if (accountId is not null && tracker.PartyIds.Contains(accountId)) return Relation.Party;
        // Fortnite only fetches ranks for you, your party and friends.
        if (accountId is not null && ranks.For(accountId).Count > 0) return Relation.Friend;
        if (settings.IsFollowed(accountId, name)) return Relation.Followed;
        return Relation.Opponent;
    }

    /// <summary>A full profile for an account ID or, for opponents, the name seen in-game.</summary>
    public async Task<PlayerProfile> GetProfileAsync(string? accountId, string? name, CancellationToken ct)
    {
        var season = accountId is not null
            ? await stats.GetByAccountIdAsync(accountId, ct)
            : await stats.GetByDisplayNameAsync(name ?? "", ct);
        // Once the account is known, everything else is looked up by ID (names can be console names).
        var id = accountId ?? season.AccountId;
        var lifetime = id is not null
            ? await stats.GetByAccountIdAsync(id, ct, StatsWindow.Lifetime)
            : season with { Overall = null, ByMode = null };
        var displayName = name ?? season.EpicName ?? lifetime.EpicName ?? "Unknown player";

        var eliminations = history.Recent(1000)
            .Where(m => m.EliminatedBy is { } e && (string.Equals(e, displayName, StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(e, season.EpicName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new PlayerProfile(
            id,
            displayName,
            RelationOf(id, displayName),
            settings.IsFollowed(id, displayName),
            season,
            lifetime,
            id is null ? [] : ranks.For(id).OrderByDescending(r => r.LastUpdatedUtc).ToList(),
            new Encounters(eliminations.Count, eliminations.FirstOrDefault()?.StartedUtc));
    }

    /// <summary>
    /// You, your party, friends with a current-season rank and followed players. Reports the list
    /// right away with ranks, then again as stats arrive (lookups are rate-limited).
    /// </summary>
    public async Task BuildLeaderboardAsync(Action<IReadOnlyList<LeaderboardEntry>> report, CancellationToken ct)
    {
        var people = new List<(string? Id, string? Name, Relation Relation)>();
        void AddPerson(string? id, string? name, Relation r)
        {
            if (people.Any(p => (id is not null && p.Id == id) || (id is null && name is not null && p.Name == name))) return;
            people.Add((id, name, r));
        }

        if (tracker.SelfId is { } self) AddPerson(self, tracker.Last?.LocalName, Relation.You);
        foreach (var id in tracker.PartyIds) AddPerson(id, null, Relation.Party);
        foreach (var id in ranks.Accounts
                     .Where(a => ranks.Latest(a) is { IsCurrentSeason: true })
                     .OrderByDescending(a => ranks.Latest(a)!.LastUpdatedUtc)
                     .Take(MaxFriends))
            AddPerson(id, null, Relation.Friend);
        foreach (var f in settings.Followed) AddPerson(f.AccountId, f.Name, Relation.Followed);

        var entries = people
            .Select(p => new LeaderboardEntry(p.Id, p.Name, p.Relation, new PlayerStats(p.Id, p.Name, StatsStatus.Loading),
                p.Id is null ? [] : ranks.For(p.Id)))
            .ToArray();
        report(entries);

        var lastReport = DateTime.UtcNow;
        var gate = new object();
        await Task.WhenAll(entries.Select(async (e, i) =>
        {
            var s = e.AccountId is not null
                ? await stats.GetByAccountIdAsync(e.AccountId, ct)
                : await stats.GetByDisplayNameAsync(e.Name!, ct);
            lock (gate)
            {
                entries[i] = e with { Stats = s, Name = e.Name ?? s.EpicName };
                if (DateTime.UtcNow - lastReport < UpdateEvery) return;
                lastReport = DateTime.UtcNow;
                report([.. entries]);
            }
        }));
        report(entries);
    }
}
