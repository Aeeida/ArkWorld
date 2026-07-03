using Godot;
using System;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Gameplay.Combat;
using Ark.Abstractions;
using Ark.Configuration;
using Ark.Player.Character;
using Ark.Player.Vehicle;
using Ark.Services.Remote;
using Ark.Gameplay.Vehicle;
using Ark.Shared.Data;

namespace Ark.Player;

/// <summary>
/// TPS 第三人称玩家控制器 — 用于调试场景的自由移动。
/// 
/// 实现 ICameraTarget 接口，可被全局 CameraController 跟随。
/// 支持 WASD 移动、空格跳跃、Shift 加速。
/// 当切换到其他角色时，自动执行 AI 跟随行为。
/// </summary>
public partial class TpsPlayerController : CharacterBody3D, IPlayerController, IControllableCameraTarget
{
    // ═══ 移动参数 ═══
    [ExportGroup("Movement")]
    [Export] public float WalkSpeed { get; set; } = 5.0f;
    [Export] public float SprintSpeed { get; set; } = 10.0f;
    [Export] public float Acceleration { get; set; } = 10.0f;
    [Export] public float Deceleration { get; set; } = 15.0f;
    [Export] public float AirControl { get; set; } = 0.3f;
    [Export] public float ArrivalRadius { get; set; } = 0.5f;

    // ═══ 跳跃参数 ═══
    [ExportGroup("Jump")]
    [Export] public float JumpVelocity { get; set; } = 6.0f;
    [Export] public float Gravity { get; set; } = 20.0f;
    [Export] public int MaxJumps { get; set; } = 2;

    // ═══ 相机目标参数 ═══
    [ExportGroup("Camera Target")]
    [Export] public Vector3 CameraAnchorOffset { get; set; } = new(0, 1.6f, 0);
    [Export] public Vector3 DefaultOffset { get; set; } = new(0, 0.5f, 4f);
    [Export] public float CameraMinZoom { get; set; } = 2.0f;
    [Export] public float CameraMaxZoom { get; set; } = 100.0f;

    // ═══ 旧相机参数（向后兼容）═══
    [ExportGroup("Legacy Camera")]
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    [Export] public float MinPitch { get; set; } = -80.0f;
    [Export] public float MaxPitch { get; set; } = 80.0f;
    [Export] public float ZoomSpeed { get; set; } = 1.0f;

    // ═══ 内部状态 ═══
    private int     _jumpsRemaining;
    private Vector3 _velocity;
    private float   _targetYaw;
    private float   _targetPitch;
    private bool    _mouseCaptured;
    private float   _currentZoom = 4f;
    private bool    _isActive = true;
    private bool    _useGlobalCamera;  // 是否使用全局相机

    // ═══ ECS 引用（用于 AI 模式）═══
    private EntityStore? _store;
    private PlayerEcsAuthority? _ecsAuth;
    private Entity       _entity;

    // ═══ 节点引用（旧相机系统）═══
    private Node3D?         _cameraRig;
    private Node3D?         _cameraArm;
    private Camera3D?       _camera;
    private MeshInstance3D? _playerMesh;

    // ═══ 全局相机引用 ═══
    private CameraController? _globalCamera;

    // ═══ 建造相机模式 ═══
    private bool    _buildCameraMode;
    private bool    _buildModeOrbiting;
    private float   _savedPitch;
    private float   _savedYaw;

    // ═══ 战斗系统 ═══
    private CombatGameplayModule? _combatModule;
    private GameplayDefinitionCatalog? _combatData;
    private bool    _isFiring;
    private int     _nearbyVehicleId = -1;
    private bool    _inVehicle;
    private int     _currentVehicleId = -1;
    private float   _vehiclePitch;      // 飞机俯仰角 (°)，仅飞行器使用
    private bool    _externalControlLocked;
    private bool    _hasPredictedVehiclePose;
    private float   _predictedVehicleSpeed;
    private Vector3 _predictedVehicleRootPosition;
    private Quaternion _predictedVehicleRootRotation = Quaternion.Identity;
    private ulong   _networkWeaponFireSequence;
    private ulong   _networkVehicleInputSequence;

