using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Ark.Abstractions;
using Ark.Analyzers.Attributes;
using Ark.Ecs.Components;
using Ark.Shared.Data;

namespace Ark.Services.Remote;

/// <summary>
/// 远程游戏世界实现 — 所有实体状态来自服务端快照。
/// 服务端通过 TCP 以 20Hz 推送 zone 快照（packet 0x10），
/// <see cref="SnapshotApplier"/> 解码后调用 <see cref="ApplySnapshot"/> 更新本地缓存。
/// </summary>
public sealed class RemoteGameWorld : IGameWorld
{
    private readonly ConcurrentDictionary<int, RemoteEntity> _entities = new();
    private readonly ConcurrentDictionary<Guid, int> _guidToId = new();
    private readonly Networking.NetworkManager _network;
    private float _worldTime;
    private ulong _lastTick;

    public bool IsLoaded { get; private set; }
    public int LocalPlayerId { get; private set; }
    public Guid LocalPlayerGuid { get; private set; }
    public ulong LastSnapshotTick => _lastTick;
    public DateTime? LastSnapshotReceivedAtUtc { get; private set; }

    public RemoteGameWorld(Networking.NetworkManager network)
    {
        _network = network;
    }

    /// <summary>
    /// 设置本地玩家身份（登录后由 GameServices 调用）。
    /// </summary>
    public void SetLocalPlayer(Guid playerId)
    {
        LocalPlayerGuid = playerId;
    }

    /// <summary>
    /// 新一轮进入世界前重置快照状态，避免沿用旧世界/旧角色数据。
    /// </summary>
    public void BeginWorldEntry(Guid playerId)
    {
        LocalPlayerGuid = playerId;
        LocalPlayerId = 0;
        IsLoaded = false;
        _lastTick = 0;
        _worldTime = 0f;
        LastSnapshotReceivedAtUtc = null;
        _entities.Clear();
        _guidToId.Clear();
    }

