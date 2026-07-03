using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

/// <summary>
/// 远景地形平面 — 在实际区块加载范围之外渲染一个超大低精度地形网格，
/// 填满视野到地平线，充当我们自己的地平线（隐藏 Godot 原生地平线）。
///
/// 工作原理：
///   1. 以摄像头 XZ 位置为中心生成一张巨型网格（覆盖半径 ~12800m）
///   2. 网格顶点使用 HeightfieldGenerator.SampleHeightAt 计算真实高度
///   3. 中心开洞（实际区块已覆盖的区域不重复渲染）
///   4. 边缘高度渐变平滑到基准高度（自然地平线过渡）
///   5. 每次摄像头移动超过阈值时重建
///   6. 使用共享地形材质 + 超低顶点密度
///   7. 始终可见（地面、空中、太空都显示）
///
/// 性能预算：
///   • 120×120 = 14,400 顶点，~28,000 三角形（极低）
///   • 单一 MeshInstance3D，无碰撞体、无阴影
///   • 只在摄像头移动时重建（不是每帧）
/// </summary>
public sealed class FarTerrainPlane
{
    private readonly HeightfieldGenerator _generator;
    private readonly BiomeSampler _biomeSampler;
    private readonly Node3D _root;
    private MeshInstance3D? _meshInstance;

    /// <summary>远景覆盖半径（米）— 覆盖到地平线。</summary>
    private const float CoverRadius = 12800f;

    /// <summary>网格分辨率（每轴顶点数）。</summary>
    private const int GridRes = 120;

    /// <summary>中心空洞半径（米），略大于实际区块覆盖范围以避免Z-fighting。</summary>
    private const float HoleRadius = 700f;

    /// <summary>边缘淡出开始半径（从此距离开始高度渐变到基准）。</summary>
    private const float FadeStartRadius = 10000f;

    /// <summary>重建阈值 — 摄像头移动超过此距离后重建。</summary>
    private const float RebuildThreshold = 256f;

    private float _lastX = float.NaN;
    private float _lastZ = float.NaN;

    public Node3D SceneRoot => _root;

    public FarTerrainPlane(HeightfieldGenerator generator, BiomeSampler biomeSampler)
    {
        _generator = generator;
        _biomeSampler = biomeSampler;
        _root = new Node3D { Name = "FarTerrain" };
    }

    /// <summary>
    /// 根据摄像头位置更新远景网格。
    /// 在高空/太空时自动隐藏（星球球体接管远景）。
    /// </summary>
    public void Update(float cameraX, float cameraZ, float cameraAltitude)
    {
        // 高空时完全隐藏远景平面（让星球球体可见）
        if (cameraAltitude > WorldConstants.PlanetVisibleMaxAlt)
        {
            if (_meshInstance != null)
                _meshInstance.Visible = false;
            return;
        }

        float dx = cameraX - _lastX;
        float dz = cameraZ - _lastZ;
        bool needsRebuild = float.IsNaN(_lastX) || (dx * dx + dz * dz) > RebuildThreshold * RebuildThreshold;

        if (needsRebuild)
        {
            RebuildMesh(cameraX, cameraZ);
            _lastX = cameraX;
            _lastZ = cameraZ;
        }

        if (_meshInstance != null)
        {
            _meshInstance.Visible = true;
            // 中高空时渐变透明
            if (cameraAltitude > WorldConstants.PlanetVisibleMinAlt)
            {
                float fade = 1f - MathF.Min(1f,
                    (cameraAltitude - WorldConstants.PlanetVisibleMinAlt) /
                    (WorldConstants.PlanetVisibleMaxAlt - WorldConstants.PlanetVisibleMinAlt));
                _meshInstance.Transparency = 1f - fade;
            }
            else
            {
                _meshInstance.Transparency = 0f;
            }
        }
    }

    public void Shutdown()
    {
        if (_meshInstance != null)
        {
            _meshInstance.GetParent()?.RemoveChild(_meshInstance);
            _meshInstance.Free();
            _meshInstance = null;
        }
        _lastX = float.NaN;
    }

