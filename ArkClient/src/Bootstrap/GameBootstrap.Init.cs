using Godot;
using Friflo.Engine.ECS;
using Ark.Services;
using Ark.Ecs.Components;
using Ark.Gameplay.Combat;
using Ark.Gameplay.Combat.Systems;
using Ark.Gameplay.Squad;
using Ark.Gameplay.Space;
using Ark.Bridge.Features.Space;
using Ark.Bridge.Features.BaseBuilding;
using Ark.Bridge.Features.Combat;
using Ark.Bridge.Features.Squad;
using Ark.Interaction;
using Ark.Player;
using Ark.Camera;
using Ark.World;
using Ark.World.Core;
using Ark.Events;
using Ark.Services.Remote;
using Ark.Systems.Gpu;
using Ark.UI;
using Ark.Systems.Squad;

namespace Ark;

public partial class GameBootstrap
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          初始化（纯胶水）
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeModules()
    {
        _combatData = new GameplayDefinitionCatalog();

        _baseBuilding = new BaseBuildingModule(_store);
        if (!GameServices.IsNetworkMode)
            GameServices.RegisterBaseBuilding(_baseBuilding);

        _buildingVisuals = new BuildingVisualManager();
        _buildingVisuals.Initialize(_store);
        AddChild(_buildingVisuals);

        // ── 通过 EventBus 桥接建筑事件 ──
        _baseBuilding.OnBuildingPlacedAt  += (eid, pos, rot, typeId) =>
            _eventBus.Publish(new BuildingPlacedEvent(eid, pos.X, pos.Y, pos.Z, rot.X, rot.Y, rot.Z, rot.W, typeId));
        _baseBuilding.OnBuildingDestroyed += (eid) =>
            _eventBus.Publish(new BuildingDestroyedEvent(eid));

        _eventBus.Subscribe<BuildingPlacedEvent>(e =>
            _buildingVisuals.SpawnBuildingVisual(e.EntityId,
                new Vector3(e.PosX, e.PosY, e.PosZ),
                new Quaternion(e.RotX, e.RotY, e.RotZ, e.RotW), e.TypeId));
        _eventBus.Subscribe<BuildingDestroyedEvent>(e =>
            _buildingVisuals.RemoveBuildingVisual(e.EntityId));

        if (!GameServices.IsNetworkMode)
        {
            _combat = new CombatModule(_store);
            GameServices.RegisterCombat(_combat);
        }

        if (!GameServices.IsNetworkMode)
        {
            _combatGameplay = new CombatGameplayModule(_store);
            _combatGameplay.Initialize();
            _combatGameplay.OnStructureCollapsed += (eid) =>
                _eventBus.Publish(new StructureCollapsedEvent(eid));
            _eventBus.Subscribe<StructureCollapsedEvent>(e =>
                _buildingVisuals?.RemoveBuildingVisual(e.EntityId));

            _space = new SpaceModule(_store);
            GameServices.RegisterSpace(_space);

            _projCollision  = new ProjectileCollisionSystem(_store, _combatGameplay.Projectiles, _combatGameplay.Damage);
            _defeatedSystem = new DefeatedEntitySystem(_store);
            _squadCombat    = new SquadCombatSystem(_store, _combatGameplay);
            _vehicleTerrain = new Ark.Gameplay.Vehicle.VehicleTerrainSystem(_store);
        }

        GD.Print("[GameBootstrap] Modules initialized");
    }

    private void InitializeGpuSystems()
    {
        _gpuMovement = new GpuMovementSystem(_gpu, _store);
        _gpuCulling  = new GpuCullingSystem(_gpu, _store);
        _gpuMovement.Initialize();
        _gpuCulling.Initialize();
        GD.Print($"[GameBootstrap] GPU: Movement={_gpuMovement.IsInitialized}, Culling={_gpuCulling.IsInitialized}");
    }

    private void InitializeSceneNodes()
    {
        // 玩家
        _player = new TpsPlayerController { Name = "Player" };
        AddChild(_player);

        // 建造 UI + 控制器
        var buildPanel = new BuildPanelUI { Name = "BuildPanel" };
        AddChild(buildPanel);

        _buildPlacement = new BuildPlacementController { Name = "BuildPlacement" };
        AddChild(_buildPlacement);
        _buildPlacement.Initialize(_baseBuilding!, _buildingVisuals!, buildPanel);
        _buildPlacement.SetCommandStore(_store);
        if (_player.Camera != null) _buildPlacement.SetCamera(_player.Camera);

        // 载具生成 UI
        var vehicleUI = new VehicleSpawnUI { Name = "VehicleSpawnUI" };
        AddChild(vehicleUI);
        vehicleUI.Initialize(_store, _buildingVisuals!);
        vehicleUI.OnVehicleSpawnRequested += OnVehicleSpawnRequested;

        // 选择 HUD
        _selectionHUD = new SelectionHUD { Name = "SelectionHUD" };
        AddChild(_selectionHUD);

        _seatWeaponPanel = new SeatWeaponPanel { Name = "SeatWeaponPanel" };
        AddChild(_seatWeaponPanel);

        _npcInteractionPanel = new NpcInteractionPanel { Name = "NpcInteractionPanel" };
        AddChild(_npcInteractionPanel);

        // 交互处理器
        var selectionHandler = new SelectionHandler(_selectionHUD, vehicleUI, _buildingVisuals!, _store);
        selectionHandler.OnLaunchPadActivated += OnLaunchPadActivated;
        selectionHandler.OnNpcSelected += (entityId, displayName) =>
            _npcInteractionPanel?.ShowFor(displayName, entityId, "对话 / 观察 / 后续任务交互");

        // 光标交互 — 使用 Func 解析当前活动控制器（支持队长/队员/未来载具）
        var cursorInteraction = new CursorInteractionSystem { Name = "CursorInteraction" };
        AddChild(cursorInteraction);
        cursorInteraction.Initialize(
            () => _player,
            _buildingVisuals);
        cursorInteraction.OnBuildingSelected += selectionHandler.OnBuildingSelected;
        cursorInteraction.OnObjectSelected   += selectionHandler.OnObjectSelected;
        cursorInteraction.OnSelectionCleared  += () =>
        {
            selectionHandler.OnSelectionCleared();
            _npcInteractionPanel?.HidePanel();
        };

        // 战斗 HUD 控制器
        _hudController = new CombatHudController(_store, _selectionHUD, _seatWeaponPanel);
        _hudController.WeaponNameResolver = id =>
            _combatData.WeaponDefs.Get(id)?.Name ?? $"Weapon #{id}";

        // 武器视觉
        _weaponVisuals = new WeaponVisualSystem { Name = "WeaponVisuals" };
        AddChild(_weaponVisuals);
        _weaponVisuals.Initialize(_store, _remoteWorldEcsCache);

        if (GameServices.IsNetworkMode)
        {
            _remotePlayerBridge = Ark.Bridge.Player.RemotePlayerBridgeFactory.Create(
                _store,
                _remoteWorldEcsCache,
                spawnTypedBuilding: (entityId, pos, rot, typeId, constructionProgress) =>
                    _buildingVisuals?.SpawnBuildingVisual(
                        entityId,
                        new Vector3(pos.X, pos.Y, pos.Z),
                        new Quaternion(rot.X, rot.Y, rot.Z, rot.W),
                        typeId,
                        constructionProgress),
                removeBuilding: entityId => _buildingVisuals?.RemoveBuildingVisual(entityId),
                attachWeapon: (entityId, node, weaponCategory) =>
                    _weaponVisuals?.AttachWeaponToCharacter(entityId, node, weaponCategory),
                detachWeapon: entityId => _weaponVisuals?.DetachWeapon(entityId));
            if (_remotePlayerBridge != null)
            {
                _remotePlayerBridge.Name = "RemotePlayerBridge";
                AddChild(_remotePlayerBridge);
                GD.Print("[GameBootstrap] RemotePlayerBridge initialized");
            }

            if (GameServices.EventDispatcher is { } dispatcher)
            {
                dispatcher.OnTerrainModificationsUpdated += modifications =>
                {
                    var persisted = new System.Collections.Generic.List<Ark.World.Data.PersistedTerrainModification>(modifications.Count);
                    foreach (var modification in modifications)
                    {
                        persisted.Add(new Ark.World.Data.PersistedTerrainModification(
                            modification.ModType,
                            modification.ChunkKey,
                            modification.SequenceTick,
                            modification.X,
                            modification.Z,
                            modification.RadiusX,
                            modification.RadiusZ,
                            modification.TargetHeight,
                            modification.MetadataJson));
                    }

                    _worldEnvManager?.ApplyServerTerrainModifications(persisted);
                    _remoteWorldEcsCache?.ApplyPersistedBuildingDamageDeltas(modifications);
                };

                if (dispatcher.LastTerrainModifications.Count > 0)
                {
                    var persisted = new System.Collections.Generic.List<Ark.World.Data.PersistedTerrainModification>(dispatcher.LastTerrainModifications.Count);
                    foreach (var modification in dispatcher.LastTerrainModifications)
                    {
                        persisted.Add(new Ark.World.Data.PersistedTerrainModification(
                            modification.ModType,
                            modification.ChunkKey,
                            modification.SequenceTick,
                            modification.X,
                            modification.Z,
                            modification.RadiusX,
                            modification.RadiusZ,
                            modification.TargetHeight,
                            modification.MetadataJson));
                    }

                    _worldEnvManager?.ApplyServerTerrainModifications(persisted);
                    _remoteWorldEcsCache?.ApplyPersistedBuildingDamageDeltas(dispatcher.LastTerrainModifications);
                }
            }
        }

        // ── 通过 EventBus 桥接战斗视觉事件 ──
        if (_combatGameplay != null)
            _combatGameplay.OnWeaponFired += (eid, wid) =>
                _eventBus.Publish(new WeaponFiredEvent(eid, wid));

        if (_projCollision != null)
        {
            _projCollision.OnHit += pos =>
                _eventBus.Publish(new ProjectileHitEvent(0, pos.X, pos.Y, pos.Z, 0, 0));
            _projCollision.OnExplosion += (pos, radius) =>
                _eventBus.Publish(new ExplosionEvent(pos.X, pos.Y, pos.Z, radius));
        }

        _eventBus.Subscribe<WeaponFiredEvent>(e =>
            _weaponVisuals?.HandleWeaponFired(e.EntityId, e.WeaponDefId));
        _eventBus.Subscribe<ProjectileHitEvent>(e =>
            _weaponVisuals?.HandleProjectileHit(
                new System.Numerics.Vector3(e.PosX, e.PosY, e.PosZ)));
        _eventBus.Subscribe<ExplosionEvent>(e =>
            _weaponVisuals?.HandleExplosion(
                new System.Numerics.Vector3(e.PosX, e.PosY, e.PosZ), e.Radius));

        // 敌人视觉
        _enemyVisuals = new EnemyVisualManager { Name = "EnemyVisuals" };
        AddChild(_enemyVisuals);
        _enemyVisuals.Initialize(_store, _weaponVisuals);

        // 地面 — 由 WorldEnvironmentManager 的 ChunkManager 接管

        GD.Print("[GameBootstrap] Scene nodes initialized");
    }

    private void InitializeSquadSystem()
    {
        if (_player == null) return;

        // 获取/创建玩家实体（网络模式下 LocalPlayerId 由服务端快照设置）
        Entity playerEntity = PlayerArchetype.EnsureComplete(_store, default);

        // 小队模块
        _squad = new SquadModule { Name = "SquadModule" };
        AddChild(_squad);
        _squad.MemberFactory = (store, entity, slotIndex, color) =>
        {
            var ctrl = new SquadMemberController();
            ctrl.Initialize(store, entity, slotIndex, color);
            return ctrl;
        };
        _squad.Initialize(_store, playerEntity, _player, _player);

        _squadFollow = new SquadFollowSystem(_store);
        _squadFollow.SetLeader(playerEntity);

        // 相机管理器
        _cameraManager = new SquadCameraManager(_squad, _player);
        _cameraManager.OnCameraChanged = cam => _buildPlacement?.SetCamera(cam);

        _squad.OnLeaderChanged    += newLeader => _squadFollow?.SetLeader(newLeader);
        _squad.OnActiveChanged    += _cameraManager.OnActiveChanged;
        _squad.OnBuildModeChanged += active =>
        {
            _cameraManager.OnBuildModeChanged(active);
            _buildPlacement?.OnBuildModeChanged(active);
        };

        _squad.SpawnAllMembers();

        // 装备武器
        if (_combatGameplay != null)
        {
            SquadEquipmentSetup.EquipLeader(_combatGameplay, playerEntity);
            SquadEquipmentSetup.EquipMembers(
                _combatGameplay,
                slot => _squad.GetMember(slot),
                _squad.MemberCount,
                onMemberEquipped: (eid, slot) =>
                {
                    var member = _squad.GetMember(slot);
                    if (_weaponVisuals != null && member is Node3D memberNode)
                        _weaponVisuals.AttachWeaponToCharacter(eid, memberNode, 2);
                });
        }

        _player.SetCombatModule(_combatGameplay);
        _player.SetCombatData(_combatData);

        // 为所有队员注入战斗模块
        for (int i = 1; i <= _squad.MemberCount; i++)
            _squad.GetMember(i)?.SetCombatModule(_combatGameplay);

        GD.Print("[GameBootstrap] Squad system initialized");
    }

    /// <summary>
    /// 网络模式下的玩家初始化 — 无小队系统，但仍需创建玩家实体、注入战斗模块、装备武器。
    /// 不生成 AI 队员（由服务端管理），但保留本地玩家的射击/建造/载具交互能力。
    /// 同时创建 ServerAuthorityBridge 将所有游戏动作路由到服务端。
    /// </summary>
    private void InitializeNetworkPlayer()
    {
        if (_player == null) return;

        // 创建本地玩家 ECS 实体
        Entity playerEntity = PlayerArchetype.EnsureComplete(_store, default);
        _player.SetEntity(_store, playerEntity);
        if (_remoteWorldEcsCache != null)
            _remoteWorldEcsCache.LocalPresentationEntityId = playerEntity.Id;

        _player.SetCombatModule(null);
        _player.SetCombatData(_combatData);
        PresentationCombatState.SeedDefaultLoadout(playerEntity, _combatData);

        if (_weaponVisuals != null)
            _weaponVisuals.AttachWeaponToCharacter(playerEntity.Id, _player, 2);

        // ── 创建 ServerAuthorityBridge ──
        var networkManager = GameServices.NetworkManager;
        var eventDispatcher = GameServices.EventDispatcher;
        if (networkManager != null && eventDispatcher != null)
        {
            _serverBridge = new Ark.Services.Remote.ServerAuthorityBridge(
                networkManager, GameServices.RemotePlayerId);
            GameServices.BindServerAuthorityBridge(_serverBridge);
            if (_remoteWorldEcsCache != null)
            {
                _serverEventEcsProjection = new Ark.Services.Remote.ServerEventEcsProjectionBuffer(_store, _remoteWorldEcsCache, _networkVisualEventBuffer);
                GameServices.BindServerEventEcsProjection(_serverEventEcsProjection);
            }

            GD.Print("[GameBootstrap] ServerAuthorityBridge created and dispatcher ECS projection wired");
        }

        GD.Print("[GameBootstrap] Network player initialized (no squad, server-authoritative combat/building/vehicle)");
    }

    private void InitializeWorldEnvironment()
    {
        // 使用世界环境管理器（统一管理地形+大气+天气+植被）
        _worldEnvManager = new WorldEnvironmentManager { Name = "WorldEnvManager" };
        AddChild(_worldEnvManager);

        // 网络模式下绝不允许本地固定种子初始化；等待服务端种子。
        if (!GameServices.IsNetworkMode)
        {
            var seed = new WorldSeed(42);
            _worldEnvManager.InitializeWorld(seed, _eventBus);
        }
        else
        {
            GD.Print("[GameBootstrap] Waiting for server terrain seed (local seed init removed)");
        }

        // 注册地形查询服务（供其他模块查询地形高度）
        GameServices.RegisterTerrain(_worldEnvManager);
        GameServices.RegisterWorldInitializer(_worldEnvManager);

        // 将地形查询注入战败实体系统（用于落地检测）
        _defeatedSystem?.SetTerrainQuery(_worldEnvManager);

        // 将地形查询注入小队跟随系统（用于瞬移时安全高度检测）
        _squadFollow?.SetTerrainQuery((x, z) => _worldEnvManager.SampleTerrainHeight(x, z));

        // 将地形查询注入投射物碰撞系统（用于炮弹命中地面检测）
        _projCollision?.SetTerrainQuery(_worldEnvManager);

        // 将地形查询注入载具系统（生成时校正 Y、间距检测）
        _vehicleTerrain?.SetTerrainQuery(_worldEnvManager);
        _combatGameplay?.SetVehicleTerrainQuery(_worldEnvManager);

        // 将地形查询注入玩家控制器（载具跟随地形）
        _player?.SetTerrainQuery((x, z) => _worldEnvManager.SampleTerrainHeight(x, z));

        // 将地形查询注入队员控制器（飞机载具地形检测）
        if (_squad != null)
        {
            for (int i = 1; i <= _squad.MemberCount; i++)
                _squad.GetMember(i)?.SetTerrainQuery((x, z) => _worldEnvManager.SampleTerrainHeight(x, z));
        }

        // 将地形查询注入建造控制器（建筑放置时平整地形）
        _buildPlacement?.SetTerrainQuery(
            (x, z) => _worldEnvManager.SampleTerrainHeight(x, z),
            (cx, cz, hx, hz, h) => _worldEnvManager.FlattenArea(cx, cz, hx, hz, h));
    }
}
