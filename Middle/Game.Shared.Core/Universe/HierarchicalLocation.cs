using MessagePack;

namespace Game.Shared.Core.Universe;

/// <summary>
/// 分层坐标 — 在超大宇宙中精确描述一个位置。
///
/// 设计原理：
/// - 大尺度（星系间）使用 long 网格坐标避免 float 精度损失
/// - 本地尺度（系统内）使用 double3 相对坐标（以恒星为中心）
/// - 行星表面使用球面坐标（经纬度 + 海拔）
/// - 每层有独立坐标系，通过 LocationId 链接层级
/// </summary>
[MessagePackObject]
public sealed record HierarchicalCoordinate(
    /// <summary>所在位置节点 ID（Location 表主键）。</summary>
    [property: Key(0)] long LocationId,

    /// <summary>本地 X 坐标（米或千米，取决于层级 Scale）。</summary>
    [property: Key(1)] double LocalX,

    /// <summary>本地 Y 坐标。</summary>
    [property: Key(2)] double LocalY,

    /// <summary>本地 Z 坐标。</summary>
    [property: Key(3)] double LocalZ,

    /// <summary>所在星系 ID（快速查询，避免递归遍历父级）。</summary>
    [property: Key(4)] long SolarSystemId,

    /// <summary>实例 ID（0 = 非实例空间）。</summary>
    [property: Key(5)] long InstanceId);

/// <summary>
/// 实体位置快照 — 用于网络同步和数据库持久化。
/// </summary>
[MessagePackObject]
public sealed record EntityPositionSnapshot(
    [property: Key(0)] Guid EntityId,
    [property: Key(1)] HierarchicalCoordinate Coordinate,
    [property: Key(2)] float RotationX,
    [property: Key(3)] float RotationY,
    [property: Key(4)] float RotationZ,
    [property: Key(5)] float RotationW,
    [property: Key(6)] double VelocityX,
    [property: Key(7)] double VelocityY,
    [property: Key(8)] double VelocityZ,
    [property: Key(9)] DateTime Timestamp);

/// <summary>
/// 出生点定义 — 新角色的初始位置配置。
/// </summary>
[MessagePackObject]
public sealed record SpawnPointDefinition(
    /// <summary>出生点名称（如"新希望号空间站"）。</summary>
    [property: Key(0)] string Name,

    /// <summary>所属阵营（null = 中立/通用）。</summary>
    [property: Key(1)] string? Faction,

    /// <summary>出生位置。</summary>
    [property: Key(2)] HierarchicalCoordinate Coordinate,

    /// <summary>出生点安全等级 (0.0 = 危险, 1.0 = 安全)。</summary>
    [property: Key(3)] float SecurityLevel,

    /// <summary>出生点类型（如 "station", "planet_surface", "orbital"）。</summary>
    [property: Key(4)] string SpawnType,

    /// <summary>随机散布半径（米）— 避免所有新角色堆叠在同一点。</summary>
    [property: Key(5)] float ScatterRadius);

/// <summary>
/// 位置节点 DTO — 位置层级树中一个节点的完整描述。
/// </summary>
[MessagePackObject]
public sealed record LocationNodeDto(
    [property: Key(0)] long LocationId,
    [property: Key(1)] long? ParentLocationId,
    [property: Key(2)] LocationType LocationType,
    [property: Key(3)] string Name,
    [property: Key(4)] string Code,
    [property: Key(5)] double LocalX,
    [property: Key(6)] double LocalY,
    [property: Key(7)] double LocalZ,
    [property: Key(8)] double BoundsRadius,
    [property: Key(9)] float SecurityLevel,
    [property: Key(10)] long Seed,
    [property: Key(11)] string? BiomeId);

/// <summary>
/// 宇宙环境状态 DTO — 描述一个位置的动态环境条件。
/// </summary>
[MessagePackObject]
public sealed record CosmicEnvironmentDto(
    [property: Key(0)] long LocationId,
    [property: Key(1)] EnvironmentHazards ActiveHazards,
    [property: Key(2)] float RadiationLevel,
    [property: Key(3)] float GravityMultiplier,
    [property: Key(4)] float Temperature,
    [property: Key(5)] float AtmosphereDensity,
    [property: Key(6)] byte WeatherId,
    [property: Key(7)] float WeatherIntensity,
    [property: Key(8)] float WindX,
    [property: Key(9)] float WindY,
    [property: Key(10)] float WindZ);
