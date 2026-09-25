using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FortniteTracker.Core;

/// <summary>
/// One player's progress on one ranked track, as Fortnite logs it ("Habanero" is Ranked's codename).
/// Fortnite fetches this for you, your party and friends shown in the social panel, never for opponents.
/// </summary>
public sealed record RankProgress(
    string AccountId,
    string Track,
    int Current,
    int Highest,
    double Progress,
    // Leaderboard position, only meaningful at Unreal.
    int? Position,
    // Null when the player never played this track (the log shows 1970-01-01).
    DateTime? LastUpdatedUtc)
{
    public string RankName => RankNames.Name(this);
    public string HighestName => LastUpdatedUtc is null ? "Unranked" : RankNames.Name(Highest);
    public string Tier => RankNames.Tier(this);
    public string TrackName => RankNames.TrackName(Track);
    /// <summary>False when the mode is only known by its codename (the name shown is derived from it).</summary>
    public bool TrackNameConfirmed => RankNames.IsKnownTrack(Track);
    public bool IsCurrentSeason => LastUpdatedUtc is { } at && at > DateTime.UtcNow - RankNames.CurrentSeasonWindow;
}

/// <summary>Fortnite's 18 ranks (Bronze I … Unreal) and the ranked tracks we can name.</summary>
public static class RankNames
{
    // Seasons last about three months; a rank updated within this window is treated as current.
    public static readonly TimeSpan CurrentSeasonWindow = TimeSpan.FromDays(120);

    private static readonly string[] Names =
    [
        "Bronze I", "Bronze II", "Bronze III", "Silver I", "Silver II", "Silver III",
        "Gold I", "Gold II", "Gold III", "Platinum I", "Platinum II", "Platinum III",
        "Diamond I", "Diamond II", "Diamond III", "Elite", "Champion", "Unreal",
    ];

    // Values above 17 appear on 2026 "combined" tracks; their names aren't known, so they show as numbers.
    public static string Name(int rank) => rank < 0 ? "Unranked" : rank < Names.Length ? Names[rank] : $"Rank {rank}";

    public static string Name(RankProgress p) =>
        p.LastUpdatedUtc is null ? "Unranked"
        : p is { Current: 17, Position: { } pos } ? $"Unreal #{pos}"
        : Name(p.Current);

    /// <summary>"Bronze", "Silver"… used for the rank colour.</summary>
    public static string Tier(RankProgress p) =>
        p.LastUpdatedUtc is null ? "Unranked"
        : p.Current >= Names.Length ? "Beyond"
        : Name(p.Current).Split(' ')[0];

    // Codenames with a confirmed name (Epic's codenames, as documented by the Fortnite wiki).
    // Others (e.g. "bling", "RadiantToothpick") get a readable name derived from the codename.
    private static readonly Dictionary<string, string> Tracks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ranked-feral"] = "Ballistic",               // FeralCorgi
        ["ranked-squareclub"] = "Arenas Boxfights",   // SquareClub
        ["ranked-pimlico"] = "Crown Jam",             // Pimlico (Fall Guys)
        ["ranked-blastberry-combined"] = "Reload",
        ["ranked_blastberry_build"] = "Reload (Build)",
        ["ranked-br-combined"] = "Battle Royale",
        ["ranked-br"] = "Battle Royale (Build)",
        ["ranked-zb"] = "Zero Build",
        ["ranked-figment-build"] = "OG (Build)",
        ["ranked-figment-nobuild"] = "OG (Zero Build)",
        ["delmar-competitive"] = "Rocket Racing",
    };

    // Codewords that make up track names, e.g. "ranked-blastberry-nobuild" = Reload (Zero Build).
    private static readonly Dictionary<string, string> Codewords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blastberry"] = "Reload",
        ["br"] = "Battle Royale",
        ["figment"] = "OG",
        ["delmar"] = "Rocket Racing",
        ["feral"] = "Ballistic",
        ["squareclub"] = "Arenas Boxfights",
        ["pimlico"] = "Crown Jam",
    };

    /// <summary>True when the name is confirmed, not just derived from an unknown codename.</summary>
    public static bool IsKnownTrack(string track) =>
        Tracks.ContainsKey(track) || Words(track).All(w => Variant(w) is not null || Codewords.ContainsKey(w) || w.Length == 0);

    public static string TrackName(string track) => Tracks.TryGetValue(track, out var name) ? name : FromCodename(track);

    // "ranked-blastberry-nobuild" → "Reload (Zero Build)", "RadiantToothpick-duos-ranked" → "Radiant Toothpick (Duos)".
    private static string FromCodename(string track)
    {
        var variants = new List<string>();
        var words = new List<string>();
        foreach (var w in Words(track))
        {
            if (Variant(w) is { } v) { if (v.Length > 0) variants.Add(v); }
            else if (Codewords.TryGetValue(w, out var known)) words.Add(known);
            // Split CamelCase and capitalise: "RadiantToothpick" → "Radiant Toothpick".
            else words.Add(Regex.Replace(char.ToUpperInvariant(w[0]) + w[1..], "(?<=[a-z])(?=[A-Z])", " "));
        }
        var name = words.Count > 0 ? string.Join(" ", words) : track;
        return variants.Count > 0 ? $"{name} ({string.Join(" ", variants)})" : name;
    }

    private static IEnumerable<string> Words(string track) =>
        Regex.Split(track, "[-_]").Where(p => p.Length > 0
            && !p.Equals("ranked", StringComparison.OrdinalIgnoreCase)
            && !p.Equals("competitive", StringComparison.OrdinalIgnoreCase));

    // Words describing a variant of a mode; "" for words that add nothing ("combined").
    private static string? Variant(string word) => word.ToLowerInvariant() switch
    {
        "nobuild" or "zb" => "Zero Build",
        "build" => "Build",
        "combined" => "",
        "solo" => "Solo",
        "duos" => "Duos",
        "trios" => "Trios",
        "squads" => "Squads",
        _ => null,
    };
}