    /// <summary>
    /// 由 <see cref="SnapshotApplier"/> 调用 — 应用服务端快照，创建/更新/销毁远程实体。
    /// </summary>
    public void ApplySnapshot(ulong tick, float serverTime, SnapshotEntity[] entities)
    {
        _lastTick = tick;
        _worldTime = serverTime;
        LastSnapshotReceivedAtUtc = DateTime.UtcNow;

        var seen = new HashSet<int>();
        int newCount = 0;

        foreach (var se in entities)
        {
            seen.Add(se.Id);

            if (_entities.TryGetValue(se.Id, out var existing))
            {
                // Update
                existing.PreviousPosition = existing.Position;
                existing.Position = se.Position;
                existing.Rotation = se.Rotation;
                existing.Velocity = se.Velocity;
                existing.TypeId = se.TypeId;
                existing.Health = se.Health;
                existing.MaxHealth = se.MaxHealth;
                existing.WeaponDefId = se.WeaponDefId;
                existing.WeaponCategory = se.WeaponCategory;
                existing.CurrentMag = se.CurrentMag;
                existing.MagCapacity = se.MagCapacity;
                existing.ReserveAmmo = se.ReserveAmmo;
                existing.MaxReserve = se.MaxReserve;
                existing.IsReloading = se.IsReloading;
                existing.AttachedVehicleEntityId = se.AttachedVehicleEntityId;
                existing.SeatIndex = se.SeatIndex;
                existing.SeatType = se.SeatType;
                existing.HasMountedWeapon = se.HasMountedWeapon;
                existing.SeatOffset = se.SeatOffset;
                existing.SeatCount = se.SeatCount;
                existing.OccupiedSeatCount = se.OccupiedSeatCount;
                existing.FuelPercent = se.FuelPercent;
                existing.TurretYaw = se.TurretYaw;
                existing.TurretPitch = se.TurretPitch;
                existing.MountedWeaponHeat = se.MountedWeaponHeat;
                existing.MountedWeaponCycleRemaining = se.MountedWeaponCycleRemaining;
                existing.MountedWeaponReloadRemaining = se.MountedWeaponReloadRemaining;
                existing.MountedWeaponFaultRemaining = se.MountedWeaponFaultRemaining;
                existing.MountedWeaponMaintenanceRemaining = se.MountedWeaponMaintenanceRemaining;
                existing.MountedWeaponMaintenanceLevel = se.MountedWeaponMaintenanceLevel;
                existing.MountedWeaponOperationProgress = se.MountedWeaponOperationProgress;
                existing.MountedWeaponSkillScalar = se.MountedWeaponSkillScalar;
                existing.MountedWeaponFaultCode = se.MountedWeaponFaultCode;
                existing.MountedWeaponRepairStep = se.MountedWeaponRepairStep;
                existing.MountedWeaponRepairStepCount = se.MountedWeaponRepairStepCount;
                existing.MountedWeaponMaterialUnits = se.MountedWeaponMaterialUnits;
                existing.SpaceFlightPhase = se.SpaceFlightPhase;
                existing.BuildingLevel = se.BuildingLevel;
                existing.BuildingConstructionProgress = se.BuildingConstructionProgress;
                existing.BuildingFrontDamage = se.BuildingFrontDamage;
                existing.BuildingBackDamage = se.BuildingBackDamage;
                existing.BuildingRightDamage = se.BuildingRightDamage;
                existing.BuildingLeftDamage = se.BuildingLeftDamage;
                existing.BuildingDamageCluster0 = new Vector4(se.BuildingDamageCluster0X, se.BuildingDamageCluster0Y, se.BuildingDamageCluster0Z, se.BuildingDamageCluster0Strength);
                existing.BuildingDamageCluster1 = new Vector4(se.BuildingDamageCluster1X, se.BuildingDamageCluster1Y, se.BuildingDamageCluster1Z, se.BuildingDamageCluster1Strength);
                existing.BuildingDamageCluster2 = new Vector4(se.BuildingDamageCluster2X, se.BuildingDamageCluster2Y, se.BuildingDamageCluster2Z, se.BuildingDamageCluster2Strength);
                existing.Altitude = se.Altitude;
                existing.OrbitalVelocity = se.OrbitalVelocity;
                existing.RemainingDeltaV = se.RemainingDeltaV;
                existing.IsAlive = se.IsAlive;
                existing.AttachToTerrain = se.AttachToTerrain;
                existing.LastUpdateTick = tick;
            }
            else
            {
                // New entity
                var remote = new RemoteEntity
                {
                    Id = se.Id,
                    NetworkId = se.NetworkId,
                    Type = MapEntityType(se.Type),
                    TypeId = se.TypeId,
                    Position = se.Position,
                    PreviousPosition = se.Position,
                    Rotation = se.Rotation,
                    Velocity = se.Velocity,
                    Health = se.Health,
                    MaxHealth = se.MaxHealth,
                    WeaponDefId = se.WeaponDefId,
                    WeaponCategory = se.WeaponCategory,
                    CurrentMag = se.CurrentMag,
                    MagCapacity = se.MagCapacity,
                    ReserveAmmo = se.ReserveAmmo,
                    MaxReserve = se.MaxReserve,
                    IsReloading = se.IsReloading,
                    AttachedVehicleEntityId = se.AttachedVehicleEntityId,
                    SeatIndex = se.SeatIndex,
                    SeatType = se.SeatType,
                    HasMountedWeapon = se.HasMountedWeapon,
                    SeatOffset = se.SeatOffset,
                    SeatCount = se.SeatCount,
                    OccupiedSeatCount = se.OccupiedSeatCount,
                    FuelPercent = se.FuelPercent,
                    TurretYaw = se.TurretYaw,
                    TurretPitch = se.TurretPitch,
                    MountedWeaponHeat = se.MountedWeaponHeat,
                    MountedWeaponCycleRemaining = se.MountedWeaponCycleRemaining,
                    MountedWeaponReloadRemaining = se.MountedWeaponReloadRemaining,
                    MountedWeaponFaultRemaining = se.MountedWeaponFaultRemaining,
                    MountedWeaponMaintenanceRemaining = se.MountedWeaponMaintenanceRemaining,
                    MountedWeaponMaintenanceLevel = se.MountedWeaponMaintenanceLevel,
                    MountedWeaponOperationProgress = se.MountedWeaponOperationProgress,
                    MountedWeaponSkillScalar = se.MountedWeaponSkillScalar,
                    MountedWeaponFaultCode = se.MountedWeaponFaultCode,
                    MountedWeaponRepairStep = se.MountedWeaponRepairStep,
                    MountedWeaponRepairStepCount = se.MountedWeaponRepairStepCount,
                    MountedWeaponMaterialUnits = se.MountedWeaponMaterialUnits,
                    SpaceFlightPhase = se.SpaceFlightPhase,
                    BuildingLevel = se.BuildingLevel,
                    BuildingConstructionProgress = se.BuildingConstructionProgress,
                    BuildingFrontDamage = se.BuildingFrontDamage,
                    BuildingBackDamage = se.BuildingBackDamage,
                    BuildingRightDamage = se.BuildingRightDamage,
                    BuildingLeftDamage = se.BuildingLeftDamage,
                    BuildingDamageCluster0 = new Vector4(se.BuildingDamageCluster0X, se.BuildingDamageCluster0Y, se.BuildingDamageCluster0Z, se.BuildingDamageCluster0Strength),
                    BuildingDamageCluster0Age = se.BuildingDamageCluster0Age,
                    BuildingDamageCluster0RepairFill = se.BuildingDamageCluster0RepairFill,
                    BuildingDamageCluster1 = new Vector4(se.BuildingDamageCluster1X, se.BuildingDamageCluster1Y, se.BuildingDamageCluster1Z, se.BuildingDamageCluster1Strength),
                    BuildingDamageCluster1Age = se.BuildingDamageCluster1Age,
                    BuildingDamageCluster1RepairFill = se.BuildingDamageCluster1RepairFill,
                    BuildingDamageCluster2 = new Vector4(se.BuildingDamageCluster2X, se.BuildingDamageCluster2Y, se.BuildingDamageCluster2Z, se.BuildingDamageCluster2Strength),
                    BuildingDamageCluster2Age = se.BuildingDamageCluster2Age,
                    BuildingDamageCluster2RepairFill = se.BuildingDamageCluster2RepairFill,
                    BuildingDamageLayerState = se.BuildingDamageLayerState,
                    Altitude = se.Altitude,
                    OrbitalVelocity = se.OrbitalVelocity,
                    RemainingDeltaV = se.RemainingDeltaV,
                    IsAlive = se.IsAlive,
                    AttachToTerrain = se.AttachToTerrain,
                    LastUpdateTick = tick
                };
                _entities[se.Id] = remote;
                _guidToId[se.NetworkId] = se.Id;

                // Check if this is the local player
                if (se.NetworkId == LocalPlayerGuid)
                {
                    LocalPlayerId = se.Id;
                    ServiceLog.Info($"[RemoteWorld] ✅ Local player matched: Id={se.Id}, NetworkId={se.NetworkId}");
                }
                else
                {
                    ServiceLog.Info($"[RemoteWorld] 👤 NEW remote entity: Id={se.Id}, NetworkId={se.NetworkId}, Type={remote.Type}, Pos=({se.Position.X:F1},{se.Position.Y:F1},{se.Position.Z:F1})");
                }

                newCount++;
            }
        }

        // Destroy entities not in snapshot (left zone / despawned)
        var toRemove = _entities.Keys.Except(seen).ToList();
        foreach (var id in toRemove)
        {
            if (id == LocalPlayerId) continue; // Never destroy local player from snapshot

            if (_entities.TryRemove(id, out var removed))
            {
                _guidToId.TryRemove(removed.NetworkId, out _);
                ServiceLog.Info($"[RemoteWorld] ❌ Entity removed: Id={id}, NetworkId={removed.NetworkId}");
            }
        }

        if (!IsLoaded)
        {
            IsLoaded = true;
            ServiceLog.Info($"[RemoteWorld] ✅ LOADED with {_entities.Count} entities from server snapshot (localPlayerId={LocalPlayerId})");
        }
    }

