using System.Text.Json;

namespace FortniteTracker.Core;

/// <summary>Your season stats as they stood at the end of a day (local date).</summary>
public sealed record StatsDay(DateOnly Day, ModeStats Overall);

/// <summary>
/// One snapshot of your season stats per day in stats-history.json, for trend charts. The latest
/// snapshot of a day wins. Differences between days give that day's kills, deaths and wins.
/// </summary>
public sealed class StatsHistory
{
    private const int MaxDays = 400;

    private readonly string _path;
    private readonly object _gate = new();
    private readonly SortedDictionary<DateOnly, StatsDay> _days = new();

    public StatsHistory(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsStore.DefaultDirectory, "stats-history.json");
        try
        {
            if (File.Exists(_path))
                foreach (var d in JsonSerializer.Deserialize<List<StatsDay>>(File.ReadAllText(_path)) ?? [])
                    _days[d.Day] = d;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
    }

    public event Action? Changed;

    public IReadOnlyList<StatsDay> All
    {
        get { lock (_gate) return [.. _days.Values]; }
    }

    public void Record(DateOnly day, ModeStats overall)
    {
        lock (_gate)
        {
            if (_days.TryGetValue(day, out var existing) && existing.Overall == overall) return;
            _days[day] = new StatsDay(day, overall);
            while (_days.Count > MaxDays) _days.Remove(_days.Keys.First());
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_days.Values.ToList()));
        }
        Changed?.Invoke();
    }
}
