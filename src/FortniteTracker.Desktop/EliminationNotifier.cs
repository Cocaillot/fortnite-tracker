using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Shows a Windows notification with your eliminator's stats right after your team is eliminated,
/// e.g. "Eliminated by X" / "Sweat · K/D 6.81 · 16.4% wins · 3.1× your K/D".
/// </summary>
public sealed class EliminationNotifier
{
    private readonly SettingsStore _settings;
    private readonly Action<string, string> _notify;
    private DateTime? _notifiedFor;

    public EliminationNotifier(LobbyTracker tracker, SettingsStore settings, Action<string, string> notify)
    {
        _settings = settings;
        _notify = notify;
        tracker.Changed += OnSnapshot;
    }

    private void OnSnapshot(LobbySnapshot s)
    {
        if (!_settings.NotifyOnElimination) return;
        if (s is not { LastMatchStartedUtc: { } match, LastMatchEndedUtc: { } ended, EliminatedBy: { } e }) return;
        // Wait for the stats, don't repeat for the same match, and stay quiet during the startup replay.
        if (e.Status == StatsStatus.Loading || _notifiedFor == match || !LobbyTracker.IsLive(ended)) return;
        _notifiedFor = match;

        var parts = new List<string>();
        if (e.Threat is { } threat) parts.Add(StatText.Threat(threat));
        parts.Add(StatText.Summary(e, s.StatsBucket));
        if (StatText.VersusYou(e, s.Squad.FirstOrDefault(), s.StatsBucket) is { } versus) parts.Add(versus);

        _notify($"Eliminated by {StatText.DisplayName(e)}", string.Join(" · ", parts));
    }
}
