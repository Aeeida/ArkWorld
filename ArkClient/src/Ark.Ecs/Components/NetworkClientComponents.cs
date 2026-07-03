using System.Runtime.InteropServices;
using Ark.Analyzers.Attributes;
using Friflo.Engine.ECS;

namespace Ark.Ecs.Components;

public enum LocalControlMode : byte
{
    Character = 0,
    Vehicle = 1,
    Spacecraft = 2,
}

public enum LocalControlSource : byte
{
    CharacterDirect = 0,
    VehicleSeat = 1,
    SpacecraftRemote = 2,
}

/// <summary>
/// 远端实体运行时状态 —— 由客户端网络缓存层写入，供 Godot 表现层消费。
/// </summary>
public struct RemoteEntityState : IComponent
{
    public System.Guid NetworkId;
    public int SnapshotEntityId;
    public int TypeId;
    public byte EntityType;
    public byte IsAlive;
    public byte AttachToTerrain;
    public byte IsLocalPlayer;
}

/// <summary>
/// 最近一次快照元数据 —— 保留上一帧位置供插值/遥测/VFX 使用。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RemoteSnapshotState : IComponent
{
    public float PreviousX;
    public float PreviousY;
    public float PreviousZ;
    public float LastServerTime;
    public ulong LastSnapshotTick;
    public ulong _pad0;
}

/// <summary>
/// 远端实体归属/权限状态 —— 由网络事件写入 ECS，供交互/UI/表现层读取。
/// </summary>
public struct RemoteOwnershipState : IComponent
{
    public System.Guid OwnerId;
    [EcsComputedField]
    public System.Guid GuildId;
    [EcsComputedField]
    public byte HasGuildId;
    public byte FactionRelation;
    public byte AccessLevel;
    public byte _pad0;
    public uint ColorPacked;
}

/// <summary>
/// 远端战斗运行时状态 —— 由远程服务/事件投影到 ECS，供 HUD 与表现层消费。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RemoteCombatState : IComponent
{
    public int WeaponDefId;
    [EcsComputedField]
    public int AimTargetId;
    public int CurrentMag;
    public int MagCapacity;
    public int ReserveAmmo;
    public int MaxReserve;
    public byte WeaponCategory;
    [EcsComputedField]
    public byte HasWeapon;
    public byte IsReloading;
    public byte _pad0;
}