    // ═══ 载具状态（载具模式下相机跟踪载具根节点而非角色物理体）═══
    private Vector3 _vehicleCameraAnchor;
    private Vector3 _vehicleRootPosition;
    private Quaternion _vehicleRotation = Quaternion.Identity;

    // ═══ 地形查询（由 GameBootstrap 注入）═══
    private Func<float, float, float>? _sampleTerrainHeight;

    private static readonly Vector3 TpsCameraOffset   = new(0f, 0.5f, 4f);
    private static readonly Vector3 BuildCameraOffset = new(0f, 6f, 12f);
    private const float PlayerPredictionSnapDistance = 4.0f;
    private const float PlayerPredictionCorrectionRate = 10.0f;
    private const float VehiclePredictionSnapDistance = 12.0f;
    private const float VehiclePredictionCorrectionRate = 8.0f;

    // ═══ 调试 UI ═══
    private Label? _debugLabel;
    private CanvasLayer? _debugCanvas;
    private Ark.UI.CrosshairWidget? _crosshairWidget;

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开属性
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>玩家相机（旧系统）或全局相机</summary>
    public Camera3D? Camera => _useGlobalCamera ? _globalCamera?.Camera : _camera;

    /// <summary>关联的 ECS 实体</summary>
    public Entity Entity => _entity;

    /// <summary>是否处于激活状态（被玩家控制）</summary>
    public bool IsActive => _isActive;

    /// <summary>鼠标是否处于锁定（FPS/TPS 模式）状态。</summary>
    public bool IsMouseCaptured => _mouseCaptured;

    /// <summary>强制恢复鼠标锁定状态（环境切换后使用）。</summary>
    public void ForceMouseCaptured()
    {
        _mouseCaptured = true;
        _isFiring = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        SyncLocalControlStateToEcs();
    }

    /// <summary>是否处于建造模式。</summary>
    public bool IsBuildModeActive => _buildCameraMode;

    /// <summary>是否处于载具中。</summary>
    public bool InVehicle => IsVehicleControlActive();

    /// <summary>当前载具实体 ID。</summary>
    public int VehicleEntityId => ResolveEffectiveVehicleEntityId();

    public bool IsExternalControlLocked => TryGetProjectedLocalControlState(out var controlState)
        ? controlState.ExternalControlLocked != 0
        : _externalControlLocked;

