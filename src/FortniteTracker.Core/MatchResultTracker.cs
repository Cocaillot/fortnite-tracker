using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FortniteTracker.Core;

/// <summary>
/// Adds details the log doesn't have to match history, using stats lookups:
/// <list type="bullet">
/// <item>Kills and win per match (experimental): your season stats are read around live matches and
/// each change is matched to the match that caused it (see <see cref="MatchResultLedger"/>).</item>
/// <item>The eliminator's K/D and threat level, from the live eliminator card, and backfilled for
/// older matches so the session dashboard can show which kind of player eliminates you.</item>
/// </list>
/// </summary>
public sealed class MatchResultTracker
{
    // How long after a match to look for it in your stats. fortnite-api usually has it within a few
    // minutes; after the last delay it keeps checking at that pace until nothing is pending.
    private static readonly TimeSpan[] PollDelays =
        [TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4)];

    private const int BackfillLimit = 60;

    private readonly FortniteStatsService _stats;
    private readonly MatchHistoryStore _history;
    private readonly SessionStore _sessions;
    private readonly ILogger<MatchResultTracker> _logger;
    private readonly MatchResultLedger _ledger = new();
    private readonly ConcurrentDictionary<DateTime, string> _live = new();
    private int _polling;
    private string? _selfId;
    private DateTime _lastStarted;
    private int _backfillRunning;

    public MatchResultTracker(
        LobbyTracker tracker, FortniteStatsService stats, MatchHistoryStore history, SessionStore sessions,
        ILogger<MatchResultTracker> logger)
    {
        _stats = stats;
        _history = history;
        _sessions = sessions;
        _logger = logger;

        tracker.LiveMatchStarted += (startedUtc, selfId) =>
        {
            _live[startedUtc] = selfId;
            _ = ReadStartAsync(startedUtc, selfId);
        };
        tracker.MatchCompleted += OnMatchCompleted;
        tracker.Changed += OnSnapshot;
    }

    /// <summary>Test hook: replaces real waiting between polls.</summary>
    public Func<TimeSpan, Task> Delay { get; init; } = Task.Delay;

    /// <summary>Test hook: the current time.</summary>
    public Func<DateTime> UtcNow { get; init; } = () => DateTime.UtcNow;

    private async Task ReadStartAsync(DateTime startedUtc, string selfId)
    {
        try
        {
            var before = await _stats.GetFreshByAccountIdAsync(selfId, CancellationToken.None);
            _sessions.MatchStarted(startedUtc, before.Overall);
            if (before.Overall is { } o) Apply(o);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read stats at the start of match {Start}", startedUtc);
        }
    }

    private void OnMatchCompleted(MatchRecord match)
    {
        // Raised twice per match (placement, then eliminator); TryRemove makes it run once.
        if (!_live.TryRemove(match.StartedUtc, out var selfId)) return;
        // Creative doesn't count in Battle Royale stats, so it would never show up.
        if (match.Mode == "Creative") return;
        _selfId = selfId;
        _lastStarted = match.StartedUtc;
        _ledger.Finished(match.StartedUtc, match.EndedUtc ?? UtcNow());
        StartPolling();
    }

    private void StartPolling()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 0) _ = PollAsync();
    }

    // One loop for every pending match, so each stats change is only used once.
    // The ledger drops matches that never show up, so the loop ends.
    private async Task PollAsync()
    {
        try
        {
            for (var i = 0; _ledger.HasPending; i++)
            {
                await Delay(PollDelays[Math.Min(i, PollDelays.Length - 1)]);
                var after = (await _stats.GetFreshByAccountIdAsync(_selfId!, CancellationToken.None)).Overall;
                if (after is null) continue;
                _sessions.StatsUpdated(_lastStarted, after); // session totals don't need per-match attribution
                Apply(after);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read match results");
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
        // A match may have finished just as the loop was ending.
        if (_ledger.HasPending) StartPolling();
    }

    private void Apply(ModeStats stats)
    {
        foreach (var (started, kills, won) in _ledger.Observe(stats, UtcNow()))
            _history.Update(started, m => m with { Kills = kills, Won = won });
    }

    private void OnSnapshot(LobbySnapshot s)
    {
        if (s is { LastMatchStartedUtc: { } started, EliminatedBy: { Status: StatsStatus.Ok, Overall: { } o } eliminator })
            _history.Update(started, m => m.EliminatorKd is null ? WithEliminator(m, eliminator, o) : m);
    }

    private static MatchRecord WithEliminator(MatchRecord m, PlayerStats p, ModeStats o) =>
        m with { EliminatorKd = o.Kd, EliminatorThreat = p.Threat };

    /// <summary>
    /// Looks up eliminators of recent matches that don't have their stats yet (e.g. imported from
    /// old logs). Uses current season stats, not stats at the time of the match.
    /// </summary>
    public async Task BackfillEliminatorsAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _backfillRunning, 1) == 1) return;
        try
        {
            var pending = _history.Recent(BackfillLimit)
                .Where(m => m is { EliminatedBy: { } name, EliminatorThreat: null } && !FortniteLogParser.IsAnonymous(name))
                .ToList();
            foreach (var match in pending)
            {
                var p = await _stats.GetByDisplayNameAsync(match.EliminatedBy!, ct);
                if (p.Status is StatsStatus.NoApiKey) return;
                if (p.Status == StatsStatus.Ok && p.Overall is { } o)
                    _history.Update(match.StartedUtc, m => WithEliminator(m, p, o));
                else if (p.Threat is { } threat) // not found: likely a bot
                    _history.Update(match.StartedUtc, m => m with { EliminatorThreat = threat });
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _backfillRunning, 0);
        }
    }
}
