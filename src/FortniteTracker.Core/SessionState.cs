namespace FortniteTracker.Core;

public sealed record MatchRecord(
    DateTime StartedUtc,
    DateTime? EndedUtc,
    string Mode,
    string? Playlist,
    int SquadSize,
    bool Finished,
    string? EliminatedBy = null,
    // Filled in later by MatchResultTracker, from stats lookups rather than the log:
    int? Kills = null,
    bool? Won = null,
    double? EliminatorKd = null,
    Threat? EliminatorThreat = null,
    // Account IDs of your party members at the start of the match (you excluded).
    IReadOnlyList<string>? PartyIds = null);

/// <summary>Raised by the log tailer when it starts reading a (new) log file, i.e. a new game session.</summary>
public sealed record LogFileOpened : GameEvent;

/// <summary>
/// The state machine over game events: who you are, your party, the selected playlist, the
/// current match, and who eliminated your team. Used live by <see cref="LobbyTracker"/> and
/// offline to import old logs into history. Not thread-safe; callers serialize access.
/// </summary>
public sealed class SessionState
{
    private const int MaxSpectated = 6;

    private readonly List<string> _party = [];
    private readonly List<string> _spectated = [];

    public string? SelfId { get; private set; }
    public string? SelfName { get; private set; }
    public IReadOnlyList<string> Party => _party;
    public string? Playlist { get; private set; }
    public string? Level { get; private set; }
    public DateTime? MatchStartedUtc { get; private set; }
    public bool GameRunning { get; private set; } = true;
    public bool InMatch => MatchStartedUtc is not null;
    public string Mode => PlaylistNames.Describe(Playlist, InMatch ? Level : null);

    /// <summary>Players spectated after your team was eliminated; the first one eliminated it.</summary>
    public IReadOnlyList<string> Spectated => _spectated;
    public string? EliminatedBy => _spectated.Count > 0 ? _spectated[0] : null;

    /// <summary>The match that just reached placement, until the next one starts.</summary>
    public MatchRecord? LastFinished => _lastFinished;

    private int _squadSizeAtStart;
    private string[] _partyAtStart = [];
    private bool _playlistConfirmed;
    private MatchRecord? _lastFinished; // set from placement until the next match starts

    /// <summary>
    /// A match finished (placement reached) or was abandoned. Raised again for the same match
    /// (same StartedUtc) once its eliminator is known.
    /// </summary>
    public event Action<MatchRecord>? MatchCompleted;

    /// <summary>Applies an event; returns false when it changed nothing.</summary>
    public bool Apply(GameEvent e)
    {
        switch (e)
        {
            case LogFileOpened:
                CompleteOpenMatch(endedUtc: null);
                _party.Clear();
                ResetSpectating();
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
                ResetSpectating();
                MatchStartedUtc = m.At;
                Level = m.Level;
                _squadSizeAtStart = _party.Count + 1;
                _partyAtStart = [.. _party];
                _playlistConfirmed = false;
                return true;
            case MatchEnded m when InMatch:
                _lastFinished = Complete(m.At, finished: true);
                return true;
            // Left the match before being eliminated: back in the lobby, match not finished.
            case ReturnedToMenu r when InMatch:
                Complete(r.At, finished: false);
                return true;
            // Before placement the camera follows your own teammates, so only targets after it count.
            case ViewTargetChanged v when _lastFinished is not null
                                          && v.PlayerName != SelfName
                                          && !_spectated.Contains(v.PlayerName)
                                          && _spectated.Count < MaxSpectated:
                _spectated.Add(v.PlayerName);
                if (_spectated.Count == 1)
                    MatchCompleted?.Invoke(_lastFinished with { EliminatedBy = v.PlayerName });
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

    private void ResetSpectating()
    {
        _spectated.Clear();
        _lastFinished = null;
    }

    private MatchRecord Complete(DateTime? endedUtc, bool finished)
    {
        var record = new MatchRecord(
            MatchStartedUtc!.Value, endedUtc, PlaylistNames.Describe(Playlist, Level), Playlist, _squadSizeAtStart, finished,
            PartyIds: _partyAtStart);
        MatchStartedUtc = null;
        MatchCompleted?.Invoke(record);
        return record;
    }
}
