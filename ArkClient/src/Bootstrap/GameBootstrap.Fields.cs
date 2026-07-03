using Godot;
using Friflo.Engine.ECS;
using Ark.Gpu;
using Ark.Services;
using Ark.Systems.Gpu;
using Ark.Services.Remote;
using Ark.Systems.Squad;
using Ark.Bridge.Features.BaseBuilding;
using Ark.Bridge.Features.Combat;
using Ark.Bridge.Features.Space;
using Ark.Bridge.Features.Squad;
using Ark.Gameplay.Combat;
using Ark.Gameplay.Combat.Systems;
using Ark.Gameplay.Squad;
using Ark.Camera;
using Ark.World;
using Ark.Interaction;
using Ark.Render;
using Ark.Player;
using Ark.UI;
using Ark.Events;
using Ark.Gameplay.Space;

namespace Ark;

public partial class GameBootstrap
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          子系统引用
    // ═══════════════════════════════════════════════════════════════════════

    // ─── 核心 ───
    private EntityStore       _store = null!;
    private BootstrapEcsAuthority _ecsAuth = null!;
    private GpuComputeManager _gpu   = null!;
    private EventBus          _eventBus = null!;

    // ─── 功能模块 ───
    private BaseBuildingModule?   _baseBuilding;
    private CombatModule?         _combat;
    private SpaceModule?          _space;
    private CombatGameplayModule? _combatGameplay;
    private GameplayDefinitionCatalog _combatData = null!;

    // ─── 战斗子系统 ───
    private ProjectileCollisionSystem? _projCollision;
    private DefeatedEntitySystem?      _defeatedSystem;
    private SquadCombatSystem?         _squadCombat;

    // ─── 载具地形 ───
    private Ark.Gameplay.Vehicle.VehicleTerrainSystem? _vehicleTerrain;

    // ─── 渲染 ───
    private Systems.Sync.MultiMeshSyncSystem? _multiMeshSync;
    private RemoteWorldEcsCacheSystem? _remoteWorldEcsCache;
    private NetworkVisualEventBuffer? _networkVisualEventBuffer;
    private Ark.Services.Remote.ServerEventEcsProjectionBuffer? _serverEventEcsProjection;
    private RemoteEcsStateSyncSystem? _remoteEcsStateSync;
    private NetworkEcsDispatchSystem? _networkEcsDispatch;

    // ─── Phase 3: 节点表现层推送（替代 Node 自轮询）───
    private Systems.Sync.CharacterPresentationSyncSystem? _characterPresentationSync;
    private Systems.Sync.VehicleHudSyncSystem? _vehicleHudSync;
    private Systems.Sync.LocalControlSyncSystem? _localControlSync;
    private Systems.Sync.PlayerHudSyncSystem? _playerHudSync;
    private Systems.Sync.RocketTelemetrySyncSystem? _rocketTelemetrySync;

    // ─── Phase 4: 本地预测/相机/输入意图（行为状态 Component 化）───
    private Systems.LocalControl.InputIntentCollectSystem? _inputIntentCollect;
    private Systems.LocalControl.LocalMovementPredictionSystem? _localMovementPredict;
    private Systems.LocalControl.CameraOrbitSystem? _cameraOrbit;

    // ─── GPU ───
    private GpuMovementSystem? _gpuMovement;
    private GpuCullingSystem?  _gpuCulling;

    // ─── 场景 / 视觉 ───
    private TpsPlayerController?      _player;
    private BuildPlacementController? _buildPlacement;
    private BuildingVisualManager?    _buildingVisuals;
    private WeaponVisualSystem?       _weaponVisuals;
    private EnemyVisualManager?       _enemyVisuals;

    // ─── 小队 ───
    private SquadModule?       _squad;
    private SquadFollowSystem? _squadFollow;

    // ─── 相机 ───
    private SquadCameraManager? _cameraManager;

    // ─── UI ───
    private SelectionHUD?              _selectionHUD;
    private SeatWeaponPanel?           _seatWeaponPanel;
    private CombatHudController?       _hudController;
    private PerfHud?                   _perfHud;
    private GameplayModeSwitchPanel?   _modeSwitchPanel;
    private NetworkInfoHud?            _networkInfoHud;
    private Ark.Bridge.Player.RemotePlayerBridge?        _remotePlayerBridge;
    private ModeHudManager?            _modeHudManager;
    private NpcInteractionPanel?       _npcInteractionPanel;

    // ─── 玩法模式 ───
    private GameplayMode _currentMode = GameplayMode.Combat;
    private WorldEnvironmentManager? _worldEnvManager;
    private bool _worldEntryCompleted;
    private bool _networkBuildMode;
    private ulong _playerInputCommandSequence;
    private ulong _spacecraftInputCommandSequence;

    // ─── 网络权威桥接器 ───
    private Ark.Services.Remote.ServerAuthorityBridge? _serverBridge;

    // ─── 火箭系统 ───
    private RocketAssemblyPanel?       _rocketPanel;
    private LaunchControlPanel?        _launchControlPanel;
    private LaunchSequenceController?  _launchController;
    private RocketCameraController?    _rocketCamera;
    private FlightReportPanel?         _flightReportPanel;
    private RocketConfig? _activeRocketConfig;
    private string _activeRocketConfigJson = string.Empty;
    private System.Guid _activeRocketNetworkId;
    private bool _hasPredictedRocketPose;
    private Vector3 _predictedRocketPosition;
    private Quaternion _predictedRocketRotation = Quaternion.Identity;
    private Vector3 _predictedRocketVelocity;
}
