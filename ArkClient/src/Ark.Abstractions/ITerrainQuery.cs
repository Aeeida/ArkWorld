namespace Ark.Abstractions;

/// <summary>
/// 地形查询接口 — 供任何模块查询地形高度。
/// 由 WorldEnvironmentManager 实现，通过 GameServices 注入。
/// </summary>
public interface ITerrainQuery
{
    /// <summary>
    /// 查询指定世界坐标处的地形高度（Y 值）。
    /// </summary>
    float SampleHeight(float worldX, float worldZ);

    /// <summary>
    /// 将指定区域的地形高度平整到目标高度。
    /// 用于建筑放置时平整地面。
    /// </summary>
    void FlattenArea(float centerX, float centerZ, float halfSizeX, float halfSizeZ, float targetHeight);
}

/// <summary>
/// 世界环境初始化接口 — 允许从服务端种子重新初始化世界地形/天气。
/// 由 WorldEnvironmentManager 实现，供 GameServices 在网络模式下调用。
/// </summary>
public interface IWorldInitializer
{
    /// <summary>当前世界是否已初始化。</summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 使用服务端提供的地形种子重新初始化世界。
    /// 如果种子相同则跳过；如果不同则先关闭再重建。
    /// </summary>
    void ReinitializeWithSeed(long terrainSeed);

    /// <summary>应用服务端时间（0-24小时制）。</summary>
    void ApplyServerTimeOfDay(float timeOfDay);
}
