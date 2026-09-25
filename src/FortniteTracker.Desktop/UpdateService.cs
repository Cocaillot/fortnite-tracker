using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace FortniteTracker.Desktop;

/// <summary>
/// Checks for updates every few hours and downloads them in the background. A downloaded update
/// installs on the next start, or right away if the user picks "Restart to update".
/// Inactive in development builds and when Updates:Url is not configured.
/// </summary>
public sealed class UpdateService(IConfiguration config, ILogger<UpdateService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);

    private UpdateManager? _manager;
    private UpdateInfo? _ready;

    public string CurrentVersion =>
        _manager?.CurrentVersion?.ToString()
        ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "dev";

    public string? ReadyVersion => _ready?.TargetFullRelease.Version.ToString();

    public event Action? UpdateReady;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var url = config["Updates:Url"];
        if (string.IsNullOrWhiteSpace(url)) return;

        // A GitHub repo URL uses GitHub Releases; anything else is a folder or static web host.
        var manager = url.Contains("github.com", StringComparison.OrdinalIgnoreCase)
            ? new UpdateManager(new GithubSource(url, accessToken: null, prerelease: false))
            : new UpdateManager(url);
        if (!manager.IsInstalled) return; // running from a build folder, not an installed copy
        _manager = manager;

        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            try
            {
                await CheckAndDownloadAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Update check failed");
            }
        }
        while (_ready is null && await timer.WaitForNextTickAsync(ct));
    }

    private async Task CheckAndDownloadAsync(CancellationToken ct)
    {
        var update = await _manager!.CheckForUpdatesAsync();
        if (update is null) return;

        logger.LogInformation("Downloading update {Version}", update.TargetFullRelease.Version);
        await _manager.DownloadUpdatesAsync(update, cancelToken: ct);
        _ready = update;
        UpdateReady?.Invoke();
    }

    public void ApplyAndRestart()
    {
        if (_manager is not null && _ready is not null) _manager.ApplyUpdatesAndRestart(_ready.TargetFullRelease);
    }
}
