namespace FortniteTracker.Core;

/// <summary>How your rank moved in one match, from the rank updates just before and after it.</summary>
/// <remarks>
/// <see cref="Matches"/> is more than 1 when Fortnite only updated the rank after several ranked
/// matches in a row ("Play again" skips the lobby, where ranks are refreshed): the change covers them all.
/// </remarks>
public sealed record RankMove(string Track, string TrackName, RankPoint Before, RankPoint After, int Matches = 1)
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

    // Epic's rank timestamp can be a little earlier than the end we read from the log.
    private static readonly TimeSpan EndSlack = TimeSpan.FromMinutes(1);

    private static readonly string[] Games = ["Reload", "Battle Royale", "OG"];

    // "Ranked Reload Duos · Build" and the "Reload" track are the same game; unknown on either side matches.
    private static bool SameGame(string mode, string trackName)
    {
        var a = Games.FirstOrDefault(g => mode.Contains(g, StringComparison.Ordinal));
        var b = Games.FirstOrDefault(g => trackName.StartsWith(g, StringComparison.Ordinal));
        return a is null || b is null || a == b;
    }

    public static bool IsRanked(MatchRecord m) =>
        m.Playlist?.Contains("Habanero", StringComparison.OrdinalIgnoreCase) ?? m.Mode.StartsWith("Ranked", StringComparison.Ordinal);

    /// <summary>
    /// How a ranked match moved your rank. Rank points carry Epic's own update time, so the update
    /// that belongs to a match is the first one after it ends and before the next match starts
    /// (after that, it belongs to a later match). The change is measured from the previous update.
    /// </summary>
    public RankMove? RankMoveFor(MatchRecord match)
    {
        if (tracker.SelfId is not { } self || match.EndedUtc is not { } ended || !IsRanked(match)) return null;
        var limit = ended + RankUpdateWindow;
        if (history.Next(match.StartedUtc) is { } next && next.StartedUtc < limit) limit = next.StartedUtc;

        foreach (var season in ranks.Seasons(self))
        {
            var trackName = RankNames.TrackName(season.Track);
            if (!SameGame(match.Mode, trackName)) continue;
            var points = ranks.History(self, season.Track, season.TrackGuid);
            var i = points.ToList().FindIndex(p => p.At >= ended - EndSlack && p.At > match.StartedUtc && p.At < limit);
            if (i <= 0) continue; // no update for this match here, or nothing to compare it with
            var before = points[i - 1];
            var after = points[i];
            // Ranked matches since the previous update that got no update of their own share this one.
            var covered = history.StartedBetween(before.At, match.StartedUtc).Count(m => IsRanked(m) && SameGame(m.Mode, trackName)) + 1;
            return new RankMove(season.Track, trackName, before, after, covered);
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
