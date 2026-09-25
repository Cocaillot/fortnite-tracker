namespace FortniteTracker.Core;

public sealed record LobbySnapshot(bool GameRunning, bool InMatch, string? LocalName, IReadOnlyList<PlayerStats> Squad);

/// <summary>
/// Keeps the current state (you, your party, whether you're in a match) and publishes
/// a debounced snapshot with stats whenever it changes.
/// </summary>
public sealed class LobbyTracker(FortniteStatsService stats)
{
    private readonly object _gate = new();
    private readonly List<string> _party = [];
    private string? _selfId;
    private string? _selfName;
    private bool _inMatch;
    private bool _gameRunning = true;
    private CancellationTokenSource? _debounce;

    public LobbySnapshot? Last { get; private set; }

    public event Action<LobbySnapshot>? Changed;

    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(300);

    public void Handle(GameEvent e)
    {
        lock (_gate)
        {
            switch (e)
            {
                case LocalPlayerDetected d:
                    _selfId = d.AccountId;
                    _selfName = d.DisplayName;
                    _party.Remove(d.AccountId);
                    break;
                case PartyMemberJoined j when j.AccountId != _selfId && !_party.Contains(j.AccountId):
                    _party.Add(j.AccountId);
                    break;
                case PartyMemberLeft l when l.AccountId == _selfId:
                    _party.Clear();
                    break;
                case PartyMemberLeft l:
                    _party.Remove(l.AccountId);
                    break;
                case LocalPartyLeft:
                    _party.Clear();
                    break;
                case MatchStarted:
                    _inMatch = true;
                    break;
                case MatchEnded:
                    _inMatch = false;
                    break;
                case GameRunningChanged g:
                    _gameRunning = g.Running;
                    if (!g.Running) _inMatch = false; // closed mid-match: no placement line is written
                    break;
                default:
                    return;
            }
        }
        SchedulePublish();
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
        bool inMatch, gameRunning;
        string? selfName;
        lock (_gate)
        {
            ids = _selfId is null ? [.. _party] : [_selfId, .. _party];
            inMatch = _inMatch;
            gameRunning = _gameRunning;
            selfName = _selfName;
        }

        var squad = await Task.WhenAll(ids.Select(id => stats.GetByAccountIdAsync(id, CancellationToken.None)));
        if (ct.IsCancellationRequested) return; // a newer state is already on its way

        var snapshot = new LobbySnapshot(gameRunning, inMatch, selfName, squad);
        Last = snapshot;
        Changed?.Invoke(snapshot);
    }
}
