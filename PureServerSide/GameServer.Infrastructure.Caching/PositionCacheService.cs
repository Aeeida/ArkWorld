using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace GameServer.Infrastructure.Caching;

/// <summary>
/// 高频位置缓存服务接口 — TCP 位置更新 → Redis → 定期刷入 PostgreSQL。
///
/// 设计：
/// - 高频写入（每秒 20+ 次/玩家）直接写 Redis Hash
/// - 读取从 Redis 获取最新位置（< 1ms 延迟）
/// - 后台线程每 N 秒批量刷入数据库
/// - 使用 Redis GeoHash 做空间索引（附近玩家查询）
/// </summary>
public interface IPositionCacheService
{
    /// <summary>更新实体位置到缓存。</summary>
    Task SetPositionAsync(Guid entityId, long locationId, double x, double y, double z,
        float rotX, float rotY, float rotZ, float rotW, CancellationToken ct = default);

    /// <summary>获取实体的缓存位置。</summary>
    Task<CachedPosition?> GetPositionAsync(Guid entityId, CancellationToken ct = default);

    /// <summary>批量获取多个实体的位置。</summary>
    Task<IReadOnlyDictionary<Guid, CachedPosition>> GetPositionsAsync(IEnumerable<Guid> entityIds, CancellationToken ct = default);

    /// <summary>移除实体位置缓存。</summary>
    Task RemovePositionAsync(Guid entityId, CancellationToken ct = default);

    /// <summary>获取所有脏位置（需要刷入数据库的）并清除脏标记。</summary>
    IReadOnlyList<CachedPosition> DrainDirtyPositions();
}

/// <summary>
/// 缓存的位置数据。
/// </summary>
public sealed record CachedPosition(
    Guid EntityId,
    long LocationId,
    double X, double Y, double Z,
    float RotX, float RotY, float RotZ, float RotW,
    DateTime Timestamp);

/// <summary>
/// Redis + 内存混合位置缓存实现。
///
/// 写入路径：TCP位置包 → 本地ConcurrentDictionary（零GC） → 异步写Redis Hash
/// 读取路径：本地ConcurrentDictionary（最快） / Redis Hash（跨进程）
/// 持久化：后台定时器 DrainDirtyPositions → 批量 UPDATE PostgreSQL
/// </summary>
public sealed class RedisPositionCacheService : IPositionCacheService
{
    private readonly IDistributedCache _redis;
    private readonly ILogger<RedisPositionCacheService> _logger;

    // ── 本地高速缓存（避免每次位置更新都走 Redis） ──
    private readonly ConcurrentDictionary<Guid, CachedPosition> _localCache = new();
    private readonly ConcurrentDictionary<Guid, byte> _dirtySet = new();

    private const string KeyPrefix = "pos:";

