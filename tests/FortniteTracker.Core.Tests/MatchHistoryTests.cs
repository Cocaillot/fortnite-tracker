using FortniteTracker.Core;
using static FortniteTracker.Core.Tests.FortniteLogParserTests;

namespace FortniteTracker.Core.Tests;

public sealed class MatchHistoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static readonly DateTime T0 = new(2026, 9, 25, 2, 39, 0, DateTimeKind.Utc);

    [Fact]
    public void Importer_reads_matches_from_a_log_file()
    {
        var log = Path.Combine(_dir, "FortniteGame-backup-2026.09.25-05.03.42.log");
        File.WriteAllLines(log,
        [
            Lines.Login,
            Lines.MateJoined,
            Lines.FriendPresence,
            Lines.SelfPresence,
            Lines.Welcomed,
            "[2026.09.25-02.40.00:000][100]LogFort: some unrelated line",
            Lines.SpectateTeammate,
            Lines.SpectateSelf,
            Lines.Placement,
            Lines.SpectateEliminator,
            Lines.SpectateEliminatorAgain,
            Lines.SpectateNext,
            // A second match the game closed during: recorded as abandoned at end of file.
            Lines.Welcomed.Replace("02.39.05", "02.45.00"),
        ]);

        var store = new MatchHistoryStore(Path.Combine(_dir, "history.json"));
        store.AddRange(MatchHistoryImporter.ReadMatches(log), importedFile: null);
        var matches = store.Recent(10).Reverse().ToList();

        Assert.Equal(2, matches.Count);
        Assert.Equal(new MatchRecord(
            new DateTime(2026, 9, 25, 2, 39, 5, 610, DateTimeKind.Utc),
            new DateTime(2026, 9, 25, 2, 40, 59, 328, DateTimeKind.Utc),
            "Ranked Reload Duos · Build", "Playlist_Habanero_PiperBoot_Duos", SquadSize: 2, Finished: true,
            EliminatedBy: "ライバル Rival 01"), matches[0] with { PartyIds = null });
        Assert.Equal([Mate], matches[0].PartyIds!);
        Assert.False(matches[1].Finished);
    }

    [Fact]
    public void Store_deduplicates_by_start_and_keeps_the_most_complete_record()
    {
        var path = Path.Combine(_dir, "history.json");
        var store = new MatchHistoryStore(path);
        var abandoned = new MatchRecord(T0, null, "Ranked Duos", null, 2, false);
        var finished = abandoned with { EndedUtc = T0.AddMinutes(9), Finished = true };

        store.Add(abandoned);
        store.Add(finished);
        store.Add(abandoned); // replaying an older view must not downgrade it

        Assert.Equal(finished, Assert.Single(store.Recent(10)));
    }

    [Fact]
    public void Store_persists_matches_and_imported_files()
    {
        var path = Path.Combine(_dir, "history.json");
        var match = new MatchRecord(T0, T0.AddMinutes(9), "Ranked Duos", null, 2, true);
        new MatchHistoryStore(path).AddRange([match], importedFile: "FortniteGame-backup-x.log");

        var reloaded = new MatchHistoryStore(path);

        Assert.Equal(match, Assert.Single(reloaded.Recent(10)));
        Assert.True(reloaded.WasImported("FortniteGame-backup-x.log"));
    }

    [Fact]
    public void History_from_an_older_version_reimports_logs_and_keeps_matches()
    {
        var path = Path.Combine(_dir, "history.json");
        var match = new MatchRecord(T0, T0.AddMinutes(9), "Ranked Duos", null, 2, true);
        // A 0.1.0 file: no Version field, no EliminatedBy.
        File.WriteAllText(path, """
            {"Matches":[{"StartedUtc":"2026-09-25T02:39:00Z","EndedUtc":"2026-09-25T02:48:00Z","Mode":"Ranked Duos","Playlist":null,"SquadSize":2,"Finished":true}],
             "ImportedFiles":["FortniteGame-backup-x.log"]}
            """);

        var store = new MatchHistoryStore(path);

        Assert.Equal(match, Assert.Single(store.Recent(10)));
        Assert.False(store.WasImported("FortniteGame-backup-x.log"));
    }

    [Fact]
    public void Kill_counts_from_before_version_7_are_cleared()
    {
        var path = Path.Combine(_dir, "history.json");
        File.WriteAllText(path, """
            {"Matches":[{"StartedUtc":"2026-09-26T11:29:00Z","EndedUtc":"2026-09-26T11:32:00Z","Mode":"Battle Royale Solo","Playlist":"Playlist_DefaultSolo","SquadSize":1,"Finished":true,"Kills":6,"Won":false}],
             "ImportedFiles":[],"Version":6}
            """);

        var m = Assert.Single(new MatchHistoryStore(path).Recent(10));

        Assert.Null(m.Kills);
        Assert.Null(m.Won);
        Assert.Equal("Battle Royale Solo · Build", m.Mode);
    }

    [Theory]
    [InlineData("Playlist_Habanero_RopeSmile_Solo", null, "Ranked Reload Solo · Build")]
    [InlineData("Playlist_Habanero_NoBuild_PunchBerry_Solo", null, "Ranked Reload Solo · Zero Build")]
    [InlineData("Playlist_HabaneroTrio", null, "Ranked Battle Royale Trios · Build")]
    [InlineData("Playlist_Habanero_Sunflower_Duos", null, "Ranked Duos · Build")] // unknown codename: no guess
    [InlineData("Playlist_DefaultSquad", null, "Battle Royale Squads · Build")]
    [InlineData("Playlist_NoBuildBR_Trio", null, "Battle Royale Trios · Zero Build")]
    [InlineData("Playlist_PunchBerryNoBuildSquad", null, "Reload Squads · Zero Build")]
    [InlineData("Playlist_ShowdownTournament_RE_PiperBootSolo_PBM", null, "Tournament Reload Solo · Build")]
    [InlineData("Playlist_ShowdownTournament_BlastBerryNoBuildDuo_PBM", null, "Tournament Reload Duos · Zero Build")]
    [InlineData("Playlist_VK_Play", null, "Creative")]
    [InlineData("Playlist_Something_New", null, "Battle Royale · Build")]
    [InlineData(null, null, "Match")]
    public void Playlist_names(string? playlist, string? level, string expected) =>
        Assert.Equal(expected, PlaylistNames.Describe(playlist, level));
}
