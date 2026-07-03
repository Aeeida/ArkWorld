using System.Numerics;
using System.Text.Json;
using GameLayer.Core;

namespace GameLayer.Building;

/// <summary>
/// Server-authoritative base-building management.
/// Mirrors Ark's IBaseBuildingService — placement, construction, upgrade, destruction.
/// </summary>
public sealed class BuildingManager
{
    private readonly EntityRegistry _entities;
    private readonly BuildingTypeRegistry _types;
    private readonly ZoneManager _zones;
    private readonly Dictionary<int, BuildingState> _buildings = [];
    private readonly Dictionary<System.Guid, int> _rocketLaunchPads = [];
    private readonly Dictionary<System.Guid, System.Guid> _rocketOwners = [];
    private readonly Dictionary<System.Guid, string> _rocketConfigs = [];
    private readonly List<BuildingDamagePersistenceDelta> _pendingPersistenceDeltas = [];
    private ulong _persistenceSequence;

    public event Action<int>? OnBuildingPlaced;
    public event Action<int>? OnBuildingDestroyed;
    public event Action<int, int>? OnBuildingUpgraded; // entityId, newLevel

    public BuildingManager(EntityRegistry entities, BuildingTypeRegistry types, ZoneManager zones)
    {
        _entities = entities;
        _types = types;
        _zones = zones;
    }

    public bool CanPlaceAt(int buildingTypeId, Vector3 position, Quaternion rotation, string worldId)
    {
        var def = _types.Get(buildingTypeId);
        if (def is null) return false;

        // Check for overlapping buildings
        foreach (var entity in _entities.GetInRadius(worldId, position, 5f))
        {
            if (entity.Type == ServerEntityType.Building)
                return false; // Too close to existing building
        }

        return true;
    }

    public int? PlaceBuilding(Guid ownerId, int buildingTypeId, Vector3 position, Quaternion rotation, string worldId = "default")
    {
        if (!CanPlaceAt(buildingTypeId, position, rotation, worldId))
            return null;

        var def = _types.Get(buildingTypeId);
        if (def is null) return null;

        var entity = _entities.Create(Guid.NewGuid(), worldId, ServerEntityType.Building, position, def.MaxHealth);
        entity.Rotation = rotation;
        entity.TypeId = buildingTypeId;
        entity.Name = def.Name;
        entity.ZoneId = _zones.GetZoneId(position.X, position.Y, position.Z);
        entity.BuildingLevel = 1;
        entity.BuildingConstructionProgress = 0;

        _buildings[entity.Id] = new BuildingState
        {
            EntityId = entity.Id,
            OwnerId = ownerId,
            TypeId = buildingTypeId,
            Level = 1,
            ConstructionProgress = 0f,
            PlacedAt = DateTime.UtcNow,
            WorldPosition = position,
            PersistenceDirty = true,
        };

        OnBuildingPlaced?.Invoke(entity.Id);
        return entity.Id;
    }

    public bool DestroyBuilding(int entityId, Guid requesterId)
    {
        if (!_buildings.TryGetValue(entityId, out var state)) return false;
        if (state.OwnerId != requesterId) return false;

        _buildings.Remove(entityId);
        _entities.Remove(entityId);

        OnBuildingDestroyed?.Invoke(entityId);
        return true;
    }

    public bool UpgradeBuilding(int entityId, Guid requesterId)
    {
        if (!_buildings.TryGetValue(entityId, out var state)) return false;
        if (state.OwnerId != requesterId) return false;
        if (state.Level >= 5) return false; // Max level

        state.Level++;

        // Increase health on upgrade
        if (_entities.TryGet(entityId, out var entity) && entity is not null)
        {
            entity.MaxHealth *= 1.5f;
            entity.Health = entity.MaxHealth;
            entity.BuildingLevel = (byte)state.Level;
        }

        OnBuildingUpgraded?.Invoke(entityId, state.Level);
        return true;
    }

    public IReadOnlyList<BuildingTypeInfo> GetAvailableBuildingTypes()
    {
        return _types.GetAll().Select(d => new BuildingTypeInfo(
            d.TypeId, d.Name, d.RequiredResources, d.BuildTime)).ToList().AsReadOnly();
    }

