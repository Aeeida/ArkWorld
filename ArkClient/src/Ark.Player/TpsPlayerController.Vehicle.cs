using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Gameplay.Vehicle;
using Ark.Player.Vehicle;
using Ark.Services.Remote;
using static Friflo.Engine.ECS.Tags;

namespace Ark.Player;

public partial class TpsPlayerController
{
    // ═══════════════════════════════════════════════════════════════════════
    //                     本地载具驾驶 / 进入 / 退出
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 载具驾驶控制 — A/D 转向, W/S 前进/后退, 鼠标控制视角与射击方向。
    /// </summary>
    private void ProcessVehicleControl(float dt)
    {
        int vehicleEntityId = ResolveEffectiveVehicleEntityId();
        if (_store == null || vehicleEntityId <= 0) return;

        if (Ark.Services.GameServices.IsNetworkMode)
        {
            ProcessRemoteVehicleControl(dt);
            return;
        }

        if (_combatModule == null) return;

        var vehicleEntity = _store.GetEntityById(vehicleEntityId);
        if (vehicleEntity.IsNull) { ExitVehicle(); return; }
        if (!vehicleEntity.TryGetComponent<VehicleState>(out var vs)) return;

        // 只有驾驶座才能操控载具移动
        bool isDriver = _entity.TryGetComponent<VehicleSeat>(out var mySeat)
                        && mySeat.SeatType == (byte)Ark.Shared.Data.SeatType.Driver;

        if (!vehicleEntity.TryGetComponent<WorldRotation>(out var vRot)) return;
        var vehicleQuat = new Quaternion(vRot.X, vRot.Y, vRot.Z, vRot.W);

        if (isDriver)
        {
            bool isAircraft = vs.VehicleType == 3; // 3 = Plane

            // WASD → 载具移动（via VehicleDriveInput）
            Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            bool isSprinting = Input.IsActionPressed("sprint");

            // 转向（A/D）— 正值右转，负值左转
            float turnRate = VehicleDriveInput.TurnRate(vs.CurrentSpeed, vs.MaxSpeed);
            float yawDelta = inputDir.X * turnRate * dt;
            var rotDelta = new Quaternion(Vector3.Up, yawDelta);
            vehicleQuat = (rotDelta * vehicleQuat).Normalized();

            // 前进/后退（W/S）
            var forward = vehicleQuat * Vector3.Forward;
            float targetSpd = VehicleDriveInput.TargetSpeed(-inputDir.Y, vs.MaxSpeed, isSprinting);
            vs.CurrentSpeed = VehicleDriveInput.ApproachSpeed(vs.CurrentSpeed, targetSpd, vs.MaxSpeed, dt);

            // 更新载具 ECS
            if (!vehicleEntity.TryGetComponent<WorldPosition>(out var vPos)) return;

            float velY = 0f;
            if (isAircraft)
            {
                ProcessAircraftControl(dt, ref vehicleQuat, ref vPos, ref vs, ref velY, vehicleEntity);
            }
            else
            {
                var velocity = forward * vs.CurrentSpeed;
                vPos.X += velocity.X * dt;
                vPos.Z += velocity.Z * dt;
                // 地面载具 Y 贴附由 VehicleTerrainSystem 统一处理
                _ecsAuth?.Write(vehicleEntity, new WorldRotation { X = vehicleQuat.X, Y = vehicleQuat.Y, Z = vehicleQuat.Z, W = vehicleQuat.W });
            }

            _ecsAuth?.Write(vehicleEntity, vPos);
            _ecsAuth?.Write(vehicleEntity, vs);

            // 使用载具最终旋转计算正确的 3D 速度分量
            if (vehicleEntity.TryGetComponent<WorldRotation>(out var velRot))
            {
                var vq = new Quaternion(velRot.X, velRot.Y, velRot.Z, velRot.W);
                var vf = (vq * Vector3.Forward).Normalized();
                _ecsAuth?.Write(vehicleEntity, new Velocity { X = vf.X * vs.CurrentSpeed, Y = vf.Y * vs.CurrentSpeed, Z = vf.Z * vs.CurrentSpeed, Speed = Mathf.Abs(vs.CurrentSpeed) });
            }
            else
            {
                _ecsAuth?.Write(vehicleEntity, new Velocity { X = forward.X * vs.CurrentSpeed, Y = velY, Z = forward.Z * vs.CurrentSpeed, Speed = Mathf.Abs(vs.CurrentSpeed) });
            }
        }

        // 同步角色位置到载具座椅（使用载具最终旋转+位置，消除帧延迟抖动）
        if (vehicleEntity.TryGetComponent<WorldPosition>(out var syncPos)
            && vehicleEntity.TryGetComponent<WorldRotation>(out var syncRot))
        {
            // 地面载具：在计算座位前先做内联地形校正，避免一帧延迟抖动
            if (_sampleTerrainHeight != null
                && vehicleEntity.TryGetComponent<VehicleState>(out var terrainVs)
                && VehicleTerrainSystem.IsGroundVehicle((Ark.Shared.Data.VehicleType)terrainVs.VehicleType))
            {
                float terrainY = _sampleTerrainHeight(syncPos.X, syncPos.Z);
                syncPos.Y = terrainY;
                _ecsAuth?.Write(vehicleEntity, syncPos);
            }

            var syncQuat = new Quaternion(syncRot.X, syncRot.Y, syncRot.Z, syncRot.W);
            var seatOffset = Vector3.Zero;
            if (_entity.TryGetComponent<VehicleSeat>(out var seatComp))
                seatOffset = new Vector3(seatComp.OffsetX, seatComp.OffsetY, seatComp.OffsetZ);
            var worldOffset = syncQuat * seatOffset;
            var seatWorldPos = new Vector3(syncPos.X + worldOffset.X, syncPos.Y + worldOffset.Y, syncPos.Z + worldOffset.Z);
            GlobalPosition = new Vector3(syncPos.X, syncPos.Y, syncPos.Z);
            Quaternion = syncQuat;
            ApplySeatedVisualOffset(seatOffset);

            // 载具根位置/旋转供相机使用，角色朝向同步到载具
            _vehicleRootPosition = new Vector3(syncPos.X, syncPos.Y, syncPos.Z);
            _vehicleRotation = syncQuat;
            _vehicleCameraAnchor = _vehicleRootPosition;
            SyncSeatedCharacterToEcs(seatWorldPos, syncQuat);
        }
        Velocity = Vector3.Zero;
        _velocity = Vector3.Zero;

        // 载具炮塔射击（LMB）
        if (_isFiring)
        {
            var shootPos = vehicleEntity.TryGetComponent<WorldPosition>(out var sp) ? sp : default;
            var aimOrigin = new Vector3(shootPos.X, shootPos.Y + 2.0f, shootPos.Z);
            Vector3 aimDir = GetAimDirectionTowardCrosshair(aimOrigin);

            var sysOrigin = new System.Numerics.Vector3(aimOrigin.X, aimOrigin.Y, aimOrigin.Z);
            var sysDir = new System.Numerics.Vector3(aimDir.X, aimDir.Y, aimDir.Z);

            if (Ark.Services.GameServices.IsNetworkMode)
            {
                var weaponDefId = ResolveRemoteVehicleWeaponDefId(vehicleEntityId);
                WriteNetworkWeaponFireCommand(weaponDefId, aimOrigin, aimDir);
            }
            else
            {
                _combatModule.TryFire(vehicleEntityId, sysOrigin, sysDir, _combatModule.GameTime);
            }
        }

        // 车辆探测
        if (_mouseCaptured)
        {
            DetectNearbyVehicles();
        }
    }

