using System.Text.Json;
using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

public class FortniteStatsServiceTests
{
    [Fact]
    public void Parses_overall_stats()
    {
        var json = JsonDocument.Parse("""
            {"status":200,"data":{
              "account":{"id":"abc","name":"PlayerOne"},
              "battlePass":{"level":42,"progress":10},
              "stats":{"all":{"overall":{"wins":12,"kills":340,"kd":2.15,"matches":170,"winRate":7.06}}}
            }}
            """).RootElement;

        var s = FortniteStatsService.Parse(json, "abc", null);

        Assert.Equal(new PlayerStats("abc", "PlayerOne", StatsStatus.Ok, 12, 7.06, 2.15, 340, 170), s);
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
}
