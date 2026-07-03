namespace Ark.World.Core;

/// <summary>
/// 世界常量 — 全局配置，集中管理。
/// </summary>
public static class WorldConstants
{
    // ═══ 区块 ═══
    /// <summary>区块水平边长（米）。</summary>
    public const float ChunkSize = 64f;

    /// <summary>区块高度场网格分辨率（顶点/边）。</summary>
    public const int HeightmapResolution = 65; // 64+1 so edges align

    /// <summary>区块加载半径（区块单位）。</summary>
    public const int LoadRadius = 5;

    /// <summary>区块卸载半径（比加载半径大一圈防止频繁卸载加载）。</summary>
    public const int UnloadRadius = 7;

    /// <summary>高 LOD 半径。</summary>
    public const int HighLodRadius = 2;

    // ═══ 高度场 ═══
    /// <summary>基础地形最大高度（米）。</summary>
    public const float MaxTerrainHeight = 200f;

    /// <summary>地形基准抬升高度（米）— 保持为 0，通过 SkyDome 遮挡 Godot 原生地平线。
    /// 注意：不可使用大值，float32 在高 Y 坐标处精度不足会导致抖动。</summary>
    public const float TerrainBaseElevation = 0f;

    /// <summary>海平面高度。</summary>
    public const float SeaLevel = 0f;

    // ═══ 星球 ═══
    /// <summary>星球半径（米）。地形贴附在球面顶部。</summary>
    public const float PlanetRadius = 50000f;

    /// <summary>星球开始可见的最低海拔（米）。</summary>
    public const float PlanetVisibleMinAlt = 200f;

    /// <summary>星球完全可见的海拔（米）。</summary>
    public const float PlanetVisibleMaxAlt = 10000f;

    /// <summary>进入太空的海拔阈值（米）— 环境切换为太空风格。</summary>
    public const float SpaceAltitudeThreshold = 15000f;

    // ═══ 出生点 ═══
    /// <summary>玩家默认出生 X 坐标（地图中央偏北）。</summary>
    public const float SpawnX = 32f;

    /// <summary>玩家默认出生 Z 坐标（地图中央偏北）。</summary>
    public const float SpawnZ = -24f;

    /// <summary>出生点地形上方安全余量（米）。</summary>
    public const float SpawnHeightMargin = 2f;

    // ═══ 时间 ═══
    /// <summary>游戏内一天的真实秒数。</summary>
    public const float DayDurationSeconds = 600f; // 10 分钟 = 游戏一天

    /// <summary>日出时间（归一化 0~1，0.25 = 06:00）。</summary>
    public const float SunriseNormalized = 0.25f;

    /// <summary>日落时间。</summary>
    public const float SunsetNormalized = 0.75f;

    // ═══ 生态 ═══
    /// <summary>每区块最大植被实例数。</summary>
    public const int MaxVegetationPerChunk = 200;

    /// <summary>植被 LOD 距离（米）。</summary>
    public const float VegetationLodDistance = 80f;

    // ═══ 天气 ═══
    /// <summary>天气状态切换最小间隔（秒）。</summary>
    public const float WeatherMinInterval = 60f;

    /// <summary>天气渐变持续时间（秒）。</summary>
    public const float WeatherTransitionDuration = 15f;
}
