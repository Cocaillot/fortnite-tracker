using FortniteTracker.Core;
using static FortniteTracker.Core.Tests.FortniteLogParserTests;

namespace FortniteTracker.Core.Tests;

public class SessionStateTests
{
    private static readonly DateTime T0 = new(2026, 9, 25, 2, 39, 0, DateTimeKind.Utc);

    private static (SessionState State, List<MatchRecord> Matches) Create()
    {
        var state = new SessionState();
        var matches = new List<MatchRecord>();
        state.MatchCompleted += matches.Add;
        return (state, matches);
    }

    [Fact]
    public void Finished_match_records_mode_squad_and_duration()
    {
        var (s, matches) = Create();
        s.Apply(new LocalPlayerDetected(Self, "PlayerOne"));
        s.Apply(new PartyMemberJoined(Mate));
        s.Apply(new PlaylistSeen(Self, "Playlist_Habanero_PiperBoot_Duos"));
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new MatchEnded { At = T0.AddMinutes(12) });

        var m = Assert.Single(matches);
        Assert.Equal(new MatchRecord(T0, T0.AddMinutes(12), "Ranked Reload Duos · Build", "Playlist_Habanero_PiperBoot_Duos", 2, true), m with { PartyIds = null });
        Assert.Equal([Mate], m.PartyIds!);
        Assert.False(s.InMatch);
    }

    [Fact]
    public void Friends_presence_does_not_change_playlist()
    {
        var (s, _) = Create();
        s.Apply(new LocalPlayerDetected(Self, "PlayerOne"));
        s.Apply(new PlaylistSeen(Self, "Playlist_Habanero_PiperBoot_Duos"));
        s.Apply(new PlaylistSeen("ee3a0...08392", "Playlist_Habanero_RopeSmile_Solo"));

        Assert.Equal("Ranked Reload Duos · Build", s.Mode);
    }

    [Fact]
    public void First_match_of_a_session_gets_its_mode_from_presence_during_the_match()
    {
        var (s, matches) = Create();
        s.Apply(new LocalPlayerDetected(Self, "PlayerOne"));
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        Assert.Equal("Match", s.Mode);

        s.Apply(new PlaylistSeen(Self, "Playlist_Habanero_PunchBerry_Duos") { At = T0.AddSeconds(7) });
        s.Apply(new MatchEnded { At = T0.AddMinutes(5) });

        Assert.Equal("Ranked Reload Duos · Build", Assert.Single(matches).Mode);
    }

    [Fact]
    public void Playlist_is_locked_after_the_first_in_match_presence()
    {
        var (s, matches) = Create();
        s.Apply(new LocalPlayerDetected(Self, "PlayerOne"));
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new PlaylistSeen(Self, "Playlist_Habanero_PiperBoot_Duos"));
        s.Apply(new PlaylistSeen(Self, "Playlist_VK_Play")); // e.g. the party leader queues something else
        s.Apply(new MatchEnded { At = T0.AddMinutes(5) });

        Assert.Equal("Ranked Reload Duos · Build", Assert.Single(matches).Mode);
    }

    [Fact]
    public void Starting_a_new_match_without_placement_records_the_previous_as_abandoned()
    {
        var (s, matches) = Create();
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0.AddMinutes(3) });

        var m = Assert.Single(matches);
        Assert.False(m.Finished);
        Assert.Null(m.EndedUtc);
        Assert.True(s.InMatch);
    }

    [Fact]
    public void Closing_the_game_mid_match_records_it_as_abandoned()
    {
        var (s, matches) = Create();
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new GameRunningChanged(false) { At = T0.AddHours(3) });

        var m = Assert.Single(matches);
        Assert.False(m.Finished);
        Assert.Null(m.EndedUtc);
        Assert.False(s.GameRunning);
    }

    [Fact]
    public void Leaving_a_match_early_ends_it_when_the_menu_loads()
    {
        var (s, matches) = Create();
        s.Apply(new LocalPlayerDetected(Self, "PlayerOne"));
        s.Apply(new PlaylistSeen(Self, "Playlist_Habanero_PunchBerry_Solo"));
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new ReturnedToMenu { At = T0.AddMinutes(1) });

        Assert.False(s.InMatch);
        var m = Assert.Single(matches);
        Assert.False(m.Finished);
        Assert.Equal(T0.AddMinutes(1), m.EndedUtc);
        Assert.Equal("Ranked Reload Solo · Build", m.Mode);
    }

    [Fact]
    public void Returning_to_the_menu_after_placement_changes_nothing()
    {
        var (s, matches) = Create();
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new MatchEnded { At = T0.AddMinutes(5) });

        Assert.False(s.Apply(new ReturnedToMenu { At = T0.AddMinutes(6) }));
        Assert.True(Assert.Single(matches).Finished);
    }

    [Fact]
    public void Creative_island_is_named_from_the_level()
    {
        var (s, matches) = Create();
        s.Apply(new MatchStarted("/VKPlay/Maps/VKPlay_EmptyOcean_VolumeSupport") { At = T0 });
        s.Apply(new MatchEnded { At = T0.AddMinutes(1) });

        Assert.Equal("Creative", Assert.Single(matches).Mode);
    }

    [Fact]
    public void First_player_spectated_after_placement_is_the_eliminator()
    {
        var (s, matches) = Create();
        s.Apply(new LocalPlayerDetected(Self, "PlayerOne"));
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new ViewTargetChanged("Mate Name"));  // you died; camera follows your teammate
        s.Apply(new ViewTargetChanged("PlayerOne"));  // Reload respawn
        s.Apply(new MatchEnded { At = T0.AddMinutes(5) });
        s.Apply(new ViewTargetChanged("PlayerOne"));
        s.Apply(new ViewTargetChanged("Rival"));
        s.Apply(new ViewTargetChanged("Rival"));
        s.Apply(new ViewTargetChanged("Anonyme[272]"));

        Assert.Equal("Rival", s.EliminatedBy);
        Assert.Equal(["Rival", "Anonyme[272]"], s.Spectated);
        // Placement completes the match; the eliminator arrives as an update of the same match.
        Assert.Equal(2, matches.Count);
        Assert.Null(matches[0].EliminatedBy);
        Assert.Equal(matches[0] with { EliminatedBy = "Rival" }, matches[1]);
    }

    [Fact]
    public void Next_match_clears_spectated_players()
    {
        var (s, _) = Create();
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0 });
        s.Apply(new MatchEnded { At = T0.AddMinutes(5) });
        s.Apply(new ViewTargetChanged("Rival"));
        s.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = T0.AddMinutes(7) });
        s.Apply(new ViewTargetChanged("Someone")); // e.g. spectating a teammate in the new match

        Assert.Null(s.EliminatedBy);
        Assert.Empty(s.Spectated);
    }

    [Fact]
    public void New_log_file_resets_party_and_playlist()
    {
        var (s, _) = Create();
        s.Apply(new LocalPlayerDetected(Self, "PlayerOne"));
        s.Apply(new PartyMemberJoined(Mate));
        s.Apply(new PlaylistSeen(Self, "Playlist_Habanero_PiperBoot_Duos"));
        s.Apply(new LogFileOpened());

        Assert.Empty(s.Party);
        Assert.Null(s.Playlist);
    }
}

public class MatchPartyTests
{
    [Fact]
    public void Matches_remember_who_was_in_the_party()
    {
        var state = new SessionState();
        var matches = new List<MatchRecord>();
        state.MatchCompleted += matches.Add;
        var t0 = new DateTime(2026, 9, 25, 2, 39, 0, DateTimeKind.Utc);

        state.Apply(new LocalPlayerDetected(FortniteLogParserTests.Self, "PlayerOne"));
        state.Apply(new PartyMemberJoined(FortniteLogParserTests.Mate));
        state.Apply(new MatchStarted("/Game/Athena/Maps/Athena_Empty") { At = t0 });
        state.Apply(new LocalPartyLeft()); // leaving mid-match doesn't rewrite who you started with
        state.Apply(new MatchEnded { At = t0.AddMinutes(4) });

        Assert.Equal([FortniteLogParserTests.Mate], Assert.Single(matches).PartyIds!);
    }
}
