using Ark.Abstractions;
using Ark.Ecs.Components;
using Ark.Shared.Data;
using Friflo.Engine.ECS;
using Godot;

namespace Ark.Player;

public partial class TpsPlayerController
{
    private void QueueNetworkReloadRequest()
    {
        if (_ecsAuth == null)
            return;

        _ecsAuth.CreateRequest(new NetworkReloadRequest
        {
            EntityId = _entity.Id,
        });
    }

    private void QueueNetworkSeatWeaponRequest(NetworkSeatWeaponActionKind actionKind)
    {
        if (_ecsAuth == null)
            return;

        _ecsAuth.CreateRequest(new NetworkSeatWeaponRequest
        {
            EntityId = _entity.Id,
            ActionKind = (byte)actionKind,
        });
    }

    private void QueueNetworkVehicleActionRequest(NetworkVehicleActionKind actionKind, int snapshotVehicleId, int seatIndex = 0)
    {
        if (_ecsAuth == null)
            return;

        GD.Print($"[VehicleDebug][Controller->ECS] queue action={actionKind} snapshotVehicleId={snapshotVehicleId} seatIndex={seatIndex} localEntity={_entity.Id} inVehicle={IsVehicleControlActive()} effectiveVehicle={ResolveEffectiveVehicleEntityId()}");

        _ecsAuth.CreateRequest(new NetworkVehicleActionRequest
        {
            SnapshotVehicleEntityId = snapshotVehicleId,
            SeatIndex = seatIndex,
            ActionKind = (byte)actionKind,
        });
    }

    private void WriteNetworkWeaponFireCommand(int weaponDefId, Vector3 origin, Vector3 direction)
    {
        var commandEntity = ResolveNetworkCommandEntity(IsVehicleControlActive() ? ResolveEffectiveVehicleEntityId() : 0);
        if (commandEntity.IsNull)
            return;

        _ecsAuth?.WriteCommand(commandEntity, new NetworkWeaponFireCommand
        {
            WeaponDefId = weaponDefId,
            OriginX = origin.X,
            OriginY = origin.Y,
            OriginZ = origin.Z,
            DirX = direction.X,
            DirY = direction.Y,
            DirZ = direction.Z,
            Sequence = ++_networkWeaponFireSequence,
        });
    }

    private void WriteNetworkVehicleInputCommand(int snapshotVehicleId, VehicleInputData input)
    {
        var commandEntity = ResolveNetworkCommandEntity(snapshotVehicleId);
        if (commandEntity.IsNull)
            return;

        _ecsAuth?.WriteCommand(commandEntity, new NetworkVehicleInputCommand
        {
            SnapshotVehicleEntityId = snapshotVehicleId,
            Throttle = input.Throttle,
            Steering = input.Steering,
            Brake = input.Brake,
            TurretYaw = input.TurretYaw,
            TurretPitch = input.TurretPitch,
            ActionFlags = input.ActionFlags,
            Sequence = ++_networkVehicleInputSequence,
        });
    }

    private Entity ResolveNetworkCommandEntity(int snapshotEntityId)
    {
        if (_store == null)
            return default;

        if (snapshotEntityId > 0
            && Ark.Services.GameServices.RemoteWorldEcsCache?.TryGetEcsEntityId(snapshotEntityId, out var ecsEntityId) == true)
        {
            var mappedEntity = _store.GetEntityById(ecsEntityId);
            if (!mappedEntity.IsNull)
                return mappedEntity;
        }

        return _entity.Id != 0 ? _store.GetEntityById(_entity.Id) : default;
    }
}
