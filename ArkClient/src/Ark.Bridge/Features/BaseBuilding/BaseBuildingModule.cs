using System;
using System.Collections.Generic;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Analyzers.Attributes;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;
using Ark.Shared.Data;
using GodotVec3 = Godot.Vector3;
using GodotQuat = Godot.Quaternion;

namespace Ark.Bridge.Features.BaseBuilding;

/// <summary>
/// 基地建造模块 — 建筑放置、升级、拆除。
/// 当前为客户端本地授权路径，本地构造建筑实体。待迁移到服务端授权
/// 后应移除 [EcsAuthorityBridge]，让 ECS005 重新拦截违规写入。
/// </summary>
[EcsAuthorityBridge]
public sealed class BaseBuildingModule : IBaseBuildingService
{
    private readonly EntityStore _store;
    private bool _inBuildMode;
    private int  _selectedBuildingTypeId;

    // IBaseBuildingService 接口用的类型表（保持接口兼容）
    private readonly List<BuildingTypeInfo> _buildingTypes = new()
    {
        new(1, "Wall",       new() { [4] = 10  }, 3f),
        new(2, "Tower",      new() { [4] = 50  }, 8f),
        new(3, "Storage",    new() { [4] = 30  }, 6f),
        new(4, "Barracks",   new() { [4] = 100 }, 12f),
        new(5, "Rocket Pad", new() { [4] = 200, [5] = 50 }, 20f),
        new(6, "Tank Factory", new() { [4] = 150 }, 15f),
    };

    // ═══ 公开状态（供 BuildPlacementController 读取）═══
    public bool IsInBuildMode        => _inBuildMode;
    public int  SelectedBuildingType => _selectedBuildingTypeId;

    // ═══ 事件 ═══
    /// <summary>IBaseBuildingService 兼容事件（只携带 entityId）</summary>
    public event Action<int>? OnBuildingPlaced;
    public event Action<int>? OnBuildingDestroyed;

    /// <summary>扩展事件：携带完整放置信息供视觉管理器使用</summary>
    public event Action<int, GodotVec3, GodotQuat, int>? OnBuildingPlacedAt;
    // 参数：entityId, worldPosition, rotation, buildingTypeId

    public BaseBuildingModule(EntityStore store)
    {
        _store = store;
    }

    // ═══════════════════════════════════════════════════════════════════
    //                       IBaseBuildingService 实现
    // ═══════════════════════════════════════════════════════════════════

    public void EnterBuildMode(int buildingTypeId)
    {
        _inBuildMode              = true;
        _selectedBuildingTypeId   = buildingTypeId;
    }

    public void ExitBuildMode()
    {
        _inBuildMode            = false;
        _selectedBuildingTypeId = 0;
    }

    public bool CanPlaceAt(Vector3 position, Quaternion rotation)
    {
        return CanPlaceAtGodot(new GodotVec3(position.X, position.Y, position.Z));
    }

    public bool PlaceBuilding(Vector3 position, Quaternion rotation)
    {
        var gPos = new GodotVec3(position.X, position.Y, position.Z);
        var gRot = new GodotQuat(rotation.X, rotation.Y, rotation.Z, rotation.W);
        return PlaceBuildingAt(gPos, gRot);
    }

    public bool DestroyBuilding(int buildingEntityId)
    {
        if (!_store.TryGetEntityById(buildingEntityId, out var entity))
            return false;
        if (!entity.Tags.Has<BuildingTag>())
            return false;

        entity.AddTag<PendingDestroy>();
        OnBuildingDestroyed?.Invoke(buildingEntityId);
        return true;
    }

    public bool UpgradeBuilding(int buildingEntityId)
    {
        if (!_store.TryGetEntityById(buildingEntityId, out var entity))
            return false;
        if (!entity.TryGetComponent<Building>(out var building))
            return false;
        if (building.Level >= 5)
            return false;

        entity.GetComponent<Building>() = building with { Level = (byte)(building.Level + 1) };
        return true;
    }

