using System.Text.Json;

namespace FortniteTracker.Core;

/// <summary>
/// Match history in %APPDATA%\FortniteTracker\history.json. Matches are keyed by start time, so
/// replaying the same log (every app start) or re-importing a backup never creates duplicates.
/// </summary>
public sealed class MatchHistoryStore
{
    private const int MaxMatches = 1000;

    // Bump when the importer learns something new, so old logs are read again (2: eliminators, 3: ranks,
    // 4: end time of matches left early, 5: every ranked season and rank history, 6: party per match).
    // 7: kills and wins recorded before it could count one stats change for several matches, so they were cleared.
    private const int FormatVersion = 7;

    private readonly string _path;
    private readonly object _gate = new();
    private readonly SortedDictionary<DateTime, MatchRecord> _matches = new();
    private readonly HashSet<string> _importedFiles = new(StringComparer.OrdinalIgnoreCase);

    public MatchHistoryStore(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsStore.DefaultDirectory, "history.json");
        Load();
    }

    public event Action? Changed;

    public IReadOnlyList<MatchRecord> Recent(int count)
    {
        lock (_gate) return _matches.Values.Reverse().Take(count).ToList();
    }

    public MatchRecord? Get(DateTime startedUtc)
    {
        lock (_gate) return _matches.GetValueOrDefault(startedUtc);
    }

    /// <summary>Matches that started in [from, to), oldest first.</summary>
    public IReadOnlyList<MatchRecord> StartedBetween(DateTime fromUtc, DateTime toUtc)
    {
        lock (_gate) return _matches.Values.Where(m => m.StartedUtc >= fromUtc && m.StartedUtc < toUtc).ToList();
    }

    /// <summary>The match started right after this one, if any.</summary>
    public MatchRecord? Next(DateTime startedUtc)
    {
        lock (_gate) return _matches.Values.FirstOrDefault(m => m.StartedUtc > startedUtc);
    }

    /// <summary>Changes a stored match in place (e.g. adding kills); no-op if it isn't stored.</summary>
    public bool Update(DateTime startedUtc, Func<MatchRecord, MatchRecord> change)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(startedUtc, out var existing)) return false;
            var updated = change(existing);
            if (updated == existing) return false;
            _matches[startedUtc] = updated;
            Save();
        }
        Changed?.Invoke();
        return true;
    }

    public bool WasImported(string fileName)
    {
        lock (_gate) return _importedFiles.Contains(fileName);
    }

    public void Add(MatchRecord match) => AddRange([match], importedFile: null);

    /// <summary>
    /// Adds matches; an existing entry is only replaced by one that knows more from the log
    /// (finished/ended/playlist/eliminator), and keeps the stats-based details already added to it.
    /// </summary>
    public void AddRange(IEnumerable<MatchRecord> matches, string? importedFile)
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var m in matches)
            {
                if (_matches.TryGetValue(m.StartedUtc, out var existing))
                {
                    if (!IsBetter(m, existing)) continue;
                    _matches[m.StartedUtc] = m with
                    {
                        Kills = m.Kills ?? existing.Kills,
                        Won = m.Won ?? existing.Won,
                        EliminatorKd = m.EliminatorKd ?? existing.EliminatorKd,
                        EliminatorThreat = m.EliminatorThreat ?? existing.EliminatorThreat,
                    };
                }
                else
                {
                    _matches[m.StartedUtc] = m;
                }
                changed = true;
            }
            while (_matches.Count > MaxMatches) _matches.Remove(_matches.Keys.First());
            if (importedFile is not null) changed |= _importedFiles.Add(importedFile);
            if (changed) Save();
        }
        if (changed) Changed?.Invoke();
    }

    private static bool IsBetter(MatchRecord candidate, MatchRecord existing) =>
        (candidate.Finished && !existing.Finished)
        || (candidate.Finished == existing.Finished
            && ((candidate.EndedUtc is not null && existing.EndedUtc is null)
                || (candidate.Playlist is not null && existing.Playlist is null)
                || (candidate.EliminatedBy is not null && existing.EliminatedBy is null)
                || (candidate.PartyIds is not null && existing.PartyIds is null)));

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var file = JsonSerializer.Deserialize<HistoryFile>(File.ReadAllText(_path));
            if (file is null) return;
            // Names are recomputed from the playlist, so older records get today's wording
            // (e.g. "Ranked Duos" became "Ranked Reload Duos · Build"). Creative can also come from
            // the map, so a record already marked Creative stays that way.
            foreach (var saved in file.Matches)
            {
                var m = saved.Playlist is null || saved.Mode == "Creative" ? saved : saved with { Mode = PlaylistNames.Describe(saved.Playlist) };
                if (file.Version < 7) m = m with { Kills = null, Won = null };
                _matches[m.StartedUtc] = m;
            }
            if (file.Version >= FormatVersion) _importedFiles.UnionWith(file.ImportedFiles);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupt or locked history is not fatal; it is rebuilt from the logs that still exist.
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(new HistoryFile([.. _matches.Values], [.. _importedFiles], FormatVersion)));
        File.Move(tmp, _path, overwrite: true);
    }

    private sealed record HistoryFile(List<MatchRecord> Matches, List<string> ImportedFiles, int Version = 1);
}
