using Game.Shared.Core.Universe;
using GameServer.Domain.Core;

namespace GameServer.Domain.Entities;

/// <summary>
/// 位置节点 — 宇宙层级树中的一个节点。
/// 自引用外键 ParentLocationId 形成 Universe → Galaxy → Region → SolarSystem → Planet → ... 的树。
/// 
/// 坐标说明：
/// - LocalX/Y/Z 是相对于父节点原点的坐标（大尺度用光年/千米，本地用米）
/// - Scale 定义当前层级坐标的单位（米/单位），用于跨层级换算
/// - BoundsRadius 用于快速 AABB/球体过滤
/// </summary>
public class LocationNode : AggregateRoot<long>
{
    public static LocationNode Create(long id) { var e = new LocationNode(); e.Id = id; return e; }

    /// <summary>父位置 ID（null = 根节点/宇宙）。</summary>
    public long? ParentLocationId { get; set; }

    /// <summary>位置类型。</summary>
    public LocationType LocationType { get; set; }

    /// <summary>名称（如"银河系"、"阿尔法恒星系"、"新希望号空间站"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>代码/短名（如 "alpha_centauri", "sol_3"）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>层级路径（物化路径，如 "1/42/137/2001"）— 加速祖先查询。</summary>
    public string HierarchyPath { get; set; } = string.Empty;

    /// <summary>层级深度（0 = Universe）。</summary>
    public int Depth { get; set; }

    // ── 本地坐标（相对父节点原点）──
    public double LocalX { get; set; }
    public double LocalY { get; set; }
    public double LocalZ { get; set; }

    /// <summary>当前层级的单位尺度（米/单位）。</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>该位置的边界球半径（快速空间过滤）。</summary>
    public double BoundsRadius { get; set; }

    /// <summary>安全等级 (0.0 = 极度危险, 1.0 = 高安全)。</summary>
    public float SecurityLevel { get; set; } = 1.0f;

    // ── 生成参数 ──

    /// <summary>该位置的生成种子（确定性程序化生成）。</summary>
    public long Seed { get; set; }

    /// <summary>天体子类型（仅 CelestialBody 使用）。</summary>
    public CelestialBodyType? BodyType { get; set; }

    /// <summary>恒星光谱类型（仅 Star 使用）。</summary>
    public SpectralClass? SpectralClass { get; set; }

    /// <summary>生物群系 ID（行星表面/地面区域使用）。</summary>
    public string? BiomeId { get; set; }

    /// <summary>大气密度 (0 = 真空, 1 = 标准大气压)。</summary>
    public float AtmosphereDensity { get; set; }

    /// <summary>重力倍率 (1.0 = 地球标准)。</summary>
    public float GravityMultiplier { get; set; } = 1.0f;

    /// <summary>基础温度（开尔文）。</summary>
    public float BaseTemperature { get; set; } = 293f; // ~20°C

    /// <summary>地形种子（行星/地面区域使用）。</summary>
    public long? TerrainSeed { get; set; }

    /// <summary>扩展元数据 (JSONB) — 存储跃迁门目标、空间站停靠点等。</summary>
    public string? MetadataJson { get; set; }

    /// <summary>是否已由程序生成器填充。</summary>
    public bool IsGenerated { get; set; }

    /// <summary>是否对玩家可见/可发现。</summary>
    public bool IsDiscoverable { get; set; } = true;
}

/// <summary>
/// 地形修改记录 — 玩家/事件对地形的持久化编辑。
/// </summary>
public class TerrainModification : AggregateRoot<long>
{
    public static TerrainModification Create(long id) { var e = new TerrainModification(); e.Id = id; return e; }

    /// <summary>所属位置 ID（行星表面/地面区域）。</summary>
    public long LocationId { get; set; }

    /// <summary>修改中心 X 坐标（本地）。</summary>
    public float CenterX { get; set; }

    /// <summary>修改中心 Z 坐标（本地）。</summary>
    public float CenterZ { get; set; }

