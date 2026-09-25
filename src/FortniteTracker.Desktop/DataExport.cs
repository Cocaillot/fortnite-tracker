using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Match history as a spreadsheet, and a backup of all app data (the .json files in the data
/// folder) that can be restored on this or another PC.
/// </summary>
public static class DataExport
{
    private const string PendingRestore = "restore-pending.zip";

    /// <summary>CSV that opens directly in Excel: ";" and decimal commas in French, "," otherwise.</summary>
    public static void WriteCsv(IEnumerable<MatchRecord> matches, string path)
    {
        var culture = Loc.Culture;
        var sep = Loc.French ? ";" : ",";
        string Cell(object? v) => v switch
        {
            null => "",
            double d => d.ToString("0.00", culture),
            _ => Quote(Convert.ToString(v, culture) ?? ""),
        };
        string Quote(string s) => s.Contains(sep) || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, new[]
        {
            "Date", "Start", "Mode", "Playlist", "Party size", "Result", "Kills", "Duration (min)", "Eliminated by", "Eliminator K/D",
        }.Select(h => Quote(Loc.T(h)))));
        foreach (var m in matches.OrderBy(m => m.StartedUtc))
        {
            var start = m.StartedUtc.ToLocalTime();
            var result = m.Won == true ? "Victory" : !m.Finished ? "Left early" : "Eliminated";
            double? minutes = m.EndedUtc is { } end ? Math.Round((end - m.StartedUtc).TotalMinutes, 1) : null;
            sb.AppendLine(string.Join(sep,
                Cell(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Cell(start.ToString("HH:mm", CultureInfo.InvariantCulture)),
                Cell(Loc.Name(m.Mode)),
                Cell(m.Playlist),
                Cell(m.SquadSize),
                Cell(Loc.T(result)),
                Cell(m.Kills),
                minutes is { } min ? min.ToString("0.#", culture) : "",
                Cell(m.EliminatedBy),
                Cell(m.EliminatorKd)));
        }
        // UTF-8 with a BOM so Excel shows accents and non-Latin names correctly.
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static void CreateBackup(string dataDir, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(dataDir, "*.json"))
            zip.CreateEntryFromFile(file, Path.GetFileName(file));
    }

    /// <summary>
    /// Checks a backup and queues it; it's applied at the next start, before anything is loaded.
    /// Returns false if the file isn't a Fortnite Tracker backup.
    /// </summary>
    public static bool QueueRestore(string zipPath, string dataDir)
    {
        try
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var names = zip.Entries.Select(e => e.FullName).ToList();
                if (names.Count == 0 || names.Any(n => n.Contains('/') || n.Contains('\\') || !n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                    return false;
                if (!names.Contains("settings.json") && !names.Contains("history.json")) return false;
            }
            Directory.CreateDirectory(dataDir);
            File.Copy(zipPath, Path.Combine(dataDir, PendingRestore), overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Applies a queued restore, keeping a copy of the data it replaces in "backups".</summary>
    public static void ApplyPendingRestore(string dataDir)
    {
        var pending = Path.Combine(dataDir, PendingRestore);
        if (!File.Exists(pending)) return;
        try
        {
            var backups = Directory.CreateDirectory(Path.Combine(dataDir, "backups")).FullName;
            CreateBackup(dataDir, Path.Combine(backups, $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.zip"));
            using var zip = ZipFile.OpenRead(pending);
            foreach (var entry in zip.Entries)
                entry.ExtractToFile(Path.Combine(dataDir, Path.GetFileName(entry.FullName)), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // The copy in "backups" holds the data from before the attempt.
        }
        finally
        {
            try { File.Delete(pending); } catch (IOException) { }
        }
    }
}
