using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FortniteTracker.Core;

/// <summary>
/// Adds details the log doesn't have to match history, using stats lookups:
/// <list type="bullet">
/// <item>Kills and win per match (experimental): your season stats are read when a live match
/// starts and polled after it ends until the match shows up; the difference is that match.</item>
/// <item>The eliminator's K/D and threat level, from the live eliminator card, and backfilled for
/// older matches so the session dashboard can show which kind of player eliminates you.</item>
/// </list>
/// </summary>
public sealed class MatchResultTracker
{
    // How long after a match to look for it in your stats. fortnite-api usually has it within a minute.
    private static readonly TimeSpan[] PollDelays =
        [TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8)];

    private const int BackfillLimit = 60;

    private readonly FortniteStatsService _stats;
    private readonly MatchHistoryStore _history;
    private readonly ILogger<MatchResultTracker> _logger;
    private readonly ConcurrentDictionary<DateTime, (string SelfId, Task<PlayerStats> Before)> _live = new();
    private int _backfillRunning;

    public MatchResultTracker(LobbyTracker tracker, FortniteStatsService stats, MatchHistoryStore history, ILogger<MatchResultTracker> logger)
    {
        _stats = stats;
        _history = history;
        _logger = logger;

        tracker.LiveMatchStarted += (startedUtc, selfId) =>
            _live[startedUtc] = (selfId, _stats.GetFreshByAccountIdAsync(selfId, CancellationToken.None));
        tracker.MatchCompleted += OnMatchCompleted;
        tracker.Changed += OnSnapshot;
    }

    /// <summary>Test hook: replaces real waiting between polls.</summary>
    public Func<TimeSpan, Task> Delay { get; init; } = Task.Delay;

    private void OnMatchCompleted(MatchRecord match)
    {
        // Raised twice per match (placement, then eliminator); TryRemove makes it run once.
        if (_live.TryRemove(match.StartedUtc, out var live))
            _ = ResolveResultAsync(match.StartedUtc, live.SelfId, live.Before);
    }

    private async Task ResolveResultAsync(DateTime startedUtc, string selfId, Task<PlayerStats> beforeTask)
    {
        try
        {
            var before = (await beforeTask).Overall;
            if (before is null) return;

            foreach (var delay in PollDelays)
            {
                await Delay(delay);
                var after = (await _stats.GetFreshByAccountIdAsync(selfId, CancellationToken.None)).Overall;
                if (after is null || after.Matches == before.Matches) continue;

                // More than one new match (e.g. the previous one landed late): can't attribute it.
                if (after.Matches - before.Matches != 1)
                {
                    _logger.LogInformation("Match {Start}: {Count} matches landed at once; result unknown", startedUtc, after.Matches - before.Matches);
                    return;
                }
                _history.Update(startedUtc, m => m with { Kills = after.Kills - before.Kills, Won = after.Wins > before.Wins });
                return;
            }
            _logger.LogInformation("Match {Start} never showed up in stats", startedUtc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve the result of match {Start}", startedUtc);
        }
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
