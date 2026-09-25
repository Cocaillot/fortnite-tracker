namespace FortniteTracker.Core;

public sealed record LobbySnapshot(
    bool GameRunning,
    bool InMatch,
    string? LocalName,
    string Mode,
    DateTime? MatchStartedUtc,
    IReadOnlyList<PlayerStats> Squad,
    PlayerStats? EliminatedBy,
    IReadOnlyList<PlayerStats> Spectated);

/// <summary>
/// Live view of the current session: publishes a debounced snapshot with stats for your squad,
/// the player who eliminated your team, and players you spectated after that. Also forwards
/// finished matches to history.
/// </summary>
public sealed class LobbyTracker
{
    private readonly FortniteStatsService _stats;
    private readonly object _gate = new();
    private readonly SessionState _state = new();
    private CancellationTokenSource? _debounce;

    public LobbyTracker(FortniteStatsService stats)
    {
        _stats = stats;
        _state.MatchCompleted += m => MatchCompleted?.Invoke(m);
    }

    public LobbySnapshot? Last { get; private set; }

    public event Action<LobbySnapshot>? Changed;
    public event Action<MatchRecord>? MatchCompleted;

    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(300);

    public void Handle(GameEvent e)
    {
        bool changed;
        lock (_gate) changed = _state.Apply(e);
        if (changed) SchedulePublish();
    }

    /// <summary>Re-fetches stats (e.g. after the API key changes).</summary>
    public void Refresh() => SchedulePublish();

    // Startup replays the whole log; debouncing collapses that burst into one publish.
    private void SchedulePublish()
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _debounce, cts)?.Cancel();
        _ = PublishAfterDelayAsync(cts.Token);
    }

    private async Task PublishAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Debounce, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        string[] ids, opponents;
        LobbySnapshot partial;
        lock (_gate)
        {
            ids = _state.SelfId is null ? [.. _state.Party] : [_state.SelfId, .. _state.Party];
            opponents = [.. _state.Spectated];
            partial = new LobbySnapshot(
                _state.GameRunning, _state.InMatch, _state.SelfName, _state.Mode, _state.MatchStartedUtc, [], null, []);
        }

        var squad = Task.WhenAll(ids.Select(id => _stats.GetByAccountIdAsync(id, CancellationToken.None)));
        var others = Task.WhenAll(opponents.Select(name => _stats.GetByDisplayNameAsync(name, CancellationToken.None)));
        await Task.WhenAll(squad, others);
        if (ct.IsCancellationRequested) return; // a newer state is already on its way

        var snapshot = partial with
        {
            Squad = squad.Result,
            EliminatedBy = others.Result.FirstOrDefault(),
            Spectated = others.Result.Skip(1).ToArray(),
        };
        Last = snapshot;
        Changed?.Invoke(snapshot);
    }
}
