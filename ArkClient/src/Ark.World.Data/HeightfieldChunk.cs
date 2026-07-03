using Ark.World.Core;

namespace Ark.World.Data;

/// <summary>
/// 高度场区块数据 — 存储单个区块的高度值网格 + 法线 + 群系权重。
/// 不包含任何 Godot 渲染逻辑（纯数据）。
/// </summary>
public sealed class HeightfieldChunk
{
    /// <summary>区块坐标。</summary>
    public ChunkCoord Coord { get; }

    /// <summary>高度值数组 [Resolution × Resolution]。</summary>
    public float[] Heights { get; }

    /// <summary>每顶点主群系 ID。</summary>
    public BiomeId[] Biomes { get; }

    /// <summary>网格分辨率（每边顶点数）。</summary>
    public int Resolution { get; }

    /// <summary>是否被玩家/事件修改过（需要持久化）。</summary>
    public bool IsDirty { get; set; }

    /// <summary>LOD 级别（0 = 最高精度）。</summary>
    public int LodLevel { get; set; }

    public HeightfieldChunk(ChunkCoord coord, int resolution = WorldConstants.HeightmapResolution)
    {
        Coord = coord;
        Resolution = resolution;
        Heights = new float[resolution * resolution];
        Biomes = new BiomeId[resolution * resolution];
    }

    /// <summary>获取指定网格位置的高度。</summary>
    public float GetHeight(int gridX, int gridZ)
    {
        int idx = gridZ * Resolution + gridX;
        return idx >= 0 && idx < Heights.Length ? Heights[idx] : 0;
    }

    /// <summary>设置指定网格位置的高度。</summary>
    public void SetHeight(int gridX, int gridZ, float height)
    {
        int idx = gridZ * Resolution + gridX;
        if (idx >= 0 && idx < Heights.Length)
        {
            Heights[idx] = height;
            IsDirty = true;
        }
    }

    /// <summary>插值获取精确世界坐标处的高度。</summary>
    public float SampleHeight(float localX, float localZ, float chunkSize)
    {
        float gridStepX = chunkSize / (Resolution - 1);
        float gridStepZ = chunkSize / (Resolution - 1);

        float gx = localX / gridStepX;
        float gz = localZ / gridStepZ;

        int x0 = Math.Clamp((int)MathF.Floor(gx), 0, Resolution - 2);
        int z0 = Math.Clamp((int)MathF.Floor(gz), 0, Resolution - 2);
        float fx = gx - x0;
        float fz = gz - z0;

        float h00 = GetHeight(x0, z0);
        float h10 = GetHeight(x0 + 1, z0);
        float h01 = GetHeight(x0, z0 + 1);
        float h11 = GetHeight(x0 + 1, z0 + 1);

        // 双线性插值
        float h0 = h00 + (h10 - h00) * fx;
        float h1 = h01 + (h11 - h01) * fx;
        return h0 + (h1 - h0) * fz;
    }
}
