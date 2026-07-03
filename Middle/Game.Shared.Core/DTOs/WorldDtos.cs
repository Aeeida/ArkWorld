using Game.Shared.Core.Universe;
using MessagePack;

namespace Game.Shared.Core.DTOs;

// ══════════════════════════════════════════════════════════════════════
// World Environment (sent to client before world entry)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record WorldEnvironmentDto(
    [property: Key(0)] long TerrainSeed,
    [property: Key(1)] IReadOnlyList<TerrainModificationDto> TerrainModifications,
    [property: Key(2)] WeatherDto Weather,
    [property: Key(3)] float TimeOfDay,
    [property: Key(4)] float TimeScale,
    [property: Key(5)] string BiomeId,
    [property: Key(6)] string WorldId,
    [property: Key(7)] float GravityMultiplier)
{
    /// <summary>位置节点 ID（在分层宇宙中的位置）。</summary>
    [Key(8)] public long LocationId { get; init; }

    /// <summary>所在星系 ID。</summary>
    [Key(9)] public long SolarSystemId { get; init; }

    /// <summary>宇宙环境条件（辐射/危害等）。</summary>
    [Key(10)] public CosmicEnvironmentDto? CosmicEnvironment { get; init; }

    /// <summary>位置层级路径（如 "Arkiverse/银河系/星云星域/阿尔法-1系统/阿尔法-1 II 地表"）。</summary>
    [Key(11)] public string? LocationPath { get; init; }

    /// <summary>出生点本地坐标 X（新角色使用）。</summary>
    [Key(12)] public double SpawnX { get; init; }

    /// <summary>出生点本地坐标 Z（新角色使用）。</summary>
    [Key(13)] public double SpawnZ { get; init; }

    /// <summary>世界物体生成列表（NPC/怪物/资源/建筑等）。</summary>
    [Key(14)] public IReadOnlyList<WorldSpawnDto>? WorldSpawns { get; init; }
}

[MessagePackObject]
public sealed record TerrainModificationDto(
    [property: Key(0)] float X,
    [property: Key(1)] float Z,
    [property: Key(2)] float RadiusX,
    [property: Key(3)] float RadiusZ,
    [property: Key(4)] float TargetHeight,
    [property: Key(5)] string ModType,
    [property: Key(6)] string? ChunkKey = null,
    [property: Key(7)] long SequenceTick = 0,
    [property: Key(8)] string? MetadataJson = null);

[MessagePackObject]
public sealed record WeatherDto(
    [property: Key(0)] byte WeatherId,
    [property: Key(1)] float Intensity,
    [property: Key(2)] float WindX,
    [property: Key(3)] float WindY,
    [property: Key(4)] float WindZ,
    [property: Key(5)] float FogDensity,
    [property: Key(6)] float Temperature);

/// <summary>
/// 世界物体生成 DTO — 描述一个 NPC/怪物/资源/建筑/任务点等在地图上的位置。
/// </summary>
[MessagePackObject]
public sealed record WorldSpawnDto(
    [property: Key(0)] string SpawnType,
    [property: Key(1)] string TemplateId,
    [property: Key(2)] string? DisplayName,
    [property: Key(3)] double LocalX,
    [property: Key(4)] double LocalY,
    [property: Key(5)] double LocalZ,
    [property: Key(6)] float Rotation,
    [property: Key(7)] int Level,
    [property: Key(8)] bool IsActive,
    [property: Key(9)] bool AttachToTerrain = false,
    [property: Key(10)] string? MetadataJson = null);

// ══════════════════════════════════════════════════════════════════════
// Character Creation (full, with squad/party)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record CreateCharacterFullCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] string Name,
    [property: Key(4)] string Faction,
    [property: Key(5)] string CharacterClass,
    [property: Key(6)] int SquadMemberCount,
    [property: Key(7)] IReadOnlyList<SquadMemberCreationDto> SquadMembers,
    [property: Key(8)] AppearanceDto? Appearance,
    [property: Key(9)] string? StartingZone)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record SquadMemberCreationDto(
    [property: Key(0)] string Name,
    [property: Key(1)] string CharacterClass,
    [property: Key(2)] string Role,
    [property: Key(3)] AppearanceDto? Appearance);