    /// <summary>影响半径 X。</summary>
    public float RadiusX { get; set; }

    /// <summary>影响半径 Z。</summary>
    public float RadiusZ { get; set; }

    /// <summary>目标高度。</summary>
    public float TargetHeight { get; set; }

    /// <summary>修改类型（如 "flatten", "raise", "dig", "blast"）。</summary>
    public string ModificationType { get; set; } = "flatten";

    /// <summary>区块/Zone 级增量同步键。</summary>
    public string? ChunkKey { get; set; }

    /// <summary>持久化增量序号。</summary>
    public long SequenceTick { get; set; }

    /// <summary>扩展元数据 (JSONB)。</summary>
    public string? MetadataJson { get; set; }

    /// <summary>上次写入时间。</summary>
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>执行修改的玩家 ID（null = 系统事件）。</summary>
    public Guid? ModifiedBy { get; set; }
}

/// <summary>
/// 世界环境状态 — 动态环境条件（天气/灾难/辐射等）持久化。
/// </summary>
public class WorldEnvironmentState : AggregateRoot<long>
{
    public static WorldEnvironmentState Create(long id) { var e = new WorldEnvironmentState(); e.Id = id; return e; }

    /// <summary>所属位置 ID。</summary>
    public long LocationId { get; set; }

    /// <summary>当前天气 ID。</summary>
    public byte WeatherId { get; set; }

    /// <summary>天气强度 [0, 1]。</summary>
    public float WeatherIntensity { get; set; }

    /// <summary>风向 X。</summary>
    public float WindX { get; set; }

    /// <summary>风向 Y。</summary>
    public float WindY { get; set; }

    /// <summary>风向 Z。</summary>
    public float WindZ { get; set; }

    /// <summary>雾密度。</summary>
    public float FogDensity { get; set; }

    /// <summary>当前温度（开尔文）。</summary>
    public float Temperature { get; set; } = 293f;

    /// <summary>当前辐射水平。</summary>
    public float RadiationLevel { get; set; }

    /// <summary>活跃的环境危害标志。</summary>
    public EnvironmentHazards ActiveHazards { get; set; }

    /// <summary>一天中的时间 (0~24)。</summary>
    public float TimeOfDay { get; set; } = 9.5f;

    /// <summary>时间流速倍率。</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>上次更新时间。</summary>
    public DateTime LastWeatherChange { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 世界物体生成记录 — NPC/怪物/任务点/建筑等散落物体的持久化位置。
/// </summary>
public class WorldSpawnEntry : AggregateRoot<long>
{
    public static WorldSpawnEntry Create(long id) { var e = new WorldSpawnEntry(); e.Id = id; return e; }

    /// <summary>所属位置 ID。</summary>
    public long LocationId { get; set; }

    /// <summary>生成物类型（如 "npc", "monster", "quest_giver", "town", "village", "meteor", "resource_node"）。</summary>
    public string SpawnType { get; set; } = string.Empty;

    /// <summary>生成物模板/定义 ID（如 "npc_blacksmith", "monster_dragon_lv5"）。</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>显示名称。</summary>
    public string? DisplayName { get; set; }

    /// <summary>本地 X 坐标。</summary>
    public double LocalX { get; set; }

    /// <summary>本地 Y 坐标。</summary>
    public double LocalY { get; set; }

    /// <summary>本地 Z 坐标。</summary>
    public double LocalZ { get; set; }

    /// <summary>朝向（弧度）。</summary>
    public float Rotation { get; set; }

    /// <summary>等级/难度。</summary>
    public int Level { get; set; } = 1;

    /// <summary>是否活跃（被击杀/完成后可变为 false，定时重生）。</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>重生间隔（秒，0 = 不重生）。</summary>
    public int RespawnSeconds { get; set; }

    /// <summary>扩展数据 (JSONB)。</summary>
    public string? MetadataJson { get; set; }
}
