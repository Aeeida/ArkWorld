using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

/// <summary>
/// 高度场生成器 — 根据世界种子和群系参数程序化生成区块高度数据。
/// 纯计算，无 Godot 依赖，可在后台线程运行。
/// </summary>
public sealed class HeightfieldGenerator
{
    private readonly WorldSeed _heightSeed;
    private readonly BiomeSampler _biomeSampler;

    public HeightfieldGenerator(WorldSeed worldSeed, BiomeSampler biomeSampler)
    {
        _heightSeed = worldSeed.Derive("height");
        _biomeSampler = biomeSampler;
    }

    /// <summary>
    /// 生成一个区块的高度场数据。
    /// </summary>
    public HeightfieldChunk Generate(ChunkCoord coord, int resolution = WorldConstants.HeightmapResolution)
    {
        var chunk = new HeightfieldChunk(coord, resolution);
        float chunkSize = WorldConstants.ChunkSize;
        var origin = coord.ToWorldOrigin(chunkSize);
        float step = chunkSize / (resolution - 1);

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX = origin.X + x * step;
                float worldZ = origin.Z + z * step;

                // 采样群系
                var (primary, secondary, weight) = _biomeSampler.SampleBlended(worldX, worldZ);
                int idx = z * resolution + x;
                chunk.Biomes[idx] = primary;

                // 获取群系定义
                var primaryDef = BiomeRegistry.Get(primary);
                var secondaryDef = BiomeRegistry.Get(secondary);

                float h = GenerateHeightForBiome(worldX, worldZ, primaryDef);

                // 群系边界混合
                if (secondary != primary && secondaryDef != null)
                {
                    float h2 = GenerateHeightForBiome(worldX, worldZ, secondaryDef);
                    h = h * weight + h2 * (1f - weight);
                }

                chunk.Heights[idx] = h;
            }
        }

        return chunk;
    }

    /// <summary>
    /// 重新生成已有区块的高度数据（不创建新对象，不修改群系数据）。
    /// 用于地形修改前的高度重置 — 防止修改叠加导致高度偏离。
    /// </summary>
    public void RegenerateHeights(HeightfieldChunk chunk)
    {
        float chunkSize = WorldConstants.ChunkSize;
        var origin = chunk.Coord.ToWorldOrigin(chunkSize);
        float step = chunkSize / (chunk.Resolution - 1);

        for (int z = 0; z < chunk.Resolution; z++)
        {
            for (int x = 0; x < chunk.Resolution; x++)
            {
                float worldX = origin.X + x * step;
                float worldZ = origin.Z + z * step;

                var (primary, secondary, weight) = _biomeSampler.SampleBlended(worldX, worldZ);
                var primaryDef = BiomeRegistry.Get(primary);
                var secondaryDef = BiomeRegistry.Get(secondary);

                float h = GenerateHeightForBiome(worldX, worldZ, primaryDef);
                if (secondary != primary && secondaryDef != null)
                {
                    float h2 = GenerateHeightForBiome(worldX, worldZ, secondaryDef);
                    h = h * weight + h2 * (1f - weight);
                }

                chunk.Heights[z * chunk.Resolution + x] = h;
            }
        }
    }

    private float GenerateHeightForBiome(float worldX, float worldZ, BiomeDefinition? biome)
    {
        if (biome == null)
        {
            return SimplexNoise.SeededFBM(worldX, worldZ, _heightSeed) * WorldConstants.MaxTerrainHeight * 0.3f;
        }

        float freq = 0.005f * biome.FrequencyScale;
        float height;

        if (biome.UseRidgedNoise)
        {
            height = SimplexNoise.RidgedFBM(worldX, worldZ, _heightSeed,
                biome.Octaves, frequency: freq);
        }
        else
        {
            height = SimplexNoise.SeededFBM(worldX, worldZ, _heightSeed,
                biome.Octaves, frequency: freq);
            // 从 [-1,1] 映射到 [0,1]
            height = (height + 1f) * 0.5f;
        }

        return biome.BaseHeight + height * WorldConstants.MaxTerrainHeight * biome.HeightAmplitude;
    }

    /// <summary>
    /// 在未加载区块的情况下，程序化计算指定世界坐标的地形高度。
    /// 比 Generate 快得多（只算一个点），用于 SampleHeight 回退。
    /// </summary>
    public float SampleHeightAt(float worldX, float worldZ)
    {
        var (primary, secondary, weight) = _biomeSampler.SampleBlended(worldX, worldZ);
        var primaryDef = BiomeRegistry.Get(primary);
        var secondaryDef = BiomeRegistry.Get(secondary);

        float h = GenerateHeightForBiome(worldX, worldZ, primaryDef);

        if (secondary != primary && secondaryDef != null)
        {
            float h2 = GenerateHeightForBiome(worldX, worldZ, secondaryDef);
            h = h * weight + h2 * (1f - weight);
        }

        return h;
    }

    /// <summary>
    /// 应用修改日志到已生成的区块。
    /// </summary>
    public static void ApplyModifications(HeightfieldChunk chunk, ModificationLog log, float chunkSize)
    {
        var origin = chunk.Coord.ToWorldOrigin(chunkSize);
        float halfChunk = chunkSize * 0.5f;
        float centerX = origin.X + halfChunk;
        float centerZ = origin.Z + halfChunk;

        // 搜索范围稍大于区块对角线
        float searchRadius = chunkSize * 0.8f;
        float step = chunkSize / (chunk.Resolution - 1);

        foreach (var mod in log.GetModificationsInRange(centerX, centerZ, searchRadius + mod_max_radius))
        {
            for (int z = 0; z < chunk.Resolution; z++)
            {
                for (int x = 0; x < chunk.Resolution; x++)
                {
                    float wx = origin.X + x * step;
                    float wz = origin.Z + z * step;
                    float dx = wx - mod.PosX;
                    float dz = wz - mod.PosZ;
                    float dist = MathF.Sqrt(dx * dx + dz * dz);

                    if (dist > mod.Radius) continue;

                    float t = dist / mod.Radius; // [0..1] center→edge
                    float falloff = 1f - t;
                    falloff *= falloff; // 平滑衰减

                    int idx = z * chunk.Resolution + x;
                    switch (mod.ModType)
                    {
                        case TerrainModType.Dig:
                        case TerrainModType.Explosion:
                            chunk.Heights[idx] -= mod.Intensity * falloff;
                            break;
                        case TerrainModType.Fill:
                            chunk.Heights[idx] += mod.Intensity * falloff;
                            break;
                        case TerrainModType.Flatten:
                        {
                            // 平整地形到目标高度（mod.PosY）
                            // Intensity = 混合强度 [0..1]
                            // 使用 Hermite 平滑插值让边缘过渡自然
                            float blend = (1f - t * t) * mod.Intensity;
                            blend = blend * blend * (3f - 2f * blend); // smoothstep
                            chunk.Heights[idx] = chunk.Heights[idx] * (1f - blend) + mod.PosY * blend;
                            break;
                        }
                        case TerrainModType.Erosion:
                            chunk.Heights[idx] -= mod.Intensity * falloff * 0.3f;
                            break;
                    }
                    chunk.IsDirty = true;
                }
            }
        }
    }

    private const float mod_max_radius = 50f; // 最大搜索扩展
}
