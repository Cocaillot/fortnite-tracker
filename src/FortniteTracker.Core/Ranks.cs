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
    DateTime? LastUpdatedUtc,
    // Each season of a mode is a separate track with its own ID.
    string TrackGuid = "")
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
    /// <summary>Index of the top rank, which shows a leaderboard position instead of progress.</summary>
    public const int Unreal = 17;

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

/// <summary>A rank at one point in time, for rank history graphs.</summary>
public sealed record RankPoint(DateTime At, int Current, double Progress);

/// <summary>A rank that moved within one season, e.g. Silver I → Silver II.</summary>
public sealed record RankChange(RankProgress Before, RankProgress After)
{
    public bool IsUp => After.Current > Before.Current;
}

/// <summary>
/// Everyone's ranks as they appear in the log (you, party members, friends): every season of every
/// mode, plus a history of rank points per season. Fortnite re-sends ranks after most matches, each
/// with its update time, which is what builds the history. Persisted in ranks.json.
/// </summary>
public sealed class RankBook
{
    private const int MaxPointsPerSeason = 400;

    private readonly string _path;
    private readonly object _gate = new();
    // account → (mode, season) → newest entry
    private readonly Dictionary<string, Dictionary<(string Track, string Guid), RankProgress>> _ranks = new();
    // (account, mode, season) → points by time
    private readonly Dictionary<(string Account, string Track, string Guid), SortedList<DateTime, RankPoint>> _history = new();

    public RankBook(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsStore.DefaultDirectory, "ranks.json");
        Load();
    }

    public event Action? Changed;

    /// <summary>A rank went up or down within a season (both entries dated; see <see cref="RankChange"/>).</summary>
    public event Action<RankChange>? RankChanged;

    /// <summary>One entry per mode: the season played most recently (or a never-played placeholder).</summary>
    public IReadOnlyList<RankProgress> For(string accountId)
    {
        lock (_gate)
        {
            if (!_ranks.TryGetValue(accountId, out var seasons)) return [];
            return seasons.Values
                .GroupBy(r => r.Track)
                .Select(g => g.MaxBy(r => r.LastUpdatedUtc ?? DateTime.MinValue)!)
                .ToList();
        }
    }

    /// <summary>Every season played, newest first.</summary>
    public IReadOnlyList<RankProgress> Seasons(string accountId)
    {
        lock (_gate)
        {
            return _ranks.TryGetValue(accountId, out var seasons)
                ? seasons.Values.Where(r => r.LastUpdatedUtc is not null).OrderByDescending(r => r.LastUpdatedUtc).ToList()
                : [];
        }
    }

    /// <summary>Rank points of the given season, oldest first.</summary>
    public IReadOnlyList<RankPoint> History(string accountId, string track, string guid)
    {
        lock (_gate) return _history.TryGetValue((accountId, track, guid), out var points) ? [.. points.Values] : [];
    }

    public IReadOnlyCollection<string> Accounts
    {
        get { lock (_gate) return [.. _ranks.Keys]; }
    }

    /// <summary>The mode a player played most recently: their current rank for the mode they're in.</summary>
    public RankProgress? Latest(string accountId) =>
        For(accountId).Where(r => r.LastUpdatedUtc is not null).MaxBy(r => r.LastUpdatedUtc);

    public void Add(IEnumerable<RankProgress> ranks, bool save = true)
    {
        var changed = false;
        var moves = new List<RankChange>();
        lock (_gate)
        {
            foreach (var r in ranks)
            {
                if (r.LastUpdatedUtc is { } at)
                {
                    var key = (r.AccountId, r.Track, r.TrackGuid);
                    if (!_history.TryGetValue(key, out var points)) _history[key] = points = new();
                    if (!points.ContainsKey(at))
                    {
                        points[at] = new RankPoint(at, r.Current, r.Progress);
                        if (points.Count > MaxPointsPerSeason) points.RemoveAt(0);
                        changed = true;
                    }
                }

                if (!_ranks.TryGetValue(r.AccountId, out var seasons)) _ranks[r.AccountId] = seasons = new();
                // Keep the newest snapshot per season; a never-played entry doesn't replace a real one.
                if (seasons.TryGetValue((r.Track, r.TrackGuid), out var existing))
                {
                    if ((r.LastUpdatedUtc ?? DateTime.MinValue) <= (existing.LastUpdatedUtc ?? DateTime.MinValue)) continue;
                    if (existing.LastUpdatedUtc is not null && existing.Current != r.Current) moves.Add(new RankChange(existing, r));
                }
                seasons[(r.Track, r.TrackGuid)] = r;
                changed = true;
            }
            if (changed && save) Save();
        }
        if (changed) Changed?.Invoke();
        foreach (var move in moves) RankChanged?.Invoke(move);
    }

    public void Flush()
    {
        lock (_gate) Save();
    }

    private sealed record HistoryEntry(string AccountId, string Track, string Guid, List<RankPoint> Points);
    private sealed record RankFile(List<RankProgress> Entries, List<HistoryEntry> History);

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            // Before 0.8 the file was a plain list of entries without history.
            if (json.TrimStart().StartsWith('['))
            {
                Add(JsonSerializer.Deserialize<List<RankProgress>>(json) ?? [], save: false);
                return;
            }
            var file = JsonSerializer.Deserialize<RankFile>(json);
            if (file is null) return;
            foreach (var h in file.History)
            {
                var points = new SortedList<DateTime, RankPoint>();
                foreach (var p in h.Points) points[p.At] = p;
                _history[(h.AccountId, h.Track, h.Guid)] = points;
            }
            Add(file.Entries, save: false);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var file = new RankFile(
            [.. _ranks.Values.SelectMany(s => s.Values)],
            [.. _history.Select(h => new HistoryEntry(h.Key.Account, h.Key.Track, h.Key.Guid, [.. h.Value.Values]))]);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file));
        File.Move(tmp, _path, overwrite: true);
    }
}

/// <summary>Parses the HabaneroProgress entries of one log line (a line can hold several).</summary>
public static partial class RankParser
{
    [GeneratedRegex(@"\{HabaneroProgress: \{LastUpdatedTime: '(?<at>[^']*)' / GameId: '[^']*', AccountId: '(?<id>[0-9a-f]{32})' / HabaneroType: '(?<track>[^']+)' / TrackGUID: '(?<guid>[^']*)' / Current rank: (?<cur>\d+) / Highest rank: (?<high>\d+) / CurrentPlayerPosition: (?<pos>-?\d+) / ProgressTowardsNextHabanero: (?<prog>[0-9.]+)\}")]
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
                at,
                m.Groups["guid"].Value));
        }
        return list;
    }
}
