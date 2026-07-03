using System.Collections.Generic;
using Ark.Abstractions;
using Ark.Ecs.Components;
using Ark.Services;
using Ark.Shared.Data;
using Friflo.Engine.ECS;

namespace Ark;

/// <summary>
/// ECS-first 网络分发器 —— 统一消费 ECS 命令/请求并路由到远程服务。
/// </summary>
public sealed class NetworkEcsDispatchSystem
{
    private const float PlayerInputDispatchInterval = 0.1f;
    private const float SpacecraftInputDispatchInterval = 0.05f;

    private readonly EntityStore _store;
    private readonly Dictionary<int, ulong> _lastPlayerInputSequence = new();
    private readonly Dictionary<int, ulong> _lastWeaponFireSequence = new();
    private readonly Dictionary<int, ulong> _lastVehicleInputSequence = new();
    private readonly Dictionary<int, ulong> _lastSpacecraftInputSequence = new();
    private readonly List<int> _drainedRequests = new();
    private float _playerInputAccum;
    private float _spacecraftInputAccum;

    public NetworkEcsDispatchSystem(EntityStore store)
    {
        _store = store;
    }

    public void Update(float dt)
    {
        if (!GameServices.IsNetworkMode)
            return;

        _playerInputAccum += dt;
        _spacecraftInputAccum += dt;

        DispatchPlayerInputCommands();
        DispatchWeaponFireCommands();
        DispatchVehicleInputCommands();
        DispatchSpacecraftInputCommands();
        DispatchDiscreteRequests();
    }

