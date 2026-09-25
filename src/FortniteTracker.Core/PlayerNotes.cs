using System.Text.Json;

namespace FortniteTracker.Core;

/// <summary>Your own note and tags on a player ("Teamer", "Good duo"...).</summary>
public sealed record PlayerNote(string? AccountId, string Name, IReadOnlyList<string> Tags, string Text, DateTime UpdatedUtc)
{
    public bool IsEmpty => Tags.Count == 0 && string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// Notes on players in notes.json. A note is found by account ID when known, otherwise by the name
/// seen in-game (opponents are only known by name).
/// </summary>
public sealed class PlayerNotes
{
    private readonly string _path;
    private readonly object _gate = new();
    private List<PlayerNote> _notes = [];

    public PlayerNotes(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsStore.DefaultDirectory, "notes.json");
        try
        {
            if (File.Exists(_path)) _notes = JsonSerializer.Deserialize<List<PlayerNote>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
    }

    public event Action? Changed;

    public IReadOnlyList<PlayerNote> All
    {
        get { lock (_gate) return [.. _notes]; }
    }

    public PlayerNote? Find(string? accountId, string? name)
    {
        lock (_gate) return _notes.FirstOrDefault(n => Matches(n, accountId, name));
    }

    /// <summary>Saves a note (an empty one deletes it).</summary>
    public void Save(string? accountId, string name, IEnumerable<string> tags, string text)
    {
        var note = new PlayerNote(accountId, name, tags.Distinct().ToList(), text.Trim(), DateTime.UtcNow);
        lock (_gate)
        {
            _notes = [.. _notes.Where(n => !Matches(n, accountId, name))];
            if (!note.IsEmpty) _notes.Add(note);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_notes));
        }
        Changed?.Invoke();
    }

    private static bool Matches(PlayerNote n, string? accountId, string? name) =>
        (accountId is not null && n.AccountId == accountId)
        || (name is not null && string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
}
