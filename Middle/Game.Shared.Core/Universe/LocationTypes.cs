namespace Game.Shared.Core.Universe;

/// <summary>
/// 宇宙位置层级类型 — 从超星系团到空间站内部房间。
/// 每一层有独立的本地坐标系，通过 ParentLocationId 组成树状结构。
/// </summary>
public enum LocationType : byte
{
    /// <summary>宇宙 — 最顶层，通常只有一个。</summary>
    Universe = 0,

    /// <summary>星系 — 如银河系。</summary>
    Galaxy = 1,

    /// <summary>星区/区域 — 星系内的行政区域划分。</summary>
    Region = 2,

    /// <summary>星座 — 区域内的子分区（EVE 风格）。</summary>
    Constellation = 3,

    /// <summary>恒星系统/太阳系 — 核心层，拥有独立的本地坐标原点（以恒星为中心）。</summary>
    SolarSystem = 4,

    /// <summary>天体 — 行星、卫星、小行星带等。</summary>
    CelestialBody = 5,

    /// <summary>轨道区 — 行星轨道、拉格朗日点、小行星场等空间区域。</summary>
    OrbitalZone = 6,

    /// <summary>空间站/建筑 — 可停靠、可进入的结构。</summary>
    Station = 7,

    /// <summary>行星表面区域 — 地面的大型区域（城市、荒野等）。</summary>
    PlanetSurface = 8,

    /// <summary>副本/实例 — 空间站内部、飞船舱室、任务副本等独立地图。</summary>
    Instance = 9,

    /// <summary>跃迁点/虫洞 — 连接两个位置的特殊锚点。</summary>
    JumpPoint = 10,

    /// <summary>子位置 — 房间/甲板/格子等最小单元。</summary>
    SubLocation = 11
}

/// <summary>
/// 天体类型 — CelestialBody 的子分类。
/// </summary>
public enum CelestialBodyType : byte
{
    Star = 0,
    RockyPlanet = 1,
    GasGiant = 2,
    IcePlanet = 3,
    Moon = 4,
    AsteroidBelt = 5,
    Comet = 6,
    DwarfPlanet = 7,
    Nebula = 8,
    BlackHole = 9,
    Wormhole = 10,
    Pulsar = 11
}

/// <summary>
/// 星系恒星光谱类型 — 决定恒星颜色、温度和宜居带。
/// </summary>
public enum SpectralClass : byte
{
    O = 0, // 蓝色超巨星
    B = 1, // 蓝白色
    A = 2, // 白色
    F = 3, // 黄白色
    G = 4, // 黄色（类太阳）
    K = 5, // 橙色
    M = 6, // 红矮星
    L = 7, // 褐矮星
    T = 8, // 极冷褐矮星
    Neutron = 9,
    BlackHole = 10
}

/// <summary>
/// 环境危害类型。
/// </summary>
[Flags]
public enum EnvironmentHazards : int
{
    None = 0,
    Radiation = 1 << 0,
    CosmicRay = 1 << 1,
    GravityWell = 1 << 2,
    EmpStorm = 1 << 3,
    Nebula = 1 << 4,
    AsteroidField = 1 << 5,
    SolarFlare = 1 << 6,
    DarkMatter = 1 << 7,
    Sandstorm = 1 << 8,
    Blizzard = 1 << 9,
    Earthquake = 1 << 10,
    Flood = 1 << 11,
    VolcanicActivity = 1 << 12,
    ToxicAtmosphere = 1 << 13
}