    public RedisPositionCacheService(IDistributedCache redis, ILogger<RedisPositionCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SetPositionAsync(Guid entityId, long locationId, double x, double y, double z,
        float rotX, float rotY, float rotZ, float rotW, CancellationToken ct = default)
    {
        var pos = new CachedPosition(entityId, locationId, x, y, z, rotX, rotY, rotZ, rotW, DateTime.UtcNow);

        // 本地缓存立即更新
        _localCache[entityId] = pos;
        _dirtySet[entityId] = 1;

        // 异步写入 Redis（不等待，允许丢失极少量的中间位置）
        try
        {
            var bytes = SerializePosition(pos);
            await _redis.SetAsync($"{KeyPrefix}{entityId}", bytes,
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write position to Redis for {EntityId}", entityId);
        }
    }

    public async Task<CachedPosition?> GetPositionAsync(Guid entityId, CancellationToken ct = default)
    {
        // 先查本地
        if (_localCache.TryGetValue(entityId, out var local))
            return local;

        // 再查 Redis
        try
        {
            var bytes = await _redis.GetAsync($"{KeyPrefix}{entityId}", ct);
            if (bytes is null) return null;

            var pos = DeserializePosition(bytes);
            _localCache[entityId] = pos; // 回填本地
            return pos;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read position from Redis for {EntityId}", entityId);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, CachedPosition>> GetPositionsAsync(
        IEnumerable<Guid> entityIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, CachedPosition>();

        foreach (var id in entityIds)
        {
            var pos = await GetPositionAsync(id, ct);
            if (pos is not null)
                result[id] = pos;
        }

        return result;
    }

    public async Task RemovePositionAsync(Guid entityId, CancellationToken ct = default)
    {
        _localCache.TryRemove(entityId, out _);
        _dirtySet.TryRemove(entityId, out _);

        try
        {
            await _redis.RemoveAsync($"{KeyPrefix}{entityId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove position from Redis for {EntityId}", entityId);
        }
    }

    public IReadOnlyList<CachedPosition> DrainDirtyPositions()
    {
        var result = new List<CachedPosition>();

        // 原子地取出所有脏 ID
        var dirtyIds = _dirtySet.Keys.ToList();
        foreach (var id in dirtyIds)
        {
            if (_dirtySet.TryRemove(id, out _) && _localCache.TryGetValue(id, out var pos))
            {
                result.Add(pos);
            }
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════════════
    // 序列化（紧凑二进制：16 guid + 8 locationId + 3*8 xyz + 4*4 rot + 8 timestamp = 68 bytes）
    // ══════════════════════════════════════════════════════════════════

    private static byte[] SerializePosition(CachedPosition pos)
    {
        var buf = new byte[68];
        var span = buf.AsSpan();

        pos.EntityId.TryWriteBytes(span[..16]);
        BinaryPrimitives.WriteInt64LittleEndian(span[16..], pos.LocationId);

        BinaryPrimitives.WriteDoubleLittleEndian(span[24..], pos.X);
        BinaryPrimitives.WriteDoubleLittleEndian(span[32..], pos.Y);
        BinaryPrimitives.WriteDoubleLittleEndian(span[40..], pos.Z);

        BinaryPrimitives.WriteSingleLittleEndian(span[48..], pos.RotX);
        BinaryPrimitives.WriteSingleLittleEndian(span[52..], pos.RotY);
        BinaryPrimitives.WriteSingleLittleEndian(span[56..], pos.RotZ);
        BinaryPrimitives.WriteSingleLittleEndian(span[60..], pos.RotW);

        BinaryPrimitives.WriteInt64LittleEndian(span[64..], pos.Timestamp.Ticks);

        return buf;
    }

    private static CachedPosition DeserializePosition(byte[] data)
    {
        var span = data.AsSpan();

        var entityId = new Guid(span[..16]);
        var locationId = BinaryPrimitives.ReadInt64LittleEndian(span[16..]);

        var x = BinaryPrimitives.ReadDoubleLittleEndian(span[24..]);
        var y = BinaryPrimitives.ReadDoubleLittleEndian(span[32..]);
        var z = BinaryPrimitives.ReadDoubleLittleEndian(span[40..]);

        var rotX = BinaryPrimitives.ReadSingleLittleEndian(span[48..]);
        var rotY = BinaryPrimitives.ReadSingleLittleEndian(span[52..]);
        var rotZ = BinaryPrimitives.ReadSingleLittleEndian(span[56..]);
        var rotW = BinaryPrimitives.ReadSingleLittleEndian(span[60..]);

        var ticks = BinaryPrimitives.ReadInt64LittleEndian(span[64..]);

        return new CachedPosition(entityId, locationId, x, y, z, rotX, rotY, rotZ, rotW, new DateTime(ticks, DateTimeKind.Utc));
    }
}

/// <summary>
/// 位置持久化后台服务 — 定期将 Redis 中的脏位置批量刷入 PostgreSQL。
/// </summary>
public sealed class PositionFlushService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IPositionCacheService _cache;
    private readonly IServiceProvider _services;
    private readonly ILogger<PositionFlushService> _logger;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);

    public PositionFlushService(IPositionCacheService cache, IServiceProvider services, ILogger<PositionFlushService> logger)
    {
        _cache = cache;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PositionFlushService started (interval: {Interval}s)", FlushInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(FlushInterval, stoppingToken);

            try
            {
                var dirtyPositions = _cache.DrainDirtyPositions();
                if (dirtyPositions.Count == 0) continue;

                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<GameServer.Infrastructure.Persistence.GameDbContext>();

                foreach (var pos in dirtyPositions)
                {
                    var player = await db.Players.FindAsync([pos.EntityId], stoppingToken);
                    if (player is not null)
                    {
                        player.CurrentLocationId = pos.LocationId;
                        player.LocalPositionX = pos.X;
                        player.LocalPositionY = pos.Y;
                        player.LocalPositionZ = pos.Z;
                    }
                }

                await db.SaveChangesAsync(stoppingToken);

                _logger.LogDebug("Flushed {Count} positions to database", dirtyPositions.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush positions to database");
            }
        }
    }
}
