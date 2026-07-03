using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Ark.Core.Threading;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Systems.Parallel;

/// <summary>
/// 空间分区系统 — 使用 Grid 分区实现高效 AOI（Area of Interest）更新。
/// 只有在玩家 AOI 内的实体才需要完整更新。
/// </summary>
public sealed class SpatialPartitionSystem : QuerySystem<WorldPosition>
{
    // Grid 配置
    private const float CellSize = 50f; // 每个 Cell 50x50 米
    private const int   GridWidth = 100; // 5000x5000 米世界
    private const int   GridHeight = 100;

    // AOI 配置
    private float _aoiRadius = 100f;        // 完整更新范围
    private float _extendedAoiRadius = 200f; // 低频更新范围

    // 玩家位置（每帧采样）
    private float _playerX, _playerZ;
    private int   _playerCellX, _playerCellZ;
    private bool  _playerValid;

    // 空间分区数据（Grid of Entity lists）
    private readonly List<int>[] _grid = new List<int>[GridWidth * GridHeight];
    private readonly HashSet<int> _entitiesInAoi = new();
    private readonly HashSet<int> _entitiesInExtendedAoi = new();

    // 帧限流器（大范围 AOI 更新不需要每帧执行）
    private readonly FrameThrottler _throttler = new(2000);

    public SpatialPartitionSystem()
    {
        // 初始化 Grid
        for (int i = 0; i < _grid.Length; i++)
        {
            _grid[i] = new List<int>(16);
        }
    }

    protected override void OnUpdate()
    {
        // ═══ 1. 采样玩家位置 ═══
        SamplePlayerPosition();
        if (!_playerValid) return;

        // ═══ 2. 清空旧的 Grid（分帧执行以避免单帧卡顿）═══
        // 只清空玩家周围的 Grid
        int clearRadius = (int)(_extendedAoiRadius / CellSize) + 2;
        ClearGridAroundPlayer(clearRadius);

        // ═══ 3. 重新填充 Grid ═══
        RebuildGridAroundPlayer(clearRadius);

        // ═══ 4. 更新 AOI 标签 ═══
        UpdateAoiTags();

        _throttler.AdvanceFrame();
    }

    private void SamplePlayerPosition()
    {
        var store = CommandBuffer.EntityStore;
        var playerQuery = store.Query<WorldPosition>()
            .AllTags(Tags.Get<LocalPlayer>());
        _playerValid = false;

        foreach (var chunk in playerQuery.Chunks)
        {
            var positions = chunk.Chunk1;
            if (positions.Length > 0)
            {
                var pos = positions.Span[0];
                _playerX = pos.X;
                _playerZ = pos.Z;
                _playerCellX = WorldToCellX(pos.X);
                _playerCellZ = WorldToCellZ(pos.Z);
                _playerValid = true;
                break;
            }
        }
    }

