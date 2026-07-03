using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

public sealed partial class ChunkManager
{
    private void BuildChunkVisual(LoadedChunk loaded)
    {
        var mesh = TerrainMeshBuilder.BuildMesh(loaded.Data, WorldConstants.ChunkSize, loaded.LodStep);
        var origin = loaded.Data.Coord.ToWorldOrigin(WorldConstants.ChunkSize);

        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            Position = origin,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };

        var collisionShape = TerrainMeshBuilder.BuildCollision(loaded.Data, WorldConstants.ChunkSize);
        var staticBody = new StaticBody3D { Name = $"TerrainCol_{loaded.Data.Coord}" };
        var colShape = new CollisionShape3D { Shape = collisionShape };
        float scale = WorldConstants.ChunkSize / (loaded.Data.Resolution - 1);
        colShape.Scale = new Vector3(scale, 1f, scale);
        staticBody.Position = origin + new Vector3(WorldConstants.ChunkSize * 0.5f, 0, WorldConstants.ChunkSize * 0.5f);
        staticBody.AddChild(colShape);

        var chunkNode = new Node3D { Name = $"Chunk_{loaded.Data.Coord}" };
        chunkNode.AddChild(meshInstance);
        chunkNode.AddChild(staticBody);

        loaded.SceneNode = chunkNode;
        _root.AddChild(chunkNode);
    }

    private void RebuildChunkVisual(LoadedChunk loaded)
    {
        if (loaded.SceneNode != null)
        {
            if (loaded.SceneNode.GetParent() != null)
                loaded.SceneNode.GetParent().RemoveChild(loaded.SceneNode);
            loaded.SceneNode.Free();
            loaded.SceneNode = null;
        }
        BuildChunkVisual(loaded);
    }

    private sealed class LoadedChunk
    {
        public required HeightfieldChunk Data;
        public int LodStep;
        public Node3D? SceneNode;
    }

    private readonly record struct GeneratedChunkResult(ChunkCoord Coord, HeightfieldChunk Data, int LodStep);
}
