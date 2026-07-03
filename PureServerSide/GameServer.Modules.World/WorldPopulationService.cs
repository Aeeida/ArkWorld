using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameLayer.Core;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using GameServer.Networking.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Modules.World;

public sealed class WorldPopulationService(
    IServiceScopeFactory scopeFactory,
    ServerWorldState world,
    ILogger<WorldPopulationService> logger) : IWorldPopulationService
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _loadedWorlds = [];
    private readonly Dictionary<Guid, MobileSpawnState> _mobileSpawns = [];

    public async Task EnsureWorldPopulatedAsync(string worldId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_loadedWorlds.Contains(worldId))
                return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var location = await db.Locations
            .FirstOrDefaultAsync(l => l.Code == worldId || l.Name == worldId, ct);
        if (location is null)
            return;

        var spawns = await db.WorldSpawns
            .Where(s => s.LocationId == location.Id && s.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);

        lock (_lock)
        {
            if (!_loadedWorlds.Add(worldId))
                return;
        }

        foreach (var spawn in spawns)
        {
            var networkId = BuildDeterministicGuid($"{worldId}:{spawn.Id}:{spawn.SpawnType}:{spawn.TemplateId}");
            if (world.Entities.GetByNetworkId(networkId) is not null)
                continue;

            var position = new Vector3((float)spawn.LocalX, (float)spawn.LocalY, (float)spawn.LocalZ);
            var entity = world.SpawnEntity(
                networkId,
                worldId,
                MapEntityType(spawn.SpawnType),
                position,
                MaxHealthFor(spawn),
                spawn.DisplayName,
                spawn.Level);
            entity.AttachToTerrain = ShouldAttachToTerrain(spawn);
            entity.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, spawn.Rotation);

            if (TryBuildMobileState(worldId, spawn, networkId, position, out var mobile))
            {
                lock (_lock)
                    _mobileSpawns[networkId] = mobile;
            }
        }

        logger.LogInformation("World population loaded: world={WorldId}, spawns={Count}, mobile={Mobile}",
            worldId, spawns.Count, _mobileSpawns.Values.Count(s => s.WorldId == worldId));
    }

    public void Tick(float deltaTime)
    {
        List<Guid>? toRemove = null;

        lock (_lock)
        {
            foreach (var (networkId, state) in _mobileSpawns)
            {
                var entity = world.Entities.GetByNetworkId(networkId);
                if (entity is null || !entity.IsAlive)
                {
                    (toRemove ??= []).Add(networkId);
                    continue;
                }

                state.PauseRemaining -= deltaTime;
                if (state.PauseRemaining > 0f)
                    continue;

                var current = entity.Position;
                if (!state.HasTarget || Vector3.DistanceSquared(current, state.Target) < 1.5f)
                {
                    state.Target = NextTarget(state);
                    state.HasTarget = true;
                    state.PauseRemaining = Random.Shared.NextSingle() * (state.PauseSecondsMax - state.PauseSecondsMin) + state.PauseSecondsMin;
                    continue;
                }

                var toTarget = state.Target - current;
                toTarget.Y = 0f;
                var distance = toTarget.Length();
                if (distance <= float.Epsilon)
                    continue;

                var step = MathF.Min(distance, state.MoveSpeed * deltaTime);
                var next = current + Vector3.Normalize(toTarget) * step;
                world.UpdateEntityPosition(networkId, next);

                var yaw = MathF.Atan2(toTarget.X, toTarget.Z);
                entity.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
            }

            if (toRemove is not null)
            {
                foreach (var id in toRemove)
                    _mobileSpawns.Remove(id);
            }
        }
    }

    private static bool TryBuildMobileState(string worldId, WorldSpawnEntry spawn, Guid networkId, Vector3 home, out MobileSpawnState mobile)
    {
        mobile = default!;
        if (string.IsNullOrWhiteSpace(spawn.MetadataJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(spawn.MetadataJson);
            if (!doc.RootElement.TryGetProperty("Mobile", out var mobileProp) || !mobileProp.GetBoolean())
                return false;

            var patrolRadius = doc.RootElement.TryGetProperty("PatrolRadius", out var radiusProp)
                ? (float)radiusProp.GetDouble()
                : 24f;
            var moveSpeed = doc.RootElement.TryGetProperty("MoveSpeed", out var speedProp)
                ? (float)speedProp.GetDouble()
                : 1.2f;
            var pauseMin = doc.RootElement.TryGetProperty("PauseSecondsMin", out var pauseMinProp)
                ? (float)pauseMinProp.GetDouble()
                : 0.75f;
            var pauseMax = doc.RootElement.TryGetProperty("PauseSecondsMax", out var pauseMaxProp)
                ? (float)pauseMaxProp.GetDouble()
                : 3.0f;

            mobile = new MobileSpawnState
            {
                NetworkId = networkId,
                WorldId = worldId,
                Home = home,
                Target = home,
                PatrolRadius = MathF.Max(6f, patrolRadius),
                MoveSpeed = MathF.Max(0.5f, moveSpeed),
                PauseSecondsMin = MathF.Max(0.1f, pauseMin),
                PauseSecondsMax = MathF.Max(pauseMin, pauseMax),
                PauseRemaining = Random.Shared.NextSingle() * MathF.Max(0.1f, pauseMax),
                Seed = BitConverter.ToInt32(networkId.ToByteArray(), 0)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Vector3 NextTarget(MobileSpawnState state)
    {
        var rng = new Random(unchecked(state.Seed++));
        var angle = (float)(rng.NextDouble() * Math.PI * 2);
        var radius = (float)(rng.NextDouble() * state.PatrolRadius);
        return new Vector3(
            state.Home.X + MathF.Cos(angle) * radius,
            state.Home.Y,
            state.Home.Z + MathF.Sin(angle) * radius);
    }

    private static ServerEntityType MapEntityType(string spawnType) => spawnType switch
    {
        "npc" => ServerEntityType.Npc,
        "wildlife" => ServerEntityType.Monster,
        "monster" => ServerEntityType.Monster,
        "town" => ServerEntityType.Building,
        "village" => ServerEntityType.Building,
        "quest_giver" => ServerEntityType.Building,
        "resource_node" => ServerEntityType.GroundItem,
        "landmark" => ServerEntityType.Environment,
        _ => ServerEntityType.Environment
    };

    private static float MaxHealthFor(WorldSpawnEntry spawn) => spawn.SpawnType switch
    {
        "monster" => 60f + spawn.Level * 12f,
        "wildlife" => 35f + spawn.Level * 8f,
        "npc" => 50f + spawn.Level * 6f,
        _ => 250f
    };

    private static bool ShouldAttachToTerrain(WorldSpawnEntry spawn)
    {
        if (!string.IsNullOrWhiteSpace(spawn.MetadataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(spawn.MetadataJson);
                if (doc.RootElement.TryGetProperty("AttachToTerrain", out var attachProp)
                    && attachProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return attachProp.GetBoolean();
            }
            catch
            {
            }
        }

        return spawn.SpawnType is "npc" or "wildlife" or "monster" or "town" or "village" or "quest_giver" or "resource_node" or "landmark";
    }

    private static Guid BuildDeterministicGuid(string key)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(bytes);
    }

    private sealed class MobileSpawnState
    {
        public required Guid NetworkId { get; init; }
        public required string WorldId { get; init; }
        public required Vector3 Home { get; init; }
        public Vector3 Target { get; set; }
        public float PatrolRadius { get; init; }
        public float MoveSpeed { get; init; }
        public float PauseSecondsMin { get; init; }
        public float PauseSecondsMax { get; init; }
        public float PauseRemaining { get; set; }
        public bool HasTarget { get; set; }
        public int Seed { get; set; }
    }
}
