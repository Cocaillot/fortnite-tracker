using System.Net;
using FortniteTracker.Core;
using Microsoft.Extensions.Caching.Memory;
using static FortniteTracker.Core.Tests.FortniteLogParserTests;

namespace FortniteTracker.Core.Tests;

public sealed class MatchInsightsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static readonly DateTime T0 = new(2026, 9, 26, 11, 0, 0, DateTimeKind.Utc);
    private const string Reload = "ranked-blastberry-combined";

    private sealed class NotFound : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private (MatchInsights Insights, MatchHistoryStore History, RankBook Ranks) Create()
    {
        var settings = new SettingsStore("test-key", Path.Combine(_dir, "settings.json"));
        var stats = new FortniteStatsService(
            new HttpClient(new NotFound()) { BaseAddress = new Uri("https://fortnite-api.com/") },
            new MemoryCache(new MemoryCacheOptions()), settings);
        var tracker = new LobbyTracker(stats);
        tracker.Handle(new LocalPlayerDetected(Self, "PlayerOne") { At = T0 });
        var history = new MatchHistoryStore(Path.Combine(_dir, "history.json"));
        var ranks = new RankBook(Path.Combine(_dir, "ranks.json"));
        return (new MatchInsights(history, ranks, stats, tracker), history, ranks);
    }

    private static MatchRecord Match(double startMin, double endMin, string playlist) =>
        new(T0.AddMinutes(startMin), T0.AddMinutes(endMin), PlaylistNames.Describe(playlist), playlist, 1, true);

    private static void Rank(RankBook ranks, double atMin, int current, double progress) =>
        ranks.Add([new RankProgress(Self, Reload, current, current, progress, null, T0.AddMinutes(atMin), "M3DK1T")]);

    [Fact]
    public void Ranked_matches_get_their_own_rank_change_like_on_the_end_screen()
    {
        // A real afternoon (Sept 2026): Reload ranked matches, "Play again" once, then unranked
        // Battle Royale, then Reload ranked again.
        var (insights, history, ranks) = Create();
        Rank(ranks, -30, 3, 0.08);
        var a = Match(10, 14, "Playlist_Habanero_NoBuild_PunchBerry_Solo");     // rank updated after it: +0%
        Rank(ranks, 14.2, 3, 0.08);
        var b = Match(15, 18.5, "Playlist_Habanero_NoBuild_PunchBerry_Solo");   // "Play again": no update
        var c = Match(20.5, 27.5, "Playlist_Habanero_NoBuild_PunchBerry_Solo"); // update covers b and c
        Rank(ranks, 27.6, 3, 0.51);
        var br = Match(78, 82, "Playlist_DefaultSolo");                         // unranked
        var d = Match(85, 93, "Playlist_Habanero_NoBuild_PunchBerry_Solo");     // the +32% screenshot
        Rank(ranks, 93.3, 3, 0.83);
        foreach (var m in new[] { a, b, c, br, d }) history.Add(m);

        Assert.Equal(0, insights.RankMoveFor(a)!.Delta);
        Assert.Null(insights.RankMoveFor(b));
        var both = insights.RankMoveFor(c)!;
        Assert.Equal(43, both.Delta);
        Assert.Equal(2, both.Matches);
        Assert.Null(insights.RankMoveFor(br)); // unranked: the next match's +32% is not its own
        var last = insights.RankMoveFor(d)!;
        Assert.Equal(32, last.Delta);
        Assert.Equal(1, last.Matches);
        Assert.Equal("Silver I", last.AfterName);
    }

    [Fact]
    public void A_Battle_Royale_match_does_not_take_a_Reload_rank_change()
    {
        var (insights, history, ranks) = Create();
        Rank(ranks, -30, 3, 0.10);
        var br = Match(0, 8, "Playlist_HabaneroTrio"); // ranked Battle Royale
        history.Add(br);
        Rank(ranks, 8.2, 3, 0.40);                      // a Reload update in the same window

        Assert.Null(insights.RankMoveFor(br));
    }
}
