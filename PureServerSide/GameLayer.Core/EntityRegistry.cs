using System.Collections.Concurrent;
using System.Numerics;

namespace GameLayer.Core;

/// <summary>
/// Server-authoritative entity — mirrors Ark's ECS entity concept with
/// position, rotation, health, type, and zone membership.
/// Uses int id locally (matching Ark ECS) and Guid for network identity.
/// </summary>
public sealed class ServerEntity
{
    public int Id { get; init; }
    public Guid NetworkId { get; init; }
    public string WorldId { get; init; } = string.Empty;
    public ServerEntityType Type { get; init; }

    // ── Spatial ──
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Velocity { get; set; }

    // ── Combat ──
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public bool IsAlive => Health > 0;
    public int WeaponDefId { get; set; }
    public byte WeaponCategory { get; set; }
    public int CurrentMag { get; set; }
    public int MagCapacity { get; set; }
    public int ReserveAmmo { get; set; }
    public int MaxReserve { get; set; }
    public bool IsReloading { get; set; }

    // ── Personal combat backing state (non-wire) ──
    public int PersonalWeaponDefId { get; set; }
    public byte PersonalWeaponCategory { get; set; }
    public int PersonalCurrentMag { get; set; }
    public int PersonalMagCapacity { get; set; }
    public int PersonalReserveAmmo { get; set; }
    public int PersonalMaxReserve { get; set; }

    // ── Building replication ──
    public byte BuildingLevel { get; set; } = 1;
    public byte BuildingConstructionProgress { get; set; } = 100;
    public byte BuildingFrontDamage { get; set; }
    public byte BuildingBackDamage { get; set; }
    public byte BuildingRightDamage { get; set; }
    public byte BuildingLeftDamage { get; set; }
    public float BuildingDamageCluster0X { get; set; }
    public float BuildingDamageCluster0Y { get; set; }
    public float BuildingDamageCluster0Z { get; set; }
    public float BuildingDamageCluster0Strength { get; set; }
    public float BuildingDamageCluster0Age { get; set; }
    public float BuildingDamageCluster0RepairFill { get; set; }
    public float BuildingDamageCluster1X { get; set; }
    public float BuildingDamageCluster1Y { get; set; }
    public float BuildingDamageCluster1Z { get; set; }
    public float BuildingDamageCluster1Strength { get; set; }
    public float BuildingDamageCluster1Age { get; set; }
    public float BuildingDamageCluster1RepairFill { get; set; }
    public float BuildingDamageCluster2X { get; set; }
    public float BuildingDamageCluster2Y { get; set; }
    public float BuildingDamageCluster2Z { get; set; }
    public float BuildingDamageCluster2Strength { get; set; }
    public float BuildingDamageCluster2Age { get; set; }
    public float BuildingDamageCluster2RepairFill { get; set; }
    public byte BuildingDamageLayerState { get; set; }

    // ── Zone / AOI ──
    public string? ZoneId { get; set; }

    // ── Placement hints ──
    public bool AttachToTerrain { get; set; }

    // ── Identity ──
    public string? Name { get; set; }
    public int TypeId { get; set; }

    // ── Vehicle attachment / seat ──
    public int AttachedVehicleEntityId { get; set; }
    public byte SeatIndex { get; set; }
    public byte SeatType { get; set; }
    public bool HasMountedWeapon { get; set; }
    public Vector3 SeatOffset { get; set; }
    public ushort SeatCount { get; set; }
    public ushort OccupiedSeatCount { get; set; }
    public float FuelPercent { get; set; }
    public float TurretYaw { get; set; }
    public float TurretPitch { get; set; }
    public float MountedWeaponHeat { get; set; }
    public float MountedWeaponCycleRemaining { get; set; }
    public float MountedWeaponReloadRemaining { get; set; }
    public float MountedWeaponFaultRemaining { get; set; }
    public float MountedWeaponMaintenanceRemaining { get; set; }
    public float MountedWeaponMaintenanceLevel { get; set; }
    public float MountedWeaponOperationProgress { get; set; }
    public float MountedWeaponSkillScalar { get; set; }
    public byte MountedWeaponFaultCode { get; set; }
    public byte MountedWeaponRepairStep { get; set; }
    public byte MountedWeaponRepairStepCount { get; set; }
    public byte MountedWeaponMaterialUnits { get; set; }

