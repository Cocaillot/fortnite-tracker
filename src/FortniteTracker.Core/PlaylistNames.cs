namespace FortniteTracker.Core;

/// <summary>
/// Readable names for Fortnite playlist IDs. Epic uses internal codenames (e.g. "Habanero" is Ranked);
/// unknown codenames are left out rather than guessed.
/// </summary>
public static class PlaylistNames
{
    public static string Describe(string? playlist, string? level = null)
    {
        if (level?.Contains("VKPlay", StringComparison.OrdinalIgnoreCase) == true
            || playlist?.Contains("VK_Play", StringComparison.OrdinalIgnoreCase) == true)
            return "Creative";
        if (string.IsNullOrEmpty(playlist)) return "Match";

        var mode = playlist.Contains("Habanero", StringComparison.OrdinalIgnoreCase) ? "Ranked" : "Battle Royale";
        var size = TeamSize(playlist);
        return size is null ? mode : $"{mode} {size}";
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
        return TeamSize(playlist) switch
        {
            "Solo" => ("solo", "Solo"),
            "Duos" => ("duo", "Duos"),
            "Squads" => ("squad", "Squads"),
            _ => (null, "All modes"),
        };
    }

    private static string? TeamSize(string playlist)
    {
        // Both "Playlist_Habanero_X_Duos" and "Playlist_DefaultDuo" styles exist.
        var p = playlist.ToLowerInvariant();
        if (p.EndsWith("solo")) return "Solo";
        if (p.EndsWith("duos") || p.EndsWith("duo")) return "Duos";
        if (p.EndsWith("trios") || p.EndsWith("trio")) return "Trios";
        if (p.EndsWith("squads") || p.EndsWith("squad")) return "Squads";
        return null;
    }
}
