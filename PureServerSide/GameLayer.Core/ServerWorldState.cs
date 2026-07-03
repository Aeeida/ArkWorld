using System.Numerics;

namespace GameLayer.Core;

/// <summary>
/// Server-authoritative world state — owns all entities, time, weather, and zones.
/// This is the server-side equivalent of Ark's LocalGameWorld / IGameWorld.
/// </summary>
public sealed class ServerWorldState
{
    public EntityRegistry Entities { get; } = new();
    public ZoneManager Zones { get; }

    private float _worldTime;
    private float _timeScale = 1f;
    private byte _weatherId;
    private float _weatherIntensity;
    private ulong _tick;

    public ulong CurrentTick => _tick;
    public float WorldTime => _worldTime;
    public bool IsLoaded { get; private set; }

    /// <summary>地形种子 — 客户端使用此种子进行程序化地形生成。</summary>
    public long TerrainSeed { get; private set; }

    /// <summary>当前生物群系 ID。</summary>
    public string BiomeId { get; private set; } = "plains";

    /// <summary>重力倍率。</summary>
    public float GravityMultiplier { get; private set; } = 1f;

    public event Action<ServerEntity>? OnEntitySpawned;
    public event Action<int, Guid>? OnEntityDestroyed;

    public ServerWorldState(float zoneSize = 200f)
    {
        Zones = new ZoneManager(zoneSize);
    }

    /// <summary>
    /// Spawns a new entity in the world.
    /// </summary>
    public ServerEntity SpawnEntity(
        Guid networkId,
        string worldId,
        ServerEntityType type,
        Vector3 position,
        float maxHealth = 100f,
        string? name = null,
        int typeId = 0)
    {
        var entity = Entities.Create(networkId, worldId, type, position, maxHealth);
        entity.Name = name;
        entity.TypeId = typeId;

        // Auto-assign zone based on 3D position
        entity.ZoneId = Zones.GetZoneId(position.X, position.Y, position.Z);

        OnEntitySpawned?.Invoke(entity);
        return entity;
    }

    /// <summary>
    /// Destroys an entity by its local ID.
    /// </summary>
    public bool DestroyEntity(int entityId)
    {
        var entity = Entities.GetById(entityId);
        if (entity is null) return false;

        var networkId = entity.NetworkId;
        Entities.Remove(entityId);
        OnEntityDestroyed?.Invoke(entityId, networkId);
        return true;
    }

    /// <summary>
    /// Spawns a player entity and registers it in the zone system.
    /// </summary>
    public ServerEntity SpawnPlayer(Guid playerId, string worldId, string name, Vector3 position)
    {
        var entity = SpawnEntity(playerId, worldId, ServerEntityType.Player, position, 100f, name);
        Zones.UpdatePlayerZone(playerId, position.X, position.Y, position.Z);
        return entity;
    }

    /// <summary>
    /// Removes a player from the world entirely.
    /// </summary>
    public void RemovePlayer(Guid playerId)
    {
        var entity = Entities.GetByNetworkId(playerId);
        if (entity is not null)
        {
            Entities.Remove(entity.Id);
            Zones.RemovePlayer(playerId);
            OnEntityDestroyed?.Invoke(entity.Id, playerId);
        }
    }

    /// <summary>
    /// Updates an entity's position and handles zone transitions.
    /// Returns the new zone ID if the entity changed zones, null otherwise.
    /// </summary>
    public string? UpdateEntityPosition(Guid networkId, Vector3 newPosition)
    {
        return UpdateEntityTransform(networkId, newPosition, null);
    }

    public string? UpdateEntityPosition(Guid networkId, Vector3 newPosition, Quaternion newRotation)
    {
        return UpdateEntityTransform(networkId, newPosition, newRotation);
    }

