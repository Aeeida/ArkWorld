using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Gameplay.Vehicle;
using Ark.Player.Vehicle;

namespace Ark.Player;

public partial class TpsPlayerController
{
    // ═══════════════════════════════════════════════════════════════════════
    //                     网络载具（远程驾驶）
    // ═══════════════════════════════════════════════════════════════════════

    private void BindRemoteVehicleEvents()
    {
    }

    private void UnbindRemoteVehicleEvents()
    {
    }

    private void SyncRemoteVehicleAuthorityState()
    {
        if (_entity.Id == 0)
            return;

        bool hasVehicleRuntime = _entity.TryGetComponent<RemoteVehicleOccupantState>(out var vehicleRuntime)
            && vehicleRuntime.SnapshotVehicleEntityId > 0;

        if (!hasVehicleRuntime)
        {
            if (IsVehicleControlActive())
            {
                GD.Print("[VehicleDebug][ControllerSync] runtime lost while local still in vehicle control; forcing exit");
                HandleRemoteVehicleExited();
            }
            return;
        }

        int controlledVehicleId = ResolveEffectiveVehicleEntityId();
        if (!IsVehicleControlActive() || controlledVehicleId != vehicleRuntime.SnapshotVehicleEntityId)
        {
            GD.Print($"[VehicleDebug][ControllerSync] runtime enter/switch detected runtimeVehicle={vehicleRuntime.SnapshotVehicleEntityId} runtimeSeat={vehicleRuntime.CurrentSeatIndex} localVehicle={controlledVehicleId} localInVehicle={IsVehicleControlActive()}");
            HandleRemoteVehicleEntered(vehicleRuntime.SnapshotVehicleEntityId);
            return;
        }

        if (_entity.TryGetComponent<VehicleSeat>(out var currentSeat)
            && currentSeat.SeatIndex != vehicleRuntime.CurrentSeatIndex)
        {
            GD.Print($"[VehicleDebug][ControllerSync] seat mismatch localSeat={currentSeat.SeatIndex} runtimeSeat={vehicleRuntime.CurrentSeatIndex}, applying seat state");
            ApplyRemoteVehicleSeatState(vehicleRuntime.SnapshotVehicleEntityId);
        }
    }

    private void HandleRemoteVehicleEntered(int vehicleEntityId)
    {
        ApplyVehicleControlRuntimeState(vehicleEntityId);
        ApplyRemoteVehicleSeatState(vehicleEntityId);
        GD.Print($"[TpsPlayer] Entered remote vehicle {vehicleEntityId}");
        if (_entity.TryGetComponent<RemoteVehicleOccupantState>(out var runtime))
        {
            GD.Print($"[VehicleDebug][ControllerSync] entered runtimeVehicle={runtime.SnapshotVehicleEntityId} seat={runtime.CurrentSeatIndex} seatType={runtime.CurrentSeatType}");
        }
    }

    private void HandleRemoteVehicleExited()
    {
        var exitPos = ResolveVehicleExitPosition();

        ClearVehicleControlRuntimeState(clearVisualOffset: false, syncControlState: false);
        ClearRemoteVehicleSeatState();
        GlobalPosition = exitPos;
        _velocity = Vector3.Zero;
        if (_entity.Id != 0)
        {
            _ecsAuth?.Write(_entity, new WorldPosition { X = exitPos.X, Y = exitPos.Y, Z = exitPos.Z });
            _ecsAuth?.Write(_entity, new Velocity { X = 0f, Y = 0f, Z = 0f, Speed = 0f });
        }
        SyncLocalControlStateToEcs();
        GD.Print($"[TpsPlayer] Exited remote vehicle");
        GD.Print($"[VehicleDebug][ControllerSync] exited to pos=({exitPos.X:F2},{exitPos.Y:F2},{exitPos.Z:F2})");
    }

