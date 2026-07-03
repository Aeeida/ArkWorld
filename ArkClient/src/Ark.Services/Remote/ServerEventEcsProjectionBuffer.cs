using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Ark.Ecs.Components;
using Ark.Services;
using Ark.Services.Remote.EcsMappers;
using Ark.Shared.Data;
using Friflo.Engine.ECS;
using Game.Shared.Core.DTOs;
using Game.Shared.Core.DTOs.EcsMappers;

namespace Ark.Services.Remote;

/// <summary>
/// 将服务端推送缓存为线程安全队列，并在主线程刷入 ECS。
/// </summary>
public sealed class ServerEventEcsProjectionBuffer
{
    private readonly EntityStore _store;
    private readonly RemoteWorldEcsCacheSystem _remoteWorldEcsCache;
    private readonly NetworkVisualEventBuffer? _visualEvents;
    private readonly ConcurrentQueue<WorldEnvironmentDto> _worldEnvironments = new();
    private readonly ConcurrentQueue<WeatherDto> _weatherUpdates = new();
    private readonly ConcurrentQueue<TimeOfDayUpdate> _timeOfDayUpdates = new();
    private readonly ConcurrentQueue<TerrainModificationBatch> _terrainUpdates = new();
    private readonly ConcurrentQueue<InventoryDto> _inventoryUpdates = new();
    private readonly ConcurrentQueue<QuestSnapshotUpdate> _questSnapshots = new();
    private readonly ConcurrentQueue<PartyInfoDto> _partyUpdates = new();
    private readonly ConcurrentQueue<NearbyEntitiesDto> _nearbyUpdates = new();
    private readonly ConcurrentQueue<VehicleStateDto> _vehicleStates = new();
    private readonly ConcurrentQueue<VehicleSeatUpdate> _vehicleSeatUpdates = new();
    private readonly ConcurrentQueue<SpaceFlightStateDto> _spaceFlightStates = new();
    private readonly ConcurrentQueue<SpacePhaseUpdate> _spacePhases = new();
    private readonly ConcurrentQueue<FireWeaponResultDto> _weaponFires = new();
    private readonly ConcurrentQueue<ReloadWeaponResultDto> _reloads = new();
    private readonly ConcurrentQueue<AttackResultDto> _damageReceived = new();
    private readonly ConcurrentQueue<BuildingPlacedByPlayerUpdate> _buildingPlacements = new();
    private readonly ConcurrentQueue<SpawnVehicleResultDto> _vehicleSpawns = new();
    private readonly ConcurrentQueue<AssembleRocketResultDto> _rocketAssemblies = new();
    private readonly ConcurrentQueue<LaunchRocketResultDto> _rocketLaunches = new();
    private readonly ConcurrentQueue<EntityOwnershipDto> _ownershipUpdates = new();

    public ServerEventEcsProjectionBuffer(EntityStore store, RemoteWorldEcsCacheSystem remoteWorldEcsCache, NetworkVisualEventBuffer? visualEvents)
    {
        _store = store;
        _remoteWorldEcsCache = remoteWorldEcsCache;
        _visualEvents = visualEvents;
    }