    public void SetExternalControlLock(bool locked)
    {
        _externalControlLocked = locked;
        if (locked)
        {
            _isFiring = false;
            _velocity = Vector3.Zero;
            Velocity = Vector3.Zero;
        }

        SyncLocalControlStateToEcs(
            controlSourceOverride: ResolveEffectiveLocalControlSource(),
            externalControlLockedOverride: locked);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          ICameraTarget 实现
    // ═══════════════════════════════════════════════════════════════════════

    public Vector3 CameraAnchorPosition => IsVehicleControlActive() ? _vehicleCameraAnchor + CameraAnchorOffset : GlobalPosition + CameraAnchorOffset;
    public Quaternion CameraAnchorRotation => IsVehicleControlActive() ? _vehicleRotation : Quaternion;
    public CameraMode PreferredCameraMode => CameraMode.ThirdPerson;
    public Vector3 DefaultCameraOffset => DefaultOffset;
    public bool CanReceiveInput => _isActive;
    public bool AllowCameraRotation => true;
    float ICameraTarget.MinZoom => CameraMinZoom;
    float ICameraTarget.MaxZoom => CameraMaxZoom;

    public void OnCameraAttached()
    {
        GD.Print($"[TpsPlayer] Camera attached");
    }

    public void OnCameraDetached()
    {
        GD.Print($"[TpsPlayer] Camera detached");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          IControllableCameraTarget 实现
    // ═══════════════════════════════════════════════════════════════════════

    public void ProcessMovementInput(Vector2 input, bool sprint)
    {
        // 由 _PhysicsProcess 处理，这里留空
        // 或者可以缓存输入供 _PhysicsProcess 使用
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
        // 由 _Input 中 F 键处理
    }

    public void ProcessPrimaryAction(bool pressed)
    {
        _isFiring = pressed;
    }

    public void ProcessSecondaryAction(bool pressed)
    {
        // TODO: 次要动作（瞄准等）
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          初始化
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 设置 ECS 引用（由 SquadModule 调用）。
    /// </summary>
    public void SetEntity(EntityStore store, Entity entity)
    {
        _store  = store;
        _ecsAuth = new PlayerEcsAuthority(store);
        _entity = entity;
        AdoptControlledEntityState();
    }

    private void AdoptControlledEntityState()
    {
        if (_store == null || _entity.Id == 0)
            return;

        if (_entity.TryGetComponent<WorldPosition>(out var pos))
            GlobalPosition = ClampPresentationToTerrain(new Vector3(pos.X, pos.Y, pos.Z));

        if (_entity.TryGetComponent<WorldRotation>(out var rot))
        {
            var q = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
            Quaternion = q;
            var euler = q.GetEuler();
            _targetYaw = euler.Y;
        }

        if (_entity.TryGetComponent<VehicleSeat>(out var vehicleSeat))
        {
            ApplyVehicleControlRuntimeState(vehicleSeat.VehicleEntityId);
        }
        else
        {
            ClearVehicleControlRuntimeState();
        }
    }

    private void ApplyVehicleControlRuntimeState(int vehicleEntityId)
    {
        _inVehicle = vehicleEntityId > 0;
        _currentVehicleId = vehicleEntityId > 0 ? vehicleEntityId : -1;
        _isFiring = false;
        _hasPredictedVehiclePose = false;
        _predictedVehicleSpeed = 0f;

        if (vehicleEntityId > 0)
        {
            SyncControlledVehicleState(vehicleEntityId);
            SetVehicleCameraMode(true);
            CollisionLayer = 0;
            CollisionMask = 0;
            if (_playerMesh != null) _playerMesh.Visible = true;
        }

        SyncLocalControlStateToEcs(
            LocalControlMode.Vehicle,
            vehicleEntityId,
            controlSourceOverride: LocalControlSource.VehicleSeat);
    }

    private void ClearVehicleControlRuntimeState(bool clearVisualOffset = true, bool syncControlState = true)
    {
        _inVehicle = false;
        _currentVehicleId = -1;
        _vehiclePitch = 0f;
        _hasPredictedVehiclePose = false;
        _predictedVehicleSpeed = 0f;
        _isFiring = false;
        SetVehicleCameraMode(false);
        if (clearVisualOffset)
            ClearSeatedVisualOffset();
        CollisionLayer = 2;
        CollisionMask = 1 | 2 | 4;
        if (_playerMesh != null) _playerMesh.Visible = true;
        if (syncControlState)
            SyncLocalControlStateToEcs(
                LocalControlMode.Character,
                0,
                controlSourceOverride: LocalControlSource.CharacterDirect);
    }

    private void SyncControlledVehicleState(int vehicleEntityId)
    {
        if (_store == null || vehicleEntityId <= 0)
            return;

        var vehicleEntity = _store.GetEntityById(vehicleEntityId);
        if (vehicleEntity.IsNull)
            return;

        if (vehicleEntity.TryGetComponent<WorldPosition>(out var vehiclePos))
            _vehicleRootPosition = new Vector3(vehiclePos.X, vehiclePos.Y, vehiclePos.Z);

        if (vehicleEntity.TryGetComponent<WorldRotation>(out var vehicleRot))
            _vehicleRotation = new Quaternion(vehicleRot.X, vehicleRot.Y, vehicleRot.Z, vehicleRot.W);

        _vehicleCameraAnchor = _vehicleRootPosition;
    }

    private Vector3 ResolveVehicleExitPosition()
    {
        var exitPos = GlobalPosition;
        var rightDir = _vehicleRotation * Vector3.Right;
        exitPos = new Vector3(
            _vehicleRootPosition.X + rightDir.X * 3f,
            _vehicleRootPosition.Y + 1f,
            _vehicleRootPosition.Z + rightDir.Z * 3f);

        if (_sampleTerrainHeight != null)
        {
            float terrainY = _sampleTerrainHeight(exitPos.X, exitPos.Z);
            if (exitPos.Y < terrainY + 0.5f)
                exitPos.Y = terrainY + 0.5f;
        }

        return exitPos;
    }

    private void ApplySeatedVisualOffset(Vector3 seatOffset)
    {
        if (_playerMesh != null)
            _playerMesh.Position = new Vector3(0, 0.9f, 0) + seatOffset;
    }

    private void ClearSeatedVisualOffset()
    {
        if (_playerMesh != null)
            _playerMesh.Position = new Vector3(0, 0.9f, 0);
    }

    private void SyncSeatedCharacterToEcs(Vector3 seatWorldPos, Quaternion seatRotation)
    {
        if (_entity.Id == 0)
            return;

        _ecsAuth?.Write(_entity, new WorldPosition { X = seatWorldPos.X, Y = seatWorldPos.Y, Z = seatWorldPos.Z });
        _ecsAuth?.Write(_entity, new WorldRotation { X = seatRotation.X, Y = seatRotation.Y, Z = seatRotation.Z, W = seatRotation.W });
        _ecsAuth?.Write(_entity, new Velocity { X = 0f, Y = 0f, Z = 0f, Speed = 0f });
    }

    private void SetVehicleCameraMode(bool active)
    {
        if (_cameraRig == null)
            return;

        _cameraRig.TopLevel = active;
        if (!active)
        {
            _cameraRig.Position = new Vector3(0, 1.6f, 0);
            _cameraRig.Rotation = Vector3.Zero;
        }
    }

    /// <summary>
    /// 设置全局相机控制器（新系统）。
    /// </summary>
    public void SetGlobalCamera(CameraController? camera)
    {
        _globalCamera = camera;
        _useGlobalCamera = camera != null;

        if (_useGlobalCamera && _camera != null)
        {
            // 禁用本地相机
            _camera.Current = false;
        }
    }

    /// <summary>
    /// 设置战斗模块引用（由 GameBootstrap 调用）。
    /// </summary>
    public void SetCombatModule(CombatGameplayModule? module)
    {
        _combatModule = module;
        BindRemoteVehicleEvents();
    }

    public void SetCombatData(GameplayDefinitionCatalog? data)
    {
        _combatData = data;
        BindRemoteVehicleEvents();
    }

    private int ResolveCurrentWeaponDefId()
    {
        if (_store is null || _entity.Id == 0)
            return 0;

        if (_entity.TryGetComponent<WeaponState>(out var ws) && ws.WeaponDefId > 0)
            return ws.WeaponDefId;

        return _entity.TryGetComponent<RemoteCombatState>(out var remoteCombatState)
            ? remoteCombatState.WeaponDefId
            : 0;
    }

    private VehicleDef? ResolveVehicleDef(int vehicleDefId) => _combatData?.VehicleDefs.Get(vehicleDefId);
    private WeaponDef? ResolveWeaponDef(int weaponDefId) => _combatData?.WeaponDefs.Get(weaponDefId);

    /// <summary>
    /// 设置地形高度查询回调（由 GameBootstrap 调用）。
    /// </summary>
    public void SetTerrainQuery(Func<float, float, float>? sampleHeight)
    {
        _sampleTerrainHeight = sampleHeight;
        if (_sampleTerrainHeight != null && !IsVehicleControlActive() && _entity.Id != 0)
        {
            var clamped = ClampPresentationToTerrain(GlobalPosition);
            GlobalPosition = clamped;
            _ecsAuth?.Write(_entity, new WorldPosition { X = clamped.X, Y = clamped.Y, Z = clamped.Z });
        }
    }

    private Vector3 ClampPresentationToTerrain(Vector3 position)
    {
        if (_sampleTerrainHeight == null || IsVehicleControlActive())
            return position;

        float terrainY = _sampleTerrainHeight(position.X, position.Z);
        float safeY = MathF.Max(position.Y, terrainY + 0.5f);
        return safeY == position.Y ? position : new Vector3(position.X, safeY, position.Z);
    }

    /// <summary>从 GameConfig 覆盖默认参数（Export 值优先，若未在编辑器修改则取配置值）。</summary>
    private void ApplyConfig()
    {
        var cfg = GameConfig.Current.Player;
        WalkSpeed        = cfg.WalkSpeed;
        SprintSpeed      = cfg.SprintSpeed;
        JumpVelocity     = cfg.JumpForce;
        Gravity          = cfg.Gravity;
        MaxJumps         = cfg.MaxJumps;
        MouseSensitivity = cfg.MouseSensitivity;
    }

    public override void _Ready()
    {
        ApplyConfig();

        // 捕获鼠标
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _mouseCaptured = true;

        // 碰撞层：Layer 2 = 角色，Mask = 1(建筑/地面) | 2(友方角色) | 4(敌方角色)
        CollisionLayer = 2;
        CollisionMask  = 1 | 2 | 4;

        // 创建玩家视觉模型（简单胶囊体）
        SetupPlayerVisual();

        // 创建相机（旧系统，向后兼容）
        SetupCamera();

        // 创建调试 UI
        SetupDebugUI();

        // 初始化跳跃次数
        _jumpsRemaining = MaxJumps;

        GD.Print("[TpsPlayer] Controller ready");
    }

    private void SetupPlayerVisual()
    {
        // 创建碰撞体
        var collision = new CollisionShape3D();
        var capsule = new CapsuleShape3D
        {
            Radius = 0.4f,
            Height = 1.8f
        };
        collision.Shape = capsule;
        collision.Position = new Vector3(0, 0.9f, 0);
        AddChild(collision);

        // 创建可见网格（胶囊体）
        _playerMesh = new MeshInstance3D();
        var meshCapsule = new CapsuleMesh
        {
            Radius = 0.4f,
            Height = 1.8f
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.6f, 1.0f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        meshCapsule.Material = material;
        _playerMesh.Mesh = meshCapsule;
        _playerMesh.Position = new Vector3(0, 0.9f, 0);
        AddChild(_playerMesh);

        // 添加方向指示器（前方）
        var dirIndicator = new MeshInstance3D();
        var box = new BoxMesh
        {
            Size = new Vector3(0.15f, 0.15f, 0.5f)
        };
        var dirMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.3f, 0.3f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        box.Material = dirMat;
        dirIndicator.Mesh = box;
        dirIndicator.Position = new Vector3(0, 1.2f, -0.5f);
        AddChild(dirIndicator);
    }

    private void SetupCamera()
    {
        // 相机支架（用于旋转）
        _cameraRig = new Node3D { Name = "CameraRig" };
        _cameraRig.Position = new Vector3(0, 1.6f, 0);
        AddChild(_cameraRig);

        // 相机臂（保存引用，便于建造模式改变偏移）
        _cameraArm = new Node3D { Name = "CameraArm" };
        _cameraRig.AddChild(_cameraArm);

        // 相机
        _camera = new Camera3D
        {
            Name     = "TpsCamera",
            Position = TpsCameraOffset,
            Fov      = 75,
            Near     = 0.1f,
            Far      = 5000f
        };
        _cameraArm.AddChild(_camera);
        _currentZoom = TpsCameraOffset.Z;

        _camera.LookAt(_cameraRig.GlobalPosition, Vector3.Up);
    }

    private void SetupDebugUI()
    {
        // 创建 CanvasLayer
        _debugCanvas = new CanvasLayer { Name = "DebugUI" };
        AddChild(_debugCanvas);

        // 调试标签
        _debugLabel = new Label
        {
            Position = new Vector2(10, 10),
            Text = ""
        };
        _debugLabel.AddThemeFontSizeOverride("font_size", 14);
        _debugCanvas.AddChild(_debugLabel);

        // 十字准心（+ 形，瞄准敌人时变红）
        _crosshairWidget = new Ark.UI.CrosshairWidget();
        _debugCanvas.AddChild(_crosshairWidget);
    }

    public override void _ExitTree()
    {
        UnbindRemoteVehicleEvents();
        base._ExitTree();
    }
}
