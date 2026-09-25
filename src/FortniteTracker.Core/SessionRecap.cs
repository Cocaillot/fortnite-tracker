namespace FortniteTracker.Core;

/// <summary>A summary of one play session, for posting to Discord.</summary>
public sealed record SessionRecap(
    DateTime StartedUtc,
    DateTime EndedUtc,
    int Matches,
    double Minutes,
    int? Wins,
    int? Kills,
    double? Kd,
    string? TopMode,
    string? Nemesis,
    IReadOnlyList<string> RankChanges);

public static class SessionRecaps
{
    /// <summary>
    /// The most recent session: matches less than <see cref="SessionStore.Gap"/> apart. Wins, kills
    /// and K/D come from the session's stats difference when known, else from tracked matches.
    /// </summary>
    public static SessionRecap? Latest(
        IReadOnlyList<MatchRecord> history, IReadOnlyList<SessionRecord> sessions, RankBook ranks, string? selfId)
    {
        var sorted = history.OrderBy(m => m.StartedUtc).ToList();
        if (sorted.Count == 0) return null;

        var group = new List<MatchRecord> { sorted[^1] };
        for (var i = sorted.Count - 2; i >= 0; i--)
        {
            var end = sorted[i].EndedUtc ?? sorted[i].StartedUtc;
            if (group[0].StartedUtc - end >= SessionStore.Gap) break;
            group.Insert(0, sorted[i]);
        }

        var start = group[0].StartedUtc;
        var finish = group.Max(m => m.EndedUtc ?? m.StartedUtc);
        var delta = sessions.LastOrDefault(s => s.StartedUtc >= start.AddMinutes(-2) && s.StartedUtc <= finish)?.Delta;
        var tracked = group.Where(m => m.Kills is not null).ToList();

        return new SessionRecap(
            start,
            finish,
            group.Count,
            group.Sum(m => m.EndedUtc is { } e ? (e - m.StartedUtc).TotalMinutes : 0),
            delta?.Wins ?? (tracked.Count > 0 ? tracked.Count(m => m.Won == true) : null),
            delta?.Kills ?? (tracked.Count > 0 ? tracked.Sum(m => m.Kills!.Value) : null),
            delta is { Deaths: > 0 } ? delta.Kd : null,
            group.GroupBy(m => m.Mode).OrderByDescending(g => g.Count()).First().Key,
            group.Select(m => m.EliminatedBy).OfType<string>().Where(n => !FortniteLogParser.IsAnonymous(n))
                .GroupBy(n => n).Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key,
            selfId is null ? [] : RankChanges(ranks, selfId, start, finish));
    }

    // "Reload: Bronze I → Silver I (+118%)" for each mode whose rank moved during the session.
    private static List<string> RankChanges(RankBook ranks, string selfId, DateTime start, DateTime finish)
    {
        var list = new List<string>();
        foreach (var season in ranks.Seasons(selfId))
        {
            var points = ranks.History(selfId, season.Track, season.TrackGuid);
            var before = points.LastOrDefault(p => p.At <= start);
            var after = points.LastOrDefault(p => p.At <= finish.AddMinutes(15));
            if (before is null || after is null || after.At <= start) continue;
            var change = Math.Round(((after.Current + after.Progress) - (before.Current + before.Progress)) * 100);
            if (change == 0) continue;
            list.Add($"{RankNames.TrackName(season.Track)}: {RankNames.Name(before.Current)} → {RankNames.Name(after.Current)} ({(change > 0 ? "+" : "")}{change}%)");
        }
        return list;
    }
}
