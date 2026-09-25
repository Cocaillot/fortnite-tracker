using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace FortniteTracker.Core;

public enum StatsStatus { Ok, Private, NotFound, NoApiKey, Error }

public sealed record PlayerStats(
    string? AccountId, string? EpicName, StatsStatus Status,
    int? Wins = null, double? WinRate = null, double? Kd = null, int? Kills = null, int? Matches = null);

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

    public Task<PlayerStats> GetByAccountIdAsync(string accountId, CancellationToken ct) =>
        GetCachedAsync("id:" + accountId, $"v2/stats/br/v2/{accountId}?timeWindow=season", accountId, null, ct);

    public Task<PlayerStats> GetByNameAsync(string name, string accountType, CancellationToken ct) =>
        GetCachedAsync($"name:{accountType}:{name.ToLowerInvariant()}",
            $"v2/stats/br/v2?name={Uri.EscapeDataString(name)}&accountType={accountType}&timeWindow=season",
            null, name, ct);

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

        // "all" is null when the player has no matches in the time window.
        if (!data.TryGetProperty("stats", out var stats)
            || !stats.TryGetProperty("all", out var all) || all.ValueKind != JsonValueKind.Object
            || !all.TryGetProperty("overall", out var o) || o.ValueKind != JsonValueKind.Object)
            return new(id, epicName, StatsStatus.Ok, 0, 0, 0, 0, 0);

        return new(id, epicName, StatsStatus.Ok,
            Wins: o.GetProperty("wins").GetInt32(),
            WinRate: o.GetProperty("winRate").GetDouble(),
            Kd: o.GetProperty("kd").GetDouble(),
            Kills: o.GetProperty("kills").GetInt32(),
            Matches: o.GetProperty("matches").GetInt32());
    }
}
