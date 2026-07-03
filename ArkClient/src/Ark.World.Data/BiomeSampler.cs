using Ark.World.Core;

namespace Ark.World.Data;

/// <summary>
/// 群系采样器 — 根据世界坐标和种子确定性地选择群系。
/// 使用 Voronoi + Noise 实现自然的群系边界。
/// </summary>
public sealed class BiomeSampler
{
    private readonly WorldSeed _biomeSeed;
    private readonly float _cellSize;

    /// <summary>
    /// 群系覆盖 — 设置后所有坐标都返回此群系（用于 F7~F12 场景切换）。
    /// null = 使用噪声正常采样。
    /// </summary>
    public BiomeId? Override { get; set; }

    /// <summary>
    /// 创建群系采样器。
    /// </summary>
    /// <param name="seed">世界种子（将从中派生群系子种子）。</param>
    /// <param name="cellSize">群系单元格大小（米），影响群系区域的平均尺寸。</param>
    public BiomeSampler(WorldSeed seed, float cellSize = 256f)
    {
        _biomeSeed = seed.Derive("biome");
        _cellSize = cellSize;
    }

    /// <summary>
    /// 采样指定世界坐标处的群系。
    /// </summary>
    public BiomeId Sample(float worldX, float worldZ)
    {
        // 群系覆盖模式（场景切换）
        if (Override is BiomeId ov) return ov;

        // 使用多层噪声决定群系
        float temperature = SimplexNoise.SeededFBM(worldX, worldZ,
            _biomeSeed.Derive("temp"), octaves: 3, frequency: 0.001f);
        float moisture = SimplexNoise.SeededFBM(worldX, worldZ,
            _biomeSeed.Derive("moist"), octaves: 3, frequency: 0.0012f);
        float altitude = SimplexNoise.SeededFBM(worldX, worldZ,
            _biomeSeed.Derive("alt"), octaves: 4, frequency: 0.0008f);

        return ClassifyBiome(temperature, moisture, altitude);
    }

    /// <summary>
    /// 采样指定世界坐标处的群系权重（用于群系边界平滑过渡）。
    /// 返回 (主群系, 次群系, 主群系权重 [0,1])。
    /// </summary>
    public (BiomeId primary, BiomeId secondary, float weight) SampleBlended(float worldX, float worldZ)
    {
        // 群系覆盖模式 → 无混合
        if (Override is BiomeId ov) return (ov, ov, 1f);

        var primary = Sample(worldX, worldZ);

        // 在边界附近采样偏移点
        float offset = _cellSize * 0.3f;
        var north = Sample(worldX, worldZ - offset);
        var east = Sample(worldX + offset, worldZ);

        BiomeId secondary = primary;
        if (north != primary) secondary = north;
        else if (east != primary) secondary = east;

        if (secondary == primary)
            return (primary, primary, 1f);

        // 计算混合权重
        float edgeNoise = (SimplexNoise.Noise2D(worldX * 0.02f, worldZ * 0.02f) + 1f) * 0.5f;
        float weight = MathF.Max(0.5f, edgeNoise);
        return (primary, secondary, weight);
    }

    private static BiomeId ClassifyBiome(float temperature, float moisture, float altitude)
    {
        // temperature: [-1,1], moisture: [-1,1], altitude: [-1,1]
        if (altitude < -0.4f) return BiomeId.Ocean;
        if (altitude > 0.5f) return BiomeId.Mountain;

        if (temperature < -0.3f)
            return moisture > 0 ? BiomeId.Tundra : BiomeId.Tundra;

        if (temperature > 0.4f)
        {
            if (moisture < -0.2f) return BiomeId.Desert;
            if (moisture > 0.3f) return BiomeId.Swamp;
            return BiomeId.Plains;
        }

        // 温带
        if (moisture > 0.2f) return BiomeId.Forest;
        if (moisture < -0.3f) return BiomeId.Desert;
        return BiomeId.Plains;
    }
}
