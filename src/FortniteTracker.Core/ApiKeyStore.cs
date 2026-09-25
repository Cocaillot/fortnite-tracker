using System.Text.Json;

namespace FortniteTracker.Core;

/// <summary>
/// Holds the user's fortnite-api.com key in %APPDATA%\FortniteTracker\settings.json.
/// Each user brings their own free key, so no secret ships inside the app.
/// </summary>
public sealed class ApiKeyStore
{
    public static readonly string DefaultSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FortniteTracker", "settings.json");

    private readonly string _settingsPath;
    private string? _key;

    public ApiKeyStore(string? fallbackKey = null, string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? DefaultSettingsPath;
        _key = Load() ?? (string.IsNullOrWhiteSpace(fallbackKey) ? null : fallbackKey);
    }

    public string? Key => _key;
    public bool HasKey => !string.IsNullOrWhiteSpace(_key);

    public event Action? Changed;

    public void Save(string key)
    {
        _key = key.Trim();
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new Settings(_key)));
        Changed?.Invoke();
    }

    private string? Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath))?.FortniteApiKey;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private sealed record Settings(string? FortniteApiKey);
}
