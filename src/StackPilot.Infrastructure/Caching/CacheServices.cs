using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StackPilot.Application.Interfaces;

namespace StackPilot.Infrastructure.Caching;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class =>
        Task.FromResult(_cache.TryGetValue(key, out T? value) ? value : null);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        _cache.Set(key, value, expiry ?? TimeSpan.FromMinutes(5));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RedisCacheService(IConnectionMultiplexer multiplexer, ILogger<RedisCacheService> logger)
    {
        _db = multiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis get failed for {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(key, json, expiry ?? TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis set failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await _db.KeyDeleteAsync(key); }
        catch (Exception ex) { _logger.LogWarning(ex, "Redis delete failed for {Key}", key); }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var server = _db.Multiplexer.GetServers().FirstOrDefault();
            if (server is null) return;

            foreach (var key in server.Keys(pattern: $"{prefix}*"))
                await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis prefix delete failed for {Prefix}", prefix);
        }
    }
}

public static class CacheKeys
{
    public const string ConnectorDefinitions = "stackpilot:connectors:definitions";
    public static string DashboardStats(Guid workspaceId) => $"stackpilot:dashboard:{workspaceId}";
}
