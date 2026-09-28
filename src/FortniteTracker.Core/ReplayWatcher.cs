using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FortniteTracker.Core;

/// <summary>
/// Reads each Fortnite replay once it's finalised (when the next match starts or the player goes
/// back to the lobby) and adds its details to the matching history entry. Afterwards Fortnite's
/// automatic recordings ("UnsavedReplay-…") are deleted unless the user keeps them; replays the
/// user saved under their own name are never touched.
/// </summary>
public sealed class ReplayWatcher(MatchHistoryStore history, SettingsStore settings, ILogger<ReplayWatcher> logger) : BackgroundService
{
    // The log adds the match a few seconds before its replay exists, but old logs are imported
    // after startup: give a replay this long to find its match before giving up on it.
    private static readonly TimeSpan WaitForMatch = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan GiveUpReading = TimeSpan.FromDays(1);
    private const int MaxRemembered = 500;

    private readonly object _gate = new();
    private readonly Dictionary<string, DateTime> _firstSeen = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _done;

    public string Directory { get; init; } = ReplayParser.DefaultDirectory;

    /// <summary>Where the names of replays already read are kept (for replays the user keeps).</summary>
    public string? StatePath { get; init; }

    /// <summary>Your account ID and name (from the log), for replays that don't flag their owner.</summary>
    public Func<(string? Id, string? Name)> Self { get; init; } = () => (null, null);

    public TimeSpan StartDelay { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>When a replay was last added to a match, if ever.</summary>
    public DateTime? LastReadUtc { get; private set; }

    public bool DirectoryExists => System.IO.Directory.Exists(Directory);

    /// <summary>A replay changed a match (e.g. its eliminator), so eliminator stats may need a lookup.</summary>
    public event Action? ReplayApplied;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(StartDelay, ct);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                Scan(DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not scan replays");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    /// <summary>One pass over the replay folder (public for tests).</summary>
    public void Scan(DateTime nowUtc)
    {
        if (!DirectoryExists) return;
        var done = LoadDone();
        foreach (var file in System.IO.Directory.GetFiles(Directory, "*.replay").Order())
        {
            var name = Path.GetFileName(file);
            if (done.Contains(name)) continue;
            if (!_firstSeen.TryGetValue(name, out var firstSeen)) _firstSeen[name] = firstSeen = nowUtc;

            ParsedReplay? parsed;
            try
            {
                var (selfId, selfName) = Self();
                parsed = ReplayParser.Read(file, selfId, selfName);
            }
            catch (Exception ex)
            {
                // Retried at the next pass (it may be a moment Fortnite was reopening the file);
                // a replay the reader really can't handle is given up on below.
                logger.LogWarning(ex, "Could not read replay {File}", name);
                parsed = null;
            }
            if (parsed is null)
            {
                // Still being written, never finalised (e.g. the game crashed) or unreadable.
                if (nowUtc - firstSeen > GiveUpReading) MarkDone(name, file, delete: false);
                continue;
            }

            var match = ReplayParser.MatchFor(parsed, history.StartedBetween(parsed.StartedUtc.AddMinutes(-3), parsed.StartedUtc.AddMinutes(1)));
            if (match is null && nowUtc - firstSeen < WaitForMatch) continue;
            if (match is not null && Apply(match, parsed.Details))
            {
                LastReadUtc = nowUtc;
                ReplayApplied?.Invoke();
            }
            MarkDone(name, file, delete: true);
        }
    }

    private bool Apply(MatchRecord match, ReplayDetails details) =>
        history.Update(match.StartedUtc, m =>
        {
            var sameEliminator = string.Equals(m.EliminatedBy, details.EliminatedBy, StringComparison.Ordinal);
            return m with
            {
                Replay = details,
                Kills = details.Kills,
                Won = details.Placement is { } place ? place == 1 : m.Won,
                EliminatedBy = details.EliminatedBy,
                // A different eliminator than the log's guess: its stats are looked up again.
                EliminatorKd = sameEliminator ? m.EliminatorKd : null,
                EliminatorThreat = sameEliminator ? m.EliminatorThreat : null,
            };
        });

    private void MarkDone(string name, string path, bool delete)
    {
        var automatic = name.StartsWith("UnsavedReplay", StringComparison.OrdinalIgnoreCase);
        if (delete && automatic && settings.DeleteReplaysAfterReading)
        {
            try
            {
                File.Delete(path);
                _firstSeen.Remove(name);
                return; // gone, so there is nothing to remember
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still open somewhere; remembered below so it isn't read again.
            }
        }
        lock (_gate)
        {
            var done = LoadDone();
            done.Add(name);
            _firstSeen.Remove(name);
            SaveDone(done);
        }
    }

    private HashSet<string> LoadDone()
    {
        lock (_gate)
        {
            if (_done is not null) return _done;
            _done = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (StatePath is not null && File.Exists(StatePath))
                    _done.UnionWith(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StatePath)) ?? []);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
            }
            return _done;
        }
    }

    private void SaveDone(HashSet<string> done)
    {
        if (StatePath is null) return;
        // Only names still on disk matter; keep the list short.
        var keep = done.Where(n => File.Exists(Path.Combine(Directory, n))).TakeLast(MaxRemembered).ToList();
        _done = new(keep, StringComparer.OrdinalIgnoreCase);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(keep));
    }
}