    private bool TryGetRemoteVehicleEntity(int vehicleEntityId, out Entity vehicleEntity)
    {
        vehicleEntity = default;
        if (_store == null)
            return false;

        var remoteWorldEcsCache = Ark.Services.GameServices.RemoteWorldEcsCache;
        if (remoteWorldEcsCache is null || !remoteWorldEcsCache.TryGetEcsEntityId(vehicleEntityId, out var ecsVehicleId))
            return false;

        vehicleEntity = _store.GetEntityById(ecsVehicleId);
        return !vehicleEntity.IsNull;
    }

    private static bool TryResolveRemoteVehicleDefId(Entity vehicleEntity, out int vehicleDefId)
    {
        vehicleDefId = 0;

        if (vehicleEntity.TryGetComponent<VehicleState>(out var vehicleState) && vehicleState.VehicleDefId > 0)
        {
            vehicleDefId = vehicleState.VehicleDefId;
            return true;
        }

        if (vehicleEntity.TryGetComponent<RemoteEntityState>(out var remoteState) && remoteState.TypeId > 0)
        {
            vehicleDefId = remoteState.TypeId;
            return true;
        }

        return false;
    }

    private bool TryGetRemoteVehicleDefId(int vehicleEntityId, out int vehicleDefId)
    {
        vehicleDefId = 0;
        return TryGetRemoteVehicleEntity(vehicleEntityId, out var vehicleEntity)
            && TryResolveRemoteVehicleDefId(vehicleEntity, out vehicleDefId);
    }

    private bool TryGetRemoteVehiclePose(int vehicleEntityId, out Vector3 remotePos, out Quaternion remoteQuat, out int vehicleDefId)
    {
        remotePos = Vector3.Zero;
        remoteQuat = new Quaternion(0f, 0f, 0f, 1f);
        vehicleDefId = 0;

        if (!TryGetRemoteVehicleEntity(vehicleEntityId, out var vehicleEntity))
            return false;
        if (!vehicleEntity.TryGetComponent<WorldPosition>(out var worldPos) || !vehicleEntity.TryGetComponent<WorldRotation>(out var worldRot))
            return false;

        remotePos = new Vector3(worldPos.X, worldPos.Y, worldPos.Z);
        remoteQuat = new Quaternion(worldRot.X, worldRot.Y, worldRot.Z, worldRot.W);
        TryResolveRemoteVehicleDefId(vehicleEntity, out vehicleDefId);
        return true;
    }

    private void SyncRemoteVehicleSeatToEcs(Vector3 seatWorldPos, Quaternion seatRotation)
    {
        if (_entity.Id == 0)
            return;

        _ecsAuth?.Write(_entity, new WorldPosition { X = seatWorldPos.X, Y = seatWorldPos.Y, Z = seatWorldPos.Z });
        _ecsAuth?.Write(_entity, new WorldRotation { X = seatRotation.X, Y = seatRotation.Y, Z = seatRotation.Z, W = seatRotation.W });
        _ecsAuth?.Write(_entity, new Velocity { X = 0f, Y = 0f, Z = 0f, Speed = 0f });
    }

    private int ResolvePreferredRemoteSeat(int vehicleEntityId)
    {
        if (_combatData is null || !TryGetRemoteVehicleDefId(vehicleEntityId, out var vehicleDefId))
            return 0;

        var def = _combatData.VehicleDefs.Get(vehicleDefId);
        if (def is null) return 0;

        for (int i = 0; i < def.Value.Seats.Length; i++)
        {
            if (def.Value.Seats[i].Type == Ark.Shared.Data.SeatType.Driver)
                return i;
        }

        return 0;
    }