[MessagePackObject]
public sealed record AppearanceDto(
    [property: Key(0)] int BodyType,
    [property: Key(1)] int FaceType,
    [property: Key(2)] int HairStyle,
    [property: Key(3)] int HairColor,
    [property: Key(4)] int SkinColor,
    [property: Key(5)] int EyeColor);

[MessagePackObject]
public sealed record CharacterCreateFullResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? CharacterId,
    [property: Key(2)] IReadOnlyList<Guid> SquadMemberIds,
    [property: Key(3)] string? ErrorMessage);

// ══════════════════════════════════════════════════════════════════════
// Character Selection
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record SelectCharacterCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid CharacterId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record CharacterListDto(
    [property: Key(0)] Guid AccountId,
    [property: Key(1)] IReadOnlyList<CharacterSlotDto> Characters,
    [property: Key(2)] int MaxSlots);

[MessagePackObject]
public sealed record CharacterSlotDto(
    [property: Key(0)] Guid CharacterId,
    [property: Key(1)] string Name,
    [property: Key(2)] int Level,
    [property: Key(3)] string Faction,
    [property: Key(4)] string CharacterClass,
    [property: Key(5)] string? LastZone,
    [property: Key(6)] DateTime LastPlayedAt,
    [property: Key(7)] AppearanceDto? Appearance,
    [property: Key(8)] int SquadMemberCount);

[MessagePackObject]
public sealed record SelectCharacterResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? CharacterId,
    [property: Key(2)] PlayerDto? Player,
    [property: Key(3)] string? ErrorMessage);

// ══════════════════════════════════════════════════════════════════════
// Party / Squad Info (runtime)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record PartyInfoDto(
    [property: Key(0)] Guid LeaderId,
    [property: Key(1)] IReadOnlyList<PartyMemberDto> Members,
    [property: Key(2)] int MaxSize);

[MessagePackObject]
public sealed record PartyMemberDto(
    [property: Key(0)] Guid MemberId,
    [property: Key(1)] string Name,
    [property: Key(2)] string CharacterClass,
    [property: Key(3)] int Level,
    [property: Key(4)] double Health,
    [property: Key(5)] double MaxHealth,
    [property: Key(6)] bool IsAlive,
    [property: Key(7)] string Role,
    [property: Key(8)] bool IsAI);

// ══════════════════════════════════════════════════════════════════════
// Nearby Entities (AOI query)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record NearbyEntitiesDto(
    [property: Key(0)] IReadOnlyList<NearbyEntityDto> Entities,
    [property: Key(1)] float QueryRadius,
    [property: Key(2)] string ZoneId);

[MessagePackObject]
public sealed record NearbyEntityDto(
    [property: Key(0)] Guid EntityId,
    [property: Key(1)] string Name,
    [property: Key(2)] byte EntityType,
    [property: Key(3)] double X,
    [property: Key(4)] double Y,
    [property: Key(5)] double Z,
    [property: Key(6)] double Health,
    [property: Key(7)] double MaxHealth,
    [property: Key(8)] bool IsHostile);

// ══════════════════════════════════════════════════════════════════════
// Vehicle (server-authoritative)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record VehicleActionCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid VehicleEntityId,
    [property: Key(4)] string Action,
    [property: Key(5)] int? SeatIndex)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record VehicleActionResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid VehicleEntityId,
    [property: Key(2)] string Action,
    [property: Key(3)] int? SeatIndex,
    [property: Key(4)] string? ErrorMessage);

[MessagePackObject]
public sealed record VehicleStateDto(
    [property: Key(0)] Guid VehicleEntityId,
    [property: Key(1)] string VehicleType,
    [property: Key(2)] IReadOnlyList<VehicleSeatDto> Seats,
    [property: Key(3)] double FuelPercent,
    [property: Key(4)] double HealthPercent);

[MessagePackObject]
public sealed record VehicleSeatDto(
    [property: Key(0)] int SeatIndex,
    [property: Key(1)] Guid? OccupantId,
    [property: Key(2)] string SeatType);