    public void EnqueueWorldEnvironment(WorldEnvironmentDto environment) => _worldEnvironments.Enqueue(environment);
    public void EnqueueWeather(WeatherDto weather) => _weatherUpdates.Enqueue(weather);
    public void EnqueueTimeOfDay(float timeOfDay, float timeScale) => _timeOfDayUpdates.Enqueue(new TimeOfDayUpdate(timeOfDay, timeScale));
    public void EnqueueTerrainModifications(IReadOnlyList<TerrainModificationDto> modifications) => _terrainUpdates.Enqueue(new TerrainModificationBatch(modifications));
    public void EnqueueInventory(InventoryDto inventory) => _inventoryUpdates.Enqueue(inventory);
    public void EnqueueQuestList(IReadOnlyList<QuestDto> activeQuests) => _questSnapshots.Enqueue(CreateQuestSnapshot(activeQuests, 0));
    public void EnqueueQuestProgress(QuestProgressDto progress) => _questSnapshots.Enqueue(CreateQuestSnapshot(progress.ActiveQuests, progress.CompletedCount));
    public void EnqueuePartyInfo(PartyInfoDto partyInfo) => _partyUpdates.Enqueue(partyInfo);
    public void EnqueueNearbyEntities(NearbyEntitiesDto nearbyEntities) => _nearbyUpdates.Enqueue(nearbyEntities);
    public void EnqueueVehicleState(VehicleStateDto vehicleState) => _vehicleStates.Enqueue(vehicleState);
    public void EnqueueVehicleEntered(System.Guid playerId, System.Guid vehicleNetworkId, int seatIndex) => _vehicleSeatUpdates.Enqueue(new VehicleSeatUpdate(playerId, vehicleNetworkId, seatIndex, true));
    public void EnqueueVehicleExited(System.Guid playerId) => _vehicleSeatUpdates.Enqueue(new VehicleSeatUpdate(playerId, System.Guid.Empty, -1, false));
    public void EnqueueSpaceFlightState(SpaceFlightStateDto flightState) => _spaceFlightStates.Enqueue(flightState);
    public void EnqueueSpacePhase(System.Guid playerId, string phase) => _spacePhases.Enqueue(new SpacePhaseUpdate(playerId, phase));

    public void EnqueueWeaponFired(FireWeaponResultDto result)
    {
        _weaponFires.Enqueue(result);
        if (!result.Success)
            return;

        _visualEvents?.EnqueueWeaponFire(result.ShooterEntityId, 0);
        if (result.HitEntityId.HasValue)
        {
            _visualEvents?.EnqueueHit(new System.Numerics.Vector3(
                (float)result.HitX,
                (float)result.HitY,
                (float)result.HitZ));
        }
    }

    public void EnqueueReloadCompleted(ReloadWeaponResultDto result) => _reloads.Enqueue(result);
    public void EnqueueDamageReceived(AttackResultDto result) => _damageReceived.Enqueue(result);

    public void EnqueueProjectile(ProjectileEventDto projectile)
    {
        _visualEvents?.EnqueueWeaponFire(
            projectile.ShooterEntityId,
            projectile.WeaponDefId,
            new System.Numerics.Vector3((float)projectile.OriginX, (float)projectile.OriginY, (float)projectile.OriginZ),
            new System.Numerics.Vector3((float)projectile.DirX, (float)projectile.DirY, (float)projectile.DirZ));
    }

    public void EnqueueBuildingPlacedByPlayer(System.Guid ownerId, PlaceBuildingResultDto result) => _buildingPlacements.Enqueue(new BuildingPlacedByPlayerUpdate(ownerId, result));
    public void EnqueueVehicleSpawned(SpawnVehicleResultDto result) => _vehicleSpawns.Enqueue(result);
    public void EnqueueRocketAssembled(AssembleRocketResultDto result) => _rocketAssemblies.Enqueue(result);
    public void EnqueueRocketLaunched(LaunchRocketResultDto result) => _rocketLaunches.Enqueue(result);
    public void EnqueueOwnership(EntityOwnershipDto ownership) => _ownershipUpdates.Enqueue(ownership);

    public void FlushToEcs()
    {
        var localEntity = ResolveLocalEntity();
        FlushWorldServiceState(localEntity);
        FlushInventoryState(localEntity);
        FlushQuestState(localEntity);
        FlushCombatState(localEntity);
        FlushVehicleState(localEntity);
        FlushSpaceState(localEntity);
        FlushAuthorityEvents(localEntity);
        FlushOwnership();
    }

    private void FlushInventoryState(Entity localEntity)
    {
        if (localEntity.IsNull)
            return;

        bool changed = false;
        var state = localEntity.TryGetComponent<RemoteInventoryState>(out var existing)
            ? existing
            : default;

        while (_inventoryUpdates.TryDequeue(out var inventory))
        {
            // 自动映射：MaxSlots → SlotCount, UsedSlots → OccupiedSlotCount
            inventory.ApplyToEcs(ref state);
            // 派生字段需手算（DTO 没有直接对应）
            state.TotalItemCount = CountInventoryItems(inventory);
            state.DistinctItemCount = inventory.Items.Count;
            changed = true;
        }

        if (changed)
            localEntity.AddComponent(state);
    }

