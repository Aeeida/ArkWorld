using GameServer.Application.Core.Behaviors;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Infrastructure.Caching;

public sealed class HybridCacheService(HybridCache cache) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync<T?>(key, _ => ValueTask.FromResult(default(T)), cancellationToken: ct);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var options = expiry.HasValue
            ? new HybridCacheEntryOptions { Expiration = expiry.Value }
            : null;
        return cache.SetAsync(key, value, options, cancellationToken: ct).AsTask();
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        cache.RemoveAsync(key, ct).AsTask();

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var options = expiry.HasValue
            ? new HybridCacheEntryOptions { Expiration = expiry.Value }
            : null;
        return await cache.GetOrCreateAsync(key, async token => await factory(token), options, cancellationToken: ct);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddGameCaching(this IServiceCollection services, string redisConnection)
    {
        services.AddHybridCache();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "mmorpg:";
        });
        services.AddSingleton<ICacheService, HybridCacheService>();
        services.AddSingleton<IPositionCacheService, RedisPositionCacheService>();
        services.AddHostedService<PositionFlushService>();
        return services;
    }
}
