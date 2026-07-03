using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Player.Character;

namespace Ark.Player;

public partial class TpsPlayerController
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          物理循环 / AI 移动 / ECS 同步
    // ═══════════════════════════════════════════════════════════════════════

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (Ark.Services.GameServices.IsNetworkMode)
            SyncRemoteVehicleAuthorityState();

        bool vehicleControlActive = IsVehicleControlActive();

        // 非激活时执行 AI 跟随（含载具跟随）
        if (!_isActive)
        {
            if (vehicleControlActive)
                ProcessAiVehicleFollow(dt);
            else
                ProcessAiMovement(dt);
            return;
        }

        // ═══ 载具驾驶模式（完全绕过 CharacterBody3D 物理）═══
        if (vehicleControlActive)
        {
            // 先计算载具位置（更新 _vehicleRootPosition/_vehicleRotation 和 GlobalPosition）
            ProcessVehicleControl(dt);
            // 冻结物理体速度，防止 CharacterBody3D 物理干扰
            Velocity = Vector3.Zero;
            _velocity = Vector3.Zero;

            // ── 相机直接定位到载具根节点（绕过 CharacterBody3D 节点层级）──
            // 使用载具根位置而非座位位置，让相机围绕载具中心旋转
            if (_cameraRig != null)
            {
                _cameraRig.TopLevel = true;
                _cameraRig.GlobalPosition = _vehicleCameraAnchor + CameraAnchorOffset;
                _cameraRig.Rotation = new Vector3(_targetPitch, _targetYaw, 0);
            }
            UpdateVehicleCameraOffset(dt);
            UpdateDebugUI();
            return;
        }

        if (Ark.Services.GameServices.IsNetworkMode)
        {
            ProcessNetworkPredictedMovement(dt);
            return;
        }

        // ═══ 1. 应用相机旋转 ═══
        if (_cameraRig != null)
            _cameraRig.Rotation = new Vector3(_targetPitch, 0, 0);
        Rotation = new Vector3(0, _targetYaw, 0);

        // ═══ 1b. 相机偏移平滑过渡 + 缩放 ═══
        UpdateCameraOffset(dt);

        // ═══ 2. 获取输入（建造相机模式禁用移动）═══
        Vector3 direction = Vector3.Zero;
        if (!_buildCameraMode)
        {
            Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            if (inputDir != Vector2.Zero)
                direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        }

        // ═══ 3. 计算目标速度（via CharacterMotion）═══
        bool isSprinting = !_buildCameraMode && Input.IsActionPressed("sprint");
        float targetSpeed     = CharacterMotion.TargetSpeed(isSprinting, WalkSpeed, SprintSpeed);
        Vector3 targetVelocity = direction * targetSpeed;

        // ═══ 4. 应用加速/减速（via CharacterMotion）═══
        (_velocity.X, _velocity.Z) = CharacterMotion.ApplyHorizontal(
            _velocity.X, _velocity.Z,
            targetVelocity.X, targetVelocity.Z,
            Acceleration, Deceleration, AirControl,
            IsOnFloor(), direction != Vector3.Zero, dt);

        // ═══ 5. 重力（via CharacterMotion）═══
        (_velocity.Y, _jumpsRemaining) = CharacterMotion.ApplyGravity(
            _velocity.Y, Gravity, dt,
            IsOnFloor(), _jumpsRemaining, MaxJumps);

        // ═══ 6. 跳跃（建造相机模式禁用）═══
        bool jumpReq = !_buildCameraMode && Input.IsActionJustPressed("jump");
        (_velocity.Y, _jumpsRemaining) = CharacterMotion.TryJump(
            _velocity.Y, JumpVelocity, _jumpsRemaining, jumpReq);

        // ═══ 7. 应用移动 ═══
        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;

        // ═══ 8. 同步 ECS ═══
        SyncToEcs();

        // ═══ 9–10. 战斗逻辑（仅鼠标锁定 / TPS 模式下执行）═══
        if (_mouseCaptured)
        {
            SyncCombatTarget();
            ProcessShooting(dt);
            DetectNearbyVehicles();
        }

        // ═══ 11. 更新调试 UI ═══
        UpdateDebugUI();
    }

    private void ProcessNetworkPredictedMovement(float dt)
    {
        if (_cameraRig != null)
            _cameraRig.Rotation = new Vector3(_targetPitch, 0, 0);
        Rotation = new Vector3(0, _targetYaw, 0);

        UpdateCameraOffset(dt);
        ApplyPredictedCharacterMotion(dt);
        ReconcilePlayerPrediction(dt);
        SyncToEcs();

        if (_mouseCaptured)
        {
            SyncCombatTarget();
            ProcessShooting(dt);
            DetectNearbyVehicles();
        }

        UpdateDebugUI();
    }

    private void ApplyPredictedCharacterMotion(float dt)
    {
        Vector3 direction = Vector3.Zero;
        if (!_buildCameraMode)
        {
            Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            if (inputDir != Vector2.Zero)
                direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        }

        bool isSprinting = !_buildCameraMode && Input.IsActionPressed("sprint");
        float targetSpeed = CharacterMotion.TargetSpeed(isSprinting, WalkSpeed, SprintSpeed);
        Vector3 targetVelocity = direction * targetSpeed;

        (_velocity.X, _velocity.Z) = CharacterMotion.ApplyHorizontal(
            _velocity.X, _velocity.Z,
            targetVelocity.X, targetVelocity.Z,
            Acceleration, Deceleration, AirControl,
            IsOnFloor(), direction != Vector3.Zero, dt);

        (_velocity.Y, _jumpsRemaining) = CharacterMotion.ApplyGravity(
            _velocity.Y, Gravity, dt,
            IsOnFloor(), _jumpsRemaining, MaxJumps);

        bool jumpReq = !_buildCameraMode && Input.IsActionJustPressed("jump");
        (_velocity.Y, _jumpsRemaining) = CharacterMotion.TryJump(
            _velocity.Y, JumpVelocity, _jumpsRemaining, jumpReq);

        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;
    }

    private void ReconcilePlayerPrediction(float dt)
    {
        if (_entity.Id == 0)
            return;

        if (!_entity.TryGetComponent<RemoteEntityState>(out var remoteState) || remoteState.IsLocalPlayer == 0)
            return;

        if (!_entity.TryGetComponent<WorldPosition>(out var pos))
            return;

        var authoritativePos = ClampPresentationToTerrain(new Vector3(pos.X, pos.Y, pos.Z));
        var error = authoritativePos - GlobalPosition;
        if (error.LengthSquared() > PlayerPredictionSnapDistance * PlayerPredictionSnapDistance)
        {
            GlobalPosition = authoritativePos;
            Velocity = Vector3.Zero;
            _velocity = Vector3.Zero;
            return;
        }

        GlobalPosition = GlobalPosition.Lerp(authoritativePos, Mathf.Clamp(PlayerPredictionCorrectionRate * dt, 0f, 1f));
    }

    /// <summary>
    /// AI 载具跟随 — 角色不被控制但在载具中时，载具自动驶向 AiMovement 目标。
    /// 仅驾驶座控制载具运动；其他座位被动跟随载具位置。
    /// </summary>
    private void ProcessAiVehicleFollow(float dt)
    {
        int vehicleEntityId = ResolveEffectiveVehicleEntityId();
        if (_store == null || vehicleEntityId <= 0) return;

        var vehicleEntity = _store.GetEntityById(vehicleEntityId);
        if (vehicleEntity.IsNull)
        {
            ClearVehicleControlRuntimeState(syncControlState: false);
            ClearRemoteVehicleSeatState();
            SyncLocalControlStateToEcs();
            return;
        }

        // ── 载具瞬移检测：SquadFollowSystem 已直接设置载具 WorldPosition ──
        if (vehicleEntity.Tags.Has<Teleported>())
        {
            _ecsAuth?.RemoveTag<Teleported>(vehicleEntity);
            // 同步角色位置到载具新位置（座椅偏移）
            if (vehicleEntity.TryGetComponent<WorldPosition>(out var tpPos))
            {
                var seatOff = Vector3.Zero;
                if (_entity.TryGetComponent<VehicleSeat>(out var sc))
                    seatOff = new Vector3(sc.OffsetX, sc.OffsetY, sc.OffsetZ);
                Quaternion vq = Quaternion.Identity;
                if (vehicleEntity.TryGetComponent<WorldRotation>(out var tpRot))
                    vq = new Quaternion(tpRot.X, tpRot.Y, tpRot.Z, tpRot.W);
                var wo = vq * seatOff;
                GlobalPosition = new Vector3(tpPos.X + wo.X, tpPos.Y + wo.Y, tpPos.Z + wo.Z);
                _velocity = Vector3.Zero;

                // 载具根位置/旋转供相机使用
                _vehicleRootPosition = new Vector3(tpPos.X, tpPos.Y, tpPos.Z);
                _vehicleRotation = vq;
                _vehicleCameraAnchor = _vehicleRootPosition;

                Velocity = Vector3.Zero;
                _velocity = Vector3.Zero;
                SyncToEcs();
            }
            return;
        }

        if (!vehicleEntity.TryGetComponent<VehicleState>(out var vs)) return;
        if (!vehicleEntity.TryGetComponent<WorldPosition>(out var vPos)) return;

        // 只有驾驶座才驱动载具移动
        bool isDriver = _entity.TryGetComponent<VehicleSeat>(out var mySeat)
                        && mySeat.SeatType == (byte)Ark.Shared.Data.SeatType.Driver;

        if (isDriver)
        {
            // 读取 AI 移动目标（由 SquadFollowSystem 写入）
            if (_entity.TryGetComponent<AiMovement>(out var movement) && movement.IsMoving != 0)
            {
                var targetPos = new Vector3(movement.TargetX, movement.TargetY, movement.TargetZ);
                var vehiclePos = new Vector3(vPos.X, vPos.Y, vPos.Z);
                var toTarget = targetPos - vehiclePos;
                toTarget.Y = 0;
                float dist = toTarget.Length();

                if (dist > 2.0f)
                {
                    var aiDirection = toTarget.Normalized();
                    float targetYaw = Mathf.Atan2(-aiDirection.X, -aiDirection.Z);

                    if (!vehicleEntity.TryGetComponent<WorldRotation>(out var vRot)) vRot = WorldRotation.Identity;
                    var vehicleQuat = new Quaternion(vRot.X, vRot.Y, vRot.Z, vRot.W);
                    float currentYaw = vehicleQuat.GetEuler().Y;
                    float turnSpeed = dist > 10f ? 8.0f : 5.0f;
                    float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, turnSpeed * dt);
                    vehicleQuat = new Quaternion(Vector3.Up, newYaw);

                    var forward = vehicleQuat * Vector3.Forward;
                    float speed = dist > 15f ? vs.MaxSpeed * 1.8f
                                : dist > 8f  ? vs.MaxSpeed
                                :              vs.MaxSpeed * 0.5f;
                    vs.CurrentSpeed = Mathf.MoveToward(vs.CurrentSpeed, speed, vs.MaxSpeed * dt * 12f);

                    var velocity = forward * vs.CurrentSpeed;
                    vPos.X += velocity.X * dt;
                    vPos.Z += velocity.Z * dt;

                    _ecsAuth?.Write(vehicleEntity, vPos);
                    _ecsAuth?.Write(vehicleEntity, new WorldRotation { X = vehicleQuat.X, Y = vehicleQuat.Y, Z = vehicleQuat.Z, W = vehicleQuat.W });
                    _ecsAuth?.Write(vehicleEntity, vs);
                }
                else
                {
                    vs.CurrentSpeed = Mathf.MoveToward(vs.CurrentSpeed, 0, vs.MaxSpeed * dt * 4f);
                    _ecsAuth?.Write(vehicleEntity, vs);
                }
            }
            else
            {
                vs.CurrentSpeed = Mathf.MoveToward(vs.CurrentSpeed, 0, vs.MaxSpeed * dt);
                _ecsAuth?.Write(vehicleEntity, vs);
            }
        }

        // 同步角色位置到载具座椅（所有座位都需要）
        if (vehicleEntity.TryGetComponent<WorldPosition>(out var finalPos))
        {
            if (_sampleTerrainHeight != null
                && vehicleEntity.TryGetComponent<VehicleState>(out var terrainVs)
                && Ark.Gameplay.Vehicle.VehicleTerrainSystem.IsGroundVehicle((Ark.Shared.Data.VehicleType)terrainVs.VehicleType))
            {
                finalPos.Y = _sampleTerrainHeight(finalPos.X, finalPos.Z);
                _ecsAuth?.Write(vehicleEntity, finalPos);
            }

            var seatOffset = Vector3.Zero;
            if (_entity.TryGetComponent<VehicleSeat>(out var seatComp))
                seatOffset = new Vector3(seatComp.OffsetX, seatComp.OffsetY, seatComp.OffsetZ);
            Quaternion vq2 = Quaternion.Identity;
            if (vehicleEntity.TryGetComponent<WorldRotation>(out var finalRot))
                vq2 = new Quaternion(finalRot.X, finalRot.Y, finalRot.Z, finalRot.W);
            var worldOffset = vq2 * seatOffset;
            GlobalPosition = new Vector3(finalPos.X + worldOffset.X, finalPos.Y + worldOffset.Y, finalPos.Z + worldOffset.Z);
            _targetYaw = vq2.GetEuler().Y;

            // 载具根位置/旋转供相机使用
            _vehicleRootPosition = new Vector3(finalPos.X, finalPos.Y, finalPos.Z);
            _vehicleRotation = vq2;
            _vehicleCameraAnchor = _vehicleRootPosition;
        }
        Rotation = new Vector3(0, _targetYaw, 0);
        Velocity = Vector3.Zero;
        _velocity = Vector3.Zero;
        SyncToEcs();
    }

    /// <summary>
    /// AI 移动处理（当被其他角色控制时）。
    /// </summary>
    private void ProcessAiMovement(float dt)
    {
        if (_store == null || _entity.Id == 0) return;

        // ── 瞬移检测：ECS 位置已由 SquadFollowSystem 直接设置 ──
        if (_entity.Tags.Has<Teleported>())
        {
            _ecsAuth?.RemoveTag<Teleported>(_entity);
            if (_entity.TryGetComponent<WorldPosition>(out var tpPos))
            {
                GlobalPosition = new Vector3(tpPos.X, tpPos.Y, tpPos.Z);
                _velocity = Vector3.Zero;
            }
            return;
        }

        if (!_entity.TryGetComponent<AiMovement>(out var movement)) return;

        if (movement.IsMoving == 0)
        {
            // 停止时面朝领队方向（武器瞄准方向与领队一致）
            if (!IsOnFloor())
                _velocity.Y -= Gravity * dt;
            else if (_velocity.Y < 0)
                _velocity.Y = 0;

            _velocity.X = Mathf.MoveToward(_velocity.X, 0, Deceleration * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, Deceleration * dt);

            float facingYaw = Mathf.LerpAngle(Rotation.Y, movement.FacingYaw, 8f * dt);
            Rotation = new Vector3(0, facingYaw, 0);

            Velocity = _velocity;
            MoveAndSlide();
            _velocity = Velocity;
            SyncToEcs();
            return;
        }

        // 计算朝向目标的方向
        var targetPos2 = new Vector3(movement.TargetX, movement.TargetY, movement.TargetZ);
        var toTarget2  = targetPos2 - GlobalPosition;
        toTarget2.Y = 0;

        float distSq = toTarget2.LengthSquared();

        if (distSq < ArrivalRadius * ArrivalRadius)
        {
            // 已到达
            _ecsAuth?.Write(_entity, AiMovement.Arrived(movement));
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, Deceleration * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, Deceleration * dt);
        }
        else
        {
            // 移动中 — 面朝移动方向
            var moveDir = toTarget2.Normalized();
            var targetVel = moveDir * movement.MoveSpeed;
            _velocity.X = Mathf.MoveToward(_velocity.X, targetVel.X, Acceleration * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, targetVel.Z, Acceleration * dt);

            float targetRotY = Mathf.Atan2(-moveDir.X, -moveDir.Z);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetRotY, 10f * dt), 0);
        }

        // 重力
        if (!IsOnFloor())
            _velocity.Y -= Gravity * dt;
        else if (_velocity.Y < 0)
            _velocity.Y = 0;

        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;
        SyncToEcs();
    }

    /// <summary>
    /// 同步位置到 ECS。
    /// </summary>
    private void SyncToEcs()
    {
        if (_store == null || _entity.Id == 0) return;

        var pos = GlobalPosition;
        var rot = Quaternion;

        _ecsAuth?.Write(_entity, new WorldPosition { X = pos.X, Y = pos.Y, Z = pos.Z });
        _ecsAuth?.Write(_entity, new WorldRotation { X = rot.X, Y = rot.Y, Z = rot.Z, W = rot.W });
        _ecsAuth?.Write(_entity, new Velocity { X = _velocity.X, Y = _velocity.Y, Z = _velocity.Z, Speed = new Vector2(_velocity.X, _velocity.Z).Length() });
        SyncLocalControlStateToEcs();
    }
}