    // ── Spacecraft telemetry ──
    public byte SpaceFlightPhase { get; set; }
    public float Altitude { get; set; }
    public float OrbitalVelocity { get; set; }
    public float RemainingDeltaV { get; set; }

    // ── Metadata ──
    public DateTime SpawnedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ServerEntityType : byte
{
    Player,
    RemotePlayer,
    Npc,
    Monster,
    Building,
    Vehicle,
    Spacecraft,
    Projectile,
    GroundItem,
    Environment
}

/// <summary>
/// Bidirectional mapping between Guid (network) and int (ECS) entity identifiers.
/// Thread-safe for concurrent access from tick loop and network handlers.
/// </summary>
public sealed class EntityRegistry
{
    private readonly ConcurrentDictionary<int, ServerEntity> _byId = new();
    private readonly ConcurrentDictionary<Guid, ServerEntity> _byNetworkId = new();
    private int _nextId;

    public ServerEntity Create(Guid networkId, string worldId, ServerEntityType type, Vector3 position, float maxHealth = 100f)
    {
        var id = Interlocked.Increment(ref _nextId);
        var entity = new ServerEntity
        {
            Id = id,
            NetworkId = networkId,
            WorldId = worldId,
            Type = type,
            Position = position,
            Health = maxHealth,
            MaxHealth = maxHealth
        };

        _byId[id] = entity;
        _byNetworkId[networkId] = entity;
        return entity;
    }

    public bool TryGet(int id, out ServerEntity? entity) => _byId.TryGetValue(id, out entity);
    public bool TryGet(Guid networkId, out ServerEntity? entity) => _byNetworkId.TryGetValue(networkId, out entity);
    public ServerEntity? GetByNetworkId(Guid networkId) => _byNetworkId.GetValueOrDefault(networkId);
    public ServerEntity? GetById(int id) => _byId.GetValueOrDefault(id);

    public bool Remove(int id)
    {
        if (_byId.TryRemove(id, out var entity))
        {
            _byNetworkId.TryRemove(entity.NetworkId, out _);
            return true;
        }
        return false;
    }

    public IReadOnlyCollection<ServerEntity> GetAll() => _byId.Values.ToList().AsReadOnly();

    public IEnumerable<ServerEntity> GetInRadius(Vector3 center, float radius)
    {
        var radiusSq = radius * radius;
        foreach (var entity in _byId.Values)
        {
            var diff = entity.Position - center;
            if (diff.LengthSquared() <= radiusSq)
                yield return entity;
        }
    }

    public IEnumerable<ServerEntity> GetInRadius(string worldId, Vector3 center, float radius)
    {
        var radiusSq = radius * radius;
        foreach (var entity in _byId.Values)
        {
            if (!string.Equals(entity.WorldId, worldId, StringComparison.Ordinal))
                continue;

            var diff = entity.Position - center;
            if (diff.LengthSquared() <= radiusSq)
                yield return entity;
        }
    }

    public IEnumerable<ServerEntity> GetByType(ServerEntityType type)
    {
        foreach (var entity in _byId.Values)
        {
            if (entity.Type == type)
                yield return entity;
        }
    }

    public IEnumerable<ServerEntity> GetInZone(string zoneId)
    {
        foreach (var entity in _byId.Values)
        {
            if (entity.ZoneId == zoneId)
                yield return entity;
        }
    }

    public IEnumerable<ServerEntity> GetInWorldZone(string worldId, string zoneId)
    {
        foreach (var entity in _byId.Values)
        {
            if (entity.WorldId == worldId && entity.ZoneId == zoneId)
                yield return entity;
        }
    }

    public IEnumerable<ServerEntity> GetInWorld(string worldId)
    {
        foreach (var entity in _byId.Values)
        {
            if (entity.WorldId == worldId)
                yield return entity;
        }
    }

    public int Count => _byId.Count;
}
