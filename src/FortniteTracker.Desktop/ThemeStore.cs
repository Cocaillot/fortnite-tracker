using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace FortniteTracker.Desktop;

/// <summary>Colours the native parts (window frame, overlay) take from the UI theme.</summary>
public sealed record ThemeColors(Color Background, Color Surface, Color Text, Color Accent)
{
    public static readonly ThemeColors Default = new(
        Color.FromRgb(0x0B, 0x0D, 0x12), Color.FromRgb(0x14, 0x18, 0x21), Colors.White, Color.FromRgb(0x4C, 0xB8, 0xFF));
}

/// <summary>
/// The appearance chosen on the Appearance page, stored as-is in theme.json (colours, fonts, custom
/// icons and background image as data URLs). The UI owns the format; this side only reads colours.
/// </summary>
public sealed class ThemeStore
{
    private readonly string _path;
    private string? _json;

    public ThemeStore(string directory)
    {
        _path = Path.Combine(directory, "theme.json");
        try
        {
            if (File.Exists(_path)) _json = File.ReadAllText(_path);
        }
        catch (IOException)
        {
        }
        Colors = Parse(_json);
    }

    public string? Json => _json;

    public ThemeColors Colors { get; private set; }

    public event Action? Changed;

    public void Save(string? json)
    {
        _json = json;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (json is null) File.Delete(_path);
        else File.WriteAllText(_path, json);
        Colors = Parse(json);
        Changed?.Invoke();
    }

    private static ThemeColors Parse(string? json)
    {
        if (json is null) return ThemeColors.Default;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Color Read(string name, Color fallback) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                && ColorConverter.ConvertFromString(v.GetString()) is Color c ? c : fallback;
            var d = ThemeColors.Default;
            return new ThemeColors(Read("background", d.Background), Read("surface", d.Surface), Read("text", d.Text), Read("accent", d.Accent));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or NotSupportedException)
        {
            return ThemeColors.Default;
        }
    }
}
