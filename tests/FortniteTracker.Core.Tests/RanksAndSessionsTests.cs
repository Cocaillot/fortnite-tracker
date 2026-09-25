using FortniteTracker.Core;
using static FortniteTracker.Core.Tests.FortniteLogParserTests;

namespace FortniteTracker.Core.Tests;

public sealed class RanksAndSessionsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // Real line format (Sept 2026), IDs replaced; one line carries several tracks and accounts.
    private static readonly string RankLine =
        "[2026.09.25-02.38.09:884][502]LogOnline: MCP: [FOnlineAccountHabaneroServiceMcp::HandleJSONResponseForAccountsHabaneroProgress] " +
        $"{{HabaneroProgress: {{LastUpdatedTime: '{DateTime.UtcNow.AddDays(-1):yyyy-MM-ddTHH:mm:ss.fffZ}' / GameId: 'fortnite', AccountId: '{Self}' / HabaneroType: 'ranked-blastberry-combined' / TrackGUID: 'B07G3D' / Current rank: 4 / Highest rank: 5 / CurrentPlayerPosition: 2147483647 / ProgressTowardsNextHabanero: 0.730000}}.}}," +
        $"{{HabaneroProgress: {{LastUpdatedTime: '1970-01-01T00:00:00.000Z' / GameId: 'fortnite', AccountId: '{Self}' / HabaneroType: 'ranked-pimlico' / TrackGUID: 'XXXXXX' / Current rank: 0 / Highest rank: 0 / CurrentPlayerPosition: 2147483647 / ProgressTowardsNextHabanero: 0.000000}}.}}," +
        $"{{HabaneroProgress: {{LastUpdatedTime: '2025-05-04T10:00:00.000Z' / GameId: 'fortnite', AccountId: '{Mate}' / HabaneroType: 'ranked_blastberry_build' / TrackGUID: 'S4B3R5' / Current rank: 17 / Highest rank: 17 / CurrentPlayerPosition: 1234 / ProgressTowardsNextHabanero: 0.460000}}.}}";

    [Fact]
    public void Parses_every_rank_entry_on_a_line()
    {
        var e = Assert.IsType<RanksSeen>(FortniteLogParser.Parse(RankLine));

        Assert.Equal(3, e.Ranks.Count);
        var reload = e.Ranks[0];
        Assert.Equal((Self, "ranked-blastberry-combined", 4, 5, 0.73), (reload.AccountId, reload.Track, reload.Current, reload.Highest, reload.Progress));
        Assert.Equal("Reload", reload.TrackName);
        Assert.Equal("Silver II", reload.RankName);
        Assert.Equal("Silver III", reload.HighestName);
        Assert.True(reload.IsCurrentSeason);

        Assert.Null(e.Ranks[1].LastUpdatedUtc);           // 1970 = never played
        Assert.Equal("Unranked", e.Ranks[1].RankName);
        Assert.Equal("Crown Jam", e.Ranks[1].TrackName);

        Assert.Equal("Unreal #1234", e.Ranks[2].RankName);
        Assert.False(e.Ranks[2].IsCurrentSeason);
    }

    [Theory]
    [InlineData(0, "Bronze I")]
    [InlineData(8, "Gold III")]
    [InlineData(15, "Elite")]
    [InlineData(16, "Champion")]
    [InlineData(21, "Rank 21")] // seen on 2026 combined tracks; not guessed
    public void Rank_names(int rank, string expected) => Assert.Equal(expected, RankNames.Name(rank));

    [Fact]
    public void Rank_book_keeps_the_newest_entry_and_persists()
    {
        var path = Path.Combine(_dir, "ranks.json");
        var book = new RankBook(path);
        var now = DateTime.UtcNow;
        var newer = new RankProgress(Self, "ranked-br-combined", 6, 8, 0.5, null, now);
        book.Add([newer]);
        book.Add([newer with { Current = 3, LastUpdatedUtc = now.AddDays(-30) }]); // older snapshot from a backup log
        book.Add([newer with { Current = 0, LastUpdatedUtc = null }]);             // never-played placeholder

        var reloaded = new RankBook(path);

        Assert.Equal(newer, Assert.Single(reloaded.For(Self)));
        Assert.Equal(newer, reloaded.Latest(Self));
    }

    [Fact]
    public void Session_delta_is_the_difference_in_season_stats()
    {
        var before = new ModeStats(Wins: 1, WinRate: 3.2, Kd: 1.27, Kills: 38, Matches: 31, Deaths: 30);
        var after = new ModeStats(Wins: 2, WinRate: 5.7, Kd: 1.4, Kills: 50, Matches: 35, Deaths: 33);

        var d = new SessionRecord(DateTime.UtcNow, DateTime.UtcNow, before, after).Delta!;

        Assert.Equal((1, 12, 4, 3), (d.Wins, d.Kills, d.Matches, d.Deaths));
        Assert.Equal(4.0, d.Kd);
        Assert.Equal(25.0, d.WinRate);
    }

    [Fact]
    public void Matches_close_together_share_a_session()
    {
        var store = new SessionStore(Path.Combine(_dir, "sessions.json"));
        var t0 = DateTime.UtcNow.AddHours(-5);
        var stats = new ModeStats(1, 3, 1, 38, 31);

        store.MatchStarted(t0, stats);
        store.MatchStarted(t0.AddMinutes(20), stats with { Matches = 32 });
        store.MatchStarted(t0.AddMinutes(20 + 60), stats with { Matches = 40 }); // an hour's break
        store.StatsUpdated(t0.AddMinutes(20), stats with { Matches = 33, Kills = 45 });

        var sessions = new SessionStore(Path.Combine(_dir, "sessions.json")).All;
        Assert.Equal(2, sessions.Count);
        Assert.Equal(31, sessions[0].Baseline!.Matches); // baseline stays at the first match
        Assert.Equal(7, sessions[0].Delta!.Kills);
        Assert.Null(sessions[1].Latest);
    }
}

