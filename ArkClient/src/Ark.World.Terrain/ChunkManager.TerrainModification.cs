using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

public sealed partial class ChunkManager
{
    public float SampleHeight(float worldX, float worldZ)
    {
        var coord = ChunkCoord.FromWorldPos(worldX, worldZ, WorldConstants.ChunkSize);
        if (_chunks.TryGetValue(coord, out var loaded))
        {
            var origin = coord.ToWorldOrigin(WorldConstants.ChunkSize);
            float localX = worldX - origin.X;
            float localZ = worldZ - origin.Z;
            return loaded.Data.SampleHeight(localX, localZ, WorldConstants.ChunkSize);
        }

        return _generator.SampleHeightAt(worldX, worldZ);
    }

    public BiomeId GetBiomeAt(float worldX, float worldZ)
    {
        var coord = ChunkCoord.FromWorldPos(worldX, worldZ, WorldConstants.ChunkSize);
        if (!_chunks.TryGetValue(coord, out var loaded)) return BiomeId.None;
        int cx = (int)((worldX - coord.X * WorldConstants.ChunkSize) / WorldConstants.ChunkSize * (loaded.Data.Resolution - 1));
        int cz = (int)((worldZ - coord.Z * WorldConstants.ChunkSize) / WorldConstants.ChunkSize * (loaded.Data.Resolution - 1));
        cx = Math.Clamp(cx, 0, loaded.Data.Resolution - 1);
        cz = Math.Clamp(cz, 0, loaded.Data.Resolution - 1);
        return loaded.Data.Biomes[cz * loaded.Data.Resolution + cx];
    }

    public void ApplyTerrainModification(Vector3 worldPos, float radius, TerrainModType modType, float intensity)
    {
        _modLog.Append(new ModificationEntry(
            Timestamp: 0,
            ModType: modType,
            PosX: worldPos.X, PosY: worldPos.Y, PosZ: worldPos.Z,
            Radius: radius, Intensity: intensity));

        float chunkSize = WorldConstants.ChunkSize;
        int minCx = (int)MathF.Floor((worldPos.X - radius) / chunkSize);
        int maxCx = (int)MathF.Floor((worldPos.X + radius) / chunkSize);
        int minCz = (int)MathF.Floor((worldPos.Z - radius) / chunkSize);
        int maxCz = (int)MathF.Floor((worldPos.Z + radius) / chunkSize);

        for (int cx = minCx; cx <= maxCx; cx++)
        {
            for (int cz = minCz; cz <= maxCz; cz++)
            {
                var coord = new ChunkCoord(cx, cz);
                if (_chunks.TryGetValue(coord, out var loaded))
                {
                    _generator.RegenerateHeights(loaded.Data);
                    HeightfieldGenerator.ApplyModifications(loaded.Data, _modLog, chunkSize);
                    RebuildChunkVisual(loaded);
                }
            }
        }
    }

    public void ApplyRemoteModification(ModificationEntry entry)
    {
        _modLog.Append(entry);
        ApplyTerrainModification(new Vector3(entry.PosX, entry.PosY, entry.PosZ), entry.Radius, entry.ModType, entry.Intensity);
    }
}
