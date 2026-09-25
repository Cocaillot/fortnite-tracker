using System.Net;
using FortniteTracker.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using static FortniteTracker.Core.Tests.FortniteLogParserTests;

namespace FortniteTracker.Core.Tests;

public sealed class MatchResultTrackerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Answers each stats request with the next response in the queue (the last one repeats).</summary>
    private sealed class QueueHandler(params string[] bodies) : HttpMessageHandler
    {
        private int _next;
        public int Requests => _next;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = bodies[Math.Min(_next++, bodies.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private (LobbyTracker Tracker, MatchHistoryStore History, QueueHandler Handler) Create(params string[] responses)
    {
        var settings = new SettingsStore("test-key", Path.Combine(_dir, "settings.json"));
        var handler = new QueueHandler(responses);
        var stats = new FortniteStatsService(
            new HttpClient(handler) { BaseAddress = new Uri("https://fortnite-api.com/") },
            new MemoryCache(new MemoryCacheOptions()), settings);
        var tracker = new LobbyTracker(stats) { Debounce = TimeSpan.FromMilliseconds(1) };
        var history = new MatchHistoryStore(Path.Combine(_dir, "history.json"));
        tracker.MatchCompleted += history.Add;
        _ = new MatchResultTracker(tracker, stats, history, NullLogger<MatchResultTracker>.Instance)
        {
            Delay = _ => Task.Delay(10),
        };
        return (tracker, history, handler);
    }

    private static async Task<MatchRecord> WaitFor(MatchHistoryStore history, Func<MatchRecord, bool> condition)
    {
        for (var i = 0; i < 200; i++)
        {
            if (history.Recent(1).FirstOrDefault() is { } m && condition(m)) return m;
            await Task.Delay(10);
        }
        throw new TimeoutException("History never reached the expected state");
    }

    [Fact]
    public async Task Kills_and_win_come_from_the_stats_difference()
    {
        // Snapshot/before: 30 matches. First poll: not updated yet. Second: the match landed.
        var (tracker, history, _) = Create(
            FortniteStatsService_Json(30, 38, 1), FortniteStatsService_Json(30, 38, 1), FortniteStatsService_Json(31, 43, 2));
        var now = DateTime.UtcNow;

        tracker.Handle(new LocalPlayerDetected(Self, "PlayerOne") { At = now });
        tracker.Handle(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = now });
        await Task.Delay(50); // let the "before" lookup run
        tracker.Handle(new MatchEnded { At = now.AddMinutes(1) });

        var m = await WaitFor(history, m => m.Kills is not null);
        Assert.Equal(5, m.Kills);
        Assert.True(m.Won);
    }

    [Fact]
    public async Task Two_matches_landing_at_once_leave_the_result_unknown()
    {
        var (tracker, history, handler) = Create(FortniteStatsService_Json(30, 38, 1), FortniteStatsService_Json(32, 45, 1));
        var now = DateTime.UtcNow;

        tracker.Handle(new LocalPlayerDetected(Self, "PlayerOne") { At = now });
        tracker.Handle(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = now });
        await Task.Delay(50);
        tracker.Handle(new MatchEnded { At = now.AddMinutes(1) });
        await Task.Delay(300);

        var m = Assert.Single(history.Recent(10));
        Assert.Null(m.Kills);
        Assert.Null(m.Won);
    }

    [Fact]
    public async Task Replayed_matches_from_the_log_are_not_tracked()
    {
        var (tracker, history, handler) = Create(FortniteStatsService_Json(30, 38, 1));
        var longAgo = DateTime.UtcNow.AddHours(-3);

        tracker.Handle(new LocalPlayerDetected(Self, "PlayerOne") { At = longAgo });
        tracker.Handle(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = longAgo });
        tracker.Handle(new MatchEnded { At = longAgo.AddMinutes(5) });
        await Task.Delay(300);

        Assert.Null(Assert.Single(history.Recent(10)).Kills);
    }

    [Fact]
    public async Task Opponents_keep_the_name_seen_in_game()
    {
        // A console player: found under their PSN name, but the API returns their Epic name.
        var settings = new SettingsStore("test-key", Path.Combine(_dir, "s.json"));
        var stats = new FortniteStatsService(
            new HttpClient(new QueueHandler(FortniteStatsServiceTests.StatsJson(130, 800, 21, name: "EpicNameOfPlayer")))
            {
                BaseAddress = new Uri("https://fortnite-api.com/"),
            },
            new MemoryCache(new MemoryCacheOptions()), settings);

        var p = await stats.GetByDisplayNameAsync("Console-Name", CancellationToken.None);

        Assert.Equal(StatsStatus.Ok, p.Status);
        Assert.Equal("Console-Name", p.EpicName);
    }

    [Fact]
    public void Store_keeps_stats_details_when_the_log_adds_an_eliminator()
    {
        var history = new MatchHistoryStore(Path.Combine(_dir, "h.json"));
        var t0 = new DateTime(2026, 9, 25, 2, 39, 0, DateTimeKind.Utc);
        var placed = new MatchRecord(t0, t0.AddMinutes(5), "Ranked Duos", null, 2, true);

        history.Add(placed);
        history.Update(t0, m => m with { Kills = 4, Won = false });
        history.Add(placed with { EliminatedBy = "Rival" });

        Assert.Equal(placed with { EliminatedBy = "Rival", Kills = 4, Won = false }, history.Get(t0));
    }

    private static string FortniteStatsService_Json(int matches, int kills, int wins) =>
        FortniteStatsServiceTests.StatsJson(matches, kills, wins);
}