    private void RebuildMesh(float centerX, float centerZ)
    {
        // 清除旧网格
        if (_meshInstance != null)
        {
            _meshInstance.GetParent()?.RemoveChild(_meshInstance);
            _meshInstance.Free();
            _meshInstance = null;
        }

        float cellSize = CoverRadius * 2f / (GridRes - 1);
        float originX = centerX - CoverRadius;
        float originZ = centerZ - CoverRadius;
        float holeRadiusSq = HoleRadius * HoleRadius;

        // 顶点数据
        var vertices = new Vector3[GridRes * GridRes];
        var normals  = new Vector3[GridRes * GridRes];
        var colors   = new Color[GridRes * GridRes];
        var uvs      = new Vector2[GridRes * GridRes];

        int vi = 0;
        for (int z = 0; z < GridRes; z++)
        {
            for (int x = 0; x < GridRes; x++)
            {
                float wx = originX + x * cellSize;
                float wz = originZ + z * cellSize;

                // 到中心的距离
                float relX = wx - centerX;
                float relZ = wz - centerZ;
                float distSq = relX * relX + relZ * relZ;
                float dist = MathF.Sqrt(distSq);

                float h;
                if (distSq < holeRadiusSq)
                {
                    // 中心空洞区域 — 下沉使其隐藏在实际区块下方
                    h = _generator.SampleHeightAt(wx, wz) - 200f;
                }
                else
                {
                    h = _generator.SampleHeightAt(wx, wz);

                    // 边缘渐变 — 远处平滑过渡到平均地面高度
                    if (dist > FadeStartRadius)
                    {
                        float fadeT = MathF.Min(1f, (dist - FadeStartRadius) / (CoverRadius - FadeStartRadius));
                        fadeT = fadeT * fadeT; // 平滑衰减
                        h = h * (1f - fadeT) + 5f * fadeT;
                    }
                }

                vertices[vi] = new Vector3(wx, h, wz);
                uvs[vi] = new Vector2((float)x / (GridRes - 1), (float)z / (GridRes - 1));

                // 群系颜色（远处渐变到雾色）
                var biome = _biomeSampler.Sample(wx, wz);
                var biomeDef = BiomeRegistry.Get(biome);
                var baseColor = biomeDef?.SurfaceColor ?? new Color(0.35f, 0.45f, 0.3f);

                // 远处颜色渐变到大气雾色
                if (dist > FadeStartRadius)
                {
                    float fadeT = MathF.Min(1f, (dist - FadeStartRadius) / (CoverRadius - FadeStartRadius));
                    var fogColor = new Color(0.6f, 0.65f, 0.75f);
                    baseColor = baseColor.Lerp(fogColor, fadeT * 0.7f);
                }

                colors[vi] = new Color(baseColor.R, baseColor.G, baseColor.B, 1f);
                normals[vi] = Vector3.Up;
                vi++;
            }
        }

        // 改进法线（简单中心差分）
        for (int z = 1; z < GridRes - 1; z++)
        {
            for (int x = 1; x < GridRes - 1; x++)
            {
                int c = z * GridRes + x;
                float dhdx = vertices[c + 1].Y - vertices[c - 1].Y;
                float dhdz = vertices[c + GridRes].Y - vertices[c - GridRes].Y;
                normals[c] = new Vector3(-dhdx / (cellSize * 2), 2f, -dhdz / (cellSize * 2)).Normalized();
            }
        }

        // 三角形索引
        var indices = new int[(GridRes - 1) * (GridRes - 1) * 6];
        int ii = 0;
        for (int z = 0; z < GridRes - 1; z++)
        {
            for (int x = 0; x < GridRes - 1; x++)
            {
                int tl = z * GridRes + x;
                int tr = tl + 1;
                int bl = tl + GridRes;
                int br = bl + 1;

                indices[ii++] = tl;
                indices[ii++] = bl;
                indices[ii++] = tr;

                indices[ii++] = tr;
                indices[ii++] = bl;
                indices[ii++] = br;
            }
        }

        // 构建 ArrayMesh
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color]  = colors;
        arrays[(int)Mesh.ArrayType.TexUV]  = uvs;
        arrays[(int)Mesh.ArrayType.Index]  = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, TerrainMaterialFactory.GetShared());

        _meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, // 远景不投射阴影
            Name = "FarTerrainMesh",
        };
        _root.AddChild(_meshInstance);
    }
}
