using System.Net.Http;
using System.Windows;
using FortniteTracker.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FortniteTracker.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((ctx, services) =>
            {
                services.AddMemoryCache();
                services.AddSingleton(_ => new ApiKeyStore(ctx.Configuration["FortniteApi:Key"]));
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
                services.AddSingleton<MainWindow>();
            })
            .Build();

        var services = _host.Services;
        var tracker = services.GetRequiredService<LobbyTracker>();
        var stats = services.GetRequiredService<FortniteStatsService>();

        // Subscribe before the tailer starts so the initial replay of the log is not missed.
        services.GetRequiredService<FortniteLogTailer>().LineRead += line =>
        {
            if (FortniteLogParser.Parse(line) is { } gameEvent) tracker.Handle(gameEvent);
        };
        services.GetRequiredService<ApiKeyStore>().Changed += () =>
        {
            stats.ClearCache();
            tracker.Refresh();
        };

        services.GetRequiredService<MainWindow>().Show();
        await _host.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
