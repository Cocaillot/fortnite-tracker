using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

public sealed class SettingsAndHotkeyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ft-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("Ctrl+Shift+F", HotkeyText.Ctrl | HotkeyText.Shift, 0x46u)]
    [InlineData("alt + 3", HotkeyText.Alt, 0x33u)]
    [InlineData("F9", 0u, 0x78u)]
    [InlineData("Ctrl+Win+F12", HotkeyText.Ctrl | HotkeyText.Win, 0x7Bu)]
    public void Parses_shortcuts(string text, uint modifiers, uint key)
    {
        Assert.True(HotkeyText.TryParse(text, out var mods, out var vk));
        Assert.Equal(modifiers, mods);
        Assert.Equal(key, vk);
    }

    [Theory]
    [InlineData("F")] // a letter alone would swallow typing
    [InlineData("Ctrl+Ctrl+F")]
    [InlineData("Ctrl+Shift+Space")]
    [InlineData("Ctrl+F13")]
    [InlineData("")]
    public void Rejects_bad_shortcuts(string text) => Assert.False(HotkeyText.TryParse(text, out _, out _));

    [Fact]
    public void New_install_starts_with_the_guide()
    {
        var store = new SettingsStore(settingsPath: Path.Combine(_dir, "settings.json"));
        Assert.False(store.OnboardingDone);
        store.SetOnboardingDone();
        Assert.True(new SettingsStore(settingsPath: Path.Combine(_dir, "settings.json")).OnboardingDone);
    }

    [Fact]
    public void Settings_from_an_older_version_skip_the_guide()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, """{"FortniteApiKey":"abc","RichPresence":true}""");
        var store = new SettingsStore(settingsPath: path);
        Assert.True(store.OnboardingDone);
        Assert.Equal(SettingsStore.DefaultWindowHotkey, store.WindowHotkey);
        Assert.False(store.LaunchWithFortnite);
    }
}