public class TrackNameTests
{
    [Theory]
    [InlineData("ranked-blastberry-combined", "Reload", true)]
    [InlineData("ranked-feral", "Ballistic", true)]
    [InlineData("ranked-squareclub", "Arenas Boxfights", true)]
    [InlineData("ranked-pimlico", "Crown Jam", true)]
    [InlineData("ranked-bling", "Bling", false)]
    [InlineData("ranked-bling-nobuild", "Bling (Zero Build)", false)]
    [InlineData("ranked-blastberry-nobuild", "Reload (Zero Build)", true)]
    [InlineData("RadiantToothpick-duos-ranked", "Radiant Toothpick (Duos)", false)]
    public void Track_names(string track, string name, bool confirmed)
    {
        Assert.Equal(name, RankNames.TrackName(track));
        Assert.Equal(confirmed, RankNames.IsKnownTrack(track));
    }
}

public sealed class RankHistoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private const string Me = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1";
    private static readonly DateTime T0 = DateTime.UtcNow.AddHours(-3);

    private static RankProgress Reload(string guid, int current, double progress, DateTime at, int? highest = null) =>
        new(Me, "ranked-blastberry-combined", current, highest ?? current, progress, null, at, guid);

    [Fact]
    public void Seasons_of_the_same_mode_are_kept_apart()
    {
        var book = new RankBook(Path.Combine(_dir, "ranks.json"));
        book.Add([Reload("OLD", 4, 0.73, T0.AddDays(-80)), Reload("NOW", 1, 0.2, T0)]);

        Assert.Equal("NOW", Assert.Single(book.For(Me)).TrackGuid);          // current season per mode
        Assert.Equal(["NOW", "OLD"], book.Seasons(Me).Select(s => s.TrackGuid)); // every season
    }

    [Fact]
    public void Rank_updates_build_a_history_and_report_changes()
    {
        var path = Path.Combine(_dir, "ranks.json");
        var book = new RankBook(path);
        var changes = new List<RankChange>();
        book.RankChanged += changes.Add;

        book.Add([Reload("NOW", 0, 0.14, T0)]);
        book.Add([Reload("NOW", 0, 0.61, T0.AddMinutes(30))]);
        book.Add([Reload("NOW", 1, 0.02, T0.AddMinutes(60))]);
        book.Add([Reload("NOW", 1, 0.02, T0.AddMinutes(60))]); // the same update seen again

        Assert.Equal([0.14, 0.61, 0.02], book.History(Me, "ranked-blastberry-combined", "NOW").Select(p => p.Progress));
        var up = Assert.Single(changes);
        Assert.True(up.IsUp);
        Assert.Equal((0, 1), (up.Before.Current, up.After.Current));

        // History survives a restart.
        Assert.Equal(3, new RankBook(path).History(Me, "ranked-blastberry-combined", "NOW").Count);
    }

    [Fact]
    public void Ranks_file_from_before_history_still_loads()
    {
        var path = Path.Combine(_dir, "ranks.json");
        File.WriteAllText(path, $$"""[{"AccountId":"{{Me}}","Track":"ranked-br-combined","Current":6,"Highest":8,"Progress":0.5,"Position":null,"LastUpdatedUtc":"2026-09-01T10:00:00Z"}]""");

        var r = Assert.Single(new RankBook(path).For(Me));

        Assert.Equal(("ranked-br-combined", 6, ""), (r.Track, r.Current, r.TrackGuid));
    }
}

public sealed class PlayerNotesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Notes_are_found_by_account_or_in_game_name_and_persist()
    {
        var path = Path.Combine(_dir, "notes.json");
        var notes = new PlayerNotes(path);
        notes.Save(null, "Rival", ["Teamer"], "camps boxes");
        notes.Save("acc1", "Friend", ["Good duo"], "");

        var reloaded = new PlayerNotes(path);

        Assert.Equal("camps boxes", reloaded.Find(null, "rival")!.Text); // name match ignores case
        Assert.Equal(["Good duo"], reloaded.Find("acc1", "Renamed")!.Tags);
    }

    [Fact]
    public void Saving_an_empty_note_deletes_it()
    {
        var notes = new PlayerNotes(Path.Combine(_dir, "notes.json"));
        notes.Save(null, "Rival", ["Teamer"], "");
        notes.Save(null, "Rival", [], "  ");

        Assert.Null(notes.Find(null, "Rival"));
        Assert.Empty(notes.All);
    }
}
