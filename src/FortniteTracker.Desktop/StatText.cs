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
        Core.Threat.BotLikely => Loc.T("Bot?"),
        Core.Threat.Casual => Loc.T("Casual"),
        Core.Threat.Average => Loc.T("Average"),
        Core.Threat.Skilled => Loc.T("Skilled"),
        Core.Threat.Sweat => Loc.T("Sweat"),
        _ => "",
    };

    /// <summary>The one-line summary used under a player's name, e.g. "K/D 6.81 · 16.4% wins · 128 matches".</summary>
    public static string Summary(PlayerStats p, string? bucket) => p.Status switch
    {
        StatsStatus.Ok when p.For(bucket) is { } m => Loc.T("K/D {0:0.00} · {1:0.#}% wins · {2} matches", m.Kd, m.WinRate, m.Matches),
        StatsStatus.Hidden => Loc.T("Streamer Mode: name hidden"),
        StatsStatus.Private => Loc.T("Stats private"),
        StatsStatus.NotFound => Loc.T("No stats found, likely a bot"),
        StatsStatus.Loading => Loc.T("Loading stats…"),
        StatsStatus.NoApiKey => Loc.T("Add your API key to see stats"),
        _ => Loc.T("Stats unavailable"),
    };

    /// <summary>"2.3× your K/D" when both K/Ds are known, else null.</summary>
    public static string? VersusYou(PlayerStats opponent, PlayerStats? you, string? bucket)
    {
        if (opponent.For(bucket) is not { } theirs || you?.For(bucket) is not { Kd: > 0 } mine) return null;
        var ratio = theirs.Kd / mine.Kd;
        return ratio >= 1 ? Loc.T("{0:0.0}× your K/D", ratio) : Loc.T("{0:0.0}× lower K/D than you", 1 / ratio);
    }

    public static string DisplayName(PlayerStats p) =>
        p.Status == StatsStatus.Hidden ? Loc.T("Streamer Mode player") : p.EpicName ?? Loc.T("Squad member");

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