    public bool TryOccupyBuilding(int entityId, Guid playerId)
    {
        if (!_buildings.TryGetValue(entityId, out var state)) return false;
        if (state.ActiveOccupantId.HasValue && state.ActiveOccupantId != playerId) return false;
        state.ActiveOccupantId = playerId;
        return true;
    }

    public IReadOnlyList<BuildingDamagePersistenceDelta> DrainPersistenceDeltas()
    {
        if (_pendingPersistenceDeltas.Count == 0)
            return [];

        var snapshot = _pendingPersistenceDeltas.ToArray();
        _pendingPersistenceDeltas.Clear();
        return snapshot;
    }

    public void RegisterRocketLaunchPad(System.Guid rocketId, int launchPadEntityId, System.Guid ownerId, string rocketConfigJson)
    {
        _rocketLaunchPads[rocketId] = launchPadEntityId;
        _rocketOwners[rocketId] = ownerId;
        _rocketConfigs[rocketId] = rocketConfigJson;
    }

    /// <summary>
    /// 释放发射台占用（火箭发射后），但保留火箭控制权。
    /// </summary>
    public void ReleaseRocketLaunchPad(System.Guid rocketId)
    {
        if (!_rocketLaunchPads.Remove(rocketId, out var launchPadEntityId)) return;
        // 注意：不删除 _rocketOwners — 发射后玩家仍需控制火箭
        if (_buildings.TryGetValue(launchPadEntityId, out var state))
            state.ActiveOccupantId = null;
    }

    /// <summary>
    /// 完全释放火箭控制权（火箭坠毁/回收/退出控制时调用）。
    /// </summary>
    public void ReleaseRocketControl(System.Guid rocketId)
    {
        _rocketOwners.Remove(rocketId);
        _rocketLaunchPads.Remove(rocketId);
        _rocketConfigs.Remove(rocketId);
    }

    public bool CanControlRocket(System.Guid rocketId, System.Guid playerId) =>
        _rocketOwners.TryGetValue(rocketId, out var ownerId) && ownerId == playerId;

    public string? GetRocketConfigJson(System.Guid rocketId) =>
        _rocketConfigs.GetValueOrDefault(rocketId);

    /// <summary>
    /// Advances construction progress for all buildings currently being built.
    /// Called by the world tick loop.
    /// </summary>
    public void TickConstruction(float deltaTime)
    {
        foreach (var (_, state) in _buildings)
        {
            if (state.ConstructionProgress < 100f)
            {
                var def = _types.Get(state.TypeId);
                if (def is not null && def.BuildTime > 0)
                    state.ConstructionProgress += (deltaTime / def.BuildTime) * 100f;
                state.ConstructionProgress = MathF.Min(100f, state.ConstructionProgress);
            }
        }
    }

    public void RecordDamage(int entityId, Vector3 hitPosition, float damage, Quaternion worldRotation)
    {
        if (!_buildings.TryGetValue(entityId, out var state))
            return;

        var localHit = Vector3.Transform(hitPosition - state.WorldPosition, Quaternion.Conjugate(worldRotation));
        var localDir = localHit;
        if (localDir.LengthSquared() <= 1e-6f)
            localDir = -Vector3.UnitZ;

        float normalizedDamage = Math.Clamp(damage / 400f, 0.04f, 0.35f);
        if (MathF.Abs(localDir.X) > MathF.Abs(localDir.Z))
        {
            if (localDir.X >= 0f)
                state.RightDamage = MathF.Min(1f, state.RightDamage + normalizedDamage);
            else
                state.LeftDamage = MathF.Min(1f, state.LeftDamage + normalizedDamage);
        }
        else
        {
            if (localDir.Z >= 0f)
                state.BackDamage = MathF.Min(1f, state.BackDamage + normalizedDamage);
            else
                state.FrontDamage = MathF.Min(1f, state.FrontDamage + normalizedDamage);
        }

        RecordDamageCluster(state, localHit, normalizedDamage);
    }

