using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

public sealed class MatchResultLedgerTests
{
    private static readonly DateTime T0 = new(2026, 9, 26, 11, 29, 0, DateTimeKind.Utc);

    private static ModeStats Stats(int matches, int kills, int wins, DateTime? lastModified) =>
        new(wins, 0, 0, kills, matches, LastModified: lastModified);

    [Fact]
    public void Quick_matches_each_get_their_own_kills()
    {
        // Three ~3-minute matches in a row; fortnite-api records each one a few minutes late, so the
        // next match has already started when the previous one shows up. (Before the ledger, all
        // three were given the first match's 6 kills.)
        var ledger = new MatchResultLedger();
        ledger.Observe(Stats(32, 38, 1, T0.AddMinutes(-20)), T0); // read at the start of match A

        ledger.Finished(T0, T0.AddMinutes(3));                                   // A
        Assert.Empty(ledger.Observe(Stats(32, 38, 1, T0.AddMinutes(-20)), T0.AddMinutes(3.5))); // B starts: A not in yet
        ledger.Finished(T0.AddMinutes(3.5), T0.AddMinutes(5.5));                  // B

        var a = ledger.Observe(Stats(33, 44, 1, T0.AddMinutes(3.2)), T0.AddMinutes(6));
        Assert.Equal([(T0, 6, false)], a);

        ledger.Finished(T0.AddMinutes(6), T0.AddMinutes(9));                      // C
        var b = ledger.Observe(Stats(34, 45, 1, T0.AddMinutes(5.7)), T0.AddMinutes(9.5));
        Assert.Equal([(T0.AddMinutes(3.5), 1, false)], b);

        var c = ledger.Observe(Stats(35, 45, 1, T0.AddMinutes(9.1)), T0.AddMinutes(12));
        Assert.Equal([(T0.AddMinutes(6), 0, false)], c);
        Assert.False(ledger.HasPending);
    }

    [Fact]
    public void Two_matches_in_one_update_stay_unknown()
    {
        var ledger = new MatchResultLedger();
        ledger.Observe(Stats(10, 20, 0, T0.AddMinutes(-30)), T0);
        ledger.Finished(T0, T0.AddMinutes(4));
        ledger.Finished(T0.AddMinutes(5), T0.AddMinutes(9));

        Assert.Empty(ledger.Observe(Stats(12, 27, 1, T0.AddMinutes(9.2)), T0.AddMinutes(10)));
        Assert.False(ledger.HasPending);

        // The next match is measured from the new totals, not from the old ones.
        ledger.Finished(T0.AddMinutes(11), T0.AddMinutes(15));
        Assert.Equal([(T0.AddMinutes(11), 2, false)], ledger.Observe(Stats(13, 29, 1, T0.AddMinutes(15.1)), T0.AddMinutes(16)));
    }

    [Fact]
    public void A_match_Epic_did_not_count_does_not_take_the_next_ones_result()
    {
        var ledger = new MatchResultLedger();
        ledger.Observe(Stats(10, 20, 0, T0.AddMinutes(-30)), T0);
        ledger.Finished(T0, T0.AddMinutes(1));                  // left in the warm-up: never counted
        ledger.Finished(T0.AddMinutes(2), T0.AddMinutes(12));   // a real match

        var result = ledger.Observe(Stats(11, 23, 1, T0.AddMinutes(12.3)), T0.AddMinutes(13));

        Assert.Equal([(T0.AddMinutes(2), 3, true)], result);
        Assert.False(ledger.HasPending);
    }

    [Fact]
    public void A_match_that_never_shows_up_is_dropped()
    {
        var ledger = new MatchResultLedger();
        ledger.Observe(Stats(10, 20, 0, T0.AddMinutes(-30)), T0);
        ledger.Finished(T0, T0.AddMinutes(3));

        Assert.Empty(ledger.Observe(Stats(10, 20, 0, T0.AddMinutes(-30)), T0.AddMinutes(3) + MatchResultLedger.GiveUpAfter + TimeSpan.FromMinutes(1)));
        Assert.False(ledger.HasPending);
    }

    [Fact]
    public void A_new_season_resets_the_baseline()
    {
        var ledger = new MatchResultLedger();
        ledger.Observe(Stats(300, 500, 20, T0.AddDays(-1)), T0);
        ledger.Finished(T0, T0.AddMinutes(5));

        Assert.Empty(ledger.Observe(Stats(1, 4, 0, T0.AddMinutes(5.2)), T0.AddMinutes(6))); // season stats started over
        ledger.Finished(T0.AddMinutes(7), T0.AddMinutes(12));
        Assert.Equal([(T0.AddMinutes(7), 2, false)], ledger.Observe(Stats(2, 6, 0, T0.AddMinutes(12.2)), T0.AddMinutes(13)));
    }
}