    private void DispatchPlayerInputCommands()
    {
        if (_playerInputAccum < PlayerInputDispatchInterval)
            return;

        if (GameServices.NetworkManager?.ConnectionState != Ark.Networking.NetworkConnectionState.Connected)
            return;

        var query = _store.Query<NetworkPlayerInputCommand>();
        foreach (var chunk in query.Chunks)
        {
            var commands = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var command = ref commands.Span[i];
                int entityId = chunk.Entities[i];
                if (_lastPlayerInputSequence.TryGetValue(entityId, out var lastSequence) && lastSequence == command.Sequence)
                    continue;

                GameServices.Network.SendPlayerInput(new PlayerInputData(
                    new System.Numerics.Vector3(command.MoveDirX, command.MoveDirY, command.MoveDirZ),
                    new System.Numerics.Vector3(command.AimDirX, command.AimDirY, command.AimDirZ),
                    command.ActionFlags,
                    command.Timestamp));

                _lastPlayerInputSequence[entityId] = command.Sequence;
                _playerInputAccum = 0f;
            }
        }
    }

    private void DispatchWeaponFireCommands()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkWeaponFireCommand>();
        foreach (var chunk in query.Chunks)
        {
            var commands = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var command = ref commands.Span[i];
                int entityId = chunk.Entities[i];
                if (_lastWeaponFireSequence.TryGetValue(entityId, out var lastSequence) && lastSequence == command.Sequence)
                    continue;

                serverBridge.RequestFireWeapon(
                    command.WeaponDefId,
                    new System.Numerics.Vector3(command.OriginX, command.OriginY, command.OriginZ),
                    new System.Numerics.Vector3(command.DirX, command.DirY, command.DirZ));
                _lastWeaponFireSequence[entityId] = command.Sequence;
            }
        }
    }

    private void DispatchVehicleInputCommands()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        int currentVehicleId = ResolveLocalVehicleId();

        var query = _store.Query<NetworkVehicleInputCommand>();
        foreach (var chunk in query.Chunks)
        {
            var commands = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var command = ref commands.Span[i];
                int entityId = chunk.Entities[i];
                if (_lastVehicleInputSequence.TryGetValue(entityId, out var lastSequence) && lastSequence == command.Sequence)
                    continue;

                if (currentVehicleId != 0 && command.SnapshotVehicleEntityId > 0 && currentVehicleId != command.SnapshotVehicleEntityId)
                    continue;

                if (command.SnapshotVehicleEntityId <= 0)
                    continue;

                if (!serverBridge.RequestVehicleInput(
                        command.SnapshotVehicleEntityId,
                        command.Throttle,
                        command.Steering,
                        command.Brake,
                        command.ActionFlags,
                        command.TurretYaw,
                        command.TurretPitch))
                    continue;

                _lastVehicleInputSequence[entityId] = command.Sequence;
            }
        }
    }

    [Ark.Analyzers.Attributes.ControlAuthorityResolver]
    private int ResolveLocalVehicleId()
    {
        var localEntityId = GameServices.RemoteWorldEcsCache?.LocalPresentationEntityId ?? 0;
        if (localEntityId <= 0)
            return 0;

        var localEntity = _store.GetEntityById(localEntityId);
        if (localEntity.IsNull)
            return 0;

        if (localEntity.TryGetComponent<LocalControlState>(out var localControlState))
        {
            return (LocalControlSource)localControlState.ControlSource == LocalControlSource.VehicleSeat
                ? localControlState.ControlledSnapshotEntityId
                : 0;
        }

        return localEntity.TryGetComponent<RemoteVehicleOccupantState>(out var remoteVehicleState)
            ? remoteVehicleState.SnapshotVehicleEntityId
            : 0;
    }

    private void DispatchSpacecraftInputCommands()
    {
        if (_spacecraftInputAccum < SpacecraftInputDispatchInterval)
            return;

        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var spacecraftNetworkId = ResolveLocalSpacecraftNetworkId();
        if (spacecraftNetworkId == System.Guid.Empty)
            return;

        var query = _store.Query<NetworkSpacecraftInputCommand>();
        foreach (var chunk in query.Chunks)
        {
            var commands = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var command = ref commands.Span[i];
                int entityId = chunk.Entities[i];
                if (_lastSpacecraftInputSequence.TryGetValue(entityId, out var lastSequence) && lastSequence == command.Sequence)
                    continue;

                serverBridge.RequestSpacecraftInput(
                    spacecraftNetworkId,
                    new System.Numerics.Vector3(command.ThrustX, command.ThrustY, command.ThrustZ),
                    new System.Numerics.Vector3(command.RotationX, command.RotationY, command.RotationZ),
                    command.ActionFlags);
                _lastSpacecraftInputSequence[entityId] = command.Sequence;
                _spacecraftInputAccum = 0f;
            }
        }
    }

    [Ark.Analyzers.Attributes.ControlAuthorityResolver]
    private System.Guid ResolveLocalSpacecraftNetworkId()
    {
        var localEntityId = GameServices.RemoteWorldEcsCache?.LocalPresentationEntityId ?? 0;
        if (localEntityId <= 0)
            return System.Guid.Empty;

        var localEntity = _store.GetEntityById(localEntityId);
        if (localEntity.IsNull)
            return System.Guid.Empty;

        if (localEntity.TryGetComponent<LocalControlState>(out var localControlState))
        {
            return (LocalControlSource)localControlState.ControlSource == LocalControlSource.SpacecraftRemote
                ? localControlState.ActiveNetworkId
                : System.Guid.Empty;
        }

        return localEntity.TryGetComponent<RemoteRocketControlState>(out var rocketControlState)
            ? rocketControlState.ActiveRocketNetworkId
            : System.Guid.Empty;
    }

    private void DispatchDiscreteRequests()
    {
        _drainedRequests.Clear();
        DispatchReloadRequests();
        DispatchSeatWeaponRequests();
        DispatchVehicleActionRequests();
        DispatchBuildPlacementRequests();
        DispatchVehicleSpawnRequests();
        DispatchRocketAssemblyRequests();
        DispatchRocketLaunchRequests();

        foreach (var entityId in _drainedRequests)
        {
            var entity = _store.GetEntityById(entityId);
            if (!entity.IsNull)
                entity.DeleteEntity();
        }
    }

    private void DispatchSeatWeaponRequests()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkSeatWeaponRequest>();
        foreach (var chunk in query.Chunks)
        {
            var requests = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var request = ref requests.Span[i];
                switch ((NetworkSeatWeaponActionKind)request.ActionKind)
                {
                    case NetworkSeatWeaponActionKind.ClearFault:
                        serverBridge.RequestSeatWeaponFaultClear();
                        break;
                    case NetworkSeatWeaponActionKind.Maintain:
                        serverBridge.RequestSeatWeaponMaintenance();
                        break;
                }
                _drainedRequests.Add(chunk.Entities[i]);
            }
        }
    }

    private void DispatchReloadRequests()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkReloadRequest>();
        foreach (var chunk in query.Chunks)
        {
            var requests = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var request = ref requests.Span[i];
                int weaponDefId = ResolveWeaponDefId(request.EntityId);
                if (weaponDefId <= 0)
                    continue;

                serverBridge.RequestReload(weaponDefId);
                _drainedRequests.Add(chunk.Entities[i]);
            }
        }
    }

    private void DispatchVehicleActionRequests()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkVehicleActionRequest>();
        foreach (var chunk in query.Chunks)
        {
            var requests = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var request = ref requests.Span[i];
                var actionKind = (NetworkVehicleActionKind)request.ActionKind;
                int sourceSnapshotVehicleEntityId = request.SnapshotVehicleEntityId;
                int snapshotVehicleEntityId = actionKind == NetworkVehicleActionKind.Exit
                    ? ResolveLocalVehicleId()
                    : sourceSnapshotVehicleEntityId;

                if (snapshotVehicleEntityId <= 0)
                {
                    Godot.GD.PrintErr($"[VehicleDebug][ECSDispatch] skip action={actionKind} commandEntity={chunk.Entities[i]} reason=no_snapshot_vehicle sourceSnapshot={sourceSnapshotVehicleEntityId} resolvedSnapshot={snapshotVehicleEntityId} seat={request.SeatIndex}");
                    continue;
                }

                bool handled = serverBridge.RequestVehicleAction(
                    actionKind,
                    snapshotVehicleEntityId,
                    request.SeatIndex);

                if (handled)
                {
                    Godot.GD.Print($"[VehicleDebug][ECSDispatch] sent action={actionKind} commandEntity={chunk.Entities[i]} snapshotVehicle={snapshotVehicleEntityId} seat={request.SeatIndex}");
                    _drainedRequests.Add(chunk.Entities[i]);
                }
                else
                {
                    Godot.GD.PrintErr($"[VehicleDebug][ECSDispatch] failed action={actionKind} commandEntity={chunk.Entities[i]} snapshotVehicle={snapshotVehicleEntityId} seat={request.SeatIndex} reason=bridge_rejected_or_mapping_missing");
                }
            }
        }
    }

    private int ResolveWeaponDefId(int entityId)
    {
        var entity = _store.GetEntityById(entityId);
        if (entity.IsNull)
            return 0;

        if (entity.TryGetComponent<WeaponState>(out var weaponState) && weaponState.WeaponDefId > 0)
            return weaponState.WeaponDefId;

        return entity.TryGetComponent<RemoteCombatState>(out var remoteCombatState)
            ? remoteCombatState.WeaponDefId
            : 0;
    }

    private void DispatchBuildPlacementRequests()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkBuildPlacementRequest>();
        foreach (var chunk in query.Chunks)
        {
            var requests = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var request = ref requests.Span[i];
                serverBridge.RequestPlaceBuilding(
                    request.BuildingTypeId,
                    new System.Numerics.Vector3(request.PositionX, request.PositionY, request.PositionZ),
                    request.RotationY);
                _drainedRequests.Add(chunk.Entities[i]);
            }
        }
    }

    private void DispatchVehicleSpawnRequests()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkVehicleSpawnRequest>();
        foreach (var chunk in query.Chunks)
        {
            var requests = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var request = ref requests.Span[i];
                serverBridge.RequestSpawnVehicle(
                    request.VehicleDefId,
                    new System.Numerics.Vector3(request.SpawnX, request.SpawnY, request.SpawnZ));
                _drainedRequests.Add(chunk.Entities[i]);
            }
        }
    }

    private void DispatchRocketAssemblyRequests()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkRocketAssemblyRequest>();
        foreach (var chunk in query.Chunks)
        {
            var requests = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var request = ref requests.Span[i];
                serverBridge.RequestAssembleRocket(request.LaunchPadNetworkId, request.RocketConfigJson);
                _drainedRequests.Add(chunk.Entities[i]);
            }
        }
    }

    private void DispatchRocketLaunchRequests()
    {
        var serverBridge = GameServices.ServerBridge;
        if (serverBridge is null)
            return;

        var query = _store.Query<NetworkRocketLaunchRequest>();
        foreach (var chunk in query.Chunks)
        {
            var requests = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var request = ref requests.Span[i];
                serverBridge.RequestLaunchRocket(request.RocketNetworkId);
                _drainedRequests.Add(chunk.Entities[i]);
            }
        }
    }
}