    private void FlushQuestState(Entity localEntity)
    {
        if (localEntity.IsNull)
            return;

        bool changed = false;
        var state = localEntity.TryGetComponent<RemoteQuestState>(out var existing)
            ? existing
            : default;

        while (_questSnapshots.TryDequeue(out var snapshot))
        {
            state.ActiveQuestCount = snapshot.ActiveQuestCount;
            state.AvailableQuestCount = snapshot.AvailableQuestCount;
            state.CompletedQuestCount = snapshot.CompletedQuestCount;
            state.ObjectiveCount = snapshot.ObjectiveCount;
            changed = true;
        }

        if (changed)
            localEntity.AddComponent(state);
    }

    private Entity ResolveLocalEntity()
    {
        int localEntityId = _remoteWorldEcsCache.LocalPresentationEntityId;
        return localEntityId > 0 ? _store.GetEntityById(localEntityId) : default;
    }

    private void FlushWorldServiceState(Entity localEntity)
    {
        if (localEntity.IsNull)
            return;

        bool changed = false;
        var state = localEntity.TryGetComponent<RemoteWorldServiceState>(out var existing)
            ? existing
            : default;

        while (_worldEnvironments.TryDequeue(out var environment))
        {
            state.TerrainSeed = environment.TerrainSeed;
            state.LocationId = environment.LocationId;
            state.SolarSystemId = environment.SolarSystemId;
            state.TimeOfDay = environment.TimeOfDay;
            state.TimeScale = environment.TimeScale;
            // 嵌套 Weather 通过生成的扩展应用到同一组件
            environment.Weather.ApplyToEcs(ref state);
            state.TerrainModificationCount = environment.TerrainModifications.Count;
            state.HasWorldEnvironment = 1;
            state.HasWeather = 1;
            changed = true;
        }

        while (_weatherUpdates.TryDequeue(out var weather))
        {
            weather.ApplyToEcs(ref state);
            state.HasWeather = 1;
            changed = true;
        }

        while (_timeOfDayUpdates.TryDequeue(out var timeOfDay))
        {
            state.TimeOfDay = timeOfDay.TimeOfDay;
            state.TimeScale = timeOfDay.TimeScale;
            changed = true;
        }

        while (_terrainUpdates.TryDequeue(out var terrain))
        {
            state.TerrainModificationCount = terrain.Modifications.Count;
            changed = true;
        }

        while (_partyUpdates.TryDequeue(out var party))
        {
            state.PartyMemberCount = party.Members.Count;
            state.HasPartyInfo = 1;
            changed = true;
        }

        while (_nearbyUpdates.TryDequeue(out var nearby))
        {
            state.NearbyEntityCount = nearby.Entities.Count;
            state.NearbyQueryRadius = nearby.QueryRadius;
            state.HasNearbyEntities = 1;
            changed = true;
        }

        if (changed)
            localEntity.AddComponent(state);
    }

    private void FlushCombatState(Entity localEntity)
    {
        if (localEntity.IsNull)
            return;

        bool changed = false;
        var state = localEntity.TryGetComponent<RemoteCombatState>(out var existing)
            ? existing
            : default;

        while (_weaponFires.TryDequeue(out var result))
        {
            if (!result.Success)
                continue;

            state.CurrentMag = result.AmmoRemaining;
            state.IsReloading = 0;
            state.HasWeapon = state.WeaponDefId > 0 ? (byte)1 : state.HasWeapon;
            changed = true;
        }

        while (_reloads.TryDequeue(out var reload))
        {
            state.IsReloading = 0;
            if (reload.Success)
            {
                state.CurrentMag = reload.CurrentMag;
                state.ReserveAmmo = reload.ReserveAmmo;
                state.MaxReserve = Math.Max(state.MaxReserve, reload.ReserveAmmo);
            }
            changed = true;
        }

        while (_damageReceived.TryDequeue(out var damage))
        {
            if (localEntity.TryGetComponent<RemotePresentationFeedbackState>(out var feedback))
            {
                feedback.HitReactionTimer = 0.3f;
                feedback.HitReactionStrength = MathF.Max(feedback.HitReactionStrength, (float)damage.DamageDealt);
                feedback.LastKnownHealth = MathF.Max(0f, feedback.LastKnownHealth - (float)damage.DamageDealt);
                localEntity.AddComponent(feedback);
            }
        }

        if (changed)
            localEntity.AddComponent(state);
    }

