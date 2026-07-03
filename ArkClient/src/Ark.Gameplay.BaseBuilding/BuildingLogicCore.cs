using System;
using System.Collections.Generic;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Gameplay.BaseBuilding;

/// <summary>
/// 建筑逻辑核心 — 纯 C# 实现，不依赖 Godot。
/// 
/// 职责：
/// - 建筑放置合法性检查
/// - 建造进度推进
/// - 建筑升级/拆除逻辑
/// 
/// Godot 集成层 (BaseBuildingModule) 负责类型转换和事件桥接。
/// </summary>
public sealed class BuildingLogicCore
{
    private readonly EntityStore _store;

    // 建筑类型配置（ID → BuildTime）
    private readonly Dictionary<int, float> _buildTimes = new()
    {
        [1] = 3f,   // Wall
        [2] = 8f,   // Tower
        [3] = 6f,   // Storage
        [4] = 12f,  // Barracks
        [5] = 20f,  // Rocket Pad
    };

    /// <summary>最小建筑间距</summary>
    public const float MinSpacing = 2.0f;

    public BuildingLogicCore(EntityStore store)
    {
        _store = store;
    }

    /// <summary>
    /// 注册建筑类型配置。
    /// </summary>
    public void RegisterBuildingType(int typeId, float buildTime)
    {
        _buildTimes[typeId] = buildTime;
    }

    /// <summary>
    /// 检查指定位置是否可以放置建筑。
    /// </summary>
    /// <param name="position">世界坐标</param>
    /// <param name="footprintRadius">地基半径（用于碰撞检测）</param>
    /// <returns>true = 可放置</returns>
    public bool CanPlaceAt(Vector3 position, float footprintRadius)
    {
        float checkRadius = footprintRadius + MinSpacing;

        var query = _store.Query<WorldPosition>().AllTags(Tags.Get<BuildingTag>());
        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            foreach (ref readonly var pos in positions.Span)
            {
                float dx = pos.X - position.X;
                float dz = pos.Z - position.Z;
                if (dx * dx + dz * dz < checkRadius * checkRadius)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 创建建筑实体（ECS 层）。
    /// </summary>
    /// <returns>新建筑的 Entity ID</returns>
    public int CreateBuildingEntity(
        Vector3 position,
        Quaternion rotation,
        int buildingTypeId,
        Vector3 size,
        int ownerId = 1)
    {
        var entity = _store.CreateEntity();

        entity.AddComponent(new WorldPosition { X = position.X, Y = position.Y, Z = position.Z });
        entity.AddComponent(new WorldRotation { X = rotation.X, Y = rotation.Y, Z = rotation.Z, W = rotation.W });
        entity.AddComponent(new Scale { X = size.X, Y = size.Y, Z = size.Z, Uniform = 1f });
        entity.AddComponent(new Building
        {
            BuildingTypeId       = (ushort)buildingTypeId,
            Level                = 1,
            ConstructionProgress = 0,
            OwnerId              = ownerId
        });
        entity.AddComponent(new BoundingBox
        {
            MinX = -size.X * 0.5f, MinY = 0,     MinZ = -size.Z * 0.5f,
            MaxX =  size.X * 0.5f, MaxY = size.Y, MaxZ =  size.Z * 0.5f
        });
        entity.AddTag<BuildingTag>();
        entity.AddTag<PendingSpawn>();

        return entity.Id;
    }

    /// <summary>
    /// 推进所有建筑的建造进度。
    /// </summary>
    public void UpdateConstruction(float deltaTime)
    {
        var query = _store.Query<Building>().AllTags(Tags.Get<BuildingTag>());
        foreach (var chunk in query.Chunks)
        {
            var buildings = chunk.Chunk1;
            for (int i = 0; i < chunk.Entities.Length; i++)
            {
                ref var b = ref buildings.Span[i];
                if (b.ConstructionProgress >= 100) continue;

                // 查找建造时间
                if (!_buildTimes.TryGetValue(b.BuildingTypeId, out float buildTime))
                    buildTime = 10f; // 默认 10 秒

                float perSecond = 100f / buildTime;
                b.ConstructionProgress = (byte)Math.Min(
                    b.ConstructionProgress + perSecond * deltaTime,
                    100f
                );
            }
        }
    }

    /// <summary>
    /// 标记建筑待销毁。
    /// </summary>
    public bool MarkForDestruction(int entityId)
    {
        if (!_store.TryGetEntityById(entityId, out var entity))
            return false;
        if (!entity.Tags.Has<BuildingTag>())
            return false;

        entity.AddTag<PendingDestroy>();
        return true;
    }

    /// <summary>
    /// 升级建筑。
    /// </summary>
    public bool UpgradeBuilding(int entityId, int maxLevel = 5)
    {
        if (!_store.TryGetEntityById(entityId, out var entity))
            return false;
        if (!entity.TryGetComponent<Building>(out var building))
            return false;
        if (building.Level >= maxLevel)
            return false;

        entity.GetComponent<Building>() = building with { Level = (byte)(building.Level + 1) };
        return true;
    }

    /// <summary>
    /// 获取建筑的建造进度 (0-100)。
    /// </summary>
    public byte GetConstructionProgress(int entityId)
    {
        if (!_store.TryGetEntityById(entityId, out var entity))
            return 0;
        if (!entity.TryGetComponent<Building>(out var building))
            return 0;
        return building.ConstructionProgress;
    }
}