    public void SyncSnapshotState()
    {
        foreach (var (_, state) in _buildings)
        {
            if (_entities.TryGet(state.EntityId, out var entity) && entity is not null)
            {
                AdvanceDamageClusterLifecycle(state);
                ReconcileRepairState(state, entity);
                entity.BuildingLevel = (byte)state.Level;
                entity.BuildingConstructionProgress = (byte)Math.Clamp((int)MathF.Round(state.ConstructionProgress), 0, 100);
                entity.BuildingFrontDamage = (byte)Math.Clamp((int)MathF.Round(state.FrontDamage * 100f), 0, 100);
                entity.BuildingBackDamage = (byte)Math.Clamp((int)MathF.Round(state.BackDamage * 100f), 0, 100);
                entity.BuildingRightDamage = (byte)Math.Clamp((int)MathF.Round(state.RightDamage * 100f), 0, 100);
                entity.BuildingLeftDamage = (byte)Math.Clamp((int)MathF.Round(state.LeftDamage * 100f), 0, 100);
                entity.BuildingDamageCluster0X = state.DamageClusters[0].LocalHit.X;
                entity.BuildingDamageCluster0Y = state.DamageClusters[0].LocalHit.Y;
                entity.BuildingDamageCluster0Z = state.DamageClusters[0].LocalHit.Z;
                entity.BuildingDamageCluster0Strength = state.DamageClusters[0].Strength;
                entity.BuildingDamageCluster0Age = state.DamageClusters[0].AgeSeconds;
                entity.BuildingDamageCluster0RepairFill = state.DamageClusters[0].RepairFill;
                entity.BuildingDamageCluster1X = state.DamageClusters[1].LocalHit.X;
                entity.BuildingDamageCluster1Y = state.DamageClusters[1].LocalHit.Y;
                entity.BuildingDamageCluster1Z = state.DamageClusters[1].LocalHit.Z;
                entity.BuildingDamageCluster1Strength = state.DamageClusters[1].Strength;
                entity.BuildingDamageCluster1Age = state.DamageClusters[1].AgeSeconds;
                entity.BuildingDamageCluster1RepairFill = state.DamageClusters[1].RepairFill;
                entity.BuildingDamageCluster2X = state.DamageClusters[2].LocalHit.X;
                entity.BuildingDamageCluster2Y = state.DamageClusters[2].LocalHit.Y;
                entity.BuildingDamageCluster2Z = state.DamageClusters[2].LocalHit.Z;
                entity.BuildingDamageCluster2Strength = state.DamageClusters[2].Strength;
                entity.BuildingDamageCluster2Age = state.DamageClusters[2].AgeSeconds;
                entity.BuildingDamageCluster2RepairFill = state.DamageClusters[2].RepairFill;
                entity.BuildingDamageLayerState = PackDamageLayerState(state.DamageClusters);
                QueuePersistenceDeltaIfNeeded(state, entity);
                state.WorldPosition = entity.Position;
            }
        }
    }

    private static void AdvanceDamageClusterLifecycle(BuildingState state)
    {
        for (int i = 0; i < state.DamageClusters.Length; i++)
        {
            var cluster = state.DamageClusters[i];
            if (cluster.Strength <= 0f)
                continue;

            cluster.AgeSeconds = MathF.Min(12f, cluster.AgeSeconds + 0.1f);
            cluster.RepairFill = MathF.Max(cluster.RepairFill, 1f - cluster.Strength);
            if (cluster.RepairFill > 0.96f && cluster.Strength < 0.08f)
            {
                cluster = default;
                state.PersistenceDirty = true;
            }
            else if (MathF.Abs(cluster.AgeSeconds % 0.5f) < 0.051f)
            {
                state.PersistenceDirty = true;
            }
            state.DamageClusters[i] = cluster;
        }
    }

