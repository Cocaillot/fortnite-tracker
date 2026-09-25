using System.Text.Json;

namespace FortniteTracker.Core;

/// <summary>
/// A play session: matches with less than <see cref="SessionStore.Gap"/> between them. Baseline is
/// your season stats before its first match, Latest after its most recent one; the difference is
/// what you did this session (kills, wins, deaths, matches), straight from Epic's numbers.
/// </summary>
public sealed record SessionRecord(DateTime StartedUtc, DateTime LastMatchUtc, ModeStats? Baseline, ModeStats? Latest)
{
    public ModeStats? Delta => Baseline is { } b && Latest is { } l && l.Matches >= b.Matches
        ? new ModeStats(
            Wins: l.Wins - b.Wins,
            WinRate: l.Matches == b.Matches ? 0 : 100.0 * (l.Wins - b.Wins) / (l.Matches - b.Matches),
            Kd: (l.Kills - b.Kills) / (double)Math.Max(1, l.Deaths - b.Deaths),
            Kills: l.Kills - b.Kills,
            Matches: l.Matches - b.Matches,
            Top10: l.Top10 - b.Top10,
            Top25: l.Top25 - b.Top25,
            MinutesPlayed: l.MinutesPlayed - b.MinutesPlayed,
            KillsPerMatch: l.Matches == b.Matches ? 0 : (l.Kills - b.Kills) / (double)(l.Matches - b.Matches),
            Deaths: l.Deaths - b.Deaths)
        : null;
}

/// <summary>Sessions in %APPDATA%\FortniteTracker\sessions.json (live play only; imported logs have no stats).</summary>
public sealed class SessionStore
{
    /// <summary>A longer break than this starts a new session. The UI groups history the same way.</summary>
    public static readonly TimeSpan Gap = TimeSpan.FromMinutes(45);

    private const int MaxSessions = 200;

    private readonly string _path;
    private readonly object _gate = new();
    private List<SessionRecord> _sessions = [];

    public SessionStore(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsStore.DefaultDirectory, "sessions.json");
        try
        {
            if (File.Exists(_path)) _sessions = JsonSerializer.Deserialize<List<SessionRecord>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
    }

    public event Action? Changed;

    public IReadOnlyList<SessionRecord> All
    {
        get { lock (_gate) return [.. _sessions]; }
    }

    /// <summary>A live match started; <paramref name="before"/> is your season stats at that moment.</summary>
    public void MatchStarted(DateTime startedUtc, ModeStats? before)
    {
        lock (_gate)
        {
            var current = _sessions.LastOrDefault();
            if (current is not null && startedUtc - current.LastMatchUtc < Gap)
                _sessions[^1] = current with { LastMatchUtc = startedUtc, Baseline = current.Baseline ?? before };
            else
                _sessions.Add(new SessionRecord(startedUtc, startedUtc, before, null));
            if (_sessions.Count > MaxSessions) _sessions.RemoveAt(0);
            Save();
        }
        Changed?.Invoke();
    }

    /// <summary>A match landed in your stats; <paramref name="after"/> becomes the session's latest.</summary>
    public void StatsUpdated(DateTime matchStartedUtc, ModeStats after)
    {
        lock (_gate)
        {
            var i = _sessions.FindLastIndex(s => s.StartedUtc <= matchStartedUtc);
            if (i < 0) return;
            _sessions[i] = _sessions[i] with { Latest = after };
            Save();
        }
        Changed?.Invoke();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_sessions));
        File.Move(tmp, _path, overwrite: true);
    }
}
