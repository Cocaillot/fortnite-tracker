namespace FortniteTracker.Core;

/// <summary>
/// Works out each match's kills and win from your season stats, which only give running totals.
/// Every change in the totals is used once: it goes to the match that ended just before the stats
/// were updated (fortnite-api's lastModified). When several matches land in one update, or the
/// counts don't add up, those matches are left unknown rather than guessed.
/// </summary>
/// <remarks>
/// fortnite-api can take minutes to record a match, so with short matches the next one often starts
/// before the previous one shows up. Comparing each match with the stats read at its own start
/// then counts the same change several times; this ledger keeps one baseline for all of them.
/// </remarks>
public sealed class MatchResultLedger
{
    /// <summary>Allowed gap between the end of a match in the log and Epic's lastModified.</summary>
    public static readonly TimeSpan Slack = TimeSpan.FromMinutes(2);

    /// <summary>A match that never shows up in the stats (e.g. not counted by Epic) is dropped after this.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(30);

    private readonly object _gate = new();
    private readonly List<(DateTime Started, DateTime Ended)> _pending = [];
    private ModeStats? _baseline;

    public bool HasPending
    {
        get { lock (_gate) return _pending.Count > 0; }
    }

    /// <summary>A live match ended and should show up in the stats.</summary>
    public void Finished(DateTime startedUtc, DateTime endedUtc)
    {
        lock (_gate)
        {
            if (_pending.Any(p => p.Started == startedUtc)) return;
            _pending.Add((startedUtc, endedUtc));
            _pending.Sort((a, b) => a.Ended.CompareTo(b.Ended));
        }
    }

    /// <summary>Feeds a fresh stats reading; returns the matches whose result is now known.</summary>
    public IReadOnlyList<(DateTime StartedUtc, int Kills, bool Won)> Observe(ModeStats stats, DateTime nowUtc)
    {
        lock (_gate)
        {
            _pending.RemoveAll(p => nowUtc - p.Ended > GiveUpAfter);
            if (_baseline is null || stats.Matches < _baseline.Matches)
            {
                // First reading, or the season rolled over: nothing to compare with yet.
                _baseline = stats;
                return [];
            }

            var added = stats.Matches - _baseline.Matches;
            if (added == 0) return [];

            var cutoff = (stats.LastModified ?? nowUtc) + Slack;
            var landed = _pending.Where(p => p.Ended <= cutoff).ToList();
            _pending.RemoveAll(landed.Contains);

            var before = _baseline;
            _baseline = stats;
            // One new match: it's the one that ended closest before the update. Earlier ones in
            // "landed" weren't counted by Epic (e.g. left in the lobby) and stay unknown.
            if (added != 1 || landed.Count == 0) return [];
            return [(landed[^1].Started, stats.Kills - before.Kills, stats.Wins > before.Wins)];
        }
    }
}
