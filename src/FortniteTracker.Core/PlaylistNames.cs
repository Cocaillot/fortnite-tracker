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
