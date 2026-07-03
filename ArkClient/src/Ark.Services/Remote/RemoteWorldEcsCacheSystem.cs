using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Services.Remote.EcsMappers;
using Ark.Shared.Data;
using Game.Shared.Core.DTOs;
using Game.Shared.Core.DTOs.EcsMappers;

namespace Ark.Services.Remote;

/// <summary>
/// 将 `RemoteGameWorld` 快照缓存同步到 ECS。
/// 正确层次：网络快照 -> ECS 缓存 -> Godot 表现。
/// </summary>
public sealed partial class RemoteWorldEcsCacheSystem
{
    private readonly EntityStore _store;
    private readonly RemoteGameWorld _remoteWorld;
    private readonly Dictionary<int, int> _snapshotToEcs = new();
    private readonly Dictionary<int, int> _ecsToSnapshot = new();
    private readonly Dictionary<System.Guid, int> _networkToEcs = new();
    private readonly Dictionary<int, RemoteOwnershipState> _pendingSnapshotOwnership = new();
    private readonly Dictionary<System.Guid, RemoteOwnershipState> _pendingNetworkOwnership = new();
    private readonly HashSet<int> _seenSnapshotIds = new();
    private readonly List<int> _staleSnapshotIds = new();

    public int LocalPresentationEntityId { get; set; }
    public bool IsSnapshotReady
    {
        get
        {
            if (!_remoteWorld.IsLoaded || LocalPresentationEntityId <= 0)
                return false;

            var entity = _store.GetEntityById(LocalPresentationEntityId);
            return !entity.IsNull && entity.TryGetComponent<RemoteEntityState>(out _);
        }
    }

    public RemoteWorldEcsCacheSystem(EntityStore store, RemoteGameWorld remoteWorld)
    {
        _store = store;
        _remoteWorld = remoteWorld;
    }

    public void Update()
    {
        if (!_remoteWorld.IsLoaded)
            return;

        _seenSnapshotIds.Clear();

        foreach (var remote in _remoteWorld.GetAllRemoteEntities())
        {
            _seenSnapshotIds.Add(remote.Id);

            bool isLocalPlayer = remote.NetworkId == _remoteWorld.LocalPlayerGuid && LocalPresentationEntityId > 0;
            var entity = ResolveOrCreateEntity(remote, isLocalPlayer);
            if (entity.IsNull)
                continue;

            SyncEntity(entity, remote, isLocalPlayer);
        }

        SyncAttachedVehicleRuntimeState();

        _staleSnapshotIds.Clear();
        foreach (var snapshotEntityId in _snapshotToEcs.Keys)
        {
            if (!_seenSnapshotIds.Contains(snapshotEntityId))
                _staleSnapshotIds.Add(snapshotEntityId);
        }

        foreach (var snapshotEntityId in _staleSnapshotIds)
        {
            if (!_snapshotToEcs.TryGetValue(snapshotEntityId, out var ecsEntityId))
                continue;

            _snapshotToEcs.Remove(snapshotEntityId);
            _ecsToSnapshot.Remove(ecsEntityId);

            var entity = _store.GetEntityById(ecsEntityId);
            if (!entity.IsNull)
            {
                if (ecsEntityId == LocalPresentationEntityId)
                {
                    entity.RemoveComponent<RemoteEntityState>();
                    entity.RemoveComponent<RemoteSnapshotState>();
                    entity.RemoveTag<ServerAuthority>();
                }
                else
                {
                    if (entity.TryGetComponent<RemoteEntityState>(out var state))
                        _networkToEcs.Remove(state.NetworkId);
                    entity.DeleteEntity();
                }
            }
        }
    }

    public bool TryGetEcsEntityId(int snapshotEntityId, out int ecsEntityId) =>
        _snapshotToEcs.TryGetValue(snapshotEntityId, out ecsEntityId);

    public bool TryGetSnapshotEntityId(int ecsEntityId, out int snapshotEntityId) =>
        _ecsToSnapshot.TryGetValue(ecsEntityId, out snapshotEntityId);

    public bool TryGetEcsEntityId(System.Guid networkId, out int ecsEntityId) =>
        _networkToEcs.TryGetValue(networkId, out ecsEntityId);