    private static void RecordDamageCluster(BuildingState state, Vector3 localHit, float intensity)
    {
        int bestIndex = -1;
        float bestDistanceSq = 1.2f * 1.2f;
        for (int i = 0; i < state.DamageClusters.Length; i++)
        {
            if (state.DamageClusters[i].Strength <= 0f)
            {
                bestIndex = i;
                bestDistanceSq = float.MaxValue;
                break;
            }

            float distSq = Vector3.DistanceSquared(state.DamageClusters[i].LocalHit, localHit);
            if (distSq < bestDistanceSq)
            {
                bestDistanceSq = distSq;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            float weakest = state.DamageClusters[0].Strength;
            bestIndex = 0;
            for (int i = 1; i < state.DamageClusters.Length; i++)
            {
                if (state.DamageClusters[i].Strength < weakest)
                {
                    weakest = state.DamageClusters[i].Strength;
                    bestIndex = i;
                }
            }
        }

        var cluster = state.DamageClusters[bestIndex];
        if (cluster.Strength > 0f)
        {
            float blend = intensity / MathF.Max(0.001f, cluster.Strength + intensity);
            cluster.LocalHit = Vector3.Lerp(cluster.LocalHit, localHit, blend);
        }
        else
            cluster.LocalHit = localHit;
        cluster.Strength = MathF.Min(1f, cluster.Strength + intensity);
        cluster.AgeSeconds = 0f;
        cluster.RepairFill = 0f;
        state.DamageClusters[bestIndex] = cluster;
        state.PersistenceDirty = true;
    }

    private static void ReconcileRepairState(BuildingState state, ServerEntity entity)
    {
        if (entity.MaxHealth <= 0f)
            return;

        float allowedDamageBudget = Math.Clamp(1f - entity.Health / entity.MaxHealth, 0f, 1f) * 4f;
        float currentDamageBudget = state.FrontDamage + state.BackDamage + state.RightDamage + state.LeftDamage;
        float excessRepair = MathF.Max(0f, currentDamageBudget - allowedDamageBudget);
        if (excessRepair <= 0f)
            return;

        while (excessRepair > 0.0001f)
        {
            int zoneIndex = 0;
            float zoneValue = state.FrontDamage;
            if (state.BackDamage > zoneValue) { zoneIndex = 1; zoneValue = state.BackDamage; }
            if (state.RightDamage > zoneValue) { zoneIndex = 2; zoneValue = state.RightDamage; }
            if (state.LeftDamage > zoneValue) { zoneIndex = 3; zoneValue = state.LeftDamage; }
            if (zoneValue <= 0f)
                break;

            float repair = MathF.Min(zoneValue, excessRepair);
            switch (zoneIndex)
            {
                case 0: state.FrontDamage -= repair; break;
                case 1: state.BackDamage -= repair; break;
                case 2: state.RightDamage -= repair; break;
                default: state.LeftDamage -= repair; break;
            }
            excessRepair -= repair;
        }

        float remainingBudget = state.FrontDamage + state.BackDamage + state.RightDamage + state.LeftDamage;
        ReconcileDamageClusters(state, remainingBudget);
        PropagateRepairFill(state);
        state.PersistenceDirty = true;
    }

    private static void ReconcileDamageClusters(BuildingState state, float remainingBudget)
    {
        float currentClusterStrength = 0f;
        foreach (var cluster in state.DamageClusters)
            currentClusterStrength += cluster.Strength;

        if (currentClusterStrength <= remainingBudget || currentClusterStrength <= 0f)
            return;

        float scale = remainingBudget / currentClusterStrength;
        for (int i = 0; i < state.DamageClusters.Length; i++)
        {
            var cluster = state.DamageClusters[i];
            cluster.Strength *= scale;
            cluster.RepairFill = MathF.Max(cluster.RepairFill, 1f - cluster.Strength);
            if (cluster.Strength < 0.03f)
                cluster = default;
            state.DamageClusters[i] = cluster;
        }
    }

    private static void PropagateRepairFill(BuildingState state)
    {
        for (int i = 0; i < state.DamageClusters.Length; i++)
        {
            if (state.DamageClusters[i].Strength <= 0f)
                continue;

            float propagatedFill = state.DamageClusters[i].RepairFill;
            for (int j = 0; j < state.DamageClusters.Length; j++)
            {
                if (i == j || state.DamageClusters[j].Strength <= 0f)
                    continue;

                float distSq = Vector3.DistanceSquared(state.DamageClusters[i].LocalHit, state.DamageClusters[j].LocalHit);
                if (distSq > 2.25f)
                    continue;

                propagatedFill = MathF.Max(propagatedFill, state.DamageClusters[j].RepairFill * 0.72f);
            }

            var cluster = state.DamageClusters[i];
            cluster.RepairFill = propagatedFill;
            state.DamageClusters[i] = cluster;
        }
    }

    private void QueuePersistenceDeltaIfNeeded(BuildingState state, ServerEntity entity)
    {
        byte layerState = PackDamageLayerState(state.DamageClusters);
        if (!state.PersistenceDirty && state.LastPersistedLayerState == layerState && state.LastPersistedZoneKey == entity.ZoneId)
            return;

        state.LastPersistedLayerState = layerState;
        state.LastPersistedZoneKey = entity.ZoneId ?? string.Empty;
        state.PersistenceDirty = false;

        string payloadJson = JsonSerializer.Serialize(new
        {
            entityId = state.EntityId,
            typeId = state.TypeId,
            chunkKey = entity.ZoneId ?? string.Empty,
            layerState,
            clusters = state.DamageClusters.Select(cluster => new
            {
                x = cluster.LocalHit.X,
                y = cluster.LocalHit.Y,
                z = cluster.LocalHit.Z,
                strength = cluster.Strength,
                age = cluster.AgeSeconds,
                repairFill = cluster.RepairFill,
            }).ToArray(),
        });

        _pendingPersistenceDeltas.Add(new BuildingDamagePersistenceDelta(
            state.EntityId,
            entity.WorldId,
            entity.ZoneId ?? string.Empty,
            entity.Position,
            layerState,
            ++_persistenceSequence,
            payloadJson));
    }

    private static byte PackDamageLayerState(BuildingDamageCluster[] clusters)
    {
        byte packed = 0;
        for (int i = 0; i < clusters.Length; i++)
        {
            byte layer = clusters[i].Strength switch
            {
                > 0.66f => 3,
                > 0.33f => 2,
                > 0.05f => 1,
                _ => 0,
            };
            if (clusters[i].RepairFill > 0.7f && layer > 0)
                layer--;

            packed |= (byte)(layer << (i * 2));
        }

        return packed;
    }
}

public sealed class BuildingState
{
    public int EntityId { get; init; }
    public System.Guid OwnerId { get; init; }
    public int TypeId { get; init; }
    public int Level { get; set; } = 1;
    public float ConstructionProgress { get; set; }
    public DateTime PlacedAt { get; init; }
    public System.Guid? ActiveOccupantId { get; set; }
    public Vector3 WorldPosition { get; set; }
    public float FrontDamage { get; set; }
    public float BackDamage { get; set; }
    public float RightDamage { get; set; }
    public float LeftDamage { get; set; }
    public bool PersistenceDirty { get; set; }
    public byte LastPersistedLayerState { get; set; }
    public string LastPersistedZoneKey { get; set; } = string.Empty;
    public BuildingDamageCluster[] DamageClusters { get; } = [new(), new(), new()];
}

public struct BuildingDamageCluster
{
    public Vector3 LocalHit;
    public float Strength;
    public float AgeSeconds;
    public float RepairFill;
}

public record struct BuildingTypeInfo(int TypeId, string Name, Dictionary<int, int> RequiredResources, float BuildTime);

public sealed class BuildingTypeRegistry
{
    private readonly Dictionary<int, BuildingTypeDef> _types = [];

    public void Register(BuildingTypeDef def) => _types[def.TypeId] = def;
    public BuildingTypeDef? Get(int typeId) => _types.GetValueOrDefault(typeId);
    public IReadOnlyCollection<BuildingTypeDef> GetAll() => _types.Values.ToList().AsReadOnly();

    public void SeedDefaults()
    {
        Register(new BuildingTypeDef(1, "Wall", 500f, 5f, new() { [3] = 5 }));
        Register(new BuildingTypeDef(2, "Foundation", 800f, 8f, new() { [3] = 10, [4] = 2 }));
        Register(new BuildingTypeDef(3, "Turret", 300f, 15f, new() { [4] = 5 }));
        Register(new BuildingTypeDef(4, "Storage", 400f, 10f, new() { [3] = 8, [4] = 3 }));
        Register(new BuildingTypeDef(5, "Generator", 600f, 20f, new() { [4] = 10 }));
        Register(new BuildingTypeDef(6, "Tank Factory", 1200f, 30f, new() { [3] = 20, [4] = 15 }));
    }
}

public record BuildingTypeDef(int TypeId, string Name, float MaxHealth, float BuildTime, Dictionary<int, int> RequiredResources);
