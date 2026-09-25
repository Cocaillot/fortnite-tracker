using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FortniteTracker.Core;

/// <summary>
/// On startup, imports matches (and ranks) from Fortnite's rotated logs (FortniteGame-backup-*.log) that
/// haven't been imported yet. The live log is covered by the tailer instead.
/// </summary>
public sealed class MatchHistoryImporter(MatchHistoryStore store, RankBook ranks, ILogger<MatchHistoryImporter> logger) : BackgroundService
{
    public string LogDirectory { get; init; } = Path.GetDirectoryName(FortniteLogTailer.DefaultLogPath)!;

    protected override Task ExecuteAsync(CancellationToken ct) => Task.Run(() =>
    {
        if (!Directory.Exists(LogDirectory)) return;

        foreach (var path in Directory.EnumerateFiles(LogDirectory, "FortniteGame-backup-*.log").Order())
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);
            if (store.WasImported(name)) continue;
            try
            {
                var matches = ReadMatches(path, ct, r => ranks.Add(r, save: false));
                store.AddRange(matches, importedFile: name);
                ranks.Flush();
                logger.LogInformation("Imported {Count} matches from {File}", matches.Count, name);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not import {File}", name);
            }
        }
    }, ct);

    public static List<MatchRecord> ReadMatches(string path, CancellationToken ct = default, Action<IReadOnlyList<RankProgress>>? onRanks = null)
    {
        var matches = new List<MatchRecord>();
        var state = new SessionState();
        state.MatchCompleted += matches.Add;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            var e = FortniteLogParser.Parse(line);
            if (e is RanksSeen seen) onRanks?.Invoke(seen.Ranks);
            else if (e is not null) state.Apply(e);
            if (++lineNumber % 10_000 == 0) ct.ThrowIfCancellationRequested();
        }
        state.CompleteOpenMatch(endedUtc: null); // the game closed or crashed mid-match
        return matches;
    }
}
