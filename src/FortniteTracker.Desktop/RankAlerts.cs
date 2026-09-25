using FortniteTracker.Core;

namespace FortniteTracker.Desktop;

/// <summary>
/// Announces rank changes as they happen: yours (up or down) and friends' or party members'
/// rank-ups, e.g. "Shinothia reached Diamond III in Reload". Changes found while importing old
/// logs are not announced.
/// </summary>
public sealed class RankAlerts
{
    // Fortnite re-sends ranks shortly after a match; anything older comes from replaying a log.
    private static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(30);

    private readonly SettingsStore _settings;
    private readonly LobbyTracker _tracker;
    private readonly FortniteStatsService _stats;
    private readonly Action<string, string, bool> _notify;

    /// <param name="notify">Title, text, and whether it's good news.</param>
    public RankAlerts(RankBook ranks, SettingsStore settings, LobbyTracker tracker, FortniteStatsService stats, Action<string, string, bool> notify)
    {
        _settings = settings;
        _tracker = tracker;
        _stats = stats;
        _notify = notify;
        ranks.RankChanged += change => _ = AnnounceAsync(change);
    }

    private async Task AnnounceAsync(RankChange change)
    {
        if (!_settings.NotifyRankChanges) return;
        if (change.After.LastUpdatedUtc is not { } at || at < DateTime.UtcNow - LiveWindow) return;

        var after = change.After;
        if (after.AccountId == _tracker.SelfId)
        {
            _notify(
                change.IsUp ? $"Rank up! {after.RankName}" : $"Rank down: {after.RankName}",
                $"{after.TrackName}: {change.Before.RankName} → {after.RankName}",
                change.IsUp);
            return;
        }

        // Friends and party: celebrate rank-ups only.
        if (!change.IsUp) return;
        var stats = await _stats.GetByAccountIdAsync(after.AccountId, CancellationToken.None);
        var who = stats.EpicName ?? "A friend";
        _notify($"{who} ranked up", $"{who} reached {after.RankName} in {after.TrackName}", true);
    }
}