/// <summary>
/// 远端建造运行时状态 —— 由客户端建造服务投影到 ECS。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RemoteBuildState : IComponent
{
    public int SelectedBuildingTypeId;
    public byte IsBuildMode;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

[StructLayout(LayoutKind.Sequential)]
[ControlAuthoritySource]
public struct LocalControlState : IComponent
{
    public int ControlledSnapshotEntityId;
    public System.Guid ActiveNetworkId;
    public float SpacecraftThrottle;
    public byte Mode;
    public byte ControlSource;
    public byte SeatIndex;
    public byte SeatType;
    public byte ExternalControlLocked;
    public byte BuildMode;
    public byte MouseCaptured;
    public byte HoverMode;
    public byte EngineCutoff;
    public byte InVehicle;
    public byte _pad0;
    public byte _pad1;
}

[StructLayout(LayoutKind.Sequential)]
public struct RemoteInventoryState : IComponent
{
    public int SlotCount;
    public int OccupiedSlotCount;
    [EcsComputedField]
    public int TotalItemCount;
    [EcsComputedField]
    public int DistinctItemCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct RemoteQuestState : IComponent
{
    public int ActiveQuestCount;
    public int AvailableQuestCount;
    public int CompletedQuestCount;
    public int ObjectiveCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct RemoteWorldServiceState : IComponent
{
    public long TerrainSeed;
    public long LocationId;
    public long SolarSystemId;
    public float TimeOfDay;
    public float TimeScale;
    public float WeatherIntensity;
    public float NearbyQueryRadius;
    public int TerrainModificationCount;
    public int PartyMemberCount;
    public int NearbyEntityCount;
    public byte WeatherId;
    public byte HasWorldEnvironment;
    public byte HasWeather;
    public byte HasPartyInfo;
    public byte HasNearbyEntities;
}

[StructLayout(LayoutKind.Sequential)]
[ControlAuthoritySource]
public struct RemoteRocketControlState : IComponent
{
    public int SnapshotSpacecraftEntityId;
    public System.Guid ActiveRocketNetworkId;
    public byte HasActiveRocket;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

/// <summary>
/// 远端载具乘员归属状态 —— 由座位权威事件与快照投影到乘员实体。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[ControlAuthoritySource]
public struct RemoteVehicleOccupantState : IComponent
{
    [ControlAuthorityField("_vehicleSeatUpdates")]
    public int SnapshotVehicleEntityId;
    [ControlAuthorityField("_vehicleSeatUpdates")]
    public int CurrentSeatIndex;
    [EcsComputedField]
    public byte CurrentSeatType;
    public byte HasMountedWeapon;
    public byte _pad0;
    public byte _pad1;
}

/// <summary>
/// 远端载具运行遥测状态 —— 由载具快照与服务端状态投影到载具实体。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RemoteVehicleRuntimeState : IComponent
{
    public float FuelPercent;
    [EcsComputedField]
    public float HealthPercent;
    public ushort SeatCount;
    public ushort OccupiedSeatCount;
}

/// <summary>
/// 远端太空飞行运行遥测状态 —— 由远程太空服务投影到飞行器实体。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RemoteSpacecraftState : IComponent
{
    public float Altitude;
    public float OrbitalVelocity;
    public float RemainingDeltaV;
    [EcsComputedField]
    public float FuelPercent;
    public byte FlightPhase;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

/// <summary>
/// 远端表现反馈状态 —— 由快照健康变化和 ECS 视觉事件驱动，供角色/载具表现层读取。
/// 允许 Ark.Bridge.* 写入：表现反馈本身由 Bridge 视觉系统累积。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[BridgeWritableComponent]
public struct RemotePresentationFeedbackState : IComponent
{
    public float RecoilTimer;
    public float RecoilStrength;
    public float HitReactionTimer;
    public float HitReactionStrength;
    public float LastKnownHealth;
    public float HitDirX;
    public float HitDirY;
    public float HitDirZ;
    public byte HitZone;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

/// <summary>
/// 建筑持久战损状态 —— 由快照投影到 ECS，供建筑局部战损表现读取。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BuildingDamageState : IComponent
{
    public byte FrontDamage;
    public byte BackDamage;
    public byte RightDamage;
    public byte LeftDamage;
}

/// <summary>
/// 建筑持久命中簇状态 —— 由快照投影到 ECS，供裂纹贴花与网格破损实例读取。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BuildingDamageInstanceState : IComponent
{
    public float Cluster0X;
    public float Cluster0Y;
    public float Cluster0Z;
    public float Cluster0Strength;
    public float Cluster0Age;
    public float Cluster0RepairFill;
    public float Cluster1X;
    public float Cluster1Y;
    public float Cluster1Z;
    public float Cluster1Strength;
    public float Cluster1Age;
    public float Cluster1RepairFill;
    public float Cluster2X;
    public float Cluster2Y;
    public float Cluster2Z;
    public float Cluster2Strength;
    public float Cluster2Age;
    public float Cluster2RepairFill;
    public uint PackedLayerState;
}

/// <summary>
/// 挂载武器运行时状态 —— 由快照投影到 ECS，供多炮塔视觉和遥测读取。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MountedWeaponRuntimeState : IComponent
{
    public float Heat;
    public float FireCycleRemaining;
    public float FireCycleNormalized;
    public float ReloadRemaining;
    public float ReloadNormalized;
    public float FaultRemaining;
    public float MaintenanceRemaining;
    public float MaintenanceLevel;
    public float OperationProgress;
    public float SkillScalar;
    public byte IsOverheated;
    public byte IsReloading;
    public byte FaultCode;
    public byte IsMaintaining;
    public byte RepairStep;
    public byte RepairStepCount;
    public byte MaterialUnits;
    public byte _pad0;
}

/// <summary>
/// 远端角色动画状态 —— 由 ECS 快照/反馈状态归纳，供分段角色表现层驱动。
/// 允许 Ark.Bridge.* 写入：动画状态由 Bridge 玩家表现层从快照归纳后回写。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[BridgeWritableComponent]
public struct RemoteAnimationState : IComponent
{
    public float StateTime;
    public float Blend;
    public float AimBlend;
    public float SeatBlend;
    public uint PackedGraphState;
    public uint PackedBlendState;
    public int ResourceFragmentId;
    public int NetworkBudgetBytes;
    public int CacheHits;
    public int CacheMisses;
    public byte State;
    public byte LocomotionState;
    public byte TransitionState;
    public byte StreamingState;
    public byte _pad0;
}

/// <summary>
/// 建筑命中方向反馈 —— 由 ECS 命中事件投影，供建筑战损分区视觉读取。
/// 允许 Ark.Bridge.* 写入：方向反馈由 BuildingVisualManager 累积后回写。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[BridgeWritableComponent]
public struct BuildingDamageFeedbackState : IComponent
{
    public float HitDirX;
    public float HitDirY;
    public float HitDirZ;
    public float PulseTimer;
    public float Strength;
}

/// <summary>
/// 网络玩家输入命令 —— 由控制器写入，统一由 ECS 分发器上传到网络层。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetworkPlayerInputCommand : IComponent
{
    public float MoveDirX;
    public float MoveDirY;
    public float MoveDirZ;
    public float AimDirX;
    public float AimDirY;
    public float AimDirZ;
    public float Timestamp;
    public ulong Sequence;
    public byte ActionFlags;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

/// <summary>
/// 网络武器开火命令 —— 由控制器写入，统一由 ECS 分发器上传。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetworkWeaponFireCommand : IComponent
{
    public int WeaponDefId;
    public float OriginX;
    public float OriginY;
    public float OriginZ;
    public float DirX;
    public float DirY;
    public float DirZ;
    public ulong Sequence;
}

/// <summary>
/// 网络载具输入命令 —— 由载具实体承载，保持载具为移动权威。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetworkVehicleInputCommand : IComponent
{
    public int SnapshotVehicleEntityId;
    public float Throttle;
    public float Steering;
    public float Brake;
    public float TurretYaw;
    public float TurretPitch;
    public ulong Sequence;
    public byte ActionFlags;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

/// <summary>
/// 网络太空飞行输入命令 —— 由 ECS 分发器按固定节流上传。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetworkSpacecraftInputCommand : IComponent
{
    public int SnapshotSpacecraftEntityId;
    public float ThrustX;
    public float ThrustY;
    public float ThrustZ;
    public float RotationX;
    public float RotationY;
    public float RotationZ;
    public ulong Sequence;
    public byte ActionFlags;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

public enum NetworkVehicleActionKind : byte
{
    Enter = 0,
    Exit = 1,
    SwitchSeat = 2,
}

/// <summary>
/// 离散网络请求 —— 由控制器/UI 产生命令实体，再由分发器消费并销毁。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetworkReloadRequest : IComponent
{
    public int EntityId;
}

public enum NetworkSeatWeaponActionKind : byte
{
    ClearFault = 0,
    Maintain = 1,
}

[StructLayout(LayoutKind.Sequential)]
public struct NetworkSeatWeaponRequest : IComponent
{
    public int EntityId;
    public byte ActionKind;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NetworkVehicleActionRequest : IComponent
{
    public int SnapshotVehicleEntityId;
    public int SeatIndex;
    public byte ActionKind;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NetworkBuildPlacementRequest : IComponent
{
    public int BuildingTypeId;
    public float PositionX;
    public float PositionY;
    public float PositionZ;
    public float RotationY;
}

[StructLayout(LayoutKind.Sequential)]
public struct NetworkVehicleSpawnRequest : IComponent
{
    public int VehicleDefId;
    public float SpawnX;
    public float SpawnY;
    public float SpawnZ;
}

[StructLayout(LayoutKind.Sequential)]
public struct NetworkRocketAssemblyRequest : IComponent
{
    public System.Guid LaunchPadNetworkId;
    public string RocketConfigJson;
}

[StructLayout(LayoutKind.Sequential)]
public struct NetworkRocketLaunchRequest : IComponent
{
    public System.Guid RocketNetworkId;
}

/// <summary>
/// 开火视觉事件 —— 先进入 ECS，再由 `WeaponVisualSystem` 消费。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct WeaponFireVisualEvent : IComponent
{
    public int ShooterEntityId;
    public int WeaponDefId;
    public float OriginX;
    public float OriginY;
    public float OriginZ;
    public float DirX;
    public float DirY;
    public float DirZ;
    public byte HasExplicitTrajectory;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

/// <summary>
/// 命中视觉事件 —— 由网络层写入 ECS，再由表现层消费。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ProjectileHitVisualEvent : IComponent
{
    public float X;
    public float Y;
    public float Z;
    public float _pad0;
}

/// <summary>
/// 爆炸视觉事件 —— 由网络层写入 ECS，再由表现层消费。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ExplosionVisualEvent : IComponent
{
    public float X;
    public float Y;
    public float Z;
    public float Radius;
}

[StructLayout(LayoutKind.Sequential)]
public struct RemoteBuildingPlacementResultEvent : IComponent
{
    public int EntityId;
    public System.Guid NetworkId;
    public byte Success;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

[StructLayout(LayoutKind.Sequential)]
public struct RemoteVehicleSpawnResultEvent : IComponent
{
    public int VehicleEntityId;
    public int VehicleDefId;
    public System.Guid NetworkId;
    public byte Success;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

[StructLayout(LayoutKind.Sequential)]
public struct RemoteRocketAssemblyResultEvent : IComponent
{
    public System.Guid RocketEntityId;
    public byte Success;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

[StructLayout(LayoutKind.Sequential)]
public struct RemoteRocketLaunchResultEvent : IComponent
{
    public byte Success;
    public byte HasPhase;
    public byte FlightPhase;
    public byte _pad0;
}
