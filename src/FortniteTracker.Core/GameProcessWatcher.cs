using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace FortniteTracker.Core;

/// <summary>
/// Polls whether the Fortnite client process is running, so stale log state
/// (e.g. the game closed mid-match) is not shown as live. Only checks the process name.
/// </summary>
public sealed class GameProcessWatcher(LobbyTracker tracker) : BackgroundService
{
    private const string ProcessName = "FortniteClient-Win64-Shipping";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        bool? last = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            var running = IsRunning();
            if (running != last) tracker.Handle(new GameRunningChanged(running));
            last = running;
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private static bool IsRunning()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }
}
