using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FortniteTracker.Core;

/// <summary>
/// Follows FortniteGame.log like `tail -f`. Read-only, shared access: Fortnite keeps writing to it undisturbed.
/// On start it replays the current file so the app learns who is logged in and who is in the party.
/// </summary>
public sealed class FortniteLogTailer(ILogger<FortniteLogTailer> logger) : BackgroundService
{
    public static readonly string DefaultLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame", "Saved", "Logs", "FortniteGame.log");

    public string LogPath { get; init; } = DefaultLogPath;

    public event Action<string>? LineRead;

    /// <summary>Raised each time a log file is opened: at startup and after Fortnite starts a new one.</summary>
    public event Action? FileOpened;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FollowCurrentFileAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not read {LogPath}; retrying", LogPath);
                await Task.Delay(2000, ct);
            }
        }
    }

    private async Task FollowCurrentFileAsync(CancellationToken ct)
    {
        if (!File.Exists(LogPath))
        {
            await Task.Delay(2000, ct);
            return;
        }

        using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        var pending = new StringBuilder();
        var chunk = new char[16 * 1024];
        logger.LogInformation("Following {LogPath}", LogPath);
        FileOpened?.Invoke();

        while (!ct.IsCancellationRequested)
        {
            var n = await reader.ReadAsync(chunk, ct);
            if (n > 0)
            {
                pending.Append(chunk, 0, n);
                EmitCompleteLines(pending);
                continue;
            }

            // On launch Fortnite renames the log to *-backup-*.log and starts a new one.
            // Our handle follows the renamed file, so compare against whatever is at the path now.
            var current = new FileInfo(LogPath);
            if (current.Exists && current.Length < fs.Position)
            {
                logger.LogInformation("Log rotated; reopening");
                return;
            }
            await Task.Delay(250, ct);
        }
    }

    private void EmitCompleteLines(StringBuilder pending)
    {
        var text = pending.ToString();
        int start = 0, nl;
        while ((nl = text.IndexOf('\n', start)) >= 0)
        {
            LineRead?.Invoke(text[start..nl].TrimEnd('\r'));
            start = nl + 1;
        }
        pending.Remove(0, start); // keep a partial trailing line for the next read
    }
}