    public void DestroyEntity(int entityId)
    {
        if (_entities.TryRemove(entityId, out var removed))
            _guidToId.TryRemove(removed.NetworkId, out _);
    }

    public float GetWorldTime() => _worldTime;

    // ═══════════════════════════════════════════════════════════════════
    //                         辅助方法
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>通过 Guid 查找本地实体 ID。</summary>
    public int? GetLocalId(Guid networkId) =>
        _guidToId.TryGetValue(networkId, out var id) ? id : null;

    /// <summary>通过本地 ID 查找 Guid。</summary>
    public Guid? GetNetworkId(int localId) =>
        _entities.TryGetValue(localId, out var e) ? e.NetworkId : null;

    /// <summary>获取远程实体（含插值数据）。</summary>
    public RemoteEntity? GetRemoteEntity(int entityId) =>
        _entities.GetValueOrDefault(entityId);

    public IReadOnlyCollection<RemoteEntity> GetAllRemoteEntities() =>
        _entities.Values.ToList().AsReadOnly();

    private static EntityType MapEntityType(byte serverType) => serverType switch
    {
        0 => EntityType.Player,
        1 => EntityType.RemotePlayer,
        2 => EntityType.Npc,
        3 => EntityType.Monster,
        4 => EntityType.Building,
        5 => EntityType.Vehicle,
        6 => EntityType.Spacecraft,
        7 => EntityType.Projectile,
        8 => EntityType.GroundItem,
        _ => EntityType.Environment
    };
}