/// <summary>
/// Everyone's latest known ranks: you, party members and friends, as they appear in the log.
/// Persisted in ranks.json so friends' ranks survive restarts.
/// </summary>
public sealed class RankBook
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, RankProgress>> _ranks = new();

    public RankBook(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsStore.DefaultDirectory, "ranks.json");
        Load();
    }

    public event Action? Changed;

    public IReadOnlyList<RankProgress> For(string accountId)
    {
        lock (_gate) return _ranks.TryGetValue(accountId, out var t) ? [.. t.Values] : [];
    }

    public IReadOnlyCollection<string> Accounts
    {
        get { lock (_gate) return [.. _ranks.Keys]; }
    }

    /// <summary>The track a player plays most recently: their current rank for the mode they're in.</summary>
    public RankProgress? Latest(string accountId) =>
        For(accountId).Where(r => r.LastUpdatedUtc is not null).MaxBy(r => r.LastUpdatedUtc);

    public void Add(IEnumerable<RankProgress> ranks, bool save = true)
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var r in ranks)
            {
                if (!_ranks.TryGetValue(r.AccountId, out var tracks)) _ranks[r.AccountId] = tracks = new();
                // Keep the newest snapshot; a never-played entry doesn't replace a real one.
                if (tracks.TryGetValue(r.Track, out var existing)
                    && (r.LastUpdatedUtc ?? DateTime.MinValue) < (existing.LastUpdatedUtc ?? DateTime.MinValue)) continue;
                if (existing == r) continue;
                tracks[r.Track] = r;
                changed = true;
            }
            if (changed && save) Save();
        }
        if (changed) Changed?.Invoke();
    }

    public void Flush()
    {
        lock (_gate) Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var all = JsonSerializer.Deserialize<List<RankProgress>>(File.ReadAllText(_path)) ?? [];
            Add(all, save: false);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_ranks.Values.SelectMany(t => t.Values).ToList()));
        File.Move(tmp, _path, overwrite: true);
    }
}

/// <summary>Parses the HabaneroProgress entries of one log line (a line can hold several).</summary>
public static partial class RankParser
{
    [GeneratedRegex(@"\{HabaneroProgress: \{LastUpdatedTime: '(?<at>[^']*)' / GameId: '[^']*', AccountId: '(?<id>[0-9a-f]{32})' / HabaneroType: '(?<track>[^']+)'.*? / Current rank: (?<cur>\d+) / Highest rank: (?<high>\d+) / CurrentPlayerPosition: (?<pos>-?\d+) / ProgressTowardsNextHabanero: (?<prog>[0-9.]+)\}")]
    private static partial Regex Entry();

    public static IReadOnlyList<RankProgress> Parse(string line)
    {
        if (!line.Contains("AccountsHabaneroProgress", StringComparison.Ordinal)) return [];
        var list = new List<RankProgress>();
        foreach (Match m in Entry().Matches(line))
        {
            var at = DateTime.TryParse(m.Groups["at"].Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) && t.Year > 1970 ? t : (DateTime?)null;
            var pos = int.Parse(m.Groups["pos"].Value, CultureInfo.InvariantCulture);
            list.Add(new RankProgress(
                m.Groups["id"].Value,
                m.Groups["track"].Value,
                int.Parse(m.Groups["cur"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["high"].Value, CultureInfo.InvariantCulture),
                double.Parse(m.Groups["prog"].Value, CultureInfo.InvariantCulture),
                pos is > 0 and < int.MaxValue ? pos : null,
                at));
        }
        return list;
    }
}