[MessagePackObject]
public sealed record VehicleControlCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid VehicleEntityId,
    [property: Key(4)] float Throttle,
    [property: Key(5)] float Steering,
    [property: Key(6)] float Brake,
    [property: Key(7)] byte ActionFlags)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// Space (server-authoritative)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record SpaceActionCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] string Action,
    [property: Key(4)] Guid? SpacecraftId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record SpaceActionResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string Action,
    [property: Key(2)] string? Phase,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record SpaceFlightStateDto(
    [property: Key(0)] Guid SpacecraftId,
    [property: Key(1)] string Phase,
    [property: Key(2)] float Altitude,
    [property: Key(3)] float OrbitalVelocity,
    [property: Key(4)] float RemainingDeltaV,
    [property: Key(5)] float FuelPercent);

[MessagePackObject]
public sealed record SpacecraftControlCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid SpacecraftId,
    [property: Key(4)] float ThrustX,
    [property: Key(5)] float ThrustY,
    [property: Key(6)] float ThrustZ,
    [property: Key(7)] float RotX,
    [property: Key(8)] float RotY,
    [property: Key(9)] float RotZ,
    [property: Key(10)] byte ActionFlags)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// Building (server-authoritative)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record PlaceBuildingCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] int BuildingTypeId,
    [property: Key(4)] double X,
    [property: Key(5)] double Y,
    [property: Key(6)] double Z,
    [property: Key(7)] float RotationY)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record PlaceBuildingResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] int? EntityId,
    [property: Key(2)] Guid? NetworkId,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record DestroyBuildingCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid BuildingEntityId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record DestroyBuildingResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? ErrorMessage);

[MessagePackObject]
public sealed record UpgradeBuildingCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid BuildingEntityId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record UpgradeBuildingResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] int? NewLevel,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record BuildingTypeInfoDto(
    [property: Key(0)] int TypeId,
    [property: Key(1)] string Name,
    [property: Key(2)] string Category,
    [property: Key(3)] int MaxLevel,
    [property: Key(4)] IReadOnlyList<ItemDto> BuildCost);

[MessagePackObject]
public sealed record BuildingListDto(
    [property: Key(0)] IReadOnlyList<BuildingTypeInfoDto> Types);

// ══════════════════════════════════════════════════════════════════════
// Combat — Weapon Fire / Reload / Projectile (server-authoritative)
// ══════════════════════════════════════════════════════════════════════

/// <summary>客户端请求开火。服务端验证弹药/冷却/视线后判定命中。</summary>
[MessagePackObject]
public sealed record FireWeaponCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] int WeaponDefId,
    [property: Key(4)] double OriginX,
    [property: Key(5)] double OriginY,
    [property: Key(6)] double OriginZ,
    [property: Key(7)] double DirX,
    [property: Key(8)] double DirY,
    [property: Key(9)] double DirZ)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>服务端开火判定结果 — 推送给射击者和被命中者。</summary>
[MessagePackObject]
public sealed record FireWeaponResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] int ShooterEntityId,
    [property: Key(2)] int? HitEntityId,
    [property: Key(3)] double HitX,
    [property: Key(4)] double HitY,
    [property: Key(5)] double HitZ,
    [property: Key(6)] float Damage,
    [property: Key(7)] bool IsCrit,
    [property: Key(8)] bool IsKill,
    [property: Key(9)] int AmmoRemaining,
    [property: Key(10)] string? ErrorMessage);

/// <summary>客户端请求换弹。</summary>
[MessagePackObject]
public sealed record ReloadWeaponCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] int WeaponDefId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>服务端换弹结果。</summary>
[MessagePackObject]
public sealed record ReloadWeaponResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] int CurrentMag,
    [property: Key(2)] int ReserveAmmo,
    [property: Key(3)] string? ErrorMessage);

/// <summary>客户端请求座位武器交互（排故/维护）。</summary>
[MessagePackObject]
public sealed record SeatWeaponInteractCommandDto(
    [property: Key(0)] System.Guid PlayerId,
    [property: Key(1)] System.Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] byte ActionKind)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>服务端座位武器交互结果。</summary>
[MessagePackObject]
public sealed record SeatWeaponInteractResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] float FaultRemaining,
    [property: Key(2)] float MaintenanceRemaining,
    [property: Key(3)] float MaintenanceLevel,
    [property: Key(4)] byte FaultCode,
    [property: Key(5)] byte RepairStep,
    [property: Key(6)] byte RepairStepCount,
    [property: Key(7)] byte MaterialUnits,
    [property: Key(8)] float SkillScalar,
    [property: Key(9)] string? ErrorMessage);

