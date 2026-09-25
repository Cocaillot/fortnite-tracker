using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using FortniteTracker.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FortniteTracker.Desktop;

/// <summary>Where the app keeps its data (settings, history…).</summary>
public sealed record DataPaths(string Directory);

public partial class App : Application
{
    public const string RestartArg = "--restart";

    private readonly EventWaitHandle _showRequested;
    private IHost? _host;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private OverlayController? _overlay;

    public App(EventWaitHandle showRequested)
    {
        _showRequested = showRequested;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Started at sign-in by "Open with Fortnite": stay in the tray until the game runs.
        var background = e.Args.Contains(AutoStart.BackgroundArg);
        var hostArgs = e.Args.Where(a => a is not (AutoStart.BackgroundArg or RestartArg)).ToArray();

        _host = Host.CreateDefaultBuilder(hostArgs)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((ctx, services) =>
            {
                var config = ctx.Configuration;
                // Testing overrides (e.g. --Fortnite:LogPath=demo.log --Fortnite:AssumeRunning=true
                // --Storage:Directory=C:	empt): a replayed log without touching real data.
                var storage = config["Storage:Directory"] is { Length: > 0 } dir ? dir : SettingsStore.DefaultDirectory;
                var logPath = config["Fortnite:LogPath"] is { Length: > 0 } log ? log : FortniteLogTailer.DefaultLogPath;

                DataExport.ApplyPendingRestore(storage);
                services.AddSingleton(new DataPaths(storage));
                services.AddMemoryCache();
                services.AddSingleton(_ => new SettingsStore(config["FortniteApi:Key"], Path.Combine(storage, "settings.json")));
                services.AddSingleton(_ => new MatchHistoryStore(Path.Combine(storage, "history.json")));
                services.AddSingleton(_ => new RankBook(Path.Combine(storage, "ranks.json")));
                services.AddSingleton(_ => new SessionStore(Path.Combine(storage, "sessions.json")));
                services.AddSingleton<PlayerDirectory>();
                services.AddSingleton<MatchInsights>();
                services.AddSingleton(_ => new PlayerNotes(Path.Combine(storage, "notes.json")));
                services.AddSingleton(_ => new StatsHistory(Path.Combine(storage, "stats-history.json")));
                services.AddSingleton(_ => new GoalStore(storage));
                services.AddHttpClient("discord-webhook", c => c.Timeout = TimeSpan.FromSeconds(10));
                services.AddSingleton<DiscordRecapPoster>();
                services.AddSingleton(_ => new ThemeStore(storage));
                services.AddHttpClient("fortnite-api", c =>
                {
                    c.BaseAddress = new Uri("https://fortnite-api.com/");
                    c.Timeout = TimeSpan.FromSeconds(10);
                });
                // Singleton so the cache and rate limiter are shared by the squad view and manual lookups.
                services.AddSingleton(sp => ActivatorUtilities.CreateInstance<FortniteStatsService>(sp,
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("fortnite-api")));
                services.AddSingleton<LobbyTracker>();
                services.AddSingleton(sp => new FortniteLogTailer(sp.GetRequiredService<ILogger<FortniteLogTailer>>()) { LogPath = logPath });
                services.AddHostedService(sp => sp.GetRequiredService<FortniteLogTailer>());
                if (!config.GetValue<bool>("Fortnite:AssumeRunning")) services.AddHostedService<GameProcessWatcher>();
                services.AddHostedService(sp => new MatchHistoryImporter(
                    sp.GetRequiredService<MatchHistoryStore>(), sp.GetRequiredService<RankBook>(),
                    sp.GetRequiredService<ILogger<MatchHistoryImporter>>())
                {
                    LogDirectory = Path.GetDirectoryName(logPath)!,
                });
                services.AddSingleton<UpdateService>();
                services.AddHostedService(sp => sp.GetRequiredService<UpdateService>());
                services.AddSingleton<DiscordPresenceService>();
                services.AddSingleton<MatchResultTracker>();
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
        var ranks = services.GetRequiredService<RankBook>();
        tailer.LineRead += line =>
        {
            var gameEvent = FortniteLogParser.Parse(line);
            if (gameEvent is RanksSeen seen) ranks.Add(seen.Ranks);
            else if (gameEvent is not null) tracker.Handle(gameEvent);
        };
        tracker.MatchCompleted += history.Add;
        // A daily snapshot of your season stats for trend charts.
        var statsHistory = services.GetRequiredService<StatsHistory>();
        tracker.Changed += s =>
        {
            if (s.LocalName is not null && s.Squad.FirstOrDefault() is { Status: StatsStatus.Ok, Overall: { } overall })
                statsHistory.Record(DateOnly.FromDateTime(DateTime.Now), overall);
        };
        var settings = services.GetRequiredService<SettingsStore>();
        // French when chosen, or when Windows is in French and nothing was chosen.
        void ApplyLanguage() => Loc.Language = settings.Language
            ?? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en");
        ApplyLanguage();
        var results = services.GetRequiredService<MatchResultTracker>(); // follows the tracker from here on
        settings.Changed += apiKeyChanged =>
        {
            if (!apiKeyChanged) return;
            stats.ClearCache();
            tracker.Refresh();
            _ = results.BackfillEliminatorsAsync(CancellationToken.None);
        };
        services.GetRequiredService<DiscordPresenceService>(); // starts following the tracker
        services.GetRequiredService<DiscordRecapPoster>(); // posts a recap when Fortnite closes

        _window = services.GetRequiredService<MainWindow>();
        var theme = services.GetRequiredService<ThemeStore>();
        _overlay = new OverlayController(settings, tracker, theme, Dispatcher);
        _window.ApplyTheme(theme.Colors);
        theme.Changed += () => Dispatcher.InvokeAsync(() => _window.ApplyTheme(theme.Colors));
        _tray = new TrayIcon(_window.ToggleVisibility, _overlay.Toggle, ApplyUpdate, ExitApp);
        _tray.SetOverlayChecked(settings.OverlayEnabled);
        AutoStart.Apply(settings.LaunchWithFortnite);
        _window.SetHotkeys(settings.WindowHotkey, settings.OverlayHotkey);
        _tray.SetHotkeys(settings.WindowHotkey, settings.OverlayHotkey);
        settings.Changed += _ => Dispatcher.InvokeAsync(() =>
        {
            ApplyLanguage();
            _tray.ApplyLanguage();
            _tray.SetOverlayChecked(settings.OverlayEnabled);
            AutoStart.Apply(settings.LaunchWithFortnite);
            _window.SetHotkeys(settings.WindowHotkey, settings.OverlayHotkey);
            _tray.SetHotkeys(settings.WindowHotkey, settings.OverlayHotkey);
        });
        _window.OverlayHotkey += _overlay.Toggle;
        _window.HiddenToTray += _tray.ShowStillRunningHint;
        _ = new EliminationNotifier(tracker, settings, (title, text) => Dispatcher.InvokeAsync(() => _tray.Notify(title, text)));
        var bridge = services.GetRequiredService<UiBridge>();
        bridge.HotkeyStatusProvider = () => _window.HotkeyStatus;
        _window.HotkeysApplied += bridge.RefreshSettings;
        bridge.RestartRequested += Restart;
        _ = new NoteAlerts(tracker, services.GetRequiredService<PlayerNotes>(), (title, text) =>
        {
            Dispatcher.InvokeAsync(() => _tray.Notify(title, text));
            bridge.Toast(title, text, false);
        });
        _ = new RankAlerts(ranks, settings, tracker, stats, (title, text, good) =>
        {
            Dispatcher.InvokeAsync(() => _tray.Notify(title, text));
            bridge.Toast(title, text, good);
        });
        updates.UpdateReady += () => Dispatcher.InvokeAsync(() => _tray.ShowUpdateReady(updates.ReadyVersion!));

        // A second launch signals this instance to come to the front.
        ThreadPool.RegisterWaitForSingleObject(_showRequested,
            (_, _) => Dispatcher.InvokeAsync(() => _window.ShowAndActivate()), null, Timeout.Infinite, executeOnlyOnce: false);

        if (background) WaitForFortnite();
        else _window.Show();
        await _host.StartAsync();

        // Once old logs are imported, look up eliminators that have no stats yet (for the dashboard).
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            await results.BackfillEliminatorsAsync(CancellationToken.None);
        });
    }

    private const string FortniteProcess = "FortniteClient-Win64-Shipping";

    // The window (and its browser engine) is only created once Fortnite runs, so the app stays
    // light while it waits. Shortcuts and the tray work in the meantime.
    private void WaitForFortnite()
    {
        _window!.EnsureHandle();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            var running = System.Diagnostics.Process.GetProcessesByName(FortniteProcess);
            foreach (var p in running) p.Dispose();
            if (running.Length == 0 && !_window.IsVisible) return;
            timer.Stop();
            if (!_window.IsVisible) _window.ShowWithoutFocus();
        };
        timer.Start();
    }

    private void Restart()
    {
        System.Diagnostics.Process.Start(Environment.ProcessPath!, RestartArg);
        ExitApp();
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
        _overlay?.Close();
        if (_host is not null)
        {
            _host.Services.GetRequiredService<DiscordPresenceService>().Dispose();
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
