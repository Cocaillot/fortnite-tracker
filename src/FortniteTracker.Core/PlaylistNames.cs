using System.Text.RegularExpressions;

namespace FortniteTracker.Core;

/// <summary>
/// Readable names for Fortnite playlist IDs. Epic uses internal codenames (e.g. "Habanero" is Ranked);
/// unknown codenames are left out rather than guessed.
/// </summary>
public static partial class PlaylistNames
{
    // Reload's codenames. BlastBerry is the ranked track's name; PunchBerry, PiperBoot and RopeSmile
    // were matched to the Reload rank moving after those matches (Sept 2026), and tournaments mark
    // Reload with "RE_".
    private static readonly string[] ReloadCodenames = ["BlastBerry", "PunchBerry", "PiperBoot", "RopeSmile"];

    /// <summary>
    /// E.g. "Ranked Reload Duos · Zero Build", "Battle Royale Solo · Build", "Tournament Reload Solo · Build".
    /// </summary>
    public static string Describe(string? playlist, string? level = null)
    {
        if (level?.Contains("VKPlay", StringComparison.OrdinalIgnoreCase) == true
            || playlist?.Contains("VK_Play", StringComparison.OrdinalIgnoreCase) == true)
            return "Creative";
        if (string.IsNullOrEmpty(playlist)) return "Match";

        var ranked = Has(playlist, "Habanero");
        var kind = ranked ? "Ranked" : Has(playlist, "Tournament") ? "Tournament" : null;
        var game = Game(playlist) ?? (kind is null ? "Battle Royale" : null);
        var name = string.Join(' ', new[] { kind, game, TeamSize(playlist) }.Where(s => s is not null));
        var build = Has(playlist, "NoBuild") || Has(playlist, "ZeroBuild") || ZeroBuildToken().IsMatch(playlist) ? "Zero Build" : "Build";
        return $"{name} · {build}";
    }

    private static string? Game(string playlist)
    {
        if (ReloadCodenames.Any(c => Has(playlist, c)) || playlist.Contains("_RE_", StringComparison.Ordinal) || Has(playlist, "Reload"))
            return "Reload";
        if (Has(playlist, "Figment")) return "OG";
        // "Playlist_DefaultSolo", "Playlist_NoBuildBR_Trio", "Playlist_HabaneroTrio"...
        if (Has(playlist, "Default") || BattleRoyaleToken().IsMatch(playlist) || PlainRanked().IsMatch(playlist))
            return "Battle Royale";
        return null;
    }

    /// <summary>
    /// The fortnite-api stats bucket matching a playlist, or null for "all modes". Ranked and other
    /// limited-time modes are counted under "ltm", not under solo/duo; there is no trio bucket.
    /// </summary>
    public static (string? Bucket, string Label) StatsBucket(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist) || playlist.Contains("VK_Play", StringComparison.OrdinalIgnoreCase))
            return (null, "All modes");
        if (playlist.Contains("Habanero", StringComparison.OrdinalIgnoreCase))
            return ("ltm", "Ranked & LTMs");
        // Only the classic "…Solo/Duo/Squad" endings map to a bucket; anything fancier counts as all modes.
        var p = playlist.ToLowerInvariant();
        if (p.EndsWith("solo")) return ("solo", "Solo");
        if (p.EndsWith("duos") || p.EndsWith("duo")) return ("duo", "Duos");
        if (p.EndsWith("squads") || p.EndsWith("squad")) return ("squad", "Squads");
        return (null, "All modes");
    }

    // The last team size in the name: "Playlist_Habanero_X_Duos", "Playlist_DefaultDuo",
    // "Playlist_ShowdownTournament_RE_PiperBootSolo_PBM".
    private static string? TeamSize(string playlist)
    {
        var sizes = TeamSizeWord().Matches(playlist);
        if (sizes.Count == 0) return null;
        return sizes[^1].Value.ToLowerInvariant() switch
        {
            var s when s.StartsWith("solo") => "Solo",
            var s when s.StartsWith("duo") => "Duos",
            var s when s.StartsWith("trio") => "Trios",
            _ => "Squads",
        };
    }

    private static bool Has(string playlist, string word) => playlist.Contains(word, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("(solo|duos?|trios?|squads?)(?![a-z])", RegexOptions.IgnoreCase)]
    private static partial Regex TeamSizeWord();

    [GeneratedRegex("(^|_)ZB(_|$)")]
    private static partial Regex ZeroBuildToken();

    [GeneratedRegex("BR(_|Solo|Duo|Trio|Squad|$)")]
    private static partial Regex BattleRoyaleToken();

    // "Playlist_HabaneroTrio", "Playlist_Habanero_Duos": ranked with no mode codename is Battle Royale.
    [GeneratedRegex("Habanero_?(Solo|Duos?|Trios?|Squads?)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlainRanked();
}
