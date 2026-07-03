using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Ark.World.Data;
using Ark.World.Core;

namespace Ark.World.Terrain;

public sealed partial class ChunkManager
{
    private readonly Dictionary<string, long> _dirtyChunkSequence = new();

    public void ApplyPersistedModificationBatch(IReadOnlyList<PersistedTerrainModification> modifications)
    {
        if (modifications.Count == 0)
            return;

        foreach (var modification in modifications)
        {
            if (string.Equals(modification.ModType, "building_damage_layer_v1", System.StringComparison.Ordinal))
            {
                ReplayBuildingDamageDelta(modification);
                continue;
            }

            if (!TryMapTerrainModType(modification.ModType, out var terrainModType))
                continue;

            ApplyRemoteModification(new ModificationEntry(
                modification.SequenceTick,
                terrainModType,
                modification.X,
                modification.TargetHeight,
                modification.Z,
                Mathf.Max(modification.RadiusX, modification.RadiusZ),
                1f,
                modification.MetadataJson));
        }

        TrimDirtyChunkReplayLog();
    }

    private void ReplayBuildingDamageDelta(PersistedTerrainModification modification)
    {
        string chunkKey = modification.ChunkKey ?? "global";
        if (_dirtyChunkSequence.TryGetValue(chunkKey, out var lastSequence) && lastSequence >= modification.SequenceTick)
            return;

        _dirtyChunkSequence[chunkKey] = modification.SequenceTick;
        _modLog.Append(new ModificationEntry(
            modification.SequenceTick,
            TerrainModType.Flatten,
            modification.X,
            modification.TargetHeight,
            modification.Z,
            0f,
            0f,
            modification.MetadataJson));
    }

    private void TrimDirtyChunkReplayLog()
    {
        _modLog.Trim(entry =>
        {
            if (string.IsNullOrWhiteSpace(entry.Metadata))
                return true;

            try
            {
                using var doc = JsonDocument.Parse(entry.Metadata);
                if (!doc.RootElement.TryGetProperty("chunkKey", out var chunkKeyElement))
                    return true;

                string chunkKey = chunkKeyElement.GetString() ?? string.Empty;
                return !_dirtyChunkSequence.TryGetValue(chunkKey, out var latestSequence) || entry.Timestamp >= latestSequence;
            }
            catch
            {
                return true;
            }
        });
    }

    private static bool TryMapTerrainModType(string modType, out TerrainModType terrainModType)
    {
        terrainModType = modType switch
        {
            "flatten" => TerrainModType.Flatten,
            "raise" or "fill" => TerrainModType.Fill,
            "dig" => TerrainModType.Dig,
            "blast" or "explosion" => TerrainModType.Explosion,
            "erosion" => TerrainModType.Erosion,
            _ => TerrainModType.Flatten,
        };

        return modType is "flatten" or "raise" or "fill" or "dig" or "blast" or "explosion" or "erosion";
    }
}