    /// <summary>飞行器升降/横移控制子逻辑。</summary>
    private void ProcessAircraftControl(float dt, ref Quaternion vehicleQuat, ref WorldPosition vPos, ref VehicleState vs, ref float velY, Entity vehicleEntity)
    {
        float verticalInput = Input.GetAxis("aircraft_descend", "aircraft_ascend");
        float strafeInput = Input.GetAxis("aircraft_strafe_left", "aircraft_strafe_right");
        const float AircraftLiftFactor = 0.75f;
        const float AircraftStrafeFactor = 0.6f;

        var forward = (vehicleQuat * Vector3.Forward).Normalized();
        var right = (vehicleQuat * Vector3.Right).Normalized();
        var velocity3D = forward * vs.CurrentSpeed
            + right * (strafeInput * vs.MaxSpeed * AircraftStrafeFactor)
            + Vector3.Up * (verticalInput * vs.MaxSpeed * AircraftLiftFactor);
        vPos.X += velocity3D.X * dt;
        vPos.Y += velocity3D.Y * dt;
        vPos.Z += velocity3D.Z * dt;
        velY = velocity3D.Y;

        // 飞机不得低于地形高度
        if (_sampleTerrainHeight != null)
        {
            float terrainY = _sampleTerrainHeight(vPos.X, vPos.Z);
            float minAltitude = terrainY + 1.5f;
            if (vPos.Y < minAltitude)
            {
                vPos.Y = minAltitude;
                if (velY < 0) velY = 0;
            }
        }

        _ecsAuth?.Write(vehicleEntity, new WorldRotation { X = vehicleQuat.X, Y = vehicleQuat.Y, Z = vehicleQuat.Z, W = vehicleQuat.W });
    }