    private void ApplyRemoteVehicleSeatState(int vehicleEntityId)
    {
        if (_entity.Id == 0 || _combatData is null) return;

        if (!_entity.TryGetComponent<RemoteVehicleOccupantState>(out var remoteVehicleState)
            || !TryGetRemoteVehicleDefId(vehicleEntityId, out var vehicleDefId))
            return;

        var def = _combatData.VehicleDefs.Get(vehicleDefId);
        if (def is null || def.Value.Seats.Length == 0)
            return;

        int seatIndex = Mathf.Clamp(remoteVehicleState.CurrentSeatIndex, 0, def.Value.Seats.Length - 1);
        var seatDef = def.Value.Seats[seatIndex];

        _ecsAuth?.Write(_entity, new VehicleSeat
        {
            VehicleEntityId = vehicleEntityId,
            SeatIndex = (byte)seatIndex,
            SeatType = (byte)seatDef.Type,
            OffsetX = seatDef.LocalOffset.X,
            OffsetY = seatDef.LocalOffset.Y,
            OffsetZ = seatDef.LocalOffset.Z,
        });
        _ecsAuth?.AddTag<InVehicle>(_entity);
        _ecsAuth?.RemoveTag<IsDriver>(_entity);
        _ecsAuth?.RemoveTag<IsGunner>(_entity);
        _ecsAuth?.RemoveTag<IsPassenger>(_entity);

        switch (seatDef.Type)
        {
            case Ark.Shared.Data.SeatType.Driver: _ecsAuth?.AddTag<IsDriver>(_entity); break;
            case Ark.Shared.Data.SeatType.Gunner: _ecsAuth?.AddTag<IsGunner>(_entity); break;
            default: _ecsAuth?.AddTag<IsPassenger>(_entity); break;
        }
    }

    private void ClearRemoteVehicleSeatState()
    {
        if (_entity.Id == 0) return;
        _ecsAuth?.Remove<VehicleSeat>(_entity);
        _ecsAuth?.RemoveTag<InVehicle>(_entity);
        _ecsAuth?.RemoveTag<IsDriver>(_entity);
        _ecsAuth?.RemoveTag<IsGunner>(_entity);
        _ecsAuth?.RemoveTag<IsPassenger>(_entity);
    }

    private int ResolveRemoteVehicleWeaponDefId(int vehicleEntityId)
    {
        if (_combatData is null || !TryGetRemoteVehicleDefId(vehicleEntityId, out var vehicleDefId)) return 0;
        if (!_entity.TryGetComponent<RemoteVehicleOccupantState>(out var remoteVehicleState)) return 0;

        var def = _combatData.VehicleDefs.Get(vehicleDefId);
        if (def is null || def.Value.Seats.Length == 0) return 0;

        int seatIndex = Mathf.Clamp(remoteVehicleState.CurrentSeatIndex, 0, def.Value.Seats.Length - 1);
        var seatDef = def.Value.Seats[seatIndex];
        return seatDef.HasWeapon ? seatDef.WeaponDefId : 0;
    }

