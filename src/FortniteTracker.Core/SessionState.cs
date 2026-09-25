namespace FortniteTracker.Core;

public sealed record MatchRecord(
    DateTime StartedUtc,
    DateTime? EndedUtc,
    string Mode,
    string? Playlist,
    int SquadSize,
    bool Finished);

/// <summary>Raised by the log tailer when it starts reading a (new) log file, i.e. a new game session.</summary>
public sealed record LogFileOpened : GameEvent;

/// <summary>
/// The state machine over game events: who you are, your party, the selected playlist, and the
/// current match. Used live by <see cref="LobbyTracker"/> and offline to import old logs into history.
/// Not thread-safe; callers serialize access.
/// </summary>
public sealed class SessionState
{
    private readonly List<string> _party = [];

    public string? SelfId { get; private set; }
    public string? SelfName { get; private set; }
    public IReadOnlyList<string> Party => _party;
    public string? Playlist { get; private set; }
    public string? Level { get; private set; }
    public DateTime? MatchStartedUtc { get; private set; }
    public bool GameRunning { get; private set; } = true;
    public bool InMatch => MatchStartedUtc is not null;
    public string Mode => PlaylistNames.Describe(Playlist, InMatch ? Level : null);

    private int _squadSizeAtStart;
    private bool _playlistConfirmed;

    /// <summary>A match finished (placement reached) or was abandoned.</summary>
    public event Action<MatchRecord>? MatchCompleted;

    /// <summary>Applies an event; returns false when it changed nothing.</summary>
    public bool Apply(GameEvent e)
    {
        switch (e)
        {
            case LogFileOpened:
                CompleteOpenMatch(endedUtc: null);
                _party.Clear();
                Playlist = null;
                Level = null;
                return true;
            case LocalPlayerDetected d:
                SelfId = d.AccountId;
                SelfName = d.DisplayName;
                _party.Remove(d.AccountId);
                return true;
            case PartyMemberJoined j when j.AccountId != SelfId && !_party.Contains(j.AccountId):
                _party.Add(j.AccountId);
                return true;
            case PartyMemberLeft l when l.AccountId == SelfId:
            case LocalPartyLeft:
                if (_party.Count == 0) return false;
                _party.Clear();
                return true;
            case PartyMemberLeft l:
                return _party.Remove(l.AccountId);
            // Only your own presence is trustworthy (friends outside the party appear too). Your
            // presence is re-published a few seconds into each match with that match's playlist,
            // which also covers the first match of a session (no presence line before it). After
            // that, the playlist is locked until the match ends.
            case PlaylistSeen p when p.UserId == SelfId && (!InMatch || !_playlistConfirmed):
                if (InMatch) _playlistConfirmed = true;
                if (p.Playlist == Playlist) return false;
                Playlist = p.Playlist;
                return true;
            case MatchStarted m:
                CompleteOpenMatch(endedUtc: null); // previous match never reached placement
                MatchStartedUtc = m.At;
                Level = m.Level;
                _squadSizeAtStart = _party.Count + 1;
                _playlistConfirmed = false;
                return true;
            case MatchEnded m when InMatch:
                Complete(m.At, finished: true);
                return true;
            case GameRunningChanged g when g.Running != GameRunning:
                GameRunning = g.Running;
                // Closed mid-match: no placement line is written. The end time stays unknown, since
                // this signal can arrive long after the game closed (e.g. the app started later).
                if (!g.Running) CompleteOpenMatch(endedUtc: null);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Records a match that is still open (e.g. at the end of an old log) as abandoned.</summary>
    public void CompleteOpenMatch(DateTime? endedUtc)
    {
        if (InMatch) Complete(endedUtc, finished: false);
    }

    private void Complete(DateTime? endedUtc, bool finished)
    {
        var record = new MatchRecord(
            MatchStartedUtc!.Value, endedUtc, PlaylistNames.Describe(Playlist, Level), Playlist, _squadSizeAtStart, finished);
        MatchStartedUtc = null;
        MatchCompleted?.Invoke(record);
    }
}
