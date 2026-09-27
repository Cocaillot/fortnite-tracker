using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

public sealed class ReplayMatchingTests
{
    // Real timings (Sept 2026): the log sees the match 7–12 s before the replay's own start.
    private static readonly DateTime T0 = new(2026, 9, 27, 22, 57, 30, DateTimeKind.Utc);

    private static readonly ReplayDetails Details = new(18, 96, 0, 1, 112, 0.19, "Rival", []);

    private static MatchRecord Match(double secondsBeforeReplay, string playlist = "Playlist_HabaneroDuo") =>
        new(T0.AddSeconds(-secondsBeforeReplay), T0.AddMinutes(6), PlaylistNames.Describe(playlist), playlist, 2, true);

    [Fact]
    public void A_replay_goes_to_the_match_the_log_saw_just_before_it()
    {
        var previous = Match(8 * 60 + 8);
        var right = Match(8);
        var next = Match(-8 * 60);

        var found = ReplayParser.MatchFor(new ParsedReplay(T0, "Playlist_HabaneroDuo", Details), [previous, right, next]);

        Assert.Equal(right, found);
    }

    [Fact]
    public void A_different_playlist_is_not_the_same_match()
    {
        var creative = Match(8, "Playlist_VK_Play");

        Assert.Null(ReplayParser.MatchFor(new ParsedReplay(T0, "Playlist_HabaneroDuo", Details), [creative]));
    }

    [Fact]
    public void A_replay_with_no_match_nearby_matches_nothing()
    {
        Assert.Null(ReplayParser.MatchFor(new ParsedReplay(T0, "Playlist_HabaneroDuo", Details), [Match(10 * 60)]));
    }

    [Fact]
    public void A_replay_Fortnite_is_still_writing_is_left_alone()
    {
        var dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;
        try
        {
            var path = Path.Combine(dir, "UnsavedReplay-test.replay");
            using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                writer.Write(new byte[64]);
                Assert.True(ReplayParser.IsBeingWritten(path));
                Assert.Null(ReplayParser.Read(path));
            }
            Assert.False(ReplayParser.IsBeingWritten(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void An_unreadable_replay_is_retried_before_being_given_up()
    {
        // 0.9.7 gave up on a replay after one failed read, which happened while Fortnite was still writing it.
        var dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;
        try
        {
            var demos = Directory.CreateDirectory(Path.Combine(dir, "demos")).FullName;
            File.WriteAllBytes(Path.Combine(demos, "UnsavedReplay-broken.replay"), new byte[64]);
            var state = Path.Combine(dir, "replays-read.json");
            var watcher = new ReplayWatcher(
                new MatchHistoryStore(Path.Combine(dir, "history.json")),
                new SettingsStore(settingsPath: Path.Combine(dir, "settings.json")),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ReplayWatcher>.Instance)
            {
                Directory = demos,
                StatePath = state,
            };

            watcher.Scan(T0);
            watcher.Scan(T0.AddMinutes(1));
            Assert.False(File.Exists(state)); // not given up yet: it will be tried again

            watcher.Scan(T0.AddDays(2));
            Assert.Contains("UnsavedReplay-broken.replay", File.ReadAllText(state));
            Assert.True(File.Exists(Path.Combine(demos, "UnsavedReplay-broken.replay"))); // never deleted unread
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void The_log_merging_the_match_again_keeps_the_replay_and_its_eliminator()
    {
        var dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;
        try
        {
            var history = new MatchHistoryStore(Path.Combine(dir, "history.json"));
            var fromLog = Match(8) with { Finished = false };
            history.Add(fromLog);
            history.Update(fromLog.StartedUtc, m => m with { Replay = Details, Kills = 0, EliminatedBy = "Rival" });

            // Later the log adds what it knows (finished, and its guess of the eliminator).
            history.Add(fromLog with { Finished = true, EliminatedBy = "SomeoneSpectated" });

            var m = history.Get(fromLog.StartedUtc)!;
            Assert.True(m.Finished);
            Assert.Equal("Rival", m.EliminatedBy);
            Assert.Equal(18, m.Replay!.Placement);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