    public bool TryGetNetworkId(int snapshotEntityId, out System.Guid networkId)
    {
        networkId = System.Guid.Empty;
        if (!_snapshotToEcs.TryGetValue(snapshotEntityId, out var ecsEntityId))
            return false;

        var entity = _store.GetEntityById(ecsEntityId);
        if (entity.IsNull || !entity.TryGetComponent<RemoteEntityState>(out var state))
            return false;

        networkId = state.NetworkId;
        return networkId != System.Guid.Empty;
    }

    public IEnumerable<int> GetSnapshotEntityIds(EntityType type)
    {
        foreach (var pair in _snapshotToEcs)
        {
            var entity = _store.GetEntityById(pair.Value);
            if (entity.IsNull || !entity.TryGetComponent<RemoteEntityState>(out var state))
                continue;

            if (state.EntityType == (byte)type)
                yield return pair.Key;
        }
    }

    public void ApplyOwnership(EntityOwnershipDto ownership)
    {
        // 自动映射：OwnerId / FactionRelation / AccessLevel / ColorPacked
        var state = ownership.ToRemoteOwnershipState();
        // 可空 GuildId 与派生 HasGuildId 仍由调用方处理
        state.GuildId = ownership.GuildId ?? System.Guid.Empty;
        state.HasGuildId = ownership.GuildId.HasValue ? (byte)1 : (byte)0;

        if (_networkToEcs.TryGetValue(ownership.EntityNetworkId, out var ecsEntityId))
        {
            var entity = _store.GetEntityById(ecsEntityId);
            if (!entity.IsNull)
            {
                entity.AddComponent(state);
                return;
            }
        }

        _pendingNetworkOwnership[ownership.EntityNetworkId] = state;
    }

    public void ApplyOwnership(int snapshotEntityId, System.Guid ownerId, byte accessLevel = 2)
    {
        var state = new RemoteOwnershipState
        {
            OwnerId = ownerId,
            GuildId = System.Guid.Empty,
            HasGuildId = 0,
            FactionRelation = 0,
            AccessLevel = accessLevel,
            ColorPacked = 0,
        };

        if (_snapshotToEcs.TryGetValue(snapshotEntityId, out var ecsEntityId))
        {
            var entity = _store.GetEntityById(ecsEntityId);
            if (!entity.IsNull)
            {
                entity.AddComponent(state);
                return;
            }
        }

        _pendingSnapshotOwnership[snapshotEntityId] = state;
    }

    private Entity ResolveOrCreateEntity(RemoteEntity remote, bool isLocalPlayer)
    {
        if (isLocalPlayer)
        {
            var localEntity = _store.GetEntityById(LocalPresentationEntityId);
            if (!localEntity.IsNull)
                RegisterMappings(remote, localEntity.Id);
            return localEntity;
        }

        if (_snapshotToEcs.TryGetValue(remote.Id, out var ecsEntityId))
        {
            var entity = _store.GetEntityById(ecsEntityId);
            if (!entity.IsNull)
                return entity;
        }

        var created = _store.CreateEntity();
        RegisterMappings(remote, created.Id);
        created.AddTag<ServerAuthority>();
        ApplyTypeTags(created, remote.Type, false);
        return created;
    }

    private void RegisterMappings(RemoteEntity remote, int ecsEntityId)
    {
        _snapshotToEcs[remote.Id] = ecsEntityId;
        _ecsToSnapshot[ecsEntityId] = remote.Id;
        _networkToEcs[remote.NetworkId] = ecsEntityId;
    }

    private static void ApplyTypeTags(Entity entity, EntityType type, bool isLocalPlayer)
    {
        switch (type)
        {
            case EntityType.Player:
            case EntityType.RemotePlayer:
                if (isLocalPlayer)
                    entity.AddTag<LocalPlayer>();
                else
                    entity.AddTag<RemotePlayer>();
                entity.AddTag<Friendly>();
                break;
            case EntityType.Npc:
                entity.AddTag<Npc>();
                entity.AddTag<Neutral>();
                break;
            case EntityType.Monster:
                entity.AddTag<Monster>();
                entity.AddTag<Hostile>();
                break;
            case EntityType.Building:
                entity.AddTag<BuildingTag>();
                break;
            case EntityType.Vehicle:
                entity.AddTag<VehicleTag>();
                break;
            case EntityType.Spacecraft:
                entity.AddTag<SpacecraftTag>();
                break;
            case EntityType.Projectile:
                entity.AddTag<Projectile>();
                break;
        }
    }

