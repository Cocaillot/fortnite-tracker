using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace FortniteTracker.Desktop;

/// <summary>
/// Checks for updates regularly and downloads them in the background. The UI shows a newer
/// version as soon as it's found; "Update now" waits for the download if needed, then installs it
/// and restarts. Otherwise a downloaded update installs on the next start.
/// Inactive in development builds and when Updates:Url is not configured.
/// </summary>
public sealed class UpdateService(IConfiguration config, ILogger<UpdateService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    private readonly SemaphoreSlim _checking = new(1, 1);
    private UpdateManager? _manager;
    private UpdateInfo? _found;
    private Task? _download;
    private UpdateInfo? _ready;
    private int _progress;
    private bool _installRequested;

    public string CurrentVersion =>
        _manager?.CurrentVersion?.ToString()
        ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "dev";

    /// <summary>False in development builds, where there is nothing to update.</summary>
    public bool Enabled => _manager is not null;

    /// <summary>A newer version, as soon as it's known (it may still be downloading).</summary>
    public string? AvailableVersion => _found?.TargetFullRelease.Version.ToString();

    /// <summary>A newer version that is downloaded and can be installed right away.</summary>
    public string? ReadyVersion => _ready?.TargetFullRelease.Version.ToString();

    /// <summary>Download progress of <see cref="AvailableVersion"/>, 0–100.</summary>
    public int Progress => _ready is not null ? 100 : _progress;

    /// <summary>"Update now" was pressed and the app restarts once the download ends.</summary>
    public bool Installing => _installRequested;

    /// <summary>Any change above.</summary>
    public event Action? Changed;

    /// <summary>A version finished downloading (used for the tray notification).</summary>
    public event Action? UpdateReady;

    /// <summary>Raised just before the app exits to install an update.</summary>
    public event Action? Restarting;

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
        Changed?.Invoke();

        using var timer = new PeriodicTimer(CheckInterval);
        do await CheckAsync(ct);
        while (await timer.WaitForNextTickAsync(ct));
    }

    /// <summary>Looks for a newer version now (also used by "Check for updates"); true if one exists.</summary>
    public async Task<bool> CheckAsync(CancellationToken ct = default)
    {
        if (_manager is null) return false;
        await _checking.WaitAsync(ct);
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) return _found is not null;
            // Keep looking after one is found: a newer release replaces an older one not yet installed.
            if (_found?.TargetFullRelease.Version == update.TargetFullRelease.Version) return true;

            logger.LogInformation("Update {Version} found", update.TargetFullRelease.Version);
            _found = update;
            _ready = null;
            _progress = 0;
            Changed?.Invoke();
            _download = DownloadAsync(update, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Update check failed");
            return _found is not null;
        }
        finally
        {
            _checking.Release();
        }
    }

    private async Task DownloadAsync(UpdateInfo update, CancellationToken ct)
    {
        try
        {
            await _manager!.DownloadUpdatesAsync(update, p =>
            {
                // Report every 5% so the UI isn't flooded.
                if (p / 5 == _progress / 5) return;
                _progress = p;
                Changed?.Invoke();
            }, ct);
            if (_found != update) return; // a newer one was found meanwhile
            _ready = update;
            Changed?.Invoke();
            UpdateReady?.Invoke();
            if (_installRequested) ApplyAndRestart();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Update download failed");
            _found = null; // offer it again at the next check
            _installRequested = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Installs the newest version and restarts: right away if downloaded, else once it is.</summary>
    public void UpdateNow()
    {
        if (_ready is not null)
        {
            ApplyAndRestart();
            return;
        }
        if (_found is null) return;
        _installRequested = true;
        Changed?.Invoke();
    }

    public void ApplyAndRestart()
    {
        if (_manager is null || _ready is null) return;
        Restarting?.Invoke();
        _manager.ApplyUpdatesAndRestart(_ready.TargetFullRelease);
    }
}
