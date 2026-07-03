using System.Text.Json;
using Ark.Ecs.Components;
using Friflo.Engine.ECS;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

public sealed partial class RemoteWorldEcsCacheSystem
{
    public void ApplyPersistedBuildingDamageDeltas(IReadOnlyList<TerrainModificationDto> modifications)
    {
        foreach (var modification in modifications)
        {
            if (!string.Equals(modification.ModType, "building_damage_layer_v1", System.StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(modification.MetadataJson))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(modification.MetadataJson);
                if (!doc.RootElement.TryGetProperty("entityId", out var entityIdElement))
                    continue;

                int snapshotEntityId = entityIdElement.GetInt32();
                if (!TryGetEcsEntityId(snapshotEntityId, out var ecsEntityId))
                    continue;

                var entity = _store.GetEntityById(ecsEntityId);
                if (entity.IsNull)
                    continue;

                BuildingDamageInstanceState state = entity.TryGetComponent<BuildingDamageInstanceState>(out var existing)
                    ? existing
                    : default;
                if (doc.RootElement.TryGetProperty("layerState", out var layerStateElement))
                    state.PackedLayerState = layerStateElement.GetByte();

                if (doc.RootElement.TryGetProperty("clusters", out var clustersElement))
                {
                    for (int i = 0; i < clustersElement.GetArrayLength() && i < 3; i++)
                    {
                        var cluster = clustersElement[i];
                        float x = cluster.TryGetProperty("x", out var xElement) ? xElement.GetSingle() : 0f;
                        float y = cluster.TryGetProperty("y", out var yElement) ? yElement.GetSingle() : 0f;
                        float z = cluster.TryGetProperty("z", out var zElement) ? zElement.GetSingle() : 0f;
                        float strength = cluster.TryGetProperty("strength", out var strengthElement) ? strengthElement.GetSingle() : 0f;
                        float age = cluster.TryGetProperty("age", out var ageElement) ? ageElement.GetSingle() : 0f;
                        float repairFill = cluster.TryGetProperty("repairFill", out var repairElement) ? repairElement.GetSingle() : 0f;

                        switch (i)
                        {
                            case 0:
                                state.Cluster0X = x; state.Cluster0Y = y; state.Cluster0Z = z; state.Cluster0Strength = strength; state.Cluster0Age = age; state.Cluster0RepairFill = repairFill;
                                break;
                            case 1:
                                state.Cluster1X = x; state.Cluster1Y = y; state.Cluster1Z = z; state.Cluster1Strength = strength; state.Cluster1Age = age; state.Cluster1RepairFill = repairFill;
                                break;
                            default:
                                state.Cluster2X = x; state.Cluster2Y = y; state.Cluster2Z = z; state.Cluster2Strength = strength; state.Cluster2Age = age; state.Cluster2RepairFill = repairFill;
                                break;
                        }
                    }
                }

                entity.AddComponent(state);
            }
            catch (JsonException)
            {
            }
        }
    }
}