    private string? UpdateEntityTransform(Guid networkId, Vector3 newPosition, Quaternion? newRotation)
    {
        var entity = Entities.GetByNetworkId(networkId);
        if (entity is null) return null;

        entity.Position = newPosition;
        if (newRotation.HasValue)
            entity.Rotation = Quaternion.Normalize(newRotation.Value);
        entity.LastUpdatedAt = DateTime.UtcNow;

        // Check zone transition for players
        if (entity.Type == ServerEntityType.Player)
        {
            var newZone = Zones.UpdatePlayerZone(networkId, newPosition.X, newPosition.Y, newPosition.Z);
            if (newZone is not null)
                entity.ZoneId = newZone;
            return newZone;
        }

        // Non-player entities: just update zone tag
        var zoneId = Zones.GetZoneId(newPosition.X, newPosition.Y, newPosition.Z);
        if (entity.ZoneId != zoneId)
        {
            entity.ZoneId = zoneId;
            return zoneId;
        }
        return null;
    }

    /// <summary>
    /// Advances the world simulation by one tick.
    /// </summary>
    public void Tick(float deltaTime)
    {
        _tick++;
        _worldTime += deltaTime * _timeScale;

        foreach (var entity in Entities.GetAll())
        {
            if (entity.Velocity.LengthSquared() <= 1e-6f)
            {
                UpdateDerivedSnapshotState(entity);
                continue;
            }

            entity.Position += entity.Velocity * deltaTime;
            entity.LastUpdatedAt = DateTime.UtcNow;

            var zoneId = Zones.GetZoneId(entity.Position.X, entity.Position.Y, entity.Position.Z);
            if (entity.ZoneId != zoneId)
                entity.ZoneId = zoneId;

            UpdateDerivedSnapshotState(entity);
        }
    }

    private static void UpdateDerivedSnapshotState(ServerEntity entity)
    {
        if (entity.Type != ServerEntityType.Spacecraft)
            return;

        entity.Altitude = MathF.Max(0f, entity.Position.Y);
        entity.OrbitalVelocity = entity.Velocity.Length();
        entity.SpaceFlightPhase = DetermineSpaceFlightPhase(entity);

        if (entity.RemainingDeltaV <= 0f && entity.FuelPercent > 0f)
            entity.RemainingDeltaV = entity.FuelPercent * 10f;
    }

    private static byte DetermineSpaceFlightPhase(ServerEntity entity)
    {
        if (!entity.IsAlive)
            return 8;

        if (entity.Altitude < 5f && entity.OrbitalVelocity < 1f)
            return 0;
        if (entity.Altitude < 50f)
            return 2;
        if (entity.Altitude < 10000f)
            return 3;
        return 4;
    }

    /// <summary>
    /// Gets a snapshot of all entities in a given zone (for AOI broadcast).
    /// </summary>
    public byte[] GetZoneSnapshot(string worldId, string zoneId)
    {
        var entities = Entities.GetInWorldZone(worldId, zoneId).ToList();
        return SnapshotSerializer.Serialize(entities);
    }

    /// <summary>
    /// Gets a full world snapshot (for initial join).
    /// </summary>
    public byte[] GetFullSnapshot(string worldId)
    {
        var entities = Entities.GetInWorld(worldId).ToList();
        return SnapshotSerializer.Serialize(entities);
    }

    public void SetWeather(byte weatherId, float intensity)
    {
        _weatherId = weatherId;
        _weatherIntensity = intensity;
    }

    public (byte WeatherId, float Intensity) GetWeather() => (_weatherId, _weatherIntensity);

    public void SetTimeScale(float scale) => _timeScale = Math.Clamp(scale, 0f, 10f);

    public void MarkLoaded() => IsLoaded = true;

    /// <summary>设置地形种子和生物群系。</summary>
    public void SetTerrainConfig(long seed, string biomeId, float gravity = 1f)
    {
        TerrainSeed = seed;
        BiomeId = biomeId;
        GravityMultiplier = gravity;
    }
}