    private void FlushVehicleState(Entity localEntity)
    {
        if (localEntity.IsNull)
            return;

        bool changed = false;
        var state = localEntity.TryGetComponent<RemoteVehicleOccupantState>(out var existing)
            ? existing
            : default;

        while (_vehicleStates.TryDequeue(out var vehicleState))
        {
            if (!TryResolveSnapshotEntityId(vehicleState.VehicleEntityId, out var snapshotVehicleId))
            {
                ServiceLog.Error($"[VehicleDebug][Projection] skip vehicleState networkVehicle={vehicleState.VehicleEntityId} reason=snapshot_mapping_missing currentSnapshot={state.SnapshotVehicleEntityId}");
                continue;
            }

            bool isControlledVehicle = state.SnapshotVehicleEntityId > 0
                                      && snapshotVehicleId == state.SnapshotVehicleEntityId;
            if (!isControlledVehicle)
            {
                ServiceLog.Info($"[VehicleDebug][Projection] ignore vehicleState networkVehicle={vehicleState.VehicleEntityId} snapshotVehicle={snapshotVehicleId} reason=not_local_controlled_vehicle currentSnapshot={state.SnapshotVehicleEntityId}");
                continue;
            }

            state.CurrentSeatType = ResolveCurrentSeatType(vehicleState, state.CurrentSeatIndex);
            state.HasMountedWeapon = state.CurrentSeatType == 1 ? (byte)1 : (byte)0;
            changed = true;

            ServiceLog.Info($"[VehicleDebug][Projection] vehicleState applied networkVehicle={vehicleState.VehicleEntityId} snapshotVehicle={state.SnapshotVehicleEntityId} seatType={state.CurrentSeatType}");
        }

        while (_vehicleSeatUpdates.TryDequeue(out var seatUpdate))
        {
            if (seatUpdate.PlayerId != GameServices.RemotePlayerId)
            {
                ServiceLog.Info($"[VehicleDebug][Projection] ignore seatUpdate player={seatUpdate.PlayerId} localPlayer={GameServices.RemotePlayerId} entering={seatUpdate.IsEntering} networkVehicle={seatUpdate.VehicleNetworkId} seat={seatUpdate.SeatIndex}");
                continue;
            }

            if (!seatUpdate.IsEntering)
            {
                state.SnapshotVehicleEntityId = 0;
                state.CurrentSeatIndex = -1;
                state.CurrentSeatType = 0;
                state.HasMountedWeapon = 0;
                changed = true;
                ServiceLog.Info("[VehicleDebug][Projection] local vehicle exit applied");
                continue;
            }

            if (TryResolveSnapshotEntityId(seatUpdate.VehicleNetworkId, out var snapshotVehicleId))
            {
                state.SnapshotVehicleEntityId = snapshotVehicleId;
                ServiceLog.Info($"[VehicleDebug][Projection] local vehicle enter mapped networkVehicle={seatUpdate.VehicleNetworkId} snapshotVehicle={snapshotVehicleId} seat={seatUpdate.SeatIndex}");
            }
            else
            {
                ServiceLog.Error($"[VehicleDebug][Projection] local vehicle enter mapping missing networkVehicle={seatUpdate.VehicleNetworkId} seat={seatUpdate.SeatIndex}");
            }

            state.CurrentSeatIndex = seatUpdate.SeatIndex;
            changed = true;
        }

        if (changed)
        {
            localEntity.AddComponent(state);
            ServiceLog.Info($"[VehicleDebug][Projection] runtime state committed snapshotVehicle={state.SnapshotVehicleEntityId} seat={state.CurrentSeatIndex} seatType={state.CurrentSeatType}");
        }
    }