    private void DetectNearbyRemoteVehicles()
    {
        var remoteWorldEcsCache = Ark.Services.GameServices.RemoteWorldEcsCache;
        if (remoteWorldEcsCache is null)
            return;

        var myPos = GlobalPosition;
        float closestDist = 6f;
        _nearbyVehicleId = -1;

        foreach (var vehicleId in remoteWorldEcsCache.GetSnapshotEntityIds(Ark.Shared.Data.EntityType.Vehicle))
        {
            if (!TryGetRemoteVehiclePose(vehicleId, out var remotePos, out _, out _))
                continue;

            float dx = remotePos.X - myPos.X;
            float dy = remotePos.Y - myPos.Y;
            float dz = remotePos.Z - myPos.Z;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist < closestDist)
            {
                closestDist = dist;
                _nearbyVehicleId = vehicleId;
            }
        }
    }

    private void ProcessRemoteVehicleControl(float dt)
    {
        if (_combatData is null || !_entity.TryGetComponent<RemoteVehicleOccupantState>(out var remoteVehicleState))
            return;

        int vehicleEntityId = ResolveEffectiveVehicleEntityId();
        if (vehicleEntityId <= 0)
            vehicleEntityId = remoteVehicleState.SnapshotVehicleEntityId;
        if (vehicleEntityId <= 0)
            return;

        if (!TryGetRemoteVehiclePose(vehicleEntityId, out var remotePos, out var remoteQuat, out var vehicleDefId))
            return;

        var seatOffset = Vector3.Zero;
        bool isDriver = false;
        bool seatHasWeapon = false;
        var seatType = Ark.Shared.Data.SeatType.Passenger;
        var displayPos = remotePos;
        var displayQuat = remoteQuat;

        var vehicleDef = _combatData.VehicleDefs.Get(vehicleDefId);
        if (vehicleDef is { } def && def.Seats.Length > 0)
        {
            int seatIndex = Mathf.Clamp(remoteVehicleState.CurrentSeatIndex, 0, def.Seats.Length - 1);
            var seatDef = def.Seats[seatIndex];
            seatOffset = new Vector3(seatDef.LocalOffset.X, seatDef.LocalOffset.Y, seatDef.LocalOffset.Z);
            isDriver = seatDef.Type == Ark.Shared.Data.SeatType.Driver;
            seatHasWeapon = seatDef.HasWeapon;
            seatType = seatDef.Type;

            if (isDriver)
                PredictRemoteVehiclePose(dt, def, remotePos, remoteQuat, out displayPos, out displayQuat);
            else
                _hasPredictedVehiclePose = false;
        }

        var seatWorldPos = displayPos + displayQuat * seatOffset;
        Velocity = Vector3.Zero;
        _velocity = Vector3.Zero;
        SyncRemoteVehicleSeatToEcs(seatWorldPos, displayQuat);
        GlobalPosition = displayPos;
        Quaternion = displayQuat;
        ApplySeatedVisualOffset(seatOffset);

        // 载具根位置/旋转供相机使用，角色朝向同步到载具
        _vehicleRootPosition = displayPos;
        _vehicleRotation = displayQuat;
        _vehicleCameraAnchor = _vehicleRootPosition;

        var aimOrigin = new Vector3(remotePos.X, remotePos.Y + 2.0f, remotePos.Z);
        Vector3 aimDir = GetAimDirectionTowardCrosshair(aimOrigin);
        ComputeRemoteTurretInput(remoteQuat, aimDir, out float turretYaw, out float turretPitch);

        // ── 载具武器射击（所有座位，SPACE 键触发）──
        if (_isFiring && Ark.Services.GameServices.ServerBridge is not null)
        {
            var weaponDefId = ResolveRemoteVehicleWeaponDefId(vehicleEntityId);
            if (weaponDefId > 0)
                WriteNetworkWeaponFireCommand(weaponDefId, aimOrigin, aimDir);
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        float throttle = isDriver ? -inputDir.Y : 0f;
        float steering = isDriver ? -inputDir.X : 0f;  // 服务端转向约定：正值右转（与 Godot GetVector 方向相反）
        float brake = Input.IsActionPressed("sprint") ? 1f : 0f;
        byte actionFlags = 0;
        if (_isFiring)
            actionFlags |= 0x01;
        if (Input.IsActionPressed("aircraft_ascend"))
            actionFlags |= 0x02;
        if (Input.IsActionPressed("aircraft_descend"))
            actionFlags |= 0x04;
        if (Input.IsActionPressed("aircraft_strafe_left"))
            actionFlags |= 0x08;
        if (Input.IsActionPressed("aircraft_strafe_right"))
            actionFlags |= 0x10;

        bool shouldSendTurretInput = seatHasWeapon || seatType == Ark.Shared.Data.SeatType.Driver;
        WriteNetworkVehicleInputCommand(vehicleEntityId, new Ark.Shared.Data.VehicleInputData(
            throttle,
            steering,
            brake,
            actionFlags,
            shouldSendTurretInput ? turretYaw : 0f,
            shouldSendTurretInput ? turretPitch : 0f));

        if (!isDriver)
            return;
    }

    private static void ComputeRemoteTurretInput(Quaternion vehicleRotation, Vector3 aimDirection, out float turretYaw, out float turretPitch)
    {
        turretYaw = 0f;
        turretPitch = 0f;
        if (aimDirection.LengthSquared() <= 1e-6f)
            return;

        var localAim = vehicleRotation.Inverse() * aimDirection.Normalized();
        turretYaw = Mathf.Atan2(localAim.X, -localAim.Z);
        turretPitch = Mathf.Atan2(localAim.Y, Mathf.Max(0.001f, Mathf.Sqrt(localAim.X * localAim.X + localAim.Z * localAim.Z)));
    }

    private void PredictRemoteVehiclePose(float dt, Ark.Shared.Data.VehicleDef def, Vector3 remotePos, Quaternion remoteQuat, out Vector3 predictedPos, out Quaternion predictedQuat)
    {
        if (!_hasPredictedVehiclePose)
        {
            _hasPredictedVehiclePose = true;
            _predictedVehicleRootPosition = remotePos;
            _predictedVehicleRootRotation = remoteQuat;
            _predictedVehicleSpeed = 0f;
            _vehiclePitch = 0f;
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        bool isSprinting = Input.IsActionPressed("sprint");
        float steering = -inputDir.X;
        float turnRate = VehicleDriveInput.TurnRate(_predictedVehicleSpeed, def.MaxSpeed);
        var yawDelta = steering * turnRate * dt;
        var yawRot = new Quaternion(Vector3.Up, yawDelta);
        _predictedVehicleRootRotation = (yawRot * _predictedVehicleRootRotation).Normalized();

        float targetSpeed = VehicleDriveInput.TargetSpeed(-inputDir.Y, def.MaxSpeed, isSprinting);
        _predictedVehicleSpeed = VehicleDriveInput.ApproachSpeed(_predictedVehicleSpeed, targetSpeed, def.MaxSpeed, dt);

        predictedPos = _predictedVehicleRootPosition;
        predictedQuat = _predictedVehicleRootRotation;
        if (def.Type == Ark.Shared.Data.VehicleType.Plane)
        {
            float verticalInput = Input.GetAxis("aircraft_descend", "aircraft_ascend");
            float strafeInput = Input.GetAxis("aircraft_strafe_left", "aircraft_strafe_right");
            const float AircraftLiftFactor = 0.75f;
            const float AircraftStrafeFactor = 0.6f;
            var forward = (predictedQuat * Vector3.Forward).Normalized();
            var right = (predictedQuat * Vector3.Right).Normalized();
            var velocity3D = forward * _predictedVehicleSpeed
                + right * (strafeInput * def.MaxSpeed * AircraftStrafeFactor)
                + Vector3.Up * (verticalInput * def.MaxSpeed * AircraftLiftFactor);
            predictedPos += velocity3D * dt;

            if (_sampleTerrainHeight != null)
            {
                float terrainY = _sampleTerrainHeight(predictedPos.X, predictedPos.Z);
                predictedPos.Y = Mathf.Max(predictedPos.Y, terrainY + 1.5f);
            }
        }
        else
        {
            var forward = (predictedQuat * Vector3.Forward).Normalized();
            predictedPos += forward * _predictedVehicleSpeed * dt;

            if (_sampleTerrainHeight != null && VehicleTerrainSystem.IsGroundVehicle(def.Type))
                predictedPos.Y = _sampleTerrainHeight(predictedPos.X, predictedPos.Z);
        }

        var error = remotePos - predictedPos;
        if (error.LengthSquared() > VehiclePredictionSnapDistance * VehiclePredictionSnapDistance)
        {
            predictedPos = remotePos;
            predictedQuat = remoteQuat;
            _predictedVehicleSpeed = 0f;
        }
        else
        {
            float correction = Mathf.Clamp(VehiclePredictionCorrectionRate * dt, 0f, 1f);
            predictedPos = predictedPos.Lerp(remotePos, correction);
            predictedQuat = predictedQuat.Slerp(remoteQuat, correction);
        }

        _predictedVehicleRootPosition = predictedPos;
        _predictedVehicleRootRotation = predictedQuat;

        int vehicleEntityId = ResolveEffectiveVehicleEntityId();
        if (vehicleEntityId > 0 && TryGetRemoteVehicleEntity(vehicleEntityId, out var vehicleEntity))
        {
            _ecsAuth?.Write(vehicleEntity, new WorldPosition { X = predictedPos.X, Y = predictedPos.Y, Z = predictedPos.Z });
            _ecsAuth?.Write(vehicleEntity, new WorldRotation { X = predictedQuat.X, Y = predictedQuat.Y, Z = predictedQuat.Z, W = predictedQuat.W });
        }
    }
}
