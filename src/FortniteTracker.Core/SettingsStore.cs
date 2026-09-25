using System.Text.Json;

namespace FortniteTracker.Core;

public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// User settings in %APPDATA%\FortniteTracker\settings.json. Each user brings their own free
/// fortnite-api.com key, so no secret ships inside the app.
/// </summary>
public sealed class SettingsStore
{
    public static readonly string DefaultDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FortniteTracker");

    private readonly string _settingsPath;
    private readonly object _gate = new();
    private Settings _settings;

    public SettingsStore(string? fallbackApiKey = null, string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(DefaultDirectory, "settings.json");
        _settings = Load() ?? new Settings();
        if (string.IsNullOrWhiteSpace(_settings.FortniteApiKey) && !string.IsNullOrWhiteSpace(fallbackApiKey))
            _settings = _settings with { FortniteApiKey = fallbackApiKey };
    }

    public string? ApiKey => _settings.FortniteApiKey;
    public bool HasApiKey => !string.IsNullOrWhiteSpace(_settings.FortniteApiKey);
    public bool RichPresenceEnabled => _settings.RichPresence;
    public bool NotifyOnElimination => _settings.NotifyOnElimination;
    public bool OverlayEnabled => _settings.Overlay;
    public OverlayCorner OverlayCorner => _settings.OverlayCorner;

    /// <summary>Raised after any change; the argument says whether the API key changed.</summary>
    public event Action<bool>? Changed;

    public void SetApiKey(string key) => Update(s => s with { FortniteApiKey = key.Trim() }, apiKeyChanged: true);

    public void SetRichPresence(bool enabled) => Update(s => s with { RichPresence = enabled }, apiKeyChanged: false);

    public void SetNotifyOnElimination(bool enabled) => Update(s => s with { NotifyOnElimination = enabled }, apiKeyChanged: false);

    public void SetOverlay(bool enabled, OverlayCorner corner) =>
        Update(s => s with { Overlay = enabled, OverlayCorner = corner }, apiKeyChanged: false);

    private void Update(Func<Settings, Settings> change, bool apiKeyChanged)
    {
        lock (_gate)
        {
            _settings = change(_settings);
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings));
        }
        Changed?.Invoke(apiKeyChanged);
    }

    private Settings? Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private sealed record Settings(
        string? FortniteApiKey = null,
        bool RichPresence = true,
        bool NotifyOnElimination = true,
        bool Overlay = false,
        OverlayCorner OverlayCorner = OverlayCorner.TopRight);
}
