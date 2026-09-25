using System.Net.Http;
using System.Threading;
using System.Windows;
using FortniteTracker.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FortniteTracker.Desktop;

public partial class App : Application
{
    private readonly EventWaitHandle _showRequested;
    private IHost? _host;
    private TrayIcon? _tray;
    private MainWindow? _window;

    public App(EventWaitHandle showRequested)
    {
        _showRequested = showRequested;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((ctx, services) =>
            {
                services.AddMemoryCache();
                services.AddSingleton(_ => new SettingsStore(ctx.Configuration["FortniteApi:Key"]));
                services.AddSingleton(_ => new MatchHistoryStore());
                services.AddHttpClient("fortnite-api", c =>
                {
                    c.BaseAddress = new Uri("https://fortnite-api.com/");
                    c.Timeout = TimeSpan.FromSeconds(10);
                });
                // Singleton so the cache and rate limiter are shared by the squad view and manual lookups.
                services.AddSingleton(sp => ActivatorUtilities.CreateInstance<FortniteStatsService>(sp,
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("fortnite-api")));
                services.AddSingleton<LobbyTracker>();
                services.AddSingleton<FortniteLogTailer>();
                services.AddHostedService(sp => sp.GetRequiredService<FortniteLogTailer>());
                services.AddHostedService<GameProcessWatcher>();
                services.AddHostedService<MatchHistoryImporter>();
                services.AddSingleton<UpdateService>();
                services.AddHostedService(sp => sp.GetRequiredService<UpdateService>());
                services.AddSingleton<DiscordPresenceService>();
                services.AddSingleton<UiBridge>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        var services = _host.Services;
        var tracker = services.GetRequiredService<LobbyTracker>();
        var stats = services.GetRequiredService<FortniteStatsService>();
        var history = services.GetRequiredService<MatchHistoryStore>();
        var updates = services.GetRequiredService<UpdateService>();
        var tailer = services.GetRequiredService<FortniteLogTailer>();

        // Subscribe before the tailer starts so the initial replay of the log is not missed.
        tailer.FileOpened += () => tracker.Handle(new LogFileOpened { At = DateTime.UtcNow });
        tailer.LineRead += line =>
        {
            if (FortniteLogParser.Parse(line) is { } gameEvent) tracker.Handle(gameEvent);
        };
        tracker.MatchCompleted += history.Add;
        services.GetRequiredService<SettingsStore>().Changed += apiKeyChanged =>
        {
            if (!apiKeyChanged) return;
            stats.ClearCache();
            tracker.Refresh();
        };
        services.GetRequiredService<DiscordPresenceService>(); // starts following the tracker

        _window = services.GetRequiredService<MainWindow>();
        _tray = new TrayIcon(_window.ToggleVisibility, ApplyUpdate, ExitApp);
        _window.HiddenToTray += _tray.ShowStillRunningHint;
        updates.UpdateReady += () => Dispatcher.InvokeAsync(() => _tray.ShowUpdateReady(updates.ReadyVersion!));

        // A second launch signals this instance to come to the front.
        ThreadPool.RegisterWaitForSingleObject(_showRequested,
            (_, _) => Dispatcher.InvokeAsync(() => _window.ShowAndActivate()), null, Timeout.Infinite, executeOnlyOnce: false);

        _window.Show();
        await _host.StartAsync();
    }

    private void ApplyUpdate()
    {
        _tray?.Dispose(); // the process exits inside ApplyAndRestart; don't leave a ghost tray icon
        _host?.Services.GetRequiredService<UpdateService>().ApplyAndRestart();
    }

    private void ExitApp()
    {
        if (_window is not null) _window.AllowClose = true;
        Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_host is not null)
        {
            _host.Services.GetRequiredService<DiscordPresenceService>().Dispose();
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
