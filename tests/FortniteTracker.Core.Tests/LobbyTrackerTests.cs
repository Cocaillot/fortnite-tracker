using FortniteTracker.Core;
using Microsoft.Extensions.Caching.Memory;

namespace FortniteTracker.Core.Tests;

public class LobbyTrackerTests
{
    private const string Self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1";
    private const string Mate = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb2";

    // No API key: the stats service answers NoApiKey without touching the network.
    private static LobbyTracker CreateTracker() => new(
        new FortniteStatsService(new HttpClient(), new MemoryCache(new MemoryCacheOptions()), new ApiKeyStore(settingsPath: Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"))))
    {
        Debounce = TimeSpan.FromMilliseconds(10),
    };

    private static async Task<LobbySnapshot> NextSnapshot(LobbyTracker tracker, params GameEvent[] events)
    {
        var tcs = new TaskCompletionSource<LobbySnapshot>();
        tracker.Changed += s => tcs.TrySetResult(s);
        foreach (var e in events) tracker.Handle(e);
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Burst_of_events_publishes_self_then_party()
    {
        var snap = await NextSnapshot(CreateTracker(),
            new LocalPlayerDetected(Self, "PlayerOne"),
            new PartyMemberJoined(Self), // the log also announces you joining your own party
            new PartyMemberJoined(Mate),
            new MatchStarted("/Game/Athena/Maps/Athena_Empty"));

        Assert.True(snap.InMatch);
        Assert.Equal("PlayerOne", snap.LocalName);
        Assert.Equal([Self, Mate], snap.Squad.Select(p => p.AccountId));
    }

    [Fact]
    public async Task Leaving_the_party_keeps_only_self()
    {
        var snap = await NextSnapshot(CreateTracker(),
            new LocalPlayerDetected(Self, "PlayerOne"),
            new PartyMemberJoined(Mate),
            new LocalPartyLeft(),
            new MatchStarted("/Game/Athena/Maps/Athena_Empty"),
            new MatchEnded());

        Assert.False(snap.InMatch);
        Assert.Equal([Self], snap.Squad.Select(p => p.AccountId));
    }

    [Fact]
    public async Task Closing_the_game_mid_match_clears_in_match()
    {
        var snap = await NextSnapshot(CreateTracker(),
            new LocalPlayerDetected(Self, "PlayerOne"),
            new MatchStarted("/Game/Athena/Maps/Athena_Empty"),
            new GameRunningChanged(false));

        Assert.False(snap.GameRunning);
        Assert.False(snap.InMatch);
    }
}
