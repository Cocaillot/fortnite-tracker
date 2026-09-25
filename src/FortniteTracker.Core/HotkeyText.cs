namespace FortniteTracker.Core;

/// <summary>
/// Global shortcuts written as text, e.g. "Ctrl+Shift+F", converted to what Windows' RegisterHotKey
/// takes. Keys: A–Z, 0–9 and F1–F12; letters and digits need at least one modifier so typing isn't
/// captured.
/// </summary>
public static class HotkeyText
{
    public const uint Alt = 0x0001, Ctrl = 0x0002, Shift = 0x0004, Win = 0x0008;

    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts[..^1])
        {
            var mod = part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => Ctrl,
                "alt" => Alt,
                "shift" => Shift,
                "win" => Win,
                _ => 0u,
            };
            if (mod == 0 || (modifiers & mod) != 0) return false;
            modifiers |= mod;
        }

        var key = parts[^1].ToUpperInvariant();
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            if (modifiers == 0) return false;
            virtualKey = key[0]; // VK codes for letters and digits are their ASCII codes
            return true;
        }
        if (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key[1..], out var n) && n is >= 1 and <= 12)
        {
            virtualKey = (uint)(0x70 + n - 1); // VK_F1…
            return true;
        }
        return false;
    }
}
