using System.Text.Json;
using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

public class FortniteStatsServiceTests
{
    // Shape of a real fortnite-api.com v2 response (Sept 2026): no trio bucket, Ranked counts as ltm.
    public static string StatsJson(int matches, int kills, int wins, string name = "PlayerOne") => $$$"""
        {"status":200,"data":{
          "account":{"id":"abc","name":"{{{name}}}"},
          "battlePass":{"level":42,"progress":10},
          "stats":{"all":{
            "overall":{"wins":{{{wins}}},"kills":{{{kills}}},"kd":2.15,"matches":{{{matches}}},"winRate":7.06},
            "solo":{"wins":0,"kills":0,"kd":0,"matches":1,"winRate":0},
            "duo":null,
            "squad":null,
            "ltm":{"wins":{{{wins}}},"kills":{{{kills}}},"kd":2.3,"matches":{{{matches - 1}}},"winRate":7.5}
          }}
        }}
        """;

    [Fact]
    public void Reads_when_Epic_last_recorded_a_match()
    {
        var json = StatsJson(170, 340, 12).Replace("\"winRate\":7.06}", "\"winRate\":7.06,\"lastModified\":\"2026-09-26T11:46:23Z\"}");

        var s = FortniteStatsService.Parse(JsonDocument.Parse(json).RootElement, "abc", null);

        Assert.Equal(new DateTime(2026, 9, 26, 11, 46, 23, DateTimeKind.Utc), s.Overall!.LastModified);
        Assert.Equal(DateTimeKind.Utc, s.Overall.LastModified!.Value.Kind);
    }

    [Fact]
    public void Parses_overall_and_per_mode_stats()
    {
        var s = FortniteStatsService.Parse(JsonDocument.Parse(StatsJson(170, 340, 12)).RootElement, "abc", null);

        Assert.Equal(StatsStatus.Ok, s.Status);
        Assert.Equal("PlayerOne", s.EpicName);
        Assert.Equal(new ModeStats(12, 7.06, 2.15, 340, 170), s.Overall);
        Assert.Equal(["ltm", "solo"], s.ByMode!.Keys.Order());
        Assert.Equal(2.3, s.For("ltm")!.Kd);
        Assert.Equal(s.Overall, s.For("duo"));  // no duo matches: falls back to all modes
        Assert.Equal(s.Overall, s.For(null));
    }

    [Fact]
    public void Player_without_matches_this_season_gets_zeroes()
    {
        var json = JsonDocument.Parse("""
            {"status":200,"data":{"account":{"id":"abc","name":"PlayerOne"},"stats":{"all":null}}}
            """).RootElement;

        var s = FortniteStatsService.Parse(json, "abc", null);

        Assert.Equal(StatsStatus.Ok, s.Status);
        Assert.Equal(0, s.Matches);
    }

    [Theory]
    [InlineData(0.5, Rarity.Common)]
    [InlineData(1.31, Rarity.Uncommon)]
    [InlineData(2.21, Rarity.Rare)]
    [InlineData(3.6, Rarity.Epic)]
    [InlineData(6.81, Rarity.Legendary)]
    public void Kd_rarity(double kd, Rarity expected) => Assert.Equal(expected, StatGrades.ForKd(kd));

    [Theory]
    [InlineData(6.81, 16.4, 128, Threat.Sweat)]
    [InlineData(1.2, 16, 300, Threat.Sweat)]     // wins a lot even with a modest K/D
    [InlineData(2.21, 8.9, 610, Threat.Skilled)]
    [InlineData(1.28, 2.1, 282, Threat.Average)]
    [InlineData(0.5, 0.5, 40, Threat.Casual)]
    [InlineData(3, 10, 2, Threat.BotLikely)]      // too few matches to judge
    public void Threat_levels(double kd, double winRate, int matches, Threat expected) =>
        Assert.Equal(expected, new PlayerStats(null, "x", StatsStatus.Ok, new ModeStats(1, winRate, kd, 10, matches)).Threat);

    [Fact]
    public void Unknown_player_is_likely_a_bot_and_private_has_no_threat()
    {
        Assert.Equal(Threat.BotLikely, new PlayerStats(null, "x", StatsStatus.NotFound).Threat);
        Assert.Null(new PlayerStats(null, "x", StatsStatus.Private).Threat);
    }

    [Theory]
    [InlineData("Playlist_Habanero_PiperBoot_Duos", "ltm", "Ranked & LTMs")]
    [InlineData("Playlist_DefaultDuo", "duo", "Duos")]
    [InlineData("Playlist_DefaultSolo", "solo", "Solo")]
    [InlineData("Playlist_DefaultSquad", "squad", "Squads")]
    [InlineData("Playlist_Trios", null, "All modes")]
    [InlineData("Playlist_VK_Play", null, "All modes")]
    [InlineData(null, null, "All modes")]
    public void Stats_bucket_for_playlist(string? playlist, string? bucket, string label) =>
        Assert.Equal((bucket, label), PlaylistNames.StatsBucket(playlist));
}
