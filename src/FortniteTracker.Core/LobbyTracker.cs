namespace FortniteTracker.Core;

public sealed record LobbySnapshot(
    bool GameRunning,
    bool InMatch,
    string? LocalName,
    string Mode,
    DateTime? MatchStartedUtc,
    IReadOnlyList<PlayerStats> Squad);

/// <summary>
/// Live view of the current session: publishes a debounced snapshot with squad stats whenever
/// the state changes, and forwards finished matches to history.
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

    /// <summary>Re-fetches stats for the current squad (e.g. after the API key changes).</summary>
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

        string[] ids;
        LobbySnapshot partial;
        lock (_gate)
        {
            ids = _state.SelfId is null ? [.. _state.Party] : [_state.SelfId, .. _state.Party];
            partial = new LobbySnapshot(
                _state.GameRunning, _state.InMatch, _state.SelfName, _state.Mode, _state.MatchStartedUtc, []);
        }

        var squad = await Task.WhenAll(ids.Select(id => _stats.GetByAccountIdAsync(id, CancellationToken.None)));
        if (ct.IsCancellationRequested) return; // a newer state is already on its way

        var snapshot = partial with { Squad = squad };
        Last = snapshot;
        Changed?.Invoke(snapshot);
    }
}
