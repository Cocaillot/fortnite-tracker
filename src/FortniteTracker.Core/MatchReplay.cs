using FortniteReplayReader;
using FortniteReplayReader.Models;
using Unreal.Core.Models.Enums;

namespace FortniteTracker.Core;

/// <summary>A player your team eliminated, from the match replay.</summary>
/// <param name="AccountId">Epic account ID; null for bots.</param>
/// <param name="By">Name of the teammate who eliminated them (you included).</param>
public sealed record EliminatedPlayer(string Name, string? AccountId, bool ByYou, string? By, bool Bot);

/// <summary>
/// What Fortnite's replay of a match says: the same numbers as its end screen (placement, your
/// eliminations, the players your team eliminated) plus who really eliminated you.
/// </summary>
public sealed record ReplayDetails(
    int? Placement,
    int? Players,
    int Kills,
    int Assists,
    int DamageToPlayers,
    double Accuracy,
    string? EliminatedBy,
    IReadOnlyList<EliminatedPlayer> Eliminated);

/// <summary>A replay file read successfully.</summary>
public sealed record ParsedReplay(DateTime StartedUtc, string? Playlist, ReplayDetails Details);

/// <summary>Reads Fortnite replay files (%LOCALAPPDATA%\FortniteGame\Saved\Demos).</summary>
public static class ReplayParser
{
    public static readonly string DefaultDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");

    /// <summary>
    /// Null while Fortnite is still writing the file: it is only finalised (and its encryption key
    /// written) when the next match starts or the player goes back to the lobby.
    /// </summary>
    public static ParsedReplay? Read(string path)
    {
        FortniteReplay replay;
        try
        {
            // Fortnite may still hold the file open; shared read access doesn't disturb it.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            replay = new ReplayReader(null!, ParseMode.Normal).ReadReplay(stream);
        }
        catch (Exception ex) when (ex is IOException || ex.GetType().Name == "InvalidReplayException")
        {
            return null;
        }
        return Summarise(replay);
    }

    internal static ParsedReplay? Summarise(FortniteReplay replay)
    {
        var players = replay.PlayerData?.ToList() ?? [];
        if (players.FirstOrDefault(p => p.IsReplayOwner) is not { } me) return null;
        if ((replay.GameData?.UtcTimeStartedMatch ?? replay.Info?.Timestamp) is not { } started) return null;

        PlayerData? Find(string? id) =>
            id is null ? null : players.FirstOrDefault(p => p.EpicId == id || p.PlayerId == id || p.BotId == id);
        string Name(PlayerData? p, string? id) =>
            p?.PlayerName ?? p?.StreamerModeName ?? (p?.IsBot == true ? "Bot" : id ?? "?");

        var team = players.Where(p => p.TeamIndex is not null && p.TeamIndex == me.TeamIndex).ToHashSet();
        var eliminated = new List<EliminatedPlayer>();
        string? eliminatedBy = null;

        foreach (var e in replay.Eliminations.Where(e => !e.Knocked))
        {
            var byId = e.EliminatorInfo?.Id ?? e.Eliminator;
            var victimId = e.EliminatedInfo?.Id ?? e.Eliminated;
            var by = Find(byId);
            var victim = Find(victimId);

            if (victim == me)
            {
                // Storm, falling or leaving count as eliminating yourself: no eliminator then.
                eliminatedBy = e.IsSelfElimination || by is null || by == me ? null : Name(by, byId);
            }
            else if (by is not null && team.Contains(by) && (victim is null || !team.Contains(victim)))
            {
                eliminated.Add(new EliminatedPlayer(
                    Name(victim, victimId),
                    victim is { IsBot: false } ? victim.EpicId : null,
                    by == me,
                    Name(by, byId),
                    victim?.IsBot == true));
            }
        }

        var details = new ReplayDetails(
            (int?)replay.TeamStats?.Position ?? me.Placement,
            replay.TeamStats is { TotalPlayers: > 0 } ts ? (int)ts.TotalPlayers : null,
            (int?)replay.Stats?.Eliminations ?? eliminated.Count(p => p.ByYou),
            (int)(replay.Stats?.Assists ?? 0),
            (int)(replay.Stats?.DamageToPlayers ?? 0),
            replay.Stats?.Accuracy ?? 0,
            eliminatedBy,
            eliminated);
        return new ParsedReplay(DateTime.SpecifyKind(started, DateTimeKind.Utc), replay.GameData?.CurrentPlaylist, details);
    }

    /// <summary>
    /// The history entry a replay belongs to: same playlist, started up to 2 minutes before the
    /// replay's own start (the log sees the match a few seconds earlier), the closest one.
    /// </summary>
    public static MatchRecord? MatchFor(ParsedReplay replay, IEnumerable<MatchRecord> history) =>
        history
            .Where(m => m.StartedUtc <= replay.StartedUtc.AddSeconds(30) && m.StartedUtc >= replay.StartedUtc.AddMinutes(-2))
            .Where(m => replay.Playlist is null || m.Playlist is null || string.Equals(m.Playlist, replay.Playlist, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => Math.Abs((m.StartedUtc - replay.StartedUtc).TotalSeconds))
            .FirstOrDefault();
}
