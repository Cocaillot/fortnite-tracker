using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Reminds you about players you left a note on when you meet them again: in your party, as your
/// eliminator, or spectated after your team was eliminated. Once per player per match.
/// </summary>
public sealed class NoteAlerts
{
    private readonly PlayerNotes _notes;
    private readonly Action<string, string> _notify;
    private readonly HashSet<string> _announced = [];
    private DateTime? _match;

    public NoteAlerts(LobbyTracker tracker, PlayerNotes notes, Action<string, string> notify)
    {
        _notes = notes;
        _notify = notify;
        tracker.Changed += OnSnapshot;
    }

    private void OnSnapshot(LobbySnapshot s)
    {
        // A new match (or the one just finished) starts a new round of reminders.
        var match = s.MatchStartedUtc ?? s.LastMatchStartedUtc;
        if (match != _match)
        {
            _match = match;
            _announced.Clear();
        }
        // Stay quiet while the log is replayed at startup.
        if (match is not { } started || !LobbyTracker.IsLive(s.LastMatchEndedUtc ?? started)) return;

        var people = s.Squad.Skip(1).Select(p => (p.AccountId, p.EpicName, Role: Loc.T("is in your party")))
            .Concat(s.EliminatedBy is { } e ? [(e.AccountId, e.EpicName, Role: Loc.T("eliminated you"))] : [])
            .Concat(s.Spectated.Select(p => (p.AccountId, p.EpicName, Role: Loc.T("is in this match"))));

        foreach (var (id, name, role) in people)
        {
            if (_notes.Find(id, name) is not { } note || !_announced.Add(note.Name)) continue;
            var detail = string.Join(" · ", note.Tags.Concat(string.IsNullOrWhiteSpace(note.Text) ? [] : [$"\"{note.Text}\""]));
            _notify(Loc.T("You met {0} again", note.Name), $"{note.Name} {role}. {detail}");
        }
    }
}
