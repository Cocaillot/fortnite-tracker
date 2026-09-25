using System.Text.Json;

namespace FortniteTracker.Core;

/// <summary>
/// Match history in %APPDATA%\FortniteTracker\history.json. Matches are keyed by start time, so
/// replaying the same log (every app start) or re-importing a backup never creates duplicates.
/// </summary>
public sealed class MatchHistoryStore
{
    private const int MaxMatches = 1000;

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

    public bool WasImported(string fileName)
    {
        lock (_gate) return _importedFiles.Contains(fileName);
    }

    public void Add(MatchRecord match) => AddRange([match], importedFile: null);

    /// <summary>Adds matches; an existing entry is only replaced by one that knows more (finished/ended).</summary>
    public void AddRange(IEnumerable<MatchRecord> matches, string? importedFile)
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var m in matches)
            {
                if (_matches.TryGetValue(m.StartedUtc, out var existing) && !IsBetter(m, existing)) continue;
                _matches[m.StartedUtc] = m;
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
                || (candidate.Playlist is not null && existing.Playlist is null)));

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var file = JsonSerializer.Deserialize<HistoryFile>(File.ReadAllText(_path));
            if (file is null) return;
            foreach (var m in file.Matches) _matches[m.StartedUtc] = m;
            _importedFiles.UnionWith(file.ImportedFiles);
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
        File.WriteAllText(tmp, JsonSerializer.Serialize(new HistoryFile([.. _matches.Values], [.. _importedFiles])));
        File.Move(tmp, _path, overwrite: true);
    }

    private sealed record HistoryFile(List<MatchRecord> Matches, List<string> ImportedFiles);
}
