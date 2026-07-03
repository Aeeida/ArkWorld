using System;
using Godot;
using Ark.Services;
using Ark.Gpu;
using Ark.Render;
using Ark.World;
using Ark.World.Core;
using Ark.Events;
using Ark.GameInput;
using Ark.Bridge.Features.Space;
using Ark.Camera;
using Ark.UI;
using Ark.Gameplay.Space;
using Ark.Shared.Data;
using Ark.Ecs.Components;

namespace Ark;

public partial class GameBootstrap
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          生命周期
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        GD.Print("╔══════════════════════════════════════╗");
        GD.Print("║       Ark GameBootstrap v0.3         ║");
        GD.Print("╚══════════════════════════════════════╝");

        // 安装服务层日志 sink（Ark.Services 不直接引用 Godot；通过 ServiceLog 门面输出）。
        ServiceLog.InfoSink  = static msg => GD.Print(msg);
        ServiceLog.ErrorSink = static msg => GD.PrintErr(msg);

        // 1. 核心基础设施
        _store = new Friflo.Engine.ECS.EntityStore();
        _ecsAuth = new BootstrapEcsAuthority(_store);
        _gpu   = new GpuComputeManager();
        _eventBus = new EventBus();
        _gpu.Initialize();
        GameServices.InitializeNetworkMode("<SERVER_HOST>", 5000);
        if (GameServices.World is Ark.Services.Remote.RemoteGameWorld remoteWorld)
        {
            _remoteWorldEcsCache = new Ark.Services.Remote.RemoteWorldEcsCacheSystem(_store, remoteWorld);
            GameServices.RegisterRemoteWorldEcsCache(_remoteWorldEcsCache);
            _networkVisualEventBuffer = new Ark.Services.Remote.NetworkVisualEventBuffer(_store);
        }

        // 2. 功能模块
        InitializeModules();

        // 3. 渲染同步
        _multiMeshSync = new Ark.Systems.Sync.MultiMeshSyncSystem(_store);
        MeshGroupRegistry.RegisterDefaults(this, _multiMeshSync);

        // 4. GPU 系统
        InitializeGpuSystems();

        // 5. 场景节点 + UI
        InitializeSceneNodes();

      // 6. 小队系统（网络模式下禁用本地小队，避免伪造可见实体）
        if (!GameServices.IsNetworkMode)
            InitializeSquadSystem();
        else
        {
            // 网络模式：不生成 AI 队员，但仍初始化本地玩家的 ECS 实体/武器/战斗模块
            InitializeNetworkPlayer();
            _remoteEcsStateSync = new RemoteEcsStateSyncSystem(_store);
            _networkEcsDispatch = new NetworkEcsDispatchSystem(_store);
            GD.Print("[GameBootstrap] Local squad disabled in NETWORK mode");
        }

        // Phase 3: 表现层推送基础设施（替代 Node 自轮询）
        _characterPresentationSync = new Ark.Systems.Sync.CharacterPresentationSyncSystem(_store);
        _vehicleHudSync = new Ark.Systems.Sync.VehicleHudSyncSystem(_store);
        _localControlSync = new Ark.Systems.Sync.LocalControlSyncSystem(_store);
        _playerHudSync = new Ark.Systems.Sync.PlayerHudSyncSystem(_store);
        _rocketTelemetrySync = new Ark.Systems.Sync.RocketTelemetrySyncSystem(_store);
        Ark.Systems.Sync.SyncSystemRegistry.Set(_characterPresentationSync, _vehicleHudSync, _localControlSync, _playerHudSync, _rocketTelemetrySync);
        RegisterHudSyncReceivers();

        // Phase 4: 本地预测/相机/输入意图（opt-in 注册，控制器迁移期共存）
        _inputIntentCollect = new Ark.Systems.LocalControl.InputIntentCollectSystem(_store);
        _localMovementPredict = new Ark.Systems.LocalControl.LocalMovementPredictionSystem(_store);
        _cameraOrbit = new Ark.Systems.LocalControl.CameraOrbitSystem(_store);
        Ark.Systems.LocalControl.LocalControlRegistry.Set(_inputIntentCollect, _localMovementPredict, _cameraOrbit);

        // 7. 输入映射
        InputActions.RegisterAll();

        // 8. 世界加载（网络模式：实体由服务端快照推送，无需本地 Demo 加载）
        // 角色武器由服务端状态驱动

        // 10. 调试 HUD
        _perfHud = new PerfHud { Name = "PerfHud" };
        AddChild(_perfHud);

        // 10b. 网络信息 HUD（\ 键切换显示）
        _networkInfoHud = new NetworkInfoHud { Name = "NetworkInfoHud" };
        AddChild(_networkInfoHud);
        _networkInfoHud.SetEntityStore(_store);
        _networkInfoHud.OnWorldEntryCompleted += OnWorldEntryCompleted;

        // 11. 玩法模式切换面板（始终显示）
        _modeSwitchPanel = new GameplayModeSwitchPanel { Name = "ModeSwitchPanel" };
        AddChild(_modeSwitchPanel);
        _modeSwitchPanel.OnModeSelected += OnGameplayModeSelected;
        _modeSwitchPanel.OnForceExitSpaceMode += OnForceExitSpaceMode;

        // 11b. 模式 HUD 管理器（CivilianHud / CombatModeHud / SpaceModeHud / CrosshairWidget）
        _modeHudManager = new ModeHudManager { Name = "ModeHudManager" };
        AddChild(_modeHudManager);
        _modeHudManager.BindEventBus(_eventBus);
        _modeHudManager.BindSwitchPanel(_modeSwitchPanel);
        if (_selectionHUD != null)
            _modeHudManager.BindSelectionHud(_selectionHUD);
        _modeHudManager.OnModeChangeRequested += clientMode =>
        {
            var gameMode = clientMode switch
            {
                ClientGameplayMode.Civilian => GameplayMode.Life,
                ClientGameplayMode.Combat   => GameplayMode.Combat,
                ClientGameplayMode.Space    => GameplayMode.Space,
                _ => GameplayMode.Combat,
            };
            OnGameplayModeSelected(gameMode);
        };

        // 12. 火箭设计面板 (VAB)
        _rocketPanel = new RocketAssemblyPanel { Name = "RocketAssembly" };
        AddChild(_rocketPanel);
        _rocketPanel.OnLaunchRequested += OnRocketDesignComplete;
        _rocketPanel.OnPanelClosed += OnRocketPanelClosed;

        // 13. 太空发射控制面板
        _launchControlPanel = new LaunchControlPanel { Name = "LaunchControl" };
        AddChild(_launchControlPanel);
        _launchControlPanel.OnLaunchPressed += OnLaunchControlFire;
        _launchControlPanel.OnAbortPressed += OnLaunchControlAbort;
        _launchControlPanel.OnStagePressed += OnLaunchControlStage;
        _launchControlPanel.OnThrottleChanged += OnThrottleChanged;

        // 14. 发射序列控制器（物理 + VFX）
        _launchController = new LaunchSequenceController { Name = "LaunchSequence" };
        AddChild(_launchController);
        _launchController.OnLaunchComplete += OnLaunchSequenceComplete;
        _launchController.OnLandedSafe += OnRocketLandedSafe;
        _launchController.OnFlightReport += OnFlightReportReady;
        _launchController.OnTelemetryUpdate += OnTelemetryUpdate;

        // 15. 飞行报告面板
        _flightReportPanel = new FlightReportPanel { Name = "FlightReport" };
        AddChild(_flightReportPanel);
        _flightReportPanel.OnExitModeSelected += OnFlightReportExitSelected;

        // 16. 火箭追踪相机
        _rocketCamera = new RocketCameraController { Name = "RocketCamera" };
        AddChild(_rocketCamera);

        // 17. 世界环境 + 地形 + 天气
        InitializeWorldEnvironment();

        // 18–19. 网络模式：玩家位置由服务端快照推送，跳过本地地形放置

        // 默认战斗模式
        ApplyGameplayMode(GameplayMode.Combat);
        SetWorldEntryGate(true);
        _networkInfoHud.EnterStartupMode();
        Input.MouseMode = Input.MouseModeEnum.Visible;

        GD.Print("[GameBootstrap] All systems initialized");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        _remoteWorldEcsCache?.Update();
        _serverEventEcsProjection?.FlushToEcs();
        _remoteEcsStateSync?.Update();
        _networkVisualEventBuffer?.FlushToEcs();
        ProcessAuthorityResultEvents();

        // Phase 4: 输入 → 预测/相机（必须在 Phase 3 推送前更新，确保推送到 Node 的是最新状态）
        _inputIntentCollect?.Update();
        _localMovementPredict?.Update(dt);
        _cameraOrbit?.Update(dt);

        // Phase 3: ECS → Node 推送（在所有 ECS 更新后执行）
        _characterPresentationSync?.Update();
        _vehicleHudSync?.Update();
        _localControlSync?.Update();
        _playerHudSync?.Update();
        _rocketTelemetrySync?.Update();

        if (!_worldEntryCompleted)
        {
            UpdateTerrainFromCamera();
            _networkInfoHud?.Update(delta);
            return;
        }

        // ── 通用 ECS 逻辑（所有模式共享）──
        // 网络模式下本地 ECS 模拟不运行 — 由服务端快照驱动
        if (!GameServices.IsNetworkMode)
        {
            if (_currentMode == GameplayMode.Life || _currentMode == GameplayMode.Combat)
                _baseBuilding?.UpdateConstruction(dt);
            _squadFollow?.Update(dt);
            _squadFollow?.SyncMemberTargets();
        }

        // ── 模式专属逻辑 ──
        switch (_currentMode)
        {
            case GameplayMode.Combat:
                if (!GameServices.IsNetworkMode)
                {
                    var world = GameServices.World;
                    float gameTime = world.GetWorldTime();
                    _combatGameplay?.Update(dt, gameTime);
                    _projCollision?.Update(gameTime);
                    _defeatedSystem?.Update(dt);
                    _vehicleTerrain?.Update(dt);
                    DriveSquadCombat();
                }
                _enemyVisuals?.SyncPositions();
                UpdateTerrainFromCamera();
                break;

            case GameplayMode.Life:
                UpdateTerrainFromCamera();
                break;

            case GameplayMode.Space:
                if (!GameServices.IsNetworkMode)
                    _space?.UpdateSpaceFlight(dt);
                // 发射控制器由自身 _Process 驱动（Node3D）
                // 火箭飞行中也需要更新地形
                UpdateTerrainFromCamera();
                break;
        }

        // ── HUD ──
        UpdateHud();

        // ── 本地玩家状态上传 ──
        UploadLocalPlayerState(dt);

        if (_player?.IsSpacecraftControlActive == true)
            ProcessNetworkRocketControl(dt);

        _networkEcsDispatch?.Update(dt);

        // ── 渲染同步 ──
        _multiMeshSync?.SyncToMultiMesh();

        // ── GPU 调度 ──
        DispatchGpuWork(dt);

        // ── 调试帧率 ──
        UpdatePerfHud(delta);
    }

    private void ProcessNetworkRocketControl(float dt)
    {
        TryAttachRocketCameraToRemoteNode();

        if (_player is null)
            return;

        _player.ReadSpacecraftControlInput(dt, out var thrust, out var rotation, out var actionFlags);
        UpdatePredictedRocketPose(dt, thrust, rotation, actionFlags);

        // 构造本地遥测数据供 UI 显示（网络模式下服务端状态需从远端实体读取）
        UpdateNetworkRocketTelemetry();

        if (_activeRocketNetworkId == System.Guid.Empty || _remoteWorldEcsCache == null)
            return;
        if (!_remoteWorldEcsCache.TryGetEcsEntityId(_activeRocketNetworkId, out var ecsEntityId))
            return;

        var rocketEntity = _store.GetEntityById(ecsEntityId);
        if (rocketEntity.IsNull)
            return;

        _ecsAuth.Write(rocketEntity, new NetworkSpacecraftInputCommand
        {
            SnapshotSpacecraftEntityId = _player != null && _player.Entity.TryGetComponent<RemoteRocketControlState>(out var remoteRocketControlState)
                ? remoteRocketControlState.SnapshotSpacecraftEntityId
                : 0,
            ThrustX = thrust.X,
            ThrustY = thrust.Y,
            ThrustZ = thrust.Z,
            RotationX = rotation.X,
            RotationY = rotation.Y,
            RotationZ = rotation.Z,
            ActionFlags = actionFlags,
            Sequence = ++_spacecraftInputCommandSequence,
        });
    }

    private void UpdatePredictedRocketPose(float dt, Vector3 thrust, Vector3 rotation, byte actionFlags)
    {
        if (_activeRocketNetworkId == System.Guid.Empty || _remoteWorldEcsCache == null)
            return;
        if (!_remoteWorldEcsCache.TryGetEcsEntityId(_activeRocketNetworkId, out var ecsEntityId))
            return;

        var rocketEntity = _store.GetEntityById(ecsEntityId);
        if (rocketEntity.IsNull
            || !rocketEntity.TryGetComponent<WorldPosition>(out var rocketPos)
            || !rocketEntity.TryGetComponent<WorldRotation>(out var rocketRot)
            || !rocketEntity.TryGetComponent<RemoteSnapshotState>(out var rocketSnapshot))
            return;

        var authoritativePos = new Vector3(rocketPos.X, rocketPos.Y, rocketPos.Z);
        var authoritativeRot = NormalizeSafe(new Quaternion(rocketRot.X, rocketRot.Y, rocketRot.Z, rocketRot.W));
        var authoritativeVel = new Vector3(
            rocketPos.X - rocketSnapshot.PreviousX,
            rocketPos.Y - rocketSnapshot.PreviousY,
            rocketPos.Z - rocketSnapshot.PreviousZ) / 0.1f;

        if (!_hasPredictedRocketPose)
        {
            _hasPredictedRocketPose = true;
            _predictedRocketPosition = authoritativePos;
            _predictedRocketRotation = authoritativeRot;
            _predictedRocketVelocity = authoritativeVel;
        }

        _predictedRocketRotation = NormalizeSafe(_predictedRocketRotation);

        var localReactionDirection = _activeRocketConfig?.GetPrimaryReactionDirection() ?? Vector3.Up;

        const float RocketPredictionSnapDistance = 60f;
        const float RocketPredictionCorrectionRate = 4f;
        var correction = authoritativePos - _predictedRocketPosition;
        if (correction.LengthSquared() > RocketPredictionSnapDistance * RocketPredictionSnapDistance)
        {
            _predictedRocketPosition = authoritativePos;
            _predictedRocketRotation = authoritativeRot;
            _predictedRocketVelocity = authoritativeVel;
        }
        else
        {
            float blend = Mathf.Clamp(RocketPredictionCorrectionRate * dt, 0f, 1f);
            _predictedRocketPosition = _predictedRocketPosition.Lerp(authoritativePos, blend);
            _predictedRocketRotation = NormalizeSafe(_predictedRocketRotation).Slerp(NormalizeSafe(authoritativeRot), blend);
            _predictedRocketVelocity = _predictedRocketVelocity.Lerp(authoritativeVel, blend * 0.5f);
        }

        bool engineCutoff = (actionFlags & 0x04) != 0;
        if (!engineCutoff)
        {
            if (rotation.LengthSquared() > 0.0001f)
            {
                var yaw = new Quaternion(Vector3.Up, rotation.Y * 0.03f);
                var pitch = new Quaternion(Vector3.Right, rotation.X * 0.03f);
                var roll = new Quaternion(Vector3.Forward, rotation.Z * 0.03f);
                _predictedRocketRotation = NormalizeSafe(roll * pitch * yaw * _predictedRocketRotation);
            }

            if (Mathf.Abs(thrust.Y) > 0.0001f)
            {
                var thrustDir = _predictedRocketRotation * (localReactionDirection * Mathf.Sign(thrust.Y));
                _predictedRocketVelocity += thrustDir.Normalized() * (Mathf.Abs(thrust.Y) * 5f);
            }
        }

        if ((actionFlags & 0x02) != 0)
        {
            _predictedRocketVelocity *= 0.85f;
            if (_predictedRocketVelocity.LengthSquared() < 0.1f)
                _predictedRocketVelocity = Vector3.Zero;
        }

        if ((actionFlags & 0x01) != 0)
            _predictedRocketVelocity *= 0.97f;

        if (_predictedRocketVelocity.Y > 0)
            _predictedRocketVelocity += Vector3.Up * (5f * dt);

        _predictedRocketPosition += _predictedRocketVelocity * dt;

        _ecsAuth.Write(rocketEntity, new WorldPosition
        {
            X = _predictedRocketPosition.X,
            Y = _predictedRocketPosition.Y,
            Z = _predictedRocketPosition.Z,
        });
        _ecsAuth.Write(rocketEntity, new WorldRotation
        {
            X = NormalizeSafe(_predictedRocketRotation).X,
            Y = NormalizeSafe(_predictedRocketRotation).Y,
            Z = NormalizeSafe(_predictedRocketRotation).Z,
            W = NormalizeSafe(_predictedRocketRotation).W,
        });
    }

    private static Quaternion NormalizeSafe(Quaternion q)
    {
        var lengthSquared = q.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-6f)
            return Quaternion.Identity;

        return q.Normalized();
    }

    /// <summary>
    /// 网络模式下从远端实体构造遥测数据更新 LaunchControlPanel。
    /// Phase 3：火箭权威 ECS 状态由 RocketTelemetrySyncSystem 推送，
    /// 此处仅消费最近一帧 + 本地预测覆写。
    /// </summary>
    private void UpdateNetworkRocketTelemetry()
    {
        if (_launchControlPanel == null) return;
        if (_activeRocketNetworkId == System.Guid.Empty) return;
        if (!_hasLastRocketTelemetryFrame) return;

        ref readonly var rocketFrame = ref _lastRocketTelemetryFrame;
        if (!rocketFrame.HasAuthoritativePose) return;

        var rocketPos = rocketFrame.Position;
        var rocketRot = rocketFrame.Rotation;
        var rocketSnapshot = rocketFrame.Snapshot;

        bool spacecraftControlActive = _player?.IsSpacecraftControlActive == true;
        Vector3 rocketPosition = spacecraftControlActive && _hasPredictedRocketPose
            ? _predictedRocketPosition
            : new Vector3(rocketPos.X, rocketPos.Y, rocketPos.Z);
        Quaternion rocketRotation = spacecraftControlActive && _hasPredictedRocketPose
            ? NormalizeSafe(_predictedRocketRotation)
            : NormalizeSafe(new Quaternion(rocketRot.X, rocketRot.Y, rocketRot.Z, rocketRot.W));
        Vector3 rocketVelocity = spacecraftControlActive && _hasPredictedRocketPose
            ? _predictedRocketVelocity
            : new Vector3(
                rocketPos.X - rocketSnapshot.PreviousX,
                rocketPos.Y - rocketSnapshot.PreviousY,
                rocketPos.Z - rocketSnapshot.PreviousZ) / 0.1f;

        // 从远端实体位置/旋转推导遥测数据
        float altitude = rocketPosition.Y;
        float terrainY = 0f;
        if (GameServices.Terrain != null)
            terrainY = GameServices.Terrain.SampleHeight(rocketPosition.X, rocketPosition.Z);
        altitude -= terrainY;

        float verticalSpeed = rocketVelocity.Y;
        float speed3D = rocketVelocity.Length();
        float horizontalSpeed = MathF.Sqrt(rocketVelocity.X * rocketVelocity.X + rocketVelocity.Z * rocketVelocity.Z);

        // 旋转 → 欧拉角
        var euler = rocketRotation.GetEuler();

        string phaseName = altitude < 5 ? "待发射" : altitude < 500 ? "升空中" : altitude < 10000 ? "大气层" : "太空";

        _launchControlPanel.UpdateTelemetry(new Ark.Events.TelemetryData
        {
            Altitude = altitude,
            Velocity = verticalSpeed,
            Speed3D = speed3D,
            HorizontalSpeed = horizontalSpeed,
            Acceleration = 0f,
            TWR = 1.5f,
            FuelPercent = 100f,
            FuelBurnRate = 0f,
            Heading = Mathf.RadToDeg(euler.Y),
            Pitch = Mathf.RadToDeg(euler.X),
            Roll = Mathf.RadToDeg(euler.Z),
            DragForce = 0f,
            PhaseName = phaseName,
            Mass = 0f,
            Throttle = _player?.SpacecraftThrottle ?? 0f,
            Stage = 0,
            HoverMode = _player?.SpacecraftHoverMode == true,
            EngineCutoff = _player?.SpacecraftEngineCutoff == true,
        });
    }

    public override void _ExitTree()
    {
        UnregisterHudSyncReceivers();
        Ark.Systems.Sync.SyncSystemRegistry.Clear();
        _worldEnvManager?.ShutdownWorld();
        _eventBus.ClearAll();
        GameServices.Shutdown();
        _gpu.Dispose();
        _multiMeshSync?.Cleanup();
        GD.Print("[GameBootstrap] Shutdown complete");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("build_mode") && _squad == null && _worldEntryCompleted)
        {
            _networkBuildMode = !_networkBuildMode;
            _player?.SetBuildCameraMode(_networkBuildMode);
            _buildPlacement?.OnBuildModeChanged(_networkBuildMode);
            GD.Print($"[GameBootstrap] Network build mode: {_networkBuildMode}");
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("toggle_network_info_hud"))
        {
            _networkInfoHud?.Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        Ark.World.Core.EnvironmentPreset? preset = null;
        if (@event.IsActionPressed("env_preset_modern_city"))
            preset = Ark.World.Core.EnvironmentPreset.ModernCity;
        else if (@event.IsActionPressed("env_preset_space_universe"))
            preset = Ark.World.Core.EnvironmentPreset.SpaceUniverse;
        else if (@event.IsActionPressed("env_preset_beautiful_wild"))
            preset = Ark.World.Core.EnvironmentPreset.BeautifulWild;
        else if (@event.IsActionPressed("env_preset_dark_forest"))
            preset = Ark.World.Core.EnvironmentPreset.DarkForest;
        else if (@event.IsActionPressed("env_preset_horror_dungeon"))
            preset = Ark.World.Core.EnvironmentPreset.HorrorDungeon;
        else if (@event.IsActionPressed("env_preset_ruin_archaeology"))
            preset = Ark.World.Core.EnvironmentPreset.RuinArchaeology;
        else if (@event.IsActionPressed("env_preset_mystic_sky"))
            preset = Ark.World.Core.EnvironmentPreset.MysticSky;
        else if (@event.IsActionPressed("env_preset_natural"))
            preset = Ark.World.Core.EnvironmentPreset.Natural;

        if (preset is not Ark.World.Core.EnvironmentPreset p || _worldEnvManager == null || _player == null)
        {
            return;
        }

        // 冻结玩家防止切换期间掉落
        var prevMode = _player.ProcessMode;
        _player.ProcessMode = ProcessModeEnum.Disabled;

        // 同时冻结队员（防止 AI 移动触发物理碰撞）
        if (_squad != null)
        {
            for (int i = 1; i <= _squad.MemberCount; i++)
            {
                var member = _squad.GetMember(i);
                if (member is Node memberNode)
                    memberNode.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        try
        {
            float safeY = _worldEnvManager.SwitchEnvironment(p, _player.GlobalPosition);

            // 将玩家放到新地形安全高度
            _player.GlobalPosition = new Vector3(_player.GlobalPosition.X, safeY, _player.GlobalPosition.Z);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[GameBootstrap] Environment switch failed: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // 恢复玩家处理模式（无论是否异常）
            _player.ProcessMode = prevMode;

            // 恢复队员处理模式
            if (_squad != null)
            {
                for (int i = 1; i <= _squad.MemberCount; i++)
                {
                    var member = _squad.GetMember(i);
                    if (member is Node memberNode)
                        memberNode.ProcessMode = ProcessModeEnum.Inherit;
                }
            }
        }

        SnapEntitiesToTerrain();
        _player.ForceMouseCaptured();
        Input.MouseMode = Input.MouseModeEnum.Captured;

        GetViewport().SetInputAsHandled();
        GD.Print($"[GameBootstrap] Environment switched to {p}");
    }
}
