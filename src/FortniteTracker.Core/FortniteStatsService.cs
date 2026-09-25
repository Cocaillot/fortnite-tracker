using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace FortniteTracker.Core;

/// <summary>
/// Season stats from fortnite-api.com, with caching, request de-duplication and rate limiting.
/// </summary>
public sealed class FortniteStatsService(HttpClient http, IMemoryCache cache, SettingsStore settings)
{
    private readonly RateLimiter _limiter = new TokenBucketRateLimiter(new()
    {
        TokenLimit = 3,
        TokensPerPeriod = 3,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = 100,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    });

    public Task<PlayerStats> GetByAccountIdAsync(string accountId, CancellationToken ct, StatsWindow window = StatsWindow.Season) =>
        GetCachedAsync(AccountKey(accountId, window), AccountUrl(accountId, window), accountId, null, ct);

    /// <summary>Skips the cache (used to see a match land in your stats); refreshes it on success.</summary>
    public async Task<PlayerStats> GetFreshByAccountIdAsync(string accountId, CancellationToken ct)
    {
        if (!settings.HasApiKey) return new PlayerStats(accountId, null, StatsStatus.NoApiKey);
        ct.ThrowIfCancellationRequested();
        return await FetchAndCacheAsync(AccountKey(accountId, StatsWindow.Season), AccountUrl(accountId, StatsWindow.Season), accountId, null);
    }

    private static string Window(StatsWindow w) => w == StatsWindow.Lifetime ? "lifetime" : "season";
    private static string AccountKey(string accountId, StatsWindow w) => $"id:{Window(w)}:{accountId}";
    private static string AccountUrl(string accountId, StatsWindow w) => $"v2/stats/br/v2/{accountId}?timeWindow={Window(w)}";

    public Task<PlayerStats> GetByNameAsync(string name, string accountType, CancellationToken ct, StatsWindow window = StatsWindow.Season) =>
        GetCachedAsync($"name:{Window(window)}:{accountType}:{name.ToLowerInvariant()}",
            $"v2/stats/br/v2?name={Uri.EscapeDataString(name)}&accountType={accountType}&timeWindow={Window(window)}",
            null, name, ct);

    /// <summary>
    /// Looks up a player by the name shown in-game. Console players show their PSN/Xbox name,
    /// so Epic is tried first, then PSN, then Xbox. Streamer Mode names are not looked up.
    /// The result keeps the name seen in-game, since a console player's Epic name differs.
    /// </summary>
    public async Task<PlayerStats> GetByDisplayNameAsync(string displayName, CancellationToken ct)
    {
        if (FortniteLogParser.IsAnonymous(displayName)) return new PlayerStats(null, displayName, StatsStatus.Hidden);

        PlayerStats? result = null;
        foreach (var accountType in new[] { "epic", "psn", "xbl" })
        {
            result = await GetByNameAsync(displayName, accountType, ct);
            if (result.Status != StatsStatus.NotFound) return result with { EpicName = displayName };
        }
        return result! with { EpicName = displayName };
    }

    public void ClearCache() => (cache as MemoryCache)?.Clear();

    private Task<PlayerStats> GetCachedAsync(string key, string url, string? accountId, string? name, CancellationToken ct)
    {
        if (!settings.HasApiKey) return Task.FromResult(new PlayerStats(accountId, name, StatsStatus.NoApiKey));

        // Lazy<Task> collapses concurrent lookups for the same player into one upstream call.
        var entry = cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return new Lazy<Task<PlayerStats>>(() => FetchAndCacheAsync(key, url, accountId, name));
        })!;
        return entry.Value.WaitAsync(ct);
    }

    private async Task<PlayerStats> FetchAndCacheAsync(string key, string url, string? accountId, string? name)
    {
        PlayerStats result;
        try
        {
            result = await FetchAsync(url, accountId, name);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            result = new PlayerStats(accountId, name, StatsStatus.Error);
        }

        var ttl = result.Status switch
        {
            StatsStatus.Ok => TimeSpan.FromMinutes(10),
            StatsStatus.Private or StatsStatus.NotFound => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromSeconds(30),
        };
        cache.Set(key, new Lazy<Task<PlayerStats>>(() => Task.FromResult(result)), ttl);
        return result;
    }

    private async Task<PlayerStats> FetchAsync(string url, string? accountId, string? name)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var lease = await _limiter.AcquireAsync(1);
            if (!lease.IsAcquired) return new(accountId, name, StatsStatus.Error);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(settings.ApiKey!);
            using var res = await http.SendAsync(request);

            switch (res.StatusCode)
            {
                case HttpStatusCode.OK:
                    return Parse(await res.Content.ReadFromJsonAsync<JsonElement>(), accountId, name);
                case HttpStatusCode.Forbidden:
                    return new(accountId, name, StatsStatus.Private);
                case HttpStatusCode.NotFound:
                    return new(accountId, name, StatsStatus.NotFound);
                case HttpStatusCode.Unauthorized:
                    return new(accountId, name, StatsStatus.NoApiKey);
                case HttpStatusCode.TooManyRequests:
                case >= HttpStatusCode.InternalServerError:
                    await Task.Delay(res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 << attempt));
                    continue;
                default:
                    return new(accountId, name, StatsStatus.Error);
            }
        }
        return new(accountId, name, StatsStatus.Error);
    }

    internal static PlayerStats Parse(JsonElement json, string? accountId, string? name)
    {
        var data = json.GetProperty("data");
        var account = data.GetProperty("account");
        var id = account.GetProperty("id").GetString() ?? accountId;
        var epicName = account.GetProperty("name").GetString() ?? name;

        // "all" (and each mode inside it) is null when there are no matches in the time window.
        if (!data.TryGetProperty("stats", out var stats)
            || !stats.TryGetProperty("all", out var all) || all.ValueKind != JsonValueKind.Object)
            return new(id, epicName, StatsStatus.Ok, new ModeStats(0, 0, 0, 0, 0), new Dictionary<string, ModeStats>(), null);

        int? battlePass = data.TryGetProperty("battlePass", out var bp) && bp.ValueKind == JsonValueKind.Object
            && bp.TryGetProperty("level", out var lvl) && lvl.ValueKind == JsonValueKind.Number ? lvl.GetInt32() : null;

        var byMode = new Dictionary<string, ModeStats>();
        foreach (var mode in all.EnumerateObject())
            if (mode.Name != "overall" && ParseMode(mode.Value) is { } m) byMode[mode.Name] = m;

        return new(id, epicName, StatsStatus.Ok,
            all.TryGetProperty("overall", out var o) ? ParseMode(o) ?? new ModeStats(0, 0, 0, 0, 0) : new ModeStats(0, 0, 0, 0, 0),
            byMode,
            battlePass);
    }

    private static ModeStats? ParseMode(JsonElement m) =>
        m.ValueKind != JsonValueKind.Object ? null : new ModeStats(
            Wins: Int(m, "wins"),
            WinRate: Num(m, "winRate"),
            Kd: Num(m, "kd"),
            Kills: Int(m, "kills"),
            Matches: Int(m, "matches"),
            Top10: Int(m, "top10"),
            Top25: Int(m, "top25"),
            MinutesPlayed: Int(m, "minutesPlayed"),
            KillsPerMatch: Num(m, "killsPerMatch"),
            Deaths: Int(m, "deaths"));

    private static int Int(JsonElement m, string name) =>
        m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static double Num(JsonElement m, string name) =>
        m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
