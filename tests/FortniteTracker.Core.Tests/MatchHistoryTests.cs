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
            Lines.Placement,
            // A second match the game closed during: recorded as abandoned at end of file.
            Lines.Welcomed.Replace("02.39.05", "02.45.00"),
        ]);

        var matches = MatchHistoryImporter.ReadMatches(log);

        Assert.Equal(2, matches.Count);
        Assert.Equal(new MatchRecord(
            new DateTime(2026, 9, 25, 2, 39, 5, 610, DateTimeKind.Utc),
            new DateTime(2026, 9, 25, 2, 40, 59, 328, DateTimeKind.Utc),
            "Ranked Duos", "Playlist_Habanero_PiperBoot_Duos", SquadSize: 2, Finished: true), matches[0]);
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

    [Theory]
    [InlineData("Playlist_Habanero_RopeSmile_Solo", null, "Ranked Solo")]
    [InlineData("Playlist_DefaultSquad", null, "Battle Royale Squads")]
    [InlineData("Playlist_VK_Play", null, "Creative")]
    [InlineData("Playlist_Something_New", null, "Battle Royale")]
    [InlineData(null, null, "Match")]
    public void Playlist_names(string? playlist, string? level, string expected) =>
        Assert.Equal(expected, PlaylistNames.Describe(playlist, level));
}
