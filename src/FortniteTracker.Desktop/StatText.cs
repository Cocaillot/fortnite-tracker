using System.Windows.Media;
using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>Shared wording and colours for native surfaces (overlay, notifications, Discord).</summary>
public static class StatText
{
    // Same palette as the Vue UI (--rarity-* in App.vue).
    private static readonly Dictionary<Rarity, SolidColorBrush> RarityBrushes = new()
    {
        [Rarity.Common] = Freeze(0xB4, 0xBA, 0xC6),
        [Rarity.Uncommon] = Freeze(0x5F, 0xD8, 0x6B),
        [Rarity.Rare] = Freeze(0x4C, 0xB8, 0xFF),
        [Rarity.Epic] = Freeze(0xC0, 0x7C, 0xFF),
        [Rarity.Legendary] = Freeze(0xFF, 0xB8, 0x3D),
    };

    public static SolidColorBrush Brush(Rarity rarity) => RarityBrushes[rarity];

    public static string Threat(Threat threat) => threat switch
    {
        Core.Threat.BotLikely => "Bot?",
        Core.Threat.Casual => "Casual",
        Core.Threat.Average => "Average",
        Core.Threat.Skilled => "Skilled",
        Core.Threat.Sweat => "Sweat",
        _ => "",
    };

    /// <summary>The one-line summary used under a player's name, e.g. "K/D 6.81 · 16.4% wins · 128 matches".</summary>
    public static string Summary(PlayerStats p, string? bucket) => p.Status switch
    {
        StatsStatus.Ok when p.For(bucket) is { } m => $"K/D {m.Kd:0.00} · {m.WinRate:0.#}% wins · {m.Matches} matches",
        StatsStatus.Hidden => "Streamer Mode: name hidden",
        StatsStatus.Private => "Stats private",
        StatsStatus.NotFound => "No stats found, likely a bot",
        StatsStatus.Loading => "Loading stats…",
        StatsStatus.NoApiKey => "Add your API key to see stats",
        _ => "Stats unavailable",
    };

    /// <summary>"2.3× your K/D" when both K/Ds are known, else null.</summary>
    public static string? VersusYou(PlayerStats opponent, PlayerStats? you, string? bucket)
    {
        if (opponent.For(bucket) is not { } theirs || you?.For(bucket) is not { Kd: > 0 } mine) return null;
        var ratio = theirs.Kd / mine.Kd;
        return ratio >= 1 ? $"{ratio:0.0}× your K/D" : $"{1 / ratio:0.0}× lower K/D than you";
    }

    public static string DisplayName(PlayerStats p) =>
        p.Status == StatsStatus.Hidden ? "Streamer Mode player" : p.EpicName ?? "Squad member";

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
