using System.Windows.Threading;
using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>Shows the overlay while it's enabled and Fortnite is running, and keeps it up to date.</summary>
public sealed class OverlayController
{
    private readonly SettingsStore _settings;
    private readonly LobbyTracker _tracker;
    private readonly ThemeStore _theme;
    private readonly Dispatcher _dispatcher;
    private OverlayWindow? _window;

    public OverlayController(SettingsStore settings, LobbyTracker tracker, ThemeStore theme, Dispatcher dispatcher)
    {
        _settings = settings;
        _tracker = tracker;
        _theme = theme;
        _dispatcher = dispatcher;
        _theme.Changed += () => _dispatcher.InvokeAsync(Refresh);
        _tracker.Changed += _ => _dispatcher.InvokeAsync(Refresh);
        _settings.Changed += _ => _dispatcher.InvokeAsync(Refresh);
    }

    public void Toggle() => _settings.SetOverlay(!_settings.OverlayEnabled, _settings.OverlayCorner);

    public void Refresh()
    {
        var snapshot = _tracker.Last;
        var visible = _settings.OverlayEnabled && snapshot is { GameRunning: true };
        if (!visible)
        {
            _window?.Hide();
            return;
        }

        _window ??= new OverlayWindow();
        _window.ApplyTheme(_theme.Colors);
        _window.Update(snapshot!);
        _window.SetCorner(_settings.OverlayCorner);
        if (!_window.IsVisible) _window.Show();
    }

    public void Close() => _window?.Close();
}
