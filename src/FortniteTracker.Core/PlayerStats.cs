namespace FortniteTracker.Core;

/// <summary>Hidden: the player uses Streamer Mode, so Fortnite never revealed their real name.</summary>
public enum StatsStatus { Ok, Private, NotFound, NoApiKey, Error, Hidden, Loading }

/// <summary>Fortnite item rarity colours, used to grade a stat at a glance.</summary>
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>How dangerous a player looks from their season stats.</summary>
public enum Threat { BotLikely, Casual, Average, Skilled, Sweat }

public sealed record ModeStats(int Wins, double WinRate, double Kd, int Kills, int Matches)
{
    public Rarity KdRarity => StatGrades.ForKd(Kd);
    public Rarity WinRateRarity => StatGrades.ForWinRate(WinRate);
}

/// <summary>
/// Season stats for one player. <see cref="ByMode"/> uses fortnite-api's buckets: solo, duo,
/// squad and ltm (Ranked and limited-time modes count as ltm).
/// </summary>
public sealed record PlayerStats(
    string? AccountId,
    string? EpicName,
    StatsStatus Status,
    ModeStats? Overall = null,
    IReadOnlyDictionary<string, ModeStats>? ByMode = null)
{
    public int? Wins => Overall?.Wins;
    public double? WinRate => Overall?.WinRate;
    public double? Kd => Overall?.Kd;
    public int? Kills => Overall?.Kills;
    public int? Matches => Overall?.Matches;

    /// <summary>Stats for a bucket ("duo", "ltm"...), falling back to all modes.</summary>
    public ModeStats? For(string? bucket) =>
        bucket is not null && ByMode?.TryGetValue(bucket, out var m) == true ? m : Overall;

    public Threat? Threat => StatGrades.ThreatOf(this);
}

/// <summary>
/// Thresholds for rarity colours and threat levels. Season K/D in public matchmaking averages
/// around 1, so the scale is centred there.
/// </summary>
public static class StatGrades
{
    public static Rarity ForKd(double kd) => kd switch
    {
        >= 5 => Rarity.Legendary,
        >= 3 => Rarity.Epic,
        >= 2 => Rarity.Rare,
        >= 1 => Rarity.Uncommon,
        _ => Rarity.Common,
    };

    public static Rarity ForWinRate(double winRate) => winRate switch
    {
        >= 20 => Rarity.Legendary,
        >= 12 => Rarity.Epic,
        >= 7 => Rarity.Rare,
        >= 3 => Rarity.Uncommon,
        _ => Rarity.Common,
    };

    public static Threat? ThreatOf(PlayerStats p)
    {
        // Bots aren't real accounts, so their names aren't found. A renamed player isn't found
        // either, hence "likely".
        if (p.Status == StatsStatus.NotFound) return Core.Threat.BotLikely;
        if (p is not { Status: StatsStatus.Ok, Overall: { } o }) return null;
        if (o.Matches < 5) return Core.Threat.BotLikely;
        return ThreatOf(o.Kd, o.WinRate);
    }

    public static Threat ThreatOf(double kd, double winRate) =>
        kd >= 4 || winRate >= 15 ? Threat.Sweat
        : kd >= 2 ? Threat.Skilled
        : kd < 0.8 ? Threat.Casual
        : Threat.Average;
}