    private void FlushSpaceState(Entity localEntity)
    {
        bool runtimeChanged = false;
        bool controlChanged = false;
        var runtime = !localEntity.IsNull && localEntity.TryGetComponent<RemoteSpacecraftState>(out var runtimeExisting)
            ? runtimeExisting
            : default;
        var control = !localEntity.IsNull && localEntity.TryGetComponent<RemoteRocketControlState>(out var controlExisting)
            ? controlExisting
            : default;

        while (_spaceFlightStates.TryDequeue(out var flightState))
        {
            // 自动映射：Altitude / OrbitalVelocity / RemainingDeltaV
            flightState.ApplyToEcs(ref runtime);
            // 需要单位换算 / 类型转换的字段保留手动
            runtime.FuelPercent = flightState.FuelPercent / 100f;
            if (Enum.TryParse<SpaceFlightPhase>(flightState.Phase, out var phase))
                runtime.FlightPhase = (byte)phase;
            runtimeChanged = true;

            if (TryResolveSnapshotEntityId(flightState.SpacecraftId, out var snapshotSpacecraftId))
            {
                control.SnapshotSpacecraftEntityId = snapshotSpacecraftId;
                controlChanged = true;
            }
        }

        while (_spacePhases.TryDequeue(out var phaseUpdate))
        {
            if (phaseUpdate.PlayerId != GameServices.RemotePlayerId)
                continue;

            if (Enum.TryParse<SpaceFlightPhase>(phaseUpdate.Phase, out var phase))
            {
                runtime.FlightPhase = (byte)phase;
                runtimeChanged = true;
            }
        }

        if (control.ActiveRocketNetworkId != System.Guid.Empty
            && control.SnapshotSpacecraftEntityId == 0
            && TryResolveSnapshotEntityId(control.ActiveRocketNetworkId, out var resolvedSpacecraftId))
        {
            control.SnapshotSpacecraftEntityId = resolvedSpacecraftId;
            controlChanged = true;
        }

        while (_rocketAssemblies.TryDequeue(out var assembled))
        {
            _store.CreateEntity().AddComponent(new RemoteRocketAssemblyResultEvent
            {
                RocketEntityId = assembled.RocketEntityId ?? System.Guid.Empty,
                Success = assembled.Success ? (byte)1 : (byte)0,
            });

            if (!assembled.Success || assembled.RocketEntityId is null)
                continue;

            control.ActiveRocketNetworkId = assembled.RocketEntityId.Value;
            control.HasActiveRocket = 1;
            controlChanged = true;
        }

        while (_rocketLaunches.TryDequeue(out var launch))
        {
            byte phaseValue = 0;
            byte hasPhase = 0;
            if (!string.IsNullOrWhiteSpace(launch.Phase) && Enum.TryParse<SpaceFlightPhase>(launch.Phase, out var parsedPhase))
            {
                phaseValue = (byte)parsedPhase;
                hasPhase = 1;
                runtime.FlightPhase = phaseValue;
                runtimeChanged = true;
            }

            _store.CreateEntity().AddComponent(new RemoteRocketLaunchResultEvent
            {
                Success = launch.Success ? (byte)1 : (byte)0,
                HasPhase = hasPhase,
                FlightPhase = phaseValue,
            });
        }

        if (runtimeChanged && !localEntity.IsNull)
            localEntity.AddComponent(runtime);
        if (controlChanged && !localEntity.IsNull)
            localEntity.AddComponent(control);
    }