    private void SyncEntity(Entity entity, RemoteEntity remote, bool isLocalPlayer)
    {
        WorldPosition previousPos;
        if (entity.TryGetComponent<WorldPosition>(out var currentPos))
            previousPos = currentPos;
        else
            previousPos = new WorldPosition { X = remote.PreviousPosition.X, Y = remote.PreviousPosition.Y, Z = remote.PreviousPosition.Z };

        entity.AddComponent(new WorldPosition
        {
            X = remote.Position.X,
            Y = remote.Position.Y,
            Z = remote.Position.Z,
        });

        entity.AddComponent(new WorldRotation
        {
            X = remote.Rotation.X,
            Y = remote.Rotation.Y,
            Z = remote.Rotation.Z,
            W = remote.Rotation.W,
        });

        entity.AddComponent(new Velocity
        {
            X = remote.Velocity.X,
            Y = remote.Velocity.Y,
            Z = remote.Velocity.Z,
            Speed = MathF.Sqrt(remote.Velocity.X * remote.Velocity.X + remote.Velocity.Z * remote.Velocity.Z),
        });

        if (entity.TryGetComponent<Health>(out var health))
        {
            float previousHealth = health.Current;
            remote.ApplyToEcs(ref health);
            entity.AddComponent(health);
            UpdatePresentationFeedback(entity, previousHealth, remote.Health, remote.MaxHealth);
        }
        else
        {
            health = default;
            remote.ApplyToEcs(ref health);
            entity.AddComponent(health);
            UpdatePresentationFeedback(entity, remote.Health, remote.Health, remote.MaxHealth);
        }

        var entityState = default(RemoteEntityState);
        remote.ApplyToEcs(ref entityState);
        entityState.IsLocalPlayer = isLocalPlayer ? (byte)1 : (byte)0;
        entity.AddComponent(entityState);

        entity.AddComponent(new RemoteSnapshotState
        {
            PreviousX = previousPos.X,
            PreviousY = previousPos.Y,
            PreviousZ = previousPos.Z,
            LastServerTime = _remoteWorld.GetWorldTime(),
            LastSnapshotTick = _remoteWorld.LastSnapshotTick,
        });

        if (_pendingSnapshotOwnership.TryGetValue(remote.Id, out var snapshotOwnership))
        {
            entity.AddComponent(snapshotOwnership);
            _pendingSnapshotOwnership.Remove(remote.Id);
        }

        if (_pendingNetworkOwnership.TryGetValue(remote.NetworkId, out var networkOwnership))
        {
            entity.AddComponent(networkOwnership);
            _pendingNetworkOwnership.Remove(remote.NetworkId);
        }

        ApplyWeaponSnapshot(entity, remote);
        ApplySeatSnapshot(entity, remote);
        ApplyVehicleRuntimeSnapshot(entity, remote);
        ApplySpacecraftSnapshot(entity, remote);

        if (remote.Type == EntityType.Building)
        {
            var building = default(Building);
            remote.ApplyToEcs(ref building);
            entity.AddComponent(building);

            var damageState = default(BuildingDamageState);
            remote.ApplyToEcs(ref damageState);
            entity.AddComponent(damageState);

            entity.AddComponent(new BuildingDamageInstanceState
            {
                Cluster0X = remote.BuildingDamageCluster0.X,
                Cluster0Y = remote.BuildingDamageCluster0.Y,
                Cluster0Z = remote.BuildingDamageCluster0.Z,
                Cluster0Strength = remote.BuildingDamageCluster0.W,
                Cluster0Age = remote.BuildingDamageCluster0Age,
                Cluster0RepairFill = remote.BuildingDamageCluster0RepairFill,
                Cluster1X = remote.BuildingDamageCluster1.X,
                Cluster1Y = remote.BuildingDamageCluster1.Y,
                Cluster1Z = remote.BuildingDamageCluster1.Z,
                Cluster1Strength = remote.BuildingDamageCluster1.W,
                Cluster1Age = remote.BuildingDamageCluster1Age,
                Cluster1RepairFill = remote.BuildingDamageCluster1RepairFill,
                Cluster2X = remote.BuildingDamageCluster2.X,
                Cluster2Y = remote.BuildingDamageCluster2.Y,
                Cluster2Z = remote.BuildingDamageCluster2.Z,
                Cluster2Strength = remote.BuildingDamageCluster2.W,
                Cluster2Age = remote.BuildingDamageCluster2Age,
                Cluster2RepairFill = remote.BuildingDamageCluster2RepairFill,
                PackedLayerState = remote.BuildingDamageLayerState,
            });
        }

        if (remote.Type == EntityType.Vehicle || remote.Type == EntityType.Spacecraft)
        {
            entity.TryGetComponent<VehicleState>(out var vehicleState);
            remote.ApplyToEcs(ref vehicleState);
            entity.AddComponent(vehicleState);

            entity.AddComponent(new TurretState
            {
                Yaw = remote.TurretYaw,
                Pitch = remote.TurretPitch,
                YawSpeed = 0f,
                PitchSpeed = 0f,
                MinPitch = -1.2f,
                MaxPitch = 1.0f,
                WeaponDefId = remote.WeaponDefId,
            });
        }
        else if (remote.AttachedVehicleEntityId > 0 && remote.HasMountedWeapon)
        {
            entity.AddComponent(new TurretState
            {
                Yaw = remote.TurretYaw,
                Pitch = remote.TurretPitch,
                YawSpeed = 0f,
                PitchSpeed = 0f,
                MinPitch = -1.2f,
                MaxPitch = 1.0f,
                WeaponDefId = remote.WeaponDefId,
            });

            entity.AddComponent(new MountedWeaponRuntimeState
            {
                Heat = remote.MountedWeaponHeat,
                FireCycleRemaining = remote.MountedWeaponCycleRemaining,
                FireCycleNormalized = remote.MountedWeaponCycleRemaining > 0f && remote.WeaponDefId > 0
                    ? Math.Clamp(remote.MountedWeaponCycleRemaining / 1.5f, 0f, 1f)
                    : 0f,
                ReloadRemaining = remote.MountedWeaponReloadRemaining,
                ReloadNormalized = remote.MountedWeaponReloadRemaining > 0f && remote.IsReloading
                    ? Math.Clamp(remote.MountedWeaponReloadRemaining / 8f, 0f, 1f)
                    : 0f,
                FaultRemaining = remote.MountedWeaponFaultRemaining,
                MaintenanceRemaining = remote.MountedWeaponMaintenanceRemaining,
                MaintenanceLevel = remote.MountedWeaponMaintenanceLevel,
                OperationProgress = remote.MountedWeaponOperationProgress,
                SkillScalar = remote.MountedWeaponSkillScalar,
                IsOverheated = remote.MountedWeaponHeat >= 0.98f ? (byte)1 : (byte)0,
                IsReloading = remote.IsReloading ? (byte)1 : (byte)0,
                FaultCode = remote.MountedWeaponFaultCode,
                IsMaintaining = remote.MountedWeaponMaintenanceRemaining > 0f ? (byte)1 : (byte)0,
                RepairStep = remote.MountedWeaponRepairStep,
                RepairStepCount = remote.MountedWeaponRepairStepCount,
                MaterialUnits = remote.MountedWeaponMaterialUnits,
            });
        }
        else
        {
            entity.RemoveComponent<TurretState>();
            entity.RemoveComponent<MountedWeaponRuntimeState>();
        }

        if (remote.IsAlive)
            entity.RemoveTag<Dead>();
        else
            entity.AddTag<Dead>();

        if (isLocalPlayer)
            entity.AddTag<LocalPlayer>();
        else
            entity.AddTag<ServerAuthority>();

        UpdateAnimationState(entity);
    }

