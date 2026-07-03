using Game.Shared.Core.Universe;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameServer.Modules.World;

/// <summary>
/// 出生点服务 — 为新角色分配分散的固定初始位置（新手村）。
///
/// 策略：
/// 1. 根据阵营匹配对应的出生点行星
/// 2. 在出生点城镇附近随机散布（避免堆叠）
/// 3. 将位置写入 Player 实体的分层坐标字段
/// </summary>
public sealed class SpawnPointService : GameServer.Application.Core.ISpawnPointAssigner
{
    private readonly GameDbContext _db;
    private readonly ILogger<SpawnPointService> _logger;

    // 缓存出生点以避免每次角色创建都查库
    private IReadOnlyList<SpawnPointInfo>? _cachedSpawnPoints;

    public SpawnPointService(GameDbContext db, ILogger<SpawnPointService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 为新角色分配初始出生位置。
    /// </summary>
    public async Task AssignSpawnPositionAsync(Player player, CancellationToken ct = default)
    {
        var spawnPoints = await GetSpawnPointsAsync(ct);

        // 1. 优先匹配阵营
        var matched = spawnPoints.FirstOrDefault(sp => sp.Faction == player.Faction)
                   ?? spawnPoints.FirstOrDefault(); // fallback to any

        if (matched is null)
        {
            _logger.LogWarning("No spawn points found, using default position for player {PlayerId}", player.Id);
            player.CurrentLocationId = null;
            player.LocalPositionX = 32;
            player.LocalPositionY = 0;
            player.LocalPositionZ = -24;
            return;
        }

        // 2. 在出生点附近随机散布
        var rng = new SeededRng(player.Id.GetHashCode());
        var angle = rng.NextDouble(0, Math.PI * 2);
        var distance = rng.NextDouble(5, matched.ScatterRadius);

        player.CurrentLocationId = matched.LocationId;
        player.SolarSystemId = matched.SolarSystemId;
        player.LocalPositionX = matched.CenterX + distance * Math.Cos(angle);
        player.LocalPositionY = 0; // 地面高度由客户端地形采样确定
        player.LocalPositionZ = matched.CenterZ + distance * Math.Sin(angle);
        player.CurrentWorldId = matched.WorldCode;

        _logger.LogInformation("Assigned spawn: player={PlayerId}, location={LocationId}, pos=({X:F1}, {Z:F1}), faction={Faction}",
            player.Id, matched.LocationId, player.LocalPositionX, player.LocalPositionZ, matched.Faction);
    }

    private async Task<IReadOnlyList<SpawnPointInfo>> GetSpawnPointsAsync(CancellationToken ct)
    {
        if (_cachedSpawnPoints is not null)
            return _cachedSpawnPoints;

        var surfaces = await _db.Locations
            .Where(l => l.LocationType == LocationType.PlanetSurface
                     && l.MetadataJson != null)
            .ToListAsync(ct);

        var result = new List<SpawnPointInfo>();
        foreach (var surface in surfaces)
        {
            if (surface.MetadataJson is null) continue;

            try
            {
                var meta = System.Text.Json.JsonSerializer.Deserialize<SpawnMetadata>(surface.MetadataJson);
                if (meta is null || !meta.SpawnPoint) continue;

                // 查找对应的 SolarSystem 祖先
                var solarSystemId = await FindAncestorIdAsync(surface, LocationType.SolarSystem, ct);

                result.Add(new SpawnPointInfo(
                    surface.Id,
                    solarSystemId,
                    meta.Faction ?? "Neutral",
                    meta.SpawnX,
                    meta.SpawnZ,
                    meta.ScatterRadius,
                    surface.Code));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse spawn metadata for location {LocationId}", surface.Id);
            }
        }

        _cachedSpawnPoints = result;
        _logger.LogInformation("Loaded {Count} spawn points", result.Count);
        return result;
    }

    private async Task<long> FindAncestorIdAsync(LocationNode node, LocationType targetType, CancellationToken ct)
    {
        // 通过 HierarchyPath 快速找到祖先
        if (!string.IsNullOrEmpty(node.HierarchyPath))
        {
            var ids = node.HierarchyPath.Split('/').Select(long.Parse).ToList();
            // 反向查找
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                var ancestor = await _db.Locations.FindAsync([ids[i]], ct);
                if (ancestor?.LocationType == targetType)
                    return ancestor.Id;
            }
        }

        // fallback: 遍历 parent
        var current = node;
        while (current.ParentLocationId.HasValue)
        {
            current = await _db.Locations.FindAsync([current.ParentLocationId.Value], ct);
            if (current is null) break;
            if (current.LocationType == targetType) return current.Id;
        }

        return 0;
    }

    private sealed record SpawnPointInfo(long LocationId, long SolarSystemId, string Faction, double CenterX, double CenterZ, double ScatterRadius, string WorldCode);

    private sealed class SpawnMetadata
    {
        public bool SpawnPoint { get; set; }
        public string? Faction { get; set; }
        public double SpawnX { get; set; }
        public double SpawnZ { get; set; }
        public double ScatterRadius { get; set; } = 50;
    }
}