/// <summary>服务端广播的弹道/弹丸事件 — 所有附近玩家收到用于渲染。</summary>
[MessagePackObject]
public sealed record ProjectileEventDto(
    [property: Key(0)] int ShooterEntityId,
    [property: Key(1)] int WeaponDefId,
    [property: Key(2)] double OriginX,
    [property: Key(3)] double OriginY,
    [property: Key(4)] double OriginZ,
    [property: Key(5)] double DirX,
    [property: Key(6)] double DirY,
    [property: Key(7)] double DirZ,
    [property: Key(8)] double HitX,
    [property: Key(9)] double HitY,
    [property: Key(10)] double HitZ,
    [property: Key(11)] float TravelTime);

// ══════════════════════════════════════════════════════════════════════
// Vehicle Spawn (server-authoritative)
// ══════════════════════════════════════════════════════════════════════

/// <summary>客户端请求生成载具。</summary>
[MessagePackObject]
public sealed record SpawnVehicleCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] int VehicleDefId,
    [property: Key(4)] double SpawnX,
    [property: Key(5)] double SpawnY,
    [property: Key(6)] double SpawnZ,
    [property: Key(7)] Guid? FactoryBuildingId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>服务端生成载具结果。</summary>
[MessagePackObject]
public sealed record SpawnVehicleResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] int? VehicleEntityId,
    [property: Key(2)] Guid? NetworkId,
    [property: Key(3)] int VehicleDefId,
    [property: Key(4)] double SpawnX,
    [property: Key(5)] double SpawnY,
    [property: Key(6)] double SpawnZ,
    [property: Key(7)] string? ErrorMessage);

// ══════════════════════════════════════════════════════════════════════
// Rocket / Space Launch (server-authoritative)
// ══════════════════════════════════════════════════════════════════════

/// <summary>客户端请求组装火箭。</summary>
[MessagePackObject]
public sealed record AssembleRocketCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid LaunchPadEntityId,
    [property: Key(4)] string RocketConfigJson)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>服务端火箭组装结果。</summary>
[MessagePackObject]
public sealed record AssembleRocketResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? RocketEntityId,
    [property: Key(2)] string? ErrorMessage);

/// <summary>客户端请求发射火箭。</summary>
[MessagePackObject]
public sealed record LaunchRocketCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid RocketEntityId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>服务端发射结果。</summary>
[MessagePackObject]
public sealed record LaunchRocketResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? Phase,
    [property: Key(2)] string? ErrorMessage);

// ══════════════════════════════════════════════════════════════════════
// Entity Ownership & Whitelist (server-authoritative metadata)
// ══════════════════════════════════════════════════════════════════════

/// <summary>实体归属与权限信息 — 服务端推送给客户端做 UI/交互过滤。</summary>
[MessagePackObject]
public sealed record EntityOwnershipDto(
    [property: Key(0)] Guid EntityNetworkId,
    [property: Key(1)] Guid OwnerId,
    [property: Key(2)] Guid? GuildId,
    [property: Key(3)] byte FactionRelation,
    [property: Key(4)] byte AccessLevel,
    [property: Key(5)] IReadOnlyList<Guid>? WhitelistPlayerIds,
    [property: Key(6)] IReadOnlyList<Guid>? WhitelistGuildIds,
    [property: Key(7)] uint ColorPacked);

/// <summary>实体完整状态帧 — 服务端快照广播（含位置/速度/预测）。</summary>
[MessagePackObject]
public sealed record EntityStateFrameDto(
    [property: Key(0)] Guid EntityNetworkId,
    [property: Key(1)] byte EntityType,
    [property: Key(2)] double PosX,
    [property: Key(3)] double PosY,
    [property: Key(4)] double PosZ,
    [property: Key(5)] float RotX,
    [property: Key(6)] float RotY,
    [property: Key(7)] float RotZ,
    [property: Key(8)] float RotW,
    [property: Key(9)] float VelX,
    [property: Key(10)] float VelY,
    [property: Key(11)] float VelZ,
    [property: Key(12)] float Speed,
    [property: Key(13)] float Health,
    [property: Key(14)] float MaxHealth,
    [property: Key(15)] uint ColorPacked,
    [property: Key(16)] Guid OwnerId,
    [property: Key(17)] byte FactionRelation);