    private void FlushAuthorityEvents(Entity localEntity)
    {
        while (_buildingPlacements.TryDequeue(out var buildingPlacement))
        {
            if (buildingPlacement.Result.Success && buildingPlacement.Result.EntityId.HasValue)
                _remoteWorldEcsCache.ApplyOwnership(buildingPlacement.Result.EntityId.Value, buildingPlacement.OwnerId);

            _store.CreateEntity().AddComponent(new RemoteBuildingPlacementResultEvent
            {
                EntityId = buildingPlacement.Result.EntityId ?? 0,
                NetworkId = buildingPlacement.Result.NetworkId ?? System.Guid.Empty,
                Success = buildingPlacement.Result.Success ? (byte)1 : (byte)0,
            });
        }

        while (_vehicleSpawns.TryDequeue(out var vehicleSpawn))
        {
            _store.CreateEntity().AddComponent(new RemoteVehicleSpawnResultEvent
            {
                VehicleEntityId = vehicleSpawn.VehicleEntityId ?? 0,
                VehicleDefId = vehicleSpawn.VehicleDefId,
                NetworkId = vehicleSpawn.NetworkId ?? System.Guid.Empty,
                Success = vehicleSpawn.Success ? (byte)1 : (byte)0,
            });
        }

        if (!localEntity.IsNull && localEntity.TryGetComponent<RemoteRocketControlState>(out var controlState))
        {
            if (controlState.ActiveRocketNetworkId == System.Guid.Empty && controlState.HasActiveRocket != 0)
            {
                controlState.HasActiveRocket = 0;
                localEntity.AddComponent(controlState);
            }
        }
    }

    private void FlushOwnership()
    {
        while (_ownershipUpdates.TryDequeue(out var ownership))
            _remoteWorldEcsCache.ApplyOwnership(ownership);
    }

    private bool TryResolveSnapshotEntityId(System.Guid networkId, out int snapshotEntityId)
    {
        snapshotEntityId = 0;
        if (!_remoteWorldEcsCache.TryGetEcsEntityId(networkId, out var ecsEntityId))
            return false;

        return _remoteWorldEcsCache.TryGetSnapshotEntityId(ecsEntityId, out snapshotEntityId);
    }

    private static int CountOccupiedSeats(VehicleStateDto vehicleState)
    {
        int occupied = 0;
        foreach (var seat in vehicleState.Seats)
        {
            if (seat.OccupantId.HasValue)
                occupied++;
        }

        return occupied;
    }

    private static int CountInventoryItems(InventoryDto inventory)
    {
        int total = 0;
        foreach (var item in inventory.Items)
            total += item.Quantity;

        return total;
    }

    private static QuestSnapshotUpdate CreateQuestSnapshot(IReadOnlyList<QuestDto> activeQuests, int completedCount)
    {
        int objectiveCount = 0;
        foreach (var quest in activeQuests)
            objectiveCount += quest.Objectives.Count;

        return new QuestSnapshotUpdate(activeQuests.Count, 0, completedCount, objectiveCount);
    }

    private static byte ResolveCurrentSeatType(VehicleStateDto vehicleState, int currentSeatIndex)
    {
        foreach (var seat in vehicleState.Seats)
        {
            if (seat.SeatIndex != currentSeatIndex)
                continue;

            return seat.SeatType switch
            {
                "Driver" => 0,
                "Gunner" => 1,
                _ => 2,
            };
        }

        return 0;
    }

    private readonly record struct TimeOfDayUpdate(float TimeOfDay, float TimeScale);
    private readonly record struct TerrainModificationBatch(IReadOnlyList<TerrainModificationDto> Modifications);
    private readonly record struct QuestSnapshotUpdate(int ActiveQuestCount, int AvailableQuestCount, int CompletedQuestCount, int ObjectiveCount);
    private readonly record struct VehicleSeatUpdate(System.Guid PlayerId, System.Guid VehicleNetworkId, int SeatIndex, bool IsEntering);
    private readonly record struct SpacePhaseUpdate(System.Guid PlayerId, string Phase);
    private readonly record struct BuildingPlacedByPlayerUpdate(System.Guid OwnerId, PlaceBuildingResultDto Result);
}
