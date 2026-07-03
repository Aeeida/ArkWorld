using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

/// <summary>
/// 地形网格构建器 — 从 HeightfieldChunk 生成 Godot ArrayMesh + 碰撞形状。
/// 支持 LOD：通过 step 参数跳过顶点降低面数。
/// </summary>
public static class TerrainMeshBuilder
{
    /// <summary>
    /// 从高度场区块构建渲染网格。
    /// </summary>
    /// <param name="chunk">高度场数据。</param>
    /// <param name="chunkSize">区块世界尺寸。</param>
    /// <param name="lodStep">LOD 步进（1=全精度, 2=隔一个, 4=隔三个）。</param>
    public static ArrayMesh BuildMesh(HeightfieldChunk chunk, float chunkSize, int lodStep = 1)
    {
        int res = chunk.Resolution;
        int step = Math.Max(1, lodStep);
        int vertPerRow = (res - 1) / step + 1;
        int vertCount = vertPerRow * vertPerRow;
        int triCount = (vertPerRow - 1) * (vertPerRow - 1) * 2;

        var vertices = new Vector3[vertCount];
        var normals  = new Vector3[vertCount];
        var colors   = new Color[vertCount];
        var uvs      = new Vector2[vertCount];
        var indices  = new int[triCount * 3];

        float gridStep = chunkSize / (res - 1);

        // ── 顶点 ──
        int vi = 0;
        for (int z = 0; z < res; z += step)
        {
            for (int x = 0; x < res; x += step)
            {
                float localX = x * gridStep;
                float localZ = z * gridStep;
                float h = chunk.GetHeight(x, z);

                vertices[vi] = new Vector3(localX, h, localZ);
                uvs[vi] = new Vector2((float)x / (res - 1), (float)z / (res - 1));

                // 顶点色 = 群系地表颜色（alpha 强制 1.0）
                int idx = z * res + x;
                var biome = BiomeRegistry.Get(chunk.Biomes[idx]);
                var baseColor = biome?.SurfaceColor ?? new Color(0.35f, 0.45f, 0.3f);
                colors[vi] = new Color(baseColor.R, baseColor.G, baseColor.B, 1f);

                vi++;
            }
        }

        // ── 法线（中心差分）──
        for (int z = 0; z < vertPerRow; z++)
        {
            for (int x = 0; x < vertPerRow; x++)
            {
                int c  = z * vertPerRow + x;
                int l  = x > 0 ? c - 1 : c;
                int r  = x < vertPerRow - 1 ? c + 1 : c;
                int d  = z > 0 ? c - vertPerRow : c;
                int u  = z < vertPerRow - 1 ? c + vertPerRow : c;

                float dhdx = vertices[r].Y - vertices[l].Y;
                float dhdz = vertices[u].Y - vertices[d].Y;
                float scale = gridStep * step;

                normals[c] = new Vector3(-dhdx / scale, 2f, -dhdz / scale).Normalized();

                // 陡坡着色
                float slope = MathF.Acos(MathF.Max(0, normals[c].Y)) * (180f / MathF.PI);
                int srcIdx = (z * step) * res + (x * step);
                var biomeDef = BiomeRegistry.Get(chunk.Biomes[Math.Min(srcIdx, chunk.Biomes.Length - 1)]);
                if (biomeDef != null && slope > biomeDef.SlopeThresholdDeg)
                {
                    float blend = MathF.Min(1f, (slope - biomeDef.SlopeThresholdDeg) / 15f);
                    colors[c] = colors[c].Lerp(biomeDef.SlopeColor, blend);
                }
            }
        }

        // ── 三角形索引 ──
        int ii = 0;
        for (int z = 0; z < vertPerRow - 1; z++)
        {
            for (int x = 0; x < vertPerRow - 1; x++)
            {
                int tl = z * vertPerRow + x;
                int tr = tl + 1;
                int bl = tl + vertPerRow;
                int br = bl + 1;

                indices[ii++] = tl;
                indices[ii++] = bl;
                indices[ii++] = tr;

                indices[ii++] = tr;
                indices[ii++] = bl;
                indices[ii++] = br;
            }
        }

        // ── 构建 ArrayMesh ──
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color]  = colors;
        arrays[(int)Mesh.ArrayType.TexUV]  = uvs;
        arrays[(int)Mesh.ArrayType.Index]  = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // 使用共享 StandardMaterial3D（三面投影纹理 + 顶点色 + 法线贴图）
        // 强制不透明，绝对不会出现透明地形
        mesh.SurfaceSetMaterial(0, TerrainMaterialFactory.GetShared());

        return mesh;
    }

    /// <summary>
    /// 从高度场构建碰撞 HeightMapShape3D。
    /// </summary>
    public static HeightMapShape3D BuildCollision(HeightfieldChunk chunk, float chunkSize)
    {
        var shape = new HeightMapShape3D();
        int res = chunk.Resolution;

        // HeightMapShape3D 需要正方形且边长 = mapWidth
        shape.MapWidth = res;
        shape.MapDepth = res;

        var mapData = new float[res * res];
        Array.Copy(chunk.Heights, mapData, mapData.Length);
        shape.MapData = mapData;

        return shape;
    }
}