    private void ClearGridAroundPlayer(int radius)
    {
        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int cx = _playerCellX + dx;
                int cz = _playerCellZ + dz;
                if (cx >= 0 && cx < GridWidth && cz >= 0 && cz < GridHeight)
                {
                    _grid[cz * GridWidth + cx].Clear();
                }
            }
        }
    }

    private void RebuildGridAroundPlayer(int radius)
    {
        float minX = _playerX - radius * CellSize;
        float maxX = _playerX + radius * CellSize;
        float minZ = _playerZ - radius * CellSize;
        float maxZ = _playerZ + radius * CellSize;

        // 遍历所有实体，填入 Grid
        foreach (var chunk in Query.Chunks)
        {
            var positions = chunk.Chunk1;
            var entities = chunk.Entities;
            
            for (int i = 0; i < entities.Length; i++)
            {
                ref readonly var pos = ref positions.Span[i];

                // 跳过范围外的实体
                if (pos.X < minX || pos.X > maxX || pos.Z < minZ || pos.Z > maxZ)
                    continue;

                int cx = WorldToCellX(pos.X);
                int cz = WorldToCellZ(pos.Z);

                if (cx >= 0 && cx < GridWidth && cz >= 0 && cz < GridHeight)
                {
                    _grid[cz * GridWidth + cx].Add(entities[i]);
                }
            }
        }
    }

    private void UpdateAoiTags()
    {
        float aoiRadiusSq    = _aoiRadius * _aoiRadius;
        float extAoiRadiusSq = _extendedAoiRadius * _extendedAoiRadius;

        _entitiesInAoi.Clear();
        _entitiesInExtendedAoi.Clear();

        // 收集 AOI 内的实体
        int checkRadius = (int)(_extendedAoiRadius / CellSize) + 1;
        var store = CommandBuffer.EntityStore;
        for (int dz = -checkRadius; dz <= checkRadius; dz++)
        {
            for (int dx = -checkRadius; dx <= checkRadius; dx++)
            {
                int cx = _playerCellX + dx;
                int cz = _playerCellZ + dz;
                if (cx < 0 || cx >= GridWidth || cz < 0 || cz >= GridHeight)
                    continue;

                var cell = _grid[cz * GridWidth + cx];
                foreach (int entityId in cell)
                {
                    if (!store.TryGetEntityById(entityId, out var entity))
                        continue;

                    if (!entity.TryGetComponent<WorldPosition>(out var pos))
                        continue;

                    float dx2 = pos.X - _playerX;
                    float dz2 = pos.Z - _playerZ;
                    float distSq = dx2 * dx2 + dz2 * dz2;

                    if (distSq <= aoiRadiusSq)
                    {
                        _entitiesInAoi.Add(entityId);

                        // 添加 InAoi 标签（如果没有）
                        if (!entity.Tags.Has<InAoi>())
                        {
                            entity.AddTag<InAoi>();
                            entity.RemoveTag<InExtendedAoi>();
                            entity.RemoveTag<OutOfAoi>();
                        }
                    }
                    else if (distSq <= extAoiRadiusSq)
                    {
                        _entitiesInExtendedAoi.Add(entityId);

                        if (!entity.Tags.Has<InExtendedAoi>())
                        {
                            entity.AddTag<InExtendedAoi>();
                            entity.RemoveTag<InAoi>();
                            entity.RemoveTag<OutOfAoi>();
                        }
                    }
                }
            }
        }
    }

    // ═══ 坐标转换 ═══

    private static int WorldToCellX(float x) => (int)((x + GridWidth * CellSize * 0.5f) / CellSize);
    private static int WorldToCellZ(float z) => (int)((z + GridHeight * CellSize * 0.5f) / CellSize);

    // ═══ 公开查询方法 ═══

    /// <summary>
    /// 获取指定位置周围的实体（用于攻击/技能范围检测）。
    /// </summary>
    public IEnumerable<int> GetEntitiesInRadius(float x, float z, float radius)
    {
        int cellRadius = (int)(radius / CellSize) + 1;
        int centerCX = WorldToCellX(x);
        int centerCZ = WorldToCellZ(z);
        float radiusSq = radius * radius;

        for (int dz = -cellRadius; dz <= cellRadius; dz++)
        {
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                int cx = centerCX + dx;
                int cz = centerCZ + dz;
                if (cx < 0 || cx >= GridWidth || cz < 0 || cz >= GridHeight)
                    continue;

                var cell = _grid[cz * GridWidth + cx];
                foreach (int entityId in cell)
                {
                    var store = CommandBuffer.EntityStore;
                    if (!store.TryGetEntityById(entityId, out var entity))
                        continue;

                    if (!entity.TryGetComponent<WorldPosition>(out var pos))
                        continue;

                    float dx2 = pos.X - x;
                    float dz2 = pos.Z - z;
                    if (dx2 * dx2 + dz2 * dz2 <= radiusSq)
                    {
                        yield return entityId;
                    }
                }
            }
        }
    }

    public void SetAoiRadius(float radius) => _aoiRadius = radius;
    public void SetExtendedAoiRadius(float radius) => _extendedAoiRadius = radius;
}
