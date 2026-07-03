using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Environment;

/// <summary>
/// 植被生成器 — 在地形区块上按群系规则放置植被/装饰物。
/// 使用种子驱动的确定性分布 + MultiMesh 高效渲染。
/// </summary>
public sealed class VegetationSpawner : IWorldSystem
{
    public string SystemId => "Vegetation";

    private WorldSeed _vegSeed;
    private readonly Node3D _root;
    private readonly Dictionary<ChunkCoord, Node3D> _chunkVegetation = new();

    public Node3D SceneRoot => _root;

    public VegetationSpawner()
    {
        _root = new Node3D { Name = "Vegetation" };
    }

    public void Initialize(WorldSeed seed)
    {
        _vegSeed = seed.Derive("vegetation");
    }

    /// <summary>
    /// 为一个区块生成植被。在区块加载后调用。
    /// </summary>
    public void SpawnForChunk(HeightfieldChunk chunk)
    {
        if (_chunkVegetation.ContainsKey(chunk.Coord)) return;

        // 收集区块内群系的最大植被密度
        float maxDensity = 0;
        BiomeDefinition? dominantBiome = null;
        var biomeCounts = new Dictionary<BiomeId, int>();
        foreach (var bid in chunk.Biomes)
        {
            biomeCounts.TryGetValue(bid, out int c);
            biomeCounts[bid] = c + 1;
        }

        BiomeId dominant = BiomeId.None;
        int maxCount = 0;
        foreach (var (bid, count) in biomeCounts)
        {
            if (count > maxCount) { maxCount = count; dominant = bid; }
        }
        dominantBiome = BiomeRegistry.Get(dominant);
        if (dominantBiome == null || dominantBiome.VegetationDensity <= 0.01f) return;

        maxDensity = dominantBiome.VegetationDensity;
        int targetCount = (int)(WorldConstants.MaxVegetationPerChunk * maxDensity);
        if (targetCount <= 0) return;

        var chunkSeed = _vegSeed.DeriveForChunk(chunk.Coord);
        var rng = new Random((int)(chunkSeed.Value & 0x7FFFFFFF));
        float chunkSize = WorldConstants.ChunkSize;
        var origin = chunk.Coord.ToWorldOrigin(chunkSize);

        var chunkNode = new Node3D { Name = $"Veg_{chunk.Coord}" };

        // 使用 MultiMeshInstance3D 高效渲染
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = targetCount,
            Mesh = CreateVegetationMesh(dominantBiome),
        };

        int placed = 0;
        for (int i = 0; i < targetCount * 3 && placed < targetCount; i++)
        {
            float localX = (float)rng.NextDouble() * chunkSize;
            float localZ = (float)rng.NextDouble() * chunkSize;

            // 在高度场上采样高度
            float height = chunk.SampleHeight(localX, localZ, chunkSize);

            // 水下不放植被
            if (height < WorldConstants.SeaLevel + 0.5f) continue;

            // 检查坡度（简化：周围采样）
            float h1 = chunk.SampleHeight(MathF.Min(localX + 1, chunkSize), localZ, chunkSize);
            float h2 = chunk.SampleHeight(localX, MathF.Min(localZ + 1, chunkSize), chunkSize);
            float slopeDeg = MathF.Atan(MathF.Max(MathF.Abs(h1 - height), MathF.Abs(h2 - height))) * (180f / MathF.PI);
            if (slopeDeg > 40f) continue; // 陡坡不放

            // 缩放随机
            float scale = 0.5f + (float)rng.NextDouble() * 1.5f;
            float yRot = (float)rng.NextDouble() * MathF.PI * 2f;

            var xform = new Transform3D(
                Basis.Identity.Scaled(new Vector3(scale, scale, scale))
                    .Rotated(Vector3.Up, yRot),
                new Vector3(origin.X + localX, height, origin.Z + localZ));

            multiMesh.SetInstanceTransform(placed, xform);
            placed++;
        }

        // 裁剪多余实例
        if (placed < targetCount)
        {
            multiMesh.InstanceCount = placed;
        }

        var mmInstance = new MultiMeshInstance3D
        {
            Multimesh = multiMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        chunkNode.AddChild(mmInstance);

        _chunkVegetation[chunk.Coord] = chunkNode;
        _root.AddChild(chunkNode);
    }

    /// <summary>
    /// 卸载区块植被。
    /// </summary>
    public void UnloadForChunk(ChunkCoord coord)
    {
        if (_chunkVegetation.Remove(coord, out var node))
        {
            if (node.GetParent() != null)
                node.GetParent().RemoveChild(node);
            node.Free();
        }
    }

    public void Update(float deltaTime)
    {
        // 未来：季节性植被状态更新（颜色变化、落叶等）
    }

    public void Shutdown()
    {
        foreach (var node in _chunkVegetation.Values)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                if (node.GetParent() != null)
                    node.GetParent().RemoveChild(node);
                node.Free();
            }
        }
        _chunkVegetation.Clear();
    }

    private static Mesh CreateVegetationMesh(BiomeDefinition biome)
    {
        // 简化植被网格：根据群系类型创建不同颜色的几何体
        // 森林 = 绿色锥体+圆柱（树），草原 = 绿色小面片（草）
        bool isTree = biome.VegetationTypes.Any(v => v.StartsWith("tree"));

        if (isTree)
        {
            // 简化树：棕色树干 + 绿色树冠
            var mesh = new BoxMesh
            {
                Size = new Vector3(0.3f, 3f, 0.3f),
            };
            mesh.Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.3f, 0.5f, 0.2f),
                Roughness = 0.9f,
            };
            return mesh;
        }
        else
        {
            // 简化灌木/草
            var mesh = new BoxMesh
            {
                Size = new Vector3(0.5f, 0.6f, 0.5f),
            };
            mesh.Material = new StandardMaterial3D
            {
                AlbedoColor = biome.SurfaceColor.Lightened(0.2f),
                Roughness = 0.95f,
            };
            return mesh;
        }
    }
}