    private static void UpdatePresentationFeedback(Entity entity, float previousHealth, float currentHealth, float maxHealth)
    {
        RemotePresentationFeedbackState feedback = entity.TryGetComponent<RemotePresentationFeedbackState>(out var existing)
            ? existing
            : new RemotePresentationFeedbackState { LastKnownHealth = previousHealth };

        if (currentHealth + 0.01f < previousHealth)
        {
            feedback.HitReactionTimer = 0.32f;
            feedback.HitReactionStrength = maxHealth > 0f
                ? Math.Clamp((previousHealth - currentHealth) / maxHealth, 0.18f, 1f)
                : 0.18f;
            if (feedback.HitZone == 0)
                feedback.HitZone = 2;
        }

        feedback.LastKnownHealth = currentHealth;
        entity.AddComponent(feedback);
    }

    private static void UpdateAnimationState(Entity entity)
    {
        RemoteAnimationState animation = entity.TryGetComponent<RemoteAnimationState>(out var existing)
            ? existing
            : new RemoteAnimationState();

        byte nextState = 0;
        if (entity.TryGetComponent<RemotePresentationFeedbackState>(out var feedback) && feedback.HitReactionTimer > 0.01f)
            nextState = 4;
        else if (entity.TryGetComponent<MountedWeaponRuntimeState>(out var maintenanceRuntime) && maintenanceRuntime.IsMaintaining != 0)
            nextState = 9;
        else if ((entity.TryGetComponent<MountedWeaponRuntimeState>(out var mountedRuntime) && mountedRuntime.IsReloading != 0)
                 || (entity.TryGetComponent<RemoteCombatState>(out var remoteCombat) && remoteCombat.IsReloading != 0)
                 || (entity.TryGetComponent<WeaponState>(out var weaponState) && weaponState.IsReloading != 0))
            nextState = 3;
        else if (entity.TryGetComponent<RemotePresentationFeedbackState>(out feedback) && feedback.RecoilTimer > 0.01f)
            nextState = 2;
        else if ((entity.TryGetComponent<TurretState>(out var aimTurret) && (MathF.Abs(aimTurret.Yaw) > 0.02f || MathF.Abs(aimTurret.Pitch) > 0.02f))
                 || (entity.TryGetComponent<VehicleSeat>(out var seat) && seat.SeatType == (byte)SeatType.Gunner))
            nextState = 1;

        bool inSeat = entity.TryGetComponent<VehicleSeat>(out _);
        float previousSeatBlend = animation.SeatBlend;
        float speed = entity.TryGetComponent<Velocity>(out var velocity) ? velocity.Speed : 0f;
        animation.LocomotionState = speed > 3.5f ? (byte)2 : speed > 0.15f ? (byte)1 : (byte)0;
        animation.SeatBlend = Math.Clamp(animation.SeatBlend + (inSeat ? 0.2f : -0.2f), 0f, 1f);
        animation.TransitionState = inSeat && previousSeatBlend < 0.99f
            ? (byte)1
            : !inSeat && previousSeatBlend > 0.01f
                ? (byte)2
                : (byte)0;
        if (animation.TransitionState == 1)
            nextState = 5;
        else if (animation.TransitionState == 2)
            nextState = 6;
        else if (!inSeat)
            nextState = animation.LocomotionState == 2 ? (byte)8 : animation.LocomotionState == 1 ? (byte)7 : nextState;

        animation.StateTime = animation.State == nextState ? animation.StateTime + 0.1f : 0f;
        animation.State = nextState;
        animation.Blend = nextState == 0 ? 0.35f : 1f;
        float aimMetric = 0f;
        if (entity.TryGetComponent<TurretState>(out var turretState))
            aimMetric = MathF.Min(1f, MathF.Abs(turretState.Yaw) * 0.35f + MathF.Abs(turretState.Pitch) * 0.45f);
        if (nextState == 2 || nextState == 3)
            aimMetric = MathF.Max(aimMetric, 0.8f);
        animation.AimBlend = aimMetric;
        animation.PackedGraphState = (uint)(animation.State
            | (animation.LocomotionState << 8)
            | (animation.TransitionState << 16));
        animation.PackedBlendState = (uint)(Math.Clamp((int)MathF.Round(animation.AimBlend * 255f), 0, 255)
            | (Math.Clamp((int)MathF.Round(animation.SeatBlend * 255f), 0, 255) << 8)
            | (Math.Clamp((int)MathF.Round(animation.Blend * 255f), 0, 255) << 16));
        animation.ResourceFragmentId = nextState switch
        {
            3 => 30,
            9 => 30,
            5 => 50,
            6 => 60,
            7 => 70,
            8 => 80,
            _ => nextState * 10,
        } + (inSeat ? 1 : 0);
        animation.NetworkBudgetBytes = 8 + (animation.AimBlend > 0.4f ? 4 : 0) + (animation.LocomotionState > 0 ? 2 : 0) + (animation.TransitionState > 0 ? 2 : 0);
        entity.AddComponent(animation);
    }