/// <summary>
/// 远程实体 — 包含插值所需的上一帧位置。
/// </summary>
[MapToEcsComponent(typeof(RemoteEntityState))]
[MapToEcsComponent(typeof(Health))]
[MapToEcsComponent(typeof(VehicleState))]
[MapToEcsComponent(typeof(Building))]
[MapToEcsComponent(typeof(BuildingDamageState))]
[MapToEcsComponent(typeof(RemoteCombatState))]
[MapToEcsComponent(typeof(RemoteVehicleOccupantState))]
[MapToEcsComponent(typeof(RemoteVehicleRuntimeState))]
[MapToEcsComponent(typeof(RemoteSpacecraftState))]
public sealed class RemoteEntity
{
    [EcsFieldMap("SnapshotEntityId")] public int Id { get; init; }
    public Guid NetworkId { get; init; }
    [EcsFieldMap("EntityType")] public EntityType Type { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 PreviousPosition { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    [EcsFieldMap("VehicleDefId", typeof(VehicleState))]
    [EcsFieldMap("BuildingTypeId", typeof(Building))]
    public int TypeId { get; set; }
    [EcsFieldMap("Current", typeof(Health))]
    [EcsFieldMap("HealthCurrent", typeof(VehicleState))]
    public float Health { get; set; }
    [EcsFieldMap("Max", typeof(Health))]
    [EcsFieldMap("HealthMax", typeof(VehicleState))]
    public float MaxHealth { get; set; }
    public int WeaponDefId { get; set; }
    public byte WeaponCategory { get; set; }
    public int CurrentMag { get; set; }
    public int MagCapacity { get; set; }
    public int ReserveAmmo { get; set; }
    public int MaxReserve { get; set; }
    public bool IsReloading { get; set; }
    [EcsFieldMap("SnapshotVehicleEntityId", typeof(RemoteVehicleOccupantState))]
    public int AttachedVehicleEntityId { get; set; }
    [EcsFieldMap("CurrentSeatIndex", typeof(RemoteVehicleOccupantState))]
    public byte SeatIndex { get; set; }
    [EcsFieldMap("CurrentSeatType", typeof(RemoteVehicleOccupantState))]
    public byte SeatType { get; set; }
    public bool HasMountedWeapon { get; set; }
    public Vector3 SeatOffset { get; set; }
    public ushort SeatCount { get; set; }
    public ushort OccupiedSeatCount { get; set; }
    public float FuelPercent { get; set; }
    public float TurretYaw { get; set; }
    public float TurretPitch { get; set; }
    public float MountedWeaponHeat { get; set; }
    public float MountedWeaponCycleRemaining { get; set; }
    public float MountedWeaponReloadRemaining { get; set; }
    public float MountedWeaponFaultRemaining { get; set; }
    public float MountedWeaponMaintenanceRemaining { get; set; }
    public float MountedWeaponMaintenanceLevel { get; set; }
    public float MountedWeaponOperationProgress { get; set; }
    public float MountedWeaponSkillScalar { get; set; }
    public byte MountedWeaponFaultCode { get; set; }
    public byte MountedWeaponRepairStep { get; set; }
    public byte MountedWeaponRepairStepCount { get; set; }
    public byte MountedWeaponMaterialUnits { get; set; }
    [EcsFieldMap("FlightPhase", typeof(RemoteSpacecraftState))]
    public byte SpaceFlightPhase { get; set; }
    [EcsFieldMap("Level", typeof(Building))] public byte BuildingLevel { get; set; }
    [EcsFieldMap("ConstructionProgress", typeof(Building))] public byte BuildingConstructionProgress { get; set; }
    [EcsFieldMap("FrontDamage", typeof(BuildingDamageState))] public byte BuildingFrontDamage { get; set; }
    [EcsFieldMap("BackDamage", typeof(BuildingDamageState))] public byte BuildingBackDamage { get; set; }
    [EcsFieldMap("RightDamage", typeof(BuildingDamageState))] public byte BuildingRightDamage { get; set; }
    [EcsFieldMap("LeftDamage", typeof(BuildingDamageState))] public byte BuildingLeftDamage { get; set; }
    public Vector4 BuildingDamageCluster0 { get; set; }
    public float BuildingDamageCluster0Age { get; set; }
    public float BuildingDamageCluster0RepairFill { get; set; }
    public Vector4 BuildingDamageCluster1 { get; set; }
    public float BuildingDamageCluster1Age { get; set; }
    public float BuildingDamageCluster1RepairFill { get; set; }
    public Vector4 BuildingDamageCluster2 { get; set; }
    public float BuildingDamageCluster2Age { get; set; }
    public float BuildingDamageCluster2RepairFill { get; set; }
    public byte BuildingDamageLayerState { get; set; }
    public float Altitude { get; set; }
    public float OrbitalVelocity { get; set; }
    public float RemainingDeltaV { get; set; }
    [EcsFieldMap("IsOperational", typeof(VehicleState))] public bool IsAlive { get; set; }
    public bool AttachToTerrain { get; set; }
    public ulong LastUpdateTick { get; set; }
}

/// <summary>
/// 快照中的单个实体数据（从服务端二进制解码后）。
/// </summary>
[MapToEcsComponent(typeof(RemoteEntityState))]
public readonly record struct SnapshotEntity(
    [property: EcsFieldMap("SnapshotEntityId")] int Id,
    Guid NetworkId,
    [property: EcsFieldMap("EntityType")] byte Type,
    int TypeId,
    Vector3 Position,
    Quaternion Rotation,
    Vector3 Velocity,
    float Health,
    float MaxHealth,
    int WeaponDefId,
    byte WeaponCategory,
    int CurrentMag,
    int MagCapacity,
    int ReserveAmmo,
    int MaxReserve,
    int AttachedVehicleEntityId,
    byte SeatIndex,
    byte SeatType,
    bool HasMountedWeapon,
    Vector3 SeatOffset,
    ushort SeatCount,
    ushort OccupiedSeatCount,
    float FuelPercent,
    float TurretYaw,
    float TurretPitch,
    float MountedWeaponHeat,
    float MountedWeaponCycleRemaining,
    float MountedWeaponReloadRemaining,
    float MountedWeaponFaultRemaining,
    float MountedWeaponMaintenanceRemaining,
    float MountedWeaponMaintenanceLevel,
    float MountedWeaponOperationProgress,
    float MountedWeaponSkillScalar,
    byte SpaceFlightPhase,
    byte BuildingLevel,
    byte BuildingConstructionProgress,
    byte BuildingFrontDamage,
    byte BuildingBackDamage,
    byte BuildingRightDamage,
    byte BuildingLeftDamage,
    float BuildingDamageCluster0X,
    float BuildingDamageCluster0Y,
    float BuildingDamageCluster0Z,
    float BuildingDamageCluster0Strength,
    float BuildingDamageCluster0Age,
    float BuildingDamageCluster0RepairFill,
    float BuildingDamageCluster1X,
    float BuildingDamageCluster1Y,
    float BuildingDamageCluster1Z,
    float BuildingDamageCluster1Strength,
    float BuildingDamageCluster1Age,
    float BuildingDamageCluster1RepairFill,
    float BuildingDamageCluster2X,
    float BuildingDamageCluster2Y,
    float BuildingDamageCluster2Z,
    float BuildingDamageCluster2Strength,
    float BuildingDamageCluster2Age,
    float BuildingDamageCluster2RepairFill,
    byte BuildingDamageLayerState,
    byte MountedWeaponFaultCode,
    byte MountedWeaponRepairStep,
    byte MountedWeaponRepairStepCount,
    byte MountedWeaponMaterialUnits,
    float Altitude,
    float OrbitalVelocity,
    float RemainingDeltaV,
    bool IsReloading,
    bool IsAlive,
    bool AttachToTerrain);
