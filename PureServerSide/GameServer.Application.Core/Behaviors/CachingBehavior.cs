using Cortex.Mediator.Queries;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Core.Behaviors;

public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public sealed class CachingBehavior<TRequest, TResponse>(
    ICacheService cacheService,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IQueryPipelineBehavior<TRequest, TResponse>
    where TRequest : Cortex.Mediator.Queries.IQuery<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        QueryHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheable)
            return await next();

        var cachedResult = await cacheService.GetAsync<TResponse>(cacheable.CacheKey, cancellationToken);
        if (cachedResult is not null)
        {
            logger.LogDebug("Cache hit for {CacheKey}", cacheable.CacheKey);
            return cachedResult;
        }

        var response = await next();

        if (response is not null)
        {
            await cacheService.SetAsync(cacheable.CacheKey, response, cacheable.CacheDuration, cancellationToken);
            logger.LogDebug("Cached {CacheKey}", cacheable.CacheKey);
        }

        return response;
    }
}
