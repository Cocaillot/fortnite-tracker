namespace FortniteTracker.Core;

/// <summary>How your rank moved in one match, from the rank updates just before and after it.</summary>
public sealed record RankMove(string Track, string TrackName, RankPoint Before, RankPoint After)
{
    public string BeforeName => RankNames.Name(Before.Current);
    public string AfterName => RankNames.Name(After.Current);
    /// <summary>Progress change in percentage points, counting a whole rank as 100.</summary>
    public double Delta => Math.Round(((After.Current + After.Progress) - (Before.Current + Before.Progress)) * 100, 1);
}

public sealed record Teammate(string AccountId, string? Name, PlayerStats Stats);

public sealed record MatchDetail(
    MatchRecord Match,
    PlayerStats? Eliminator,
    IReadOnlyList<Teammate> Party,
    RankMove? Rank);

/// <summary>Your record with one party member, from match history.</summary>
public sealed record TeammateSummary(
    string AccountId,
    string? Name,
    int Matches,
    int Wins,
    int? Kills,
    double Minutes,
    DateTime LastPlayedUtc,
    // Matches where the result (wins, kills) is known, to put Wins and Kills in context.
    int Tracked);

/// <summary>Per-match details and per-teammate records, built from history, ranks and stats.</summary>
public sealed class MatchInsights(MatchHistoryStore history, RankBook ranks, FortniteStatsService stats, LobbyTracker tracker)
{
    // Fortnite re-sends ranks within a few minutes of a match ending.
    private static readonly TimeSpan RankUpdateWindow = TimeSpan.FromMinutes(15);

    public async Task<MatchDetail?> GetDetailAsync(DateTime startedUtc, CancellationToken ct)
    {
        if (history.Get(startedUtc) is not { } match) return null;

        var eliminator = match.EliminatedBy is { } name
            ? await stats.GetByDisplayNameAsync(name, ct)
            : null;
        var party = await Task.WhenAll((match.PartyIds ?? []).Select(async id =>
        {
            var s = await stats.GetByAccountIdAsync(id, ct);
            return new Teammate(id, s.EpicName, s);
        }));

        return new MatchDetail(match, eliminator, party, RankMoveFor(match));
    }

    /// <summary>
    /// The mode whose rank updated right after the match is the one it was played in; its last
    /// update before the match is the starting point.
    /// </summary>
    public RankMove? RankMoveFor(MatchRecord match)
    {
        if (tracker.SelfId is not { } self || match.EndedUtc is not { } ended) return null;
        foreach (var season in ranks.Seasons(self))
        {
            var points = ranks.History(self, season.Track, season.TrackGuid);
            var after = points.FirstOrDefault(p => p.At >= ended && p.At <= ended + RankUpdateWindow);
            var before = points.LastOrDefault(p => p.At <= match.StartedUtc);
            if (after is not null && before is not null)
                return new RankMove(season.Track, RankNames.TrackName(season.Track), before, after);
        }
        return null;
    }

    public IReadOnlyList<TeammateSummary> Teammates()
    {
        var byMate = new Dictionary<string, List<MatchRecord>>();
        foreach (var m in history.Recent(1000))
            foreach (var id in m.PartyIds ?? [])
                (byMate.TryGetValue(id, out var list) ? list : byMate[id] = []).Add(m);

        return byMate
            .Select(kv =>
            {
                var ms = kv.Value;
                var tracked = ms.Where(m => m.Kills is not null).ToList();
                return new TeammateSummary(
                    kv.Key,
                    null,
                    ms.Count,
                    ms.Count(m => m.Won == true),
                    tracked.Count > 0 ? tracked.Sum(m => m.Kills!.Value) : null,
                    ms.Sum(m => m.EndedUtc is { } e ? (e - m.StartedUtc).TotalMinutes : 0),
                    ms.Max(m => m.StartedUtc),
                    tracked.Count);
            })
            .OrderByDescending(t => t.Matches)
            .ToList();
    }

    /// <summary>Teammates with names filled in from stats lookups (cached).</summary>
    public async Task<IReadOnlyList<TeammateSummary>> TeammatesWithNamesAsync(CancellationToken ct)
    {
        var list = Teammates();
        var named = await Task.WhenAll(list.Select(async t =>
            t with { Name = (await stats.GetByAccountIdAsync(t.AccountId, ct)).EpicName }));
        return named;
    }
}