    private static void ApplyWeaponSnapshot(Entity entity, RemoteEntity remote)
    {
        if (remote.WeaponDefId <= 0)
        {
            entity.RemoveComponent<WeaponState>();
            entity.RemoveComponent<AmmoState>();
            entity.RemoveComponent<RemoteCombatState>();
            entity.RemoveTag<Reloading>();
            return;
        }

        entity.AddComponent(new WeaponState
        {
            WeaponDefId = remote.WeaponDefId,
            Category = remote.WeaponCategory,
            SlotIndex = 0,
            IsFiring = 0,
            IsReloading = remote.IsReloading ? (byte)1 : (byte)0,
            LastFireTime = 0f,
            ReloadTimer = 0f,
            BurstCount = 0,
        });

        entity.AddComponent(new AmmoState
        {
            CurrentMag = remote.CurrentMag,
            MagCapacity = remote.MagCapacity,
            ReserveAmmo = remote.ReserveAmmo,
            MaxReserve = remote.MaxReserve,
        });

        // 自动映射：WeaponDefId / CurrentMag / MagCapacity / ReserveAmmo / MaxReserve
        // / WeaponCategory / IsReloading (bool→byte)
        var combatState = remote.ToRemoteCombatState();
        // 不在 DTO 中的面向及有武器标记手动补
        combatState.AimTargetId = 0;
        combatState.HasWeapon = 1;
        entity.AddComponent(combatState);

        if (remote.IsReloading)
            entity.AddTag<Reloading>();
        else
            entity.RemoveTag<Reloading>();
    }

