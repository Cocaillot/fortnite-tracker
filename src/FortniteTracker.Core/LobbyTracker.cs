namespace FortniteTracker.Core;

public sealed record LobbySnapshot(
    bool GameRunning,
    bool InMatch,
    string? LocalName,
    string Mode,
    DateTime? MatchStartedUtc,
    IReadOnlyList<PlayerStats> Squad,
    PlayerStats? EliminatedBy,
    IReadOnlyList<PlayerStats> Spectated,
    // fortnite-api bucket for the current/last mode ("duo", "ltm"...; null = all modes) and its label.
    string? StatsBucket,
    string StatsLabel,
    // The match whose eliminator is shown, until the next match starts.
    DateTime? LastMatchStartedUtc,
    DateTime? LastMatchEndedUtc);

/// <summary>
/// Live view of the current session: publishes a debounced snapshot with stats for your squad,
/// the player who eliminated your team, and players you spectated after that. Also forwards
/// finished matches to history.
/// </summary>
public sealed class LobbyTracker
{
    // Events older than this come from replaying the log at startup, not from live play.
    private static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PlaceholderDelay = TimeSpan.FromMilliseconds(150);

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

    public string? SelfId
    {
        get { lock (_gate) return _state.SelfId; }
    }

    public IReadOnlyList<string> PartyIds
    {
        get { lock (_gate) return [.. _state.Party]; }
    }

    public event Action<LobbySnapshot>? Changed;
    public event Action<MatchRecord>? MatchCompleted;

    /// <summary>A match started just now (not during the startup replay): start time and your account ID.</summary>
    public event Action<DateTime, string>? LiveMatchStarted;

    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(300);

    public static bool IsLive(DateTime at) => at > DateTime.UtcNow - LiveWindow;

    public void Handle(GameEvent e)
    {
        bool changed;
        string? selfId;
        lock (_gate)
        {
            changed = _state.Apply(e);
            selfId = _state.SelfId;
        }
        if (changed && e is MatchStarted m && selfId is not null && IsLive(m.At)) LiveMatchStarted?.Invoke(m.At, selfId);
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
            var (bucket, label) = PlaylistNames.StatsBucket(_state.Playlist);
            partial = new LobbySnapshot(
                _state.GameRunning, _state.InMatch, _state.SelfName, _state.Mode, _state.MatchStartedUtc,
                [], null, [], bucket, label, _state.LastFinished?.StartedUtc, _state.LastFinished?.EndedUtc);
        }

        var squad = Task.WhenAll(ids.Select(id => _stats.GetByAccountIdAsync(id, CancellationToken.None)));
        var others = Task.WhenAll(opponents.Select(name => _stats.GetByDisplayNameAsync(name, CancellationToken.None)));
        var all = Task.WhenAll(squad, others);

        // Show names with loading placeholders when stats aren't cached; skip it when they are,
        // so the cards don't flicker.
        if (await Task.WhenAny(all, Task.Delay(PlaceholderDelay)) != all && !ct.IsCancellationRequested)
        {
            Publish(partial with
            {
                Squad = ids.Select(id => Previous(id) ?? new PlayerStats(id, null, StatsStatus.Loading)).ToArray(),
                EliminatedBy = opponents.Length > 0 ? new PlayerStats(null, opponents[0], StatsStatus.Loading) : null,
                Spectated = opponents.Skip(1).Select(n => new PlayerStats(null, n, StatsStatus.Loading)).ToArray(),
            });
        }

        await all;
        if (ct.IsCancellationRequested) return; // a newer state is already on its way

        Publish(partial with
        {
            Squad = squad.Result,
            EliminatedBy = others.Result.FirstOrDefault(),
            Spectated = others.Result.Skip(1).ToArray(),
        });
    }

    // Squad members already shown keep their stats while fresher ones load.
    private PlayerStats? Previous(string accountId) =>
        Last?.Squad.FirstOrDefault(p => p.AccountId == accountId && p.Status == StatsStatus.Ok);

    private void Publish(LobbySnapshot snapshot)
    {
        Last = snapshot;
        Changed?.Invoke(snapshot);
    }
}