    public IReadOnlyList<BuildingTypeInfo> GetAvailableBuildingTypes() => _buildingTypes;

    // ═══════════════════════════════════════════════════════════════════
    //                       Godot-native API（供 C# 内部调用）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 检查指定世界坐标是否可以放置当前选中的建筑。
    /// </summary>
    public bool CanPlaceAtGodot(GodotVec3 position)
    {
        if (!_inBuildMode || _selectedBuildingTypeId == 0) return false;

        var def = BuildingDef.Get(_selectedBuildingTypeId);
        if (def == null) return false;

        // 计算当前建筑的碰撞半径
        float ownRadius = Math.Max(def.Value.FootprintHalfSize.X, def.Value.FootprintHalfSize.Z)
                          + BuildingDef.MinSpacing;

        // 检查与已有建筑的重叠
        var query = _store.Query<WorldPosition>().AllTags(Tags.Get<BuildingTag>());
        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            foreach (ref readonly var pos in positions.Span)
            {
                float dx = pos.X - position.X;
                float dz = pos.Z - position.Z;
                if (dx * dx + dz * dz < ownRadius * ownRadius)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 放置建筑（Godot 坐标版本）。
    /// </summary>
    public bool PlaceBuildingAt(GodotVec3 position, GodotQuat rotation)
    {
        if (!_inBuildMode || !CanPlaceAtGodot(position))
            return false;

        var def = BuildingDef.Get(_selectedBuildingTypeId);
        if (def == null) return false;

        var entity = _store.CreateEntity();
        entity.AddComponent(new WorldPosition { X = position.X, Y = position.Y, Z = position.Z });
        entity.AddComponent(new WorldRotation { X = rotation.X, Y = rotation.Y, Z = rotation.Z, W = rotation.W });
        entity.AddComponent(new Scale { X = def.Value.Size.X, Y = def.Value.Size.Y, Z = def.Value.Size.Z, Uniform = 1f });
        entity.AddComponent(new Building
        {
            BuildingTypeId       = (ushort)_selectedBuildingTypeId,
            Level                = 1,
            ConstructionProgress = 0,  // 0 = 刚开始建造
            OwnerId              = 1
        });
        entity.AddComponent(BuildingAccess.OwnerOnly(1));
        entity.AddComponent(new BoundingBox
        {
            MinX = -def.Value.FootprintHalfSize.X, MinY = 0, MinZ = -def.Value.FootprintHalfSize.Z,
            MaxX =  def.Value.FootprintHalfSize.X, MaxY = def.Value.Size.Y, MaxZ =  def.Value.FootprintHalfSize.Z
        });
        entity.AddComponent(new Health
        {
            Current = 1000f,
            Max = 1000f,
        });
        entity.AddComponent(new StructuralIntegrity
        {
            DamageThreshold = 50f,  // 子弹(~20dmg)无效；炮弹(300dmg)有效
            AccumulatedDamage = 0f,
            MaxIntegrity = 1000f,
        });
        entity.AddTag<BuildingTag>();
        entity.AddTag<PendingSpawn>();

        int entityId = entity.Id;
        OnBuildingPlaced?.Invoke(entityId);
        OnBuildingPlacedAt?.Invoke(entityId, position, rotation, _selectedBuildingTypeId);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //                       每帧更新
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧推进建造进度（由 EcsRoot._Process 调用）。
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

                // 根据 BuildingDef.BuildTime 计算每帧增量
                var def = BuildingDef.Get(b.BuildingTypeId);
                float perSecond = def.HasValue ? (100f / def.Value.BuildTime) : 10f;
                float next = b.ConstructionProgress + perSecond * deltaTime;

                // 使用 Ceiling 避免 float→byte 截断导致进度永远卡住
                // （当每帧增量 < 1.0 时，直接截断会使 byte 值永不递增）
                b.ConstructionProgress = (byte)Math.Min(MathF.Ceiling(next), 100f);
            }
        }
    }
}