    private static void ApplySeatSnapshot(Entity entity, RemoteEntity remote)
    {
        if (remote.AttachedVehicleEntityId <= 0)
        {
            entity.RemoveComponent<VehicleSeat>();
            entity.RemoveComponent<RemoteVehicleOccupantState>();
            entity.RemoveTag<InVehicle>();
            entity.RemoveTag<IsDriver>();
            entity.RemoveTag<IsGunner>();
            entity.RemoveTag<IsPassenger>();
            return;
        }

        entity.AddComponent(new VehicleSeat
        {
            VehicleEntityId = remote.AttachedVehicleEntityId,
            SeatIndex = remote.SeatIndex,
            SeatType = remote.SeatType,
            OffsetX = remote.SeatOffset.X,
            OffsetY = remote.SeatOffset.Y,
            OffsetZ = remote.SeatOffset.Z,
        });

        entity.AddTag<InVehicle>();
        entity.RemoveTag<IsDriver>();
        entity.RemoveTag<IsGunner>();
        entity.RemoveTag<IsPassenger>();

        switch ((SeatType)remote.SeatType)
        {
            case SeatType.Driver:
                entity.AddTag<IsDriver>();
                break;
            case SeatType.Gunner:
                entity.AddTag<IsGunner>();
                break;
            default:
                entity.AddTag<IsPassenger>();
                break;
        }

        // 自动映射：AttachedVehicleEntityId→SnapshotVehicleEntityId
        // / SeatIndex→CurrentSeatIndex / SeatType→CurrentSeatType / HasMountedWeapon (bool→byte)
        entity.AddComponent(remote.ToRemoteVehicleOccupantState());
    }

    private static void ApplyVehicleRuntimeSnapshot(Entity entity, RemoteEntity remote)
    {
        if (remote.Type != EntityType.Vehicle)
            return;

        // 自动映射：FuelPercent / SeatCount / OccupiedSeatCount
        var runtime = remote.ToRemoteVehicleRuntimeState();
        // HealthPercent 需从 Health/MaxHealth 派生，保留手算
        runtime.HealthPercent = remote.MaxHealth > 0f
            ? remote.Health / remote.MaxHealth * 100f
            : 0f;
        entity.AddComponent(runtime);
    }

    private static void ApplySpacecraftSnapshot(Entity entity, RemoteEntity remote)
    {
        if (remote.Type != EntityType.Spacecraft)
            return;

        // 自动映射：Altitude / OrbitalVelocity / RemainingDeltaV / FuelPercent / SpaceFlightPhase→FlightPhase
        entity.AddComponent(remote.ToRemoteSpacecraftState());
    }

    private void SyncAttachedVehicleRuntimeState()
    {
        var query = _store.Query<VehicleSeat, RemoteVehicleOccupantState>();
        foreach (var chunk in query.Chunks)
        {
            var seats = chunk.Chunk1;
            var runtimes = chunk.Chunk2;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var seat = ref seats.Span[i];
                ref var runtime = ref runtimes.Span[i];
                if (!_snapshotToEcs.TryGetValue(seat.VehicleEntityId, out var vehicleEcsId))
                    continue;

                var vehicleEntity = _store.GetEntityById(vehicleEcsId);
                if (vehicleEntity.IsNull || !vehicleEntity.TryGetComponent<RemoteVehicleRuntimeState>(out var vehicleRuntime))
                    continue;

                runtime.SnapshotVehicleEntityId = seat.VehicleEntityId;
            }
        }
    }
}
