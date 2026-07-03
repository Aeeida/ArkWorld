using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;
using Ark.Gameplay.Combat;
using Ark.Configuration;
using Ark.Player.Character;
using Ark.Player.Vehicle;

namespace Ark.Player;

/// <summary>
/// 小队成员控制器 — AI 控制的 CharacterBody3D。
/// 
/// 实现 ICameraTarget 接口，可被全局 CameraController 跟随。
/// 负责：
/// - 根据 ECS 中的 AiMovement 组件移动角色
/// - 支持与建筑/环境的碰撞
/// - 可随时切换为玩家控制
/// </summary>
public partial class SquadMemberController : CharacterBody3D, ISquadMemberController, IControllableCameraTarget
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          导出参数
    // ═══════════════════════════════════════════════════════════════════════

    [ExportGroup("Movement")]
    [Export] public float WalkSpeed      { get; set; } = 5.0f;
    [Export] public float RunSpeed       { get; set; } = 10.0f;
    [Export] public float Acceleration   { get; set; } = 10.0f;
    [Export] public float Deceleration   { get; set; } = 15.0f;
    [Export] public float Gravity        { get; set; } = 20.0f;
    [Export] public float ArrivalRadius  { get; set; } = 0.5f;

    [ExportGroup("Jump")]
    [Export] public float JumpVelocity   { get; set; } = 6.0f;
    [Export] public int   MaxJumps       { get; set; } = 2;

    [ExportGroup("Camera Target")]
    [Export] public Vector3 CameraAnchorOffset { get; set; } = new(0, 1.6f, 0);
    [Export] public Vector3 DefaultOffset { get; set; } = new(0, 0.5f, 4f);
    [Export] public float CameraMinZoom { get; set; } = 2.0f;
    [Export] public float CameraMaxZoom { get; set; } = 100.0f;

    [ExportGroup("Legacy Camera")]
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    [Export] public float MinPitch         { get; set; } = -80.0f;
    [Export] public float MaxPitch         { get; set; } = 80.0f;
    [Export] public float ZoomSpeed        { get; set; } = 1.0f;

    // ═══════════════════════════════════════════════════════════════════════
    //                          内部状态
    // ═══════════════════════════════════════════════════════════════════════

    private EntityStore? _store;
    private PlayerEcsAuthority? _ecsAuth;
    private Entity       _entity;
    private bool         _isControlled;
    private Vector3      _velocity;
    private int          _jumpsRemaining;
    private int          _slotIndex;
    private Color        _squadColor;
    private bool         _useGlobalCamera;

    // 相机控制
    private float   _targetYaw;
    private float   _targetPitch;
    private float   _currentZoom = 4f;
    private bool    _mouseCaptured;

    // 建造相机模式（由 SquadModule 控制）
    private bool    _buildCameraMode;
    private bool    _buildModeOrbiting;
    private float   _savedPitch;
    private float   _savedYaw;

    private static readonly Vector3 TpsCameraOffset   = new(0f, 0.5f, 4f);
    private static readonly Vector3 BuildCameraOffset = new(0f, 6f, 12f);

    // 视觉
    private MeshInstance3D? _bodyMesh;
    private MeshInstance3D? _indicatorMesh;
    private Label3D?        _slotLabel;

    // 相机
    private Camera3D? _attachedCamera;
    private Node3D?   _cameraRig;
    private CameraController? _globalCamera;

    // 战斗
    private CombatGameplayModule? _combatModule;
    private bool    _isFiring;
    private int     _nearbyVehicleId = -1;
    private bool    _inVehicle;
    private int     _currentVehicleId = -1;
    private float   _vehiclePitch;      // 飞机俯仰角 (°)
    private Vector3 _vehicleRootPosition;
    private Quaternion _vehicleRotation = Quaternion.Identity;

    // 地形查询（由 GameBootstrap 注入）
    private Func<float, float, float>? _sampleTerrainHeight;

    // 调试 UI
    private Label? _debugLabel;
    private CanvasLayer? _debugCanvas;
    private Ark.UI.CrosshairWidget? _crosshairWidget;

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开属性
    // ═══════════════════════════════════════════════════════════════════════

    public Entity Entity => _entity;
    public int SlotIndex => _slotIndex;
    public Camera3D? Camera => _useGlobalCamera ? _globalCamera?.Camera : _attachedCamera;
    public bool IsMouseCaptured => _mouseCaptured;
    public bool IsBuildModeActive => _buildCameraMode;
    public bool InVehicle => _inVehicle;
    public int VehicleEntityId => _currentVehicleId;

    /// <summary>
    /// 注入战斗模块（由 GameBootstrap 调用）。
    /// </summary>
    public void SetCombatModule(object? combatModule)
    {
        _combatModule = combatModule as CombatGameplayModule;
    }

    /// <summary>
    /// 设置地形高度查询回调（由 GameBootstrap 调用）。
    /// </summary>
    public void SetTerrainQuery(Func<float, float, float>? sampleHeight)
    {
        _sampleTerrainHeight = sampleHeight;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          ICameraTarget 实现
    // ═══════════════════════════════════════════════════════════════════════

    public Vector3 CameraAnchorPosition => _inVehicle ? _vehicleRootPosition + CameraAnchorOffset : GlobalPosition + CameraAnchorOffset;
    public Quaternion CameraAnchorRotation => _inVehicle ? _vehicleRotation : Quaternion;
    public CameraMode PreferredCameraMode => CameraMode.ThirdPerson;
    public Vector3 DefaultCameraOffset => DefaultOffset;
    public bool CanReceiveInput => _isControlled;
    public bool AllowCameraRotation => true;
    float ICameraTarget.MinZoom => CameraMinZoom;
    float ICameraTarget.MaxZoom => CameraMaxZoom;

    public void OnCameraAttached()
    {
        GD.Print($"[SquadMember #{_slotIndex}] Camera attached");
    }

    public void OnCameraDetached()
    {
        GD.Print($"[SquadMember #{_slotIndex}] Camera detached");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          IControllableCameraTarget 实现
    // ═══════════════════════════════════════════════════════════════════════

    public void ProcessMovementInput(Vector2 input, bool sprint)
    {
        // 由 _PhysicsProcess 处理
    }

    public void ProcessJumpInput()
    {
        if (_jumpsRemaining > 0 && !_buildCameraMode)
        {
            _velocity.Y = JumpVelocity;
            _jumpsRemaining--;
        }
    }

    public void ProcessAimInput(Vector2 delta)
    {
        // 由 CameraController 处理
    }

    public void ProcessInteractInput()
    {
        // TODO: 交互系统
    }

    public void ProcessPrimaryAction(bool pressed)
    {
        // TODO: 主要动作
    }

    public void ProcessSecondaryAction(bool pressed)
    {
        // TODO: 次要动作
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          控制状态
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 设置全局相机控制器（新系统）。
    /// </summary>
    public void SetGlobalCamera(CameraController? camera)
    {
        _globalCamera = camera;
        _useGlobalCamera = camera != null;

        if (_useGlobalCamera && _attachedCamera != null)
        {
            _attachedCamera.Current = false;
        }
    }

    public bool IsControlled
    {
        get => _isControlled;
        set
        {
            bool wasControlled = _isControlled;
            _isControlled = value;
            UpdateIndicatorVisual();

            if (value && !wasControlled)
            {
                // 刚被激活 — 捕获鼠标
                _mouseCaptured = true;
                _isFiring = false;
                Input.MouseMode = Input.MouseModeEnum.Captured;
                _buildCameraMode = false;
            }
            else if (!value && wasControlled)
            {
                // 刚被停用 — 保留 _inVehicle/_currentVehicleId
                _mouseCaptured = false;
                _isFiring = false;
                _buildCameraMode = false;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          初始化
    // ═══════════════════════════════════════════════════════════════════════

    public void Initialize(EntityStore store, Entity entity, int slotIndex, Color color)
    {
        _store      = store;
        _ecsAuth    = new PlayerEcsAuthority(store);
        _entity     = entity;
        _slotIndex  = slotIndex;
        _squadColor = color;

        Name = $"SquadMember_{slotIndex}";
        CollisionLayer = 2;       // Layer 2 = 友方角色
        CollisionMask  = 1 | 4;   // 1 = 建筑/地面, 4 = 敌方角色

        _currentZoom = TpsCameraOffset.Z;
    }

    /// <summary>从 GameConfig 覆盖默认参数。</summary>
    private void ApplyConfig()
    {
        var cfg = GameConfig.Current.Player;
        WalkSpeed        = cfg.WalkSpeed;
        RunSpeed         = cfg.SprintSpeed;
        JumpVelocity     = cfg.JumpForce;
        Gravity          = cfg.Gravity;
        MaxJumps         = cfg.MaxJumps;
        MouseSensitivity = cfg.MouseSensitivity;
    }

    public override void _Ready()
    {
        ApplyConfig();
        SetupVisual();
        SetupDebugUI();
        GD.Print($"[SquadMember] Slot {_slotIndex} ready");
    }

    private void SetupVisual()
    {
        // ─── 身体 ───
        var capsule = new CapsuleMesh { Radius = 0.35f, Height = 1.6f };
        var bodyMat = new StandardMaterial3D
        {
            AlbedoColor = _squadColor,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        capsule.Material = bodyMat;
        _bodyMesh = new MeshInstance3D { Mesh = capsule };
        _bodyMesh.Position = new Vector3(0, 0.8f, 0);
        AddChild(_bodyMesh);

        // ─── 碰撞 ───
        var collision = new CollisionShape3D();
        collision.Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.6f };
        collision.Position = new Vector3(0, 0.8f, 0);
        AddChild(collision);

        // ─── 头顶指示器 ───
        var indicatorMesh = new SphereMesh { Radius = 0.15f };
        var indicatorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(_squadColor.Lightened(0.3f), 0.8f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        indicatorMesh.Material = indicatorMat;
        _indicatorMesh = new MeshInstance3D { Mesh = indicatorMesh };
        _indicatorMesh.Position = new Vector3(0, 2.0f, 0);
        AddChild(_indicatorMesh);

        // ─── 快捷键标签 ───
        _slotLabel = new Label3D
        {
            Text        = $"F{_slotIndex}",
            FontSize    = 32,
            PixelSize   = 0.01f,
            Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Modulate    = Colors.White,
        };
        _slotLabel.Position = new Vector3(0, 2.3f, 0);
        AddChild(_slotLabel);

        UpdateIndicatorVisual();
    }

    private void UpdateIndicatorVisual()
    {
        if (_indicatorMesh != null) _indicatorMesh.Visible = !_isControlled;
        if (_slotLabel != null)     _slotLabel.Visible     = !_isControlled;
        if (_debugCanvas != null)   _debugCanvas.Visible   = _isControlled;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          输入处理
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Input(InputEvent @event)
    {
        if (!_isControlled) return;

        // B 键由 SquadModule 统一处理，这里不响应

        // ═══ F 键：载具进入/退出 ═══
        if (@event.IsActionPressed("interact"))
        {
            if (_inVehicle)
            {
                ExitVehicle();
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (_nearbyVehicleId > 0)
            {
                EnterVehicle(_nearbyVehicleId);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // ═══ R 键：换弹 ═══
        if (@event.IsActionPressed("reload") && !_buildCameraMode)
        {
            int reloadTarget = _inVehicle ? _currentVehicleId : _entity.Id;
            _combatModule?.TryReload(reloadTarget);
        }

        // ═══ 左键：射击（仅鼠标锁定时）═══
        if (@event is InputEventMouseButton lmb &&
            lmb.ButtonIndex == MouseButton.Left && _mouseCaptured && !_buildCameraMode)
        {
            _isFiring = lmb.Pressed;
        }

        // ─── 建造相机模式下的相机旋转（RMB 按住）───
        if (_buildCameraMode)
        {
            if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
            {
                _buildModeOrbiting = rmb.Pressed;
                Input.MouseMode = _buildModeOrbiting
                    ? Input.MouseModeEnum.Captured
                    : Input.MouseModeEnum.Visible;
            }

            if (@event is InputEventMouseMotion buildMotion && _buildModeOrbiting)
            {
                _targetYaw   -= buildMotion.Relative.X * MouseSensitivity;
                _targetPitch -= buildMotion.Relative.Y * MouseSensitivity;
                _targetPitch  = Mathf.Clamp(_targetPitch, Mathf.DegToRad(-80f), Mathf.DegToRad(-10f));
            }

            // 建造模式滚轮缩放
            if (@event is InputEventMouseButton wheel)
            {
                if (wheel.ButtonIndex == MouseButton.WheelUp)
                    _currentZoom = Mathf.Max(_currentZoom - ZoomSpeed * 2f, CameraMinZoom);
                else if (wheel.ButtonIndex == MouseButton.WheelDown)
                    _currentZoom = Mathf.Min(_currentZoom + ZoomSpeed * 2f, CameraMaxZoom * 2f);
            }
            return;
        }

        // ─── 普通 TPS 模式鼠标视角 ───
        if (@event is InputEventMouseMotion mouseMotion && _mouseCaptured)
        {
            _targetYaw   -= mouseMotion.Relative.X * MouseSensitivity;
            _targetPitch -= mouseMotion.Relative.Y * MouseSensitivity;
            _targetPitch  = Mathf.Clamp(_targetPitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
        }

        // ─── ESC 释放鼠标 ───
        if (@event.IsActionPressed("ui_cancel"))
        {
            _mouseCaptured  = !_mouseCaptured;
            Input.MouseMode = _mouseCaptured
                ? Input.MouseModeEnum.Captured
                : Input.MouseModeEnum.Visible;
            if (!_mouseCaptured) _isFiring = false;
        }

        // ─── 滚轮缩放 ───
        if (@event is InputEventMouseButton wheelEvent)
        {
            if (wheelEvent.ButtonIndex == MouseButton.WheelUp)
                _currentZoom = Mathf.Max(_currentZoom - ZoomSpeed, CameraMinZoom);
            else if (wheelEvent.ButtonIndex == MouseButton.WheelDown)
                _currentZoom = Mathf.Min(_currentZoom + ZoomSpeed, CameraMaxZoom);
        }
    }

    /// <summary>
    /// 设置建造相机模式（俯视）— 由 SquadModule 调用。
    /// </summary>
    public void SetBuildCameraMode(bool active)
    {
        _buildCameraMode = active;

        if (active)
        {
            _savedPitch = _targetPitch;
            _savedYaw   = _targetYaw;
            _targetPitch = Mathf.DegToRad(-50f);
            _mouseCaptured = false;
            _buildModeOrbiting = false;
        }
        else
        {
            _targetPitch = _savedPitch;
            _targetYaw   = _savedYaw;
            _buildModeOrbiting = false;
            _mouseCaptured = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          物理更新
    // ═══════════════════════════════════════════════════════════════════════

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // 更新相机
        UpdateCamera(dt);

        if (_isControlled)
        {
            if (_inVehicle)
                ProcessVehicleControl(dt);
            else if (!_buildCameraMode)
                ProcessPlayerControl(dt);
        }
        else
        {
            if (_inVehicle)
                ProcessAiVehicleFollow(dt);
            else
                ProcessAiMovement(dt);
        }
    }

    private void UpdateCamera(float dt)
    {
        if (_cameraRig == null || _attachedCamera == null) return;

        // 应用相机旋转
        if (_inVehicle)
        {
            float vehicleYaw = _vehicleRotation.GetEuler().Y;
            _cameraRig.Rotation = new Vector3(_targetPitch, _targetYaw - vehicleYaw, 0);
            Rotation = new Vector3(0, vehicleYaw, 0);
        }
        else
        {
            _cameraRig.Rotation = new Vector3(_targetPitch, 0, 0);
            Rotation = new Vector3(0, _targetYaw, 0);
        }

        // 相机偏移平滑过渡 + 缩放
        var targetOffset = _buildCameraMode
            ? new Vector3(0, BuildCameraOffset.Y, _currentZoom)
            : new Vector3(0, TpsCameraOffset.Y, _currentZoom);

        _attachedCamera.Position = _attachedCamera.Position.Lerp(targetOffset, 7f * dt);

        // ── 地形穿透保护：相机不得低于地形表面 ──
        if (_sampleTerrainHeight != null)
        {
            var camGlobalPos = _attachedCamera.GlobalPosition;
            float terrainY = _sampleTerrainHeight(camGlobalPos.X, camGlobalPos.Z);
            if (camGlobalPos.Y < terrainY + 0.5f)
            {
                float correction = terrainY + 0.5f - camGlobalPos.Y;
                _attachedCamera.Position = new Vector3(
                    _attachedCamera.Position.X,
                    _attachedCamera.Position.Y + correction,
                    _attachedCamera.Position.Z);
            }
        }

        // ── 动态远裁面：高空时扩展以看到星球 ──
        float altitude = _attachedCamera.GlobalPosition.Y;
        _attachedCamera.Far = altitude > 2000f ? 200000f : altitude > 500f ? 50000f : 5000f;
    }

    private void ProcessPlayerControl(float dt)
    {
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        Vector3 direction = Vector3.Zero;

        if (inputDir != Vector2.Zero)
        {
            // 使用角色的 yaw 方向（与相机同步）
            var basis = new Basis(Vector3.Up, _targetYaw);
            direction = (basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        }

        bool isSprinting = Input.IsActionPressed("sprint");
        float targetSpeed = CharacterMotion.TargetSpeed(isSprinting, WalkSpeed, RunSpeed);
        Vector3 targetVelocity = direction * targetSpeed;

        // 水平移动（via CharacterMotion）
        (_velocity.X, _velocity.Z) = CharacterMotion.ApplyHorizontal(
            _velocity.X, _velocity.Z,
            targetVelocity.X, targetVelocity.Z,
            Acceleration, Deceleration, 0.3f,
            IsOnFloor(), direction != Vector3.Zero, dt);

        // 重力（via CharacterMotion）
        (_velocity.Y, _jumpsRemaining) = CharacterMotion.ApplyGravity(
            _velocity.Y, Gravity, dt,
            IsOnFloor(), _jumpsRemaining, MaxJumps);

        // 跳跃（via CharacterMotion）
        bool jumpReq = !_buildCameraMode && Input.IsActionJustPressed("jump");
        (_velocity.Y, _jumpsRemaining) = CharacterMotion.TryJump(
            _velocity.Y, JumpVelocity, _jumpsRemaining, jumpReq);

        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;

        SyncToEcs();

        // 战斗逻辑（仅鼠标锁定 / TPS 模式下执行）
        if (_mouseCaptured)
        {
            ProcessShooting();
            DetectNearbyVehicles();
        }

        UpdateDebugUI();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          载具控制
    // ═══════════════════════════════════════════════════════════════════════

    private void ProcessVehicleControl(float dt)
    {
        if (_store == null || _currentVehicleId <= 0) return;

        var vehicleEntity = _store.GetEntityById(_currentVehicleId);
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

            // 转向（A/D）
            float turnRate = VehicleDriveInput.TurnRate(vs.CurrentSpeed, vs.MaxSpeed);
            float yawDelta = -inputDir.X * turnRate * dt;
            var rotDelta = new Quaternion(Vector3.Up, yawDelta);
            vehicleQuat = (rotDelta * vehicleQuat).Normalized();

            // 前进/后退（W/S）
            var forward = vehicleQuat * Vector3.Forward;
            float targetSpeed = VehicleDriveInput.TargetSpeed(-inputDir.Y, vs.MaxSpeed, isSprinting);
            vs.CurrentSpeed = VehicleDriveInput.ApproachSpeed(vs.CurrentSpeed, targetSpeed, vs.MaxSpeed, dt);

            // 更新载具 ECS 位置
            if (!vehicleEntity.TryGetComponent<WorldPosition>(out var vPos)) return;

            float velY = 0f;
            if (isAircraft)
            {
                float verticalInput = Input.GetAxis("aircraft_descend", "aircraft_ascend");
                float strafeInput = Input.GetAxis("aircraft_strafe_left", "aircraft_strafe_right");
                const float AircraftLiftFactor = 0.75f;
                const float AircraftStrafeFactor = 0.6f;

                var aircraftForward = (vehicleQuat * Vector3.Forward).Normalized();
                var aircraftRight = (vehicleQuat * Vector3.Right).Normalized();
                var velocity3D = aircraftForward * vs.CurrentSpeed
                    + aircraftRight * (strafeInput * vs.MaxSpeed * AircraftStrafeFactor)
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
            else
            {
                var velocity = forward * vs.CurrentSpeed;
                vPos.X += velocity.X * dt;
                vPos.Z += velocity.Z * dt;
                _ecsAuth?.Write(vehicleEntity, new WorldRotation { X = vehicleQuat.X, Y = vehicleQuat.Y, Z = vehicleQuat.Z, W = vehicleQuat.W });
            }

            _ecsAuth?.Write(vehicleEntity, vPos);
            _ecsAuth?.Write(vehicleEntity, vs);

            // 使用载具最终旋转计算正确的 3D 速度分量
            if (vehicleEntity.TryGetComponent<WorldRotation>(out var finalRot))
            {
                var fq = new Quaternion(finalRot.X, finalRot.Y, finalRot.Z, finalRot.W);
                var fwd = (fq * Vector3.Forward).Normalized();
                _ecsAuth?.Write(vehicleEntity, new Velocity { X = fwd.X * vs.CurrentSpeed, Y = fwd.Y * vs.CurrentSpeed, Z = fwd.Z * vs.CurrentSpeed, Speed = Mathf.Abs(vs.CurrentSpeed) });
            }
            else
            {
                _ecsAuth?.Write(vehicleEntity, new Velocity { X = forward.X * vs.CurrentSpeed, Y = velY, Z = forward.Z * vs.CurrentSpeed, Speed = Mathf.Abs(vs.CurrentSpeed) });
            }
        }

        ApplyVehicleSeatPose(vehicleEntity);
        Velocity = Vector3.Zero;
        _velocity = Vector3.Zero;
        SyncToEcs();

        // 载具炮塔射击（LMB）
        // 驾驶员：射击方向跟随载具前方
        // 炮手：射击方向指向屏幕中心瞄准心
        if (_isFiring && _combatModule != null)
        {
            var shootPos = vehicleEntity.TryGetComponent<WorldPosition>(out var sp) ? sp : default;
            var aimOrigin = new Vector3(shootPos.X, shootPos.Y + 2.0f, shootPos.Z);

            Vector3 aimDir;
            if (isDriver)
            {
                // 驾驶员：固定向载具完整 3D 前方射击
                if (!vehicleEntity.TryGetComponent<WorldRotation>(out var shootRot))
                    shootRot = vRot;
                var shootQuat = new Quaternion(shootRot.X, shootRot.Y, shootRot.Z, shootRot.W);
                aimDir = (shootQuat * Vector3.Forward).Normalized();
            }
            else
            {
                // 炮手/其他座位：从炮塔位置指向瞄准心命中点
                aimDir = GetAimDirectionTowardCrosshair(aimOrigin);
            }

            var sysOrigin = new System.Numerics.Vector3(aimOrigin.X, aimOrigin.Y, aimOrigin.Z);
            var sysDir = new System.Numerics.Vector3(aimDir.X, aimDir.Y, aimDir.Z);
            _combatModule.TryFire(_currentVehicleId, sysOrigin, sysDir, _combatModule.GameTime);
        }

        UpdateDebugUI();
    }

    private void EnterVehicle(int vehicleEntityId)
    {
        if (_combatModule == null || _entity.Id == 0) return;

        bool success = _combatModule.EnterVehicle(_entity.Id, vehicleEntityId, 0);
        if (success)
        {
            _inVehicle = true;
            _currentVehicleId = vehicleEntityId;
            _isFiring = false;
            _velocity = Vector3.Zero;
            Velocity = Vector3.Zero;
            GD.Print($"[SquadMember #{_slotIndex}] Entered vehicle {vehicleEntityId}");
        }
    }

    private void ExitVehicle()
    {
        if (_combatModule == null || _entity.Id == 0) return;

        // 记住载具位置和朝向，用于计算安全下车点
        Vector3 exitPos = GlobalPosition;
        if (_store != null && _currentVehicleId > 0)
        {
            var vehicleEntity = _store.GetEntityById(_currentVehicleId);
            if (!vehicleEntity.IsNull && vehicleEntity.TryGetComponent<WorldPosition>(out var vPos))
            {
                // 计算载具右侧偏移方向
                var exitDir = Vector3.Right;
                if (vehicleEntity.TryGetComponent<WorldRotation>(out var vRot))
                {
                    var q = new Quaternion(vRot.X, vRot.Y, vRot.Z, vRot.W);
                    exitDir = q * Vector3.Right;
                }
                exitPos = new Vector3(vPos.X, vPos.Y, vPos.Z) + exitDir * 3.5f + Vector3.Up * 0.5f;
            }
        }

        bool success = _combatModule.ExitVehicle(_entity.Id);
        if (success)
        {
            _inVehicle = false;
            _currentVehicleId = -1;
            _vehiclePitch = 0f;
            _vehicleRootPosition = exitPos;
            _vehicleRotation = Quaternion.Identity;
            GlobalPosition = exitPos;
            _velocity = Vector3.Zero;
            SyncToEcs();
            GD.Print($"[SquadMember #{_slotIndex}] Exited vehicle");
        }
    }

    public void CycleSeat()
    {
        if (_combatModule == null || _entity.Id == 0 || !_inVehicle) return;
        if (_combatModule.CycleToNextSeat(_entity.Id))
        {
            GD.Print($"[SquadMember #{_slotIndex}] Switched seat in vehicle {_currentVehicleId}");
        }
    }

    private void ProcessShooting()
    {
        if (_combatModule == null || _store == null || _entity.Id == 0) return;
        if (!_isFiring || _buildCameraMode || _inVehicle) return;

        var aimDir = GetAimDirection();
        var aimOrigin = GlobalPosition + new Vector3(0, 1.4f, 0);
        var sysOrigin = new System.Numerics.Vector3(aimOrigin.X, aimOrigin.Y, aimOrigin.Z);
        var sysDir = new System.Numerics.Vector3(aimDir.X, aimDir.Y, aimDir.Z);

        _combatModule.TryFire(_entity.Id, sysOrigin, sysDir, _combatModule.GameTime);
    }

    private void DetectNearbyVehicles()
    {
        _nearbyVehicleId = -1;
        if (_store == null || _inVehicle) return;

        var myPos = GlobalPosition;
        float closestDist = 6f;

        var query = _store.Query<WorldPosition, VehicleState>();
        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            var entities = chunk.Entities;
            for (int i = 0; i < entities.Length; i++)
            {
                var pos = positions.Span[i];
                float dx = pos.X - myPos.X;
                float dz = pos.Z - myPos.Z;
                float dist = MathF.Sqrt(dx * dx + dz * dz);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    _nearbyVehicleId = entities[i];
                }
            }
        }
    }

    /// <summary>获取相机朝向的世界方向。</summary>
    public Vector3 GetAimDirection()
    {
        var cam = Camera;
        if (cam == null) return -Transform.Basis.Z;
        return -cam.GlobalTransform.Basis.Z;
    }

    /// <summary>
    /// 获取从武器出发点指向屏幕中心瞄准心命中点的方向。
    /// 射线从相机中心投射到场景中，找到命中点后计算武器→命中点的方向。
    /// </summary>
    public Vector3 GetAimDirectionTowardCrosshair(Vector3 weaponOrigin)
    {
        var cam = Camera;
        if (cam == null) return -Transform.Basis.Z;

        var viewport = cam.GetViewport();
        var screenCenter = viewport.GetVisibleRect().Size * 0.5f;

        var rayOrigin = cam.ProjectRayOrigin(screenCenter);
        var rayDir = cam.ProjectRayNormal(screenCenter);

        var spaceState = cam.GetWorld3D()?.DirectSpaceState;
        if (spaceState != null)
        {
            var rayEnd = rayOrigin + rayDir * 500f;
            var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
            query.CollisionMask = 0xFFFFFFFF;
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            query.HitFromInside = false;
            var result = spaceState.IntersectRay(query);
            if (result.Count > 0)
            {
                var hitPoint = (Vector3)result["position"];
                var dir = (hitPoint - weaponOrigin);
                if (dir.LengthSquared() > 1f)
                    return dir.Normalized();
            }
        }

        var farPoint = rayOrigin + rayDir * 200f;
        return (farPoint - weaponOrigin).Normalized();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          调试 HUD
    // ═══════════════════════════════════════════════════════════════════════

    private void SetupDebugUI()
    {
        _debugCanvas = new CanvasLayer { Name = "MemberDebugUI" };
        AddChild(_debugCanvas);
        _debugCanvas.Visible = false; // 初始隐藏，只有被控制时才显示

        _debugLabel = new Label
        {
            Position = new Vector2(10, 10),
            Text = ""
        };
        _debugLabel.AddThemeFontSizeOverride("font_size", 14);
        _debugCanvas.AddChild(_debugLabel);

        // 十字准心
        _crosshairWidget = new Ark.UI.CrosshairWidget();
        _debugCanvas.AddChild(_crosshairWidget);
    }

    private void UpdateDebugUI()
    {
        if (_debugLabel == null || !_isControlled) return;

        var pos = GlobalPosition;
        var speed = new Vector2(_velocity.X, _velocity.Z).Length();

        string weaponInfo = "None";
        string ammoInfo = "";
        if (_store != null && _entity.Id != 0)
        {
            // 载具中显示载具武器，否则显示角色武器
            int weaponEntityId = _inVehicle && _currentVehicleId > 0 ? _currentVehicleId : _entity.Id;
            var weaponEntity = _store.GetEntityById(weaponEntityId);
            if (!weaponEntity.IsNull &&
                weaponEntity.TryGetComponent<WeaponState>(out var ws) &&
                weaponEntity.TryGetComponent<AmmoState>(out var ammo))
            {
                weaponInfo = ws.Category switch
                {
                    0 => "Fist", 1 => "Pistol", 2 => "Rifle",
                    3 => "Shotgun", 4 => "Sniper", 5 => "Launcher",
                    6 => "Melee", _ => $"W#{ws.WeaponDefId}"
                };
                ammoInfo = $"  Ammo: {ammo.CurrentMag}/{ammo.MagCapacity} ({ammo.ReserveAmmo})";
                if (ws.IsReloading != 0) ammoInfo += "  [装填中]";
            }
        }

        string healthInfo = "";
        if (_store != null && _entity.Id != 0 &&
            _entity.TryGetComponent<Health>(out var hp))
        {
            healthInfo = $"HP: {hp.Current:F0}/{hp.Max:F0}";
        }

        string vehicleHint = "";
        if (_inVehicle)
        {
            string seatName = "乘客";
            if (_entity.TryGetComponent<VehicleSeat>(out var sc))
                seatName = sc.SeatType switch { 0 => "驾驶", 1 => "炮手", _ => "乘客" };
            vehicleHint = $"[F] 退出载具  [Tab] 换座  座位: {seatName}";
        }
        else if (_nearbyVehicleId > 0)
            vehicleHint = "[F] 进入载具";

        _debugLabel.Text = $"""
            === Squad F{_slotIndex} ===
            Position: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})
            Speed: {speed:F1} m/s  |  {healthInfo}
            Weapon: {weaponInfo}{ammoInfo}
            {vehicleHint}

            [Controls]
            WASD - Move   |  Space - Jump
            Shift - Sprint  |  Mouse - Look
            LMB - Fire    |  R - Reload
            F - Vehicle Enter/Exit
            B - Build Mode  |  ESC - Release mouse
            F1~F5 - Switch Squad  |  Tab - Formation
            """;
    }

    /// <summary>
    /// AI 载具跟随 — 角色不被控制但在载具中时，载具自动驶向 AiMovement 目标。
    /// 仅驾驶座控制载具运动；其他座位被动跟随载具位置。
    /// </summary>
    private void ProcessAiVehicleFollow(float dt)
    {
        if (_store == null || _currentVehicleId <= 0) return;

        var vehicleEntity = _store.GetEntityById(_currentVehicleId);
        if (vehicleEntity.IsNull) { _inVehicle = false; _currentVehicleId = -1; return; }

        // ── 载具瞬移检测：SquadFollowSystem 已直接设置载具 WorldPosition ──
        if (vehicleEntity.Tags.Has<Teleported>())
        {
            _ecsAuth?.RemoveTag<Teleported>(vehicleEntity);
            ApplyVehicleSeatPose(vehicleEntity, true);
            _velocity = Vector3.Zero;
            Velocity = Vector3.Zero;
            SyncToEcs();
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
                    // 朝目标转向 — 转向速率随距离增加
                    var direction = toTarget.Normalized();
                    float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);

                    if (!vehicleEntity.TryGetComponent<WorldRotation>(out var vRot)) vRot = WorldRotation.Identity;
                    var vehicleQuat = new Quaternion(vRot.X, vRot.Y, vRot.Z, vRot.W);
                    float currentYaw = vehicleQuat.GetEuler().Y;
                    float turnSpeed = dist > 10f ? 8.0f : 5.0f;
                    float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, turnSpeed * dt);
                    vehicleQuat = new Quaternion(Vector3.Up, newYaw);

                    // 前进 — 远距离用 1.8× 超速追赶，近距离常速
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
                    // 到达目标，减速
                    vs.CurrentSpeed = Mathf.MoveToward(vs.CurrentSpeed, 0, vs.MaxSpeed * dt * 4f);
                    _ecsAuth?.Write(vehicleEntity, vs);
                }
            }
            else
            {
                // 没有移动目标，减速停下
                vs.CurrentSpeed = Mathf.MoveToward(vs.CurrentSpeed, 0, vs.MaxSpeed * dt);
                _ecsAuth?.Write(vehicleEntity, vs);
            }
        }

        ApplyVehicleSeatPose(vehicleEntity, true);
        _velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
        SyncToEcs();
    }

    private void ApplyVehicleSeatPose(Entity vehicleEntity, bool snapViewYaw = false)
    {
        if (!vehicleEntity.TryGetComponent<WorldPosition>(out var vehiclePos)
            || !vehicleEntity.TryGetComponent<WorldRotation>(out var vehicleRot))
            return;

        if (_sampleTerrainHeight != null
            && vehicleEntity.TryGetComponent<VehicleState>(out var terrainVs)
            && Ark.Gameplay.Vehicle.VehicleTerrainSystem.IsGroundVehicle((Ark.Shared.Data.VehicleType)terrainVs.VehicleType))
        {
            vehiclePos.Y = _sampleTerrainHeight(vehiclePos.X, vehiclePos.Z);
            _ecsAuth?.Write(vehicleEntity, vehiclePos);
        }

        var vehicleQuat = new Quaternion(vehicleRot.X, vehicleRot.Y, vehicleRot.Z, vehicleRot.W);
        var seatOffset = Vector3.Zero;
        if (_entity.TryGetComponent<VehicleSeat>(out var seatComp))
            seatOffset = new Vector3(seatComp.OffsetX, seatComp.OffsetY, seatComp.OffsetZ);

        var seatWorldPos = new Vector3(vehiclePos.X, vehiclePos.Y, vehiclePos.Z) + vehicleQuat * seatOffset;
        _vehicleRootPosition = new Vector3(vehiclePos.X, vehiclePos.Y, vehiclePos.Z);
        _vehicleRotation = vehicleQuat;
        GlobalPosition = seatWorldPos;
        Rotation = new Vector3(0, vehicleQuat.GetEuler().Y, 0);

        if (snapViewYaw)
            _targetYaw = Rotation.Y;
    }

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
            if (!IsOnFloor())
                _velocity.Y -= Gravity * dt;
            else if (_velocity.Y < 0)
                _velocity.Y = 0;

            _velocity.X = Mathf.MoveToward(_velocity.X, 0, Deceleration * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, Deceleration * dt);

            // 停止时面朝领队方向（武器瞄准方向与领队一致）
            _targetYaw = Mathf.LerpAngle(_targetYaw, movement.FacingYaw, 8f * dt);
            Rotation = new Vector3(0, _targetYaw, 0);

            Velocity = _velocity;
            MoveAndSlide();
            _velocity = Velocity;
            SyncToEcs();
            return;
        }

        var targetPos = new Vector3(movement.TargetX, movement.TargetY, movement.TargetZ);
        var toTarget  = targetPos - GlobalPosition;
        toTarget.Y = 0;

        float distSq = toTarget.LengthSquared();

        if (distSq < ArrivalRadius * ArrivalRadius)
        {
            _ecsAuth?.Write(_entity, AiMovement.Arrived(movement));
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, Deceleration * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, Deceleration * dt);
        }
        else
        {
            var direction = toTarget.Normalized();
            var targetVel = direction * movement.MoveSpeed;
            _velocity.X = Mathf.MoveToward(_velocity.X, targetVel.X, Acceleration * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, targetVel.Z, Acceleration * dt);

            // AI 模式下角色朝向移动方向
            float targetRotY = Mathf.Atan2(-direction.X, -direction.Z);
            _targetYaw = Mathf.LerpAngle(_targetYaw, targetRotY, 10f * dt);
        }

        Rotation = new Vector3(0, _targetYaw, 0);

        if (!IsOnFloor())
            _velocity.Y -= Gravity * dt;
        else if (_velocity.Y < 0)
            _velocity.Y = 0;

        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;
        SyncToEcs();
    }

    private void SyncToEcs()
    {
        if (_store == null || _entity.Id == 0) return;

        var pos = GlobalPosition;
        var rot = Quaternion;

        _ecsAuth?.Write(_entity, new WorldPosition { X = pos.X, Y = pos.Y, Z = pos.Z });
        _ecsAuth?.Write(_entity, new WorldRotation { X = rot.X, Y = rot.Y, Z = rot.Z, W = rot.W });
        _ecsAuth?.Write(_entity, new Velocity { X = _velocity.X, Y = _velocity.Y, Z = _velocity.Z, Speed = new Vector2(_velocity.X, _velocity.Z).Length() });
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开 API
    // ═══════════════════════════════════════════════════════════════════════

    public void SetMoveTarget(Vector3 target, float speed)
    {
        if (_store == null || _entity.Id == 0) return;
        _ecsAuth?.Write(_entity, new AiMovement
        {
            TargetX = target.X, TargetY = target.Y, TargetZ = target.Z,
            MoveSpeed = speed, IsMoving = 1, HasArrived = 0
        });
    }

    public void Stop()
    {
        if (_store == null || _entity.Id == 0) return;
        if (_entity.TryGetComponent<AiMovement>(out var movement))
        {
            _ecsAuth?.Write(_entity, new AiMovement
            {
                TargetX = movement.TargetX, TargetY = movement.TargetY, TargetZ = movement.TargetZ,
                MoveSpeed = 0, IsMoving = 0, HasArrived = 1
            });
        }
    }

    public void TeleportTo(Vector3 position)
    {
        GlobalPosition = position;
        _velocity = Vector3.Zero;
        SyncToEcs();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          相机管理
    // ═══════════════════════════════════════════════════════════════════════

    public void AttachCamera(Camera3D? camera)
    {
        if (camera == null) return;

        _attachedCamera = camera;

        if (_cameraRig == null)
        {
            _cameraRig = new Node3D { Name = "CameraRig" };
            _cameraRig.Position = new Vector3(0, 1.6f, 0);
            AddChild(_cameraRig);
        }

        var oldParent = camera.GetParent();
        oldParent?.RemoveChild(camera);
        _cameraRig.AddChild(camera);

        // 初始化相机位置和角度
        camera.Position = new Vector3(0, TpsCameraOffset.Y, _currentZoom);
        camera.Current = true;

        // 重置视角为当前角色朝向
        _targetYaw = Rotation.Y;
        _targetPitch = 0;

        GD.Print($"[SquadMember] Camera attached to slot {_slotIndex}");
    }

    public void DetachCamera()
    {
        if (_attachedCamera == null) return;

        if (_cameraRig != null && _attachedCamera.GetParent() == _cameraRig)
        {
            _cameraRig.RemoveChild(_attachedCamera);
        }

        _attachedCamera.Current = false;
        _attachedCamera = null;
        _buildCameraMode = false;

        GD.Print($"[SquadMember] Camera detached from slot {_slotIndex}");
    }
}