    /// <summary>
    /// 检测附近的载具（用于 F 键交互提示）— 3D 距离检测。
    /// </summary>
    private void DetectNearbyVehicles()
    {
        _nearbyVehicleId = -1;
        if (IsVehicleControlActive()) return;

        if (Ark.Services.GameServices.IsNetworkMode)
        {
            DetectNearbyRemoteVehicles();
            return;
        }

        if (_store == null) return;

        var myPos = GlobalPosition;
        float closestDist = 6f; // 交互半径

        var query = _store.Query<WorldPosition, VehicleState>()
            .AllTags(Get<VehicleTag>());
        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            var entities = chunk.Entities;
            for (int i = 0; i < entities.Length; i++)
            {
                // 排除自身实体，防止误把角色当载具
                if (entities[i] == _entity.Id) continue;

                var pos = positions.Span[i];
                float dx = pos.X - myPos.X;
                float dy = pos.Y - myPos.Y;
                float dz = pos.Z - myPos.Z;
                float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    _nearbyVehicleId = entities[i];
                }
            }
        }
    }

    /// <summary>进入载具。</summary>
    private void EnterVehicle(int vehicleEntityId)
    {
        if (_entity.Id == 0) return;

        if (Ark.Services.GameServices.IsNetworkMode)
        {
            int preferredSeat = ResolvePreferredRemoteSeat(vehicleEntityId);
            GD.Print($"[VehicleDebug][Input] interact-enter targetVehicle={vehicleEntityId} preferredSeat={preferredSeat} nearbyVehicle={_nearbyVehicleId} localEntity={_entity.Id} currentControlVehicle={ResolveEffectiveVehicleEntityId()} inVehicle={IsVehicleControlActive()}");
            QueueNetworkVehicleActionRequest(NetworkVehicleActionKind.Enter, vehicleEntityId, preferredSeat);
            GD.Print($"[TpsPlayer] Enter vehicle requested via ECS: {vehicleEntityId}");
            return;
        }

        if (_combatModule == null) return;

        bool success = _combatModule.EnterVehicle(_entity.Id, vehicleEntityId, 0);
        if (success)
        {
            ApplyVehicleControlRuntimeState(vehicleEntityId);
            _velocity = Vector3.Zero;
            Velocity = Vector3.Zero;
            GD.Print($"[TpsPlayer] Entered vehicle {vehicleEntityId}");
        }
    }

    /// <summary>退出载具。</summary>
    private void ExitVehicle()
    {
        if (_entity.Id == 0) return;

        if (Ark.Services.GameServices.IsNetworkMode)
        {
            int currentVehicleId = ResolveEffectiveVehicleEntityId();
            GD.Print($"[VehicleDebug][Input] interact-exit currentVehicle={currentVehicleId} localEntity={_entity.Id} inVehicle={IsVehicleControlActive()}");
            QueueNetworkVehicleActionRequest(NetworkVehicleActionKind.Exit, currentVehicleId);
            GD.Print("[TpsPlayer] Exit vehicle requested via ECS");
            return;
        }

        if (_combatModule == null) return;

        var exitPos = ResolveVehicleExitPosition();

        bool success = _combatModule.ExitVehicle(_entity.Id);
        if (success)
        {
            ClearVehicleControlRuntimeState();
            GlobalPosition = exitPos;
            _velocity = Vector3.Zero;
            _ecsAuth?.Write(_entity, new WorldPosition { X = exitPos.X, Y = exitPos.Y, Z = exitPos.Z });
            _ecsAuth?.Write(_entity, new Velocity { X = 0f, Y = 0f, Z = 0f, Speed = 0f });
            GD.Print("[TpsPlayer] Exited vehicle");
        }
    }

    public void CycleSeat()
    {
        if (Ark.Services.GameServices.IsNetworkMode)
        {
            int vehicleEntityId = ResolveEffectiveVehicleEntityId();
            if (vehicleEntityId <= 0) return;
            int currentSeatIndex = _entity.TryGetComponent<RemoteVehicleOccupantState>(out var remoteVehicleState)
                ? remoteVehicleState.CurrentSeatIndex
                : 0;
            int nextSeatIndex = currentSeatIndex + 1;
            if (TryGetRemoteVehicleDefId(vehicleEntityId, out var vehicleDefId)
                && ResolveVehicleDef(vehicleDefId) is { } def
                && def.Seats.Length > 0)
            {
                nextSeatIndex = (currentSeatIndex + 1 + def.Seats.Length) % def.Seats.Length;
                if (def.Seats.Length <= 1)
                    return;
            }

            QueueNetworkVehicleActionRequest(NetworkVehicleActionKind.SwitchSeat, vehicleEntityId, nextSeatIndex);
            GD.Print($"[TpsPlayer] Requested seat switch via ECS in vehicle {vehicleEntityId}");
            return;
        }

        if (_combatModule == null || _entity.Id == 0 || !IsVehicleControlActive()) return;
        if (_combatModule.CycleToNextSeat(_entity.Id))
        {
            GD.Print($"[TpsPlayer] Switched seat in vehicle {ResolveEffectiveVehicleEntityId()}");
        }
    }
}
