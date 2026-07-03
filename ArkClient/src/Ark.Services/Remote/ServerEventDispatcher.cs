using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ark.Networking;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 统一服务端事件分发器 — 实现 <see cref="IServerEventHandler"/>，
/// 将服务端推送事件路由到各个远程服务和 UI/ECS 系统。
/// 在 <see cref="GameServices.InitializeNetworkMode"/> 中注册到 NetworkManager。
/// </summary>
public sealed class ServerEventDispatcher : IServerEventHandler
{
    private RemoteGameWorld? _world;
    private RemoteInventoryService? _inventory;
    private RemoteQuestService? _quest;
    private RemoteCharacterService? _character;
    private RemoteEconomyService? _economy;
    private RemoteFleetService? _fleet;
    private RemoteGuildService? _guild;
    private RemoteScriptService? _script;
    private SnapshotApplier? _snapshotApplier;
    private ServerEventEcsProjectionBuffer? _ecsProjection;

    public WorldEnvironmentDto? LastWorldEnvironment { get; private set; }
    public WeatherDto? LastWeather { get; private set; }
    public PartyInfoDto? LastPartyInfo { get; private set; }
    public NearbyEntitiesDto? LastNearbyEntities { get; private set; }
    public IReadOnlyList<TerrainModificationDto> LastTerrainModifications { get; private set; } = Array.Empty<TerrainModificationDto>();
    public float LastTimeOfDay { get; private set; }
    public float LastTimeScale { get; private set; } = 1f;

    /// <summary>通用服务端消息（可由 UI 订阅显示系统提示）。</summary>
    public event Action<string>? OnSystemMessage;
    /// <summary>聊天消息接收（由聊天面板订阅）。</summary>
    public event Action<Guid, string, string, DateTime>? OnChat;
    /// <summary>强制断线（由连接管理器处理）。</summary>
    public event Action<string>? OnForceDisconnected;
    /// <summary>世界环境预加载（登录流程中使用）。</summary>
    public event Action<WorldEnvironmentDto>? OnWorldEnvironmentReady;
    /// <summary>天气变化。</summary>
    public event Action<WeatherDto>? OnWeatherUpdated;
    /// <summary>地形/建筑持久化增量更新。</summary>
    public event Action<IReadOnlyList<TerrainModificationDto>>? OnTerrainModificationsUpdated;
    /// <summary>队伍更新。</summary>
    public event Action<PartyInfoDto>? OnPartyChanged;
    /// <summary>附近实体更新。</summary>
    public event Action<NearbyEntitiesDto>? OnNearbyUpdated;

    public void Bind(
        RemoteGameWorld world,
        RemoteInventoryService inventory,
        RemoteQuestService quest)
    {
        _world = world;
        _inventory = inventory;
        _quest = quest;
    }

    public void BindExtended(
        RemoteCharacterService character,
        RemoteEconomyService economy,
        RemoteFleetService fleet,
        RemoteGuildService guild,
        RemoteScriptService script)
    {
        _character = character;
        _economy = economy;
        _fleet = fleet;
        _guild = guild;
        _script = script;
    }

    /// <summary>绑定快照解码器 — 用于 SignalR 降级快照通道。</summary>
    public void BindSnapshotApplier(SnapshotApplier applier)
    {
        _snapshotApplier = applier;
    }

    public void BindEcsProjection(ServerEventEcsProjectionBuffer projection)
    {
        _ecsProjection = projection;
    }

    public void ResetWorldEntryState()
    {
        LastWorldEnvironment = null;
        LastWeather = null;
        LastPartyInfo = null;
        LastNearbyEntities = null;
        LastTerrainModifications = Array.Empty<TerrainModificationDto>();
        LastTimeOfDay = 0f;
        LastTimeScale = 1f;
    }

    // ══════════════════════════════════════════════════════════════════
    // 连接 / 会话
    // ══════════════════════════════════════════════════════════════════

    public Task OnForceDisconnect(string reason)
    {
        ServiceLog.Error($"[Server] Force disconnect: {reason}");
        OnForceDisconnected?.Invoke(reason);
        return Task.CompletedTask;
    }

    public Task OnServerMessage(string message)
    {
        ServiceLog.Info($"[Server] {message}");
        OnSystemMessage?.Invoke(message);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 玩家状态
    // ══════════════════════════════════════════════════════════════════

    public Task OnPlayerJoinedWorld(Guid playerId, string playerName, string worldId)
    {
        ServiceLog.Info($"[Server] Player {playerName} joined {worldId}");
        return Task.CompletedTask;
    }

    public Task OnPlayerLeftWorld(Guid playerId, string worldId)
    {
        ServiceLog.Info($"[Server] Player {playerId} left {worldId}");
        return Task.CompletedTask;
    }

    public Task OnPlayerLevelUp(Guid playerId, int oldLevel, int newLevel)
    {
        ServiceLog.Info($"[Server] Level up: {oldLevel} → {newLevel}");
        OnSystemMessage?.Invoke($"Level Up! {oldLevel} → {newLevel}");
        return Task.CompletedTask;
    }

    public Task OnPlayerDied(Guid playerId, Guid? killerId, string zoneId)
    {
        ServiceLog.Info($"[Server] Player {playerId} died in {zoneId}");
        return Task.CompletedTask;
    }

    public Task OnRespawned(RespawnResultDto result)
    {
        ServiceLog.Info("[Server] Respawned");
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 战斗
    // ══════════════════════════════════════════════════════════════════

    public Task OnCombatStarted(Guid attackerId, Guid defenderId, string zoneId)
    {
        return Task.CompletedTask;
    }

    public Task OnCombatEnded(Guid winnerId, Guid loserId)
    {
        return Task.CompletedTask;
    }

    public Task OnEntityDestroyed(Guid entityId, Guid destroyedById)
    {
        var localId = _world?.GetLocalId(entityId);
        if (localId.HasValue)
            _world?.DestroyEntity(localId.Value);
        return Task.CompletedTask;
    }

    public Task OnDamageReceived(AttackResultDto result)
    {
        _ecsProjection?.EnqueueDamageReceived(result);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 库存
    // ══════════════════════════════════════════════════════════════════

    public Task OnInventoryChanged(InventoryDto inventory)
    {
        _ecsProjection?.EnqueueInventory(inventory);
        return Task.CompletedTask;
    }

    public Task OnItemEquipped(EquipResultDto result)
    {
        _inventory?.RefreshFromServer();
        return Task.CompletedTask;
    }

    public Task OnItemDropped(DropItemResultDto result)
    {
        _inventory?.RefreshFromServer();
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 经济 / 市场
    // ══════════════════════════════════════════════════════════════════

    public Task OnMarketOrderPlaced(Guid orderId, string itemId, decimal pricePerUnit, int quantity, bool isBuyOrder)
    {
        OnSystemMessage?.Invoke($"Market order placed: {itemId} x{quantity}");
        return Task.CompletedTask;
    }

    public Task OnMarketOrderFilled(Guid orderId, Guid buyerId, Guid sellerId)
    {
        OnSystemMessage?.Invoke("Market order filled!");
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 制造
    // ══════════════════════════════════════════════════════════════════

    public Task OnCraftingStarted(Guid playerId, string blueprintId, int quantity)
    {
        OnSystemMessage?.Invoke($"Crafting started: {blueprintId} x{quantity}");
        _economy?.HandleCraftingStarted(blueprintId, quantity);
        return Task.CompletedTask;
    }

    public Task OnCraftingCompleted(Guid playerId, string blueprintId, int quantity)
    {
        OnSystemMessage?.Invoke($"Crafting completed: {blueprintId} x{quantity}");
        _inventory?.RefreshFromServer();
        _economy?.HandleCraftingCompleted(blueprintId, quantity);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 技能训练
    // ══════════════════════════════════════════════════════════════════

    public Task OnSkillTrainingCompleted(Guid playerId, string skillId, int newLevel)
    {
        OnSystemMessage?.Invoke($"Skill {skillId} reached level {newLevel}!");
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 舰队
    // ══════════════════════════════════════════════════════════════════

    public Task OnFleetCreated(Guid fleetId, string fleetName, Guid leaderId)
    {
        _fleet?.HandleFleetCreated(fleetId, fleetName, leaderId);
        return Task.CompletedTask;
    }

    public Task OnFleetMemberJoined(Guid fleetId, Guid playerId)
    {
        _fleet?.HandleFleetMemberJoined(fleetId, playerId);
        return Task.CompletedTask;
    }

    public Task OnFleetBattleStarted(Guid fleetId, string solarSystemId, int attackerCount, int defenderCount)
    {
        OnSystemMessage?.Invoke($"Fleet battle started in {solarSystemId}!");
        _fleet?.HandleFleetBattleStarted(fleetId, solarSystemId, attackerCount, defenderCount);
        return Task.CompletedTask;
    }

    public Task OnFleetBattleEnded(Guid winningFleetId, Guid losingFleetId, string solarSystemId) => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════
    // 公会
    // ══════════════════════════════════════════════════════════════════

    public Task OnGuildCreated(Guid guildId, string guildName, Guid founderId)
    {
        _guild?.HandleGuildCreated(guildId, guildName, founderId);
        return Task.CompletedTask;
    }

    public Task OnGuildMemberJoined(Guid guildId, Guid playerId)
    {
        _guild?.HandleGuildMemberJoined(guildId, playerId);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 社交 / 聊天
    // ══════════════════════════════════════════════════════════════════

    public Task OnChatReceived(Guid senderId, string channel, string content, DateTime timestamp)
    {
        OnChat?.Invoke(senderId, channel, content, timestamp);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 副本
    // ══════════════════════════════════════════════════════════════════

    public Task OnInstanceCreated(Guid instanceId, string templateId, string difficulty) => Task.CompletedTask;
    public Task OnInstanceCompleted(Guid instanceId, TimeSpan duration, int playerCount) => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════
    // 任务
    // ══════════════════════════════════════════════════════════════════

    public Task OnQuestAccepted(Guid playerId, string questId)
    {
        _quest?.RefreshFromServer();
        return Task.CompletedTask;
    }

    public Task OnQuestCompleted(Guid playerId, string questId)
    {
        _quest?.RefreshFromServer();
        return Task.CompletedTask;
    }

    public Task OnQuestProgressUpdated(QuestProgressDto progress)
    {
        _ecsProjection?.EnqueueQuestProgress(progress);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 主权 / 建筑
    // ══════════════════════════════════════════════════════════════════

    public Task OnSovereigntyChanged(string solarSystemId, Guid? oldOwnerId, Guid newOwnerId) => Task.CompletedTask;
    public Task OnStructurePlaced(string solarSystemId, Guid structureId, string structureType, Guid allianceId) => Task.CompletedTask;
    public Task OnStructureDestroyed(string solarSystemId, Guid structureId, string structureType) => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════
    // 探索 / 资源
    // ══════════════════════════════════════════════════════════════════

    public Task OnResourceDiscovered(Guid playerId, string solarSystemId, string resourceType) => Task.CompletedTask;
    public Task OnResourceHarvested(Guid playerId, string solarSystemId, string resourceType, int quantity) => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════
    // 成就
    // ══════════════════════════════════════════════════════════════════

    public Task OnAchievementUnlocked(Guid playerId, string achievementId, int points)
    {
        OnSystemMessage?.Invoke($"Achievement unlocked: {achievementId} (+{points}pts)");
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 脚本 / 活动
    // ══════════════════════════════════════════════════════════════════

    public Task OnScriptStarted(Guid playerId, string scriptId, int version)
    {
        _script?.HandleScriptStarted(scriptId, version);
        return Task.CompletedTask;
    }

    public Task OnScriptCompleted(Guid playerId, string scriptId)
    {
        _script?.HandleScriptCompleted(scriptId);
        return Task.CompletedTask;
    }

    public Task OnDialogueUpdated(DialogueDto dialogue)
    {
        _script?.HandleDialogueUpdated(dialogue);
        return Task.CompletedTask;
    }

    public Task OnActivityStarted(Guid activityId, string scriptId)
    {
        _script?.HandleActivityStarted(activityId, scriptId);
        return Task.CompletedTask;
    }

    public Task OnActivityEnded(Guid activityId, string scriptId)
    {
        _script?.HandleActivityEnded(activityId, scriptId);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 邮件
    // ══════════════════════════════════════════════════════════════════

    public Task OnMailReceived(MailDto mail)
    {
        OnSystemMessage?.Invoke($"New mail: {mail.Subject}");
        _guild?.HandleMailReceived(mail);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 世界环境 / 地形
    // ══════════════════════════════════════════════════════════════════

    public Task OnWorldEnvironmentReceived(WorldEnvironmentDto environment)
    {
        ServiceLog.Info($"[Server] World environment received: seed={environment.TerrainSeed}, biome={environment.BiomeId}");
        LastWorldEnvironment = environment;
        LastTimeOfDay = environment.TimeOfDay;
        LastTimeScale = environment.TimeScale;
        _ecsProjection?.EnqueueWorldEnvironment(environment);
        OnWorldEnvironmentReady?.Invoke(environment);
        return Task.CompletedTask;
    }

    public Task OnTerrainModificationsReceived(IReadOnlyList<TerrainModificationDto> modifications)
    {
        ServiceLog.Info($"[Server] Received {modifications.Count} terrain modifications");
        LastTerrainModifications = modifications;
        _ecsProjection?.EnqueueTerrainModifications(modifications);
        OnTerrainModificationsUpdated?.Invoke(modifications);
        return Task.CompletedTask;
    }

    public Task OnWeatherChanged(WeatherDto weather)
    {
        ServiceLog.Info($"[Server] Weather changed: id={weather.WeatherId}, intensity={weather.Intensity}");
        LastWeather = weather;
        _ecsProjection?.EnqueueWeather(weather);
        OnWeatherUpdated?.Invoke(weather);
        return Task.CompletedTask;
    }

    public Task OnTimeOfDayChanged(float timeOfDay, float timeScale)
    {
        ServiceLog.Info($"[Server] Time of day: {timeOfDay}, scale: {timeScale}");
        LastTimeOfDay = timeOfDay;
        LastTimeScale = timeScale;
        _ecsProjection?.EnqueueTimeOfDay(timeOfDay, timeScale);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 角色列表 / 选择 / 创建结果
    // ══════════════════════════════════════════════════════════════════

    public Task OnCharacterListReceived(CharacterListDto characterList)
    {
        ServiceLog.Info($"[Server] Character list: {characterList.Characters.Count} characters");
        _character?.HandleCharacterListReceived(characterList);
        return Task.CompletedTask;
    }

    public Task OnCharacterCreated(CharacterCreateFullResultDto result)
    {
        ServiceLog.Info($"[Server] Character created: success={result.Success}");
        _character?.HandleCharacterCreated(result);
        return Task.CompletedTask;
    }

    public Task OnCharacterSelected(SelectCharacterResultDto result)
    {
        ServiceLog.Info($"[Server] Character selected: success={result.Success}");
        _character?.HandleCharacterSelected(result);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 队伍 / 小队
    // ══════════════════════════════════════════════════════════════════

    public Task OnPartyUpdated(PartyInfoDto partyInfo)
    {
        ServiceLog.Info($"[Server] Party updated: {partyInfo.Members.Count} members");
        LastPartyInfo = partyInfo;
        _ecsProjection?.EnqueuePartyInfo(partyInfo);
        OnPartyChanged?.Invoke(partyInfo);
        return Task.CompletedTask;
    }

    public Task OnSquadMemberSpawned(Guid memberId, string name, string characterClass, double x, double y, double z)
    {
        ServiceLog.Info($"[Server] Squad member spawned: {name} ({characterClass}) at ({x},{y},{z})");
        return Task.CompletedTask;
    }

    public Task OnSquadMemberDied(Guid memberId, Guid? killerId)
    {
        ServiceLog.Info($"[Server] Squad member died: {memberId}");
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 载具（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════

    public Task OnVehicleStateChanged(VehicleStateDto vehicleState)
    {
        ServiceLog.Info($"[Server] Vehicle state: {vehicleState.VehicleEntityId}");
        _ecsProjection?.EnqueueVehicleState(vehicleState);
        return Task.CompletedTask;
    }

    public Task OnVehicleEntered(Guid playerId, Guid vehicleEntityId, int seatIndex)
    {
        ServiceLog.Info($"[Server] Player {playerId} entered vehicle {vehicleEntityId} seat {seatIndex}");
        // 广播事件发送给所有客户端 — 本地玩家已通过 SendVehicleAction 直接响应处理，
        // 不应重复触发。仅记录远程玩家的进入（供未来远程角色载具动画使用）。
        var localPlayerId = Ark.Services.GameServices.RemotePlayerId;
        _ecsProjection?.EnqueueVehicleEntered(playerId, vehicleEntityId, seatIndex);
        if (playerId == localPlayerId)
            return Task.CompletedTask; // 本地玩家已由直接响应处理，跳过广播

        // TODO: 远程玩家进入载具的可视化表现（将远程角色附着到载具座位上）
        return Task.CompletedTask;
    }

    public Task OnVehicleExited(Guid playerId, Guid vehicleEntityId)
    {
        ServiceLog.Info($"[Server] Player {playerId} exited vehicle {vehicleEntityId}");
        // 与 OnVehicleEntered 同理：本地玩家已通过直接响应处理
        var localPlayerId = Ark.Services.GameServices.RemotePlayerId;
        _ecsProjection?.EnqueueVehicleExited(playerId);
        if (playerId == localPlayerId)
            return Task.CompletedTask;

        // TODO: 远程玩家退出载具的可视化表现
        return Task.CompletedTask;
    }

    public Task OnVehicleSpawned(SpawnVehicleResultDto result)
    {
        ServiceLog.Info($"[Server] Vehicle spawned: success={result.Success}, id={result.VehicleEntityId}");
        _ecsProjection?.EnqueueVehicleSpawned(result);
        OnVehicleSpawnReceived?.Invoke(result);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 太空飞行（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════

    public Task OnSpaceFlightStateChanged(SpaceFlightStateDto flightState)
    {
        ServiceLog.Info($"[Server] Space flight: phase={flightState.Phase}, alt={flightState.Altitude}");
        _ecsProjection?.EnqueueSpaceFlightState(flightState);
        return Task.CompletedTask;
    }

    public Task OnSpacePhaseChanged(Guid playerId, string phase)
    {
        ServiceLog.Info($"[Server] Space phase changed: {phase}");
        _ecsProjection?.EnqueueSpacePhase(playerId, phase);
        return Task.CompletedTask;
    }

    public Task OnRocketAssembled(AssembleRocketResultDto result)
    {
        ServiceLog.Info($"[Server] Rocket assembled: success={result.Success}, id={result.RocketEntityId}");
        _ecsProjection?.EnqueueRocketAssembled(result);
        OnRocketAssembleReceived?.Invoke(result);
        return Task.CompletedTask;
    }

    public Task OnRocketLaunched(LaunchRocketResultDto result)
    {
        ServiceLog.Info($"[Server] Rocket launched: success={result.Success}, phase={result.Phase}");
        _ecsProjection?.EnqueueRocketLaunched(result);
        OnRocketLaunchReceived?.Invoke(result);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 建筑（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════

    public Task OnBuildingPlacedByPlayer(Guid playerId, PlaceBuildingResultDto result)
    {
        ServiceLog.Info($"[Server] Building placed: success={result.Success}");
        _ecsProjection?.EnqueueBuildingPlacedByPlayer(playerId, result);
        OnBuildingPlaceReceived?.Invoke(result);
        return Task.CompletedTask;
    }

    public Task OnBuildingDestroyedByPlayer(Guid playerId, DestroyBuildingResultDto result)
    {
        ServiceLog.Info($"[Server] Building destroyed: success={result.Success}");
        return Task.CompletedTask;
    }

    public Task OnBuildingUpgraded(Guid buildingEntityId, int newLevel)
    {
        ServiceLog.Info($"[Server] Building upgraded: {buildingEntityId} → level {newLevel}");
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 武器/弹药/弹道（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════

    public Task OnWeaponFired(FireWeaponResultDto result)
    {
        ServiceLog.Info($"[Server] Weapon fired: success={result.Success}, hit={result.HitEntityId}");
        _ecsProjection?.EnqueueWeaponFired(result);
        OnWeaponFireReceived?.Invoke(result);
        return Task.CompletedTask;
    }

    public Task OnReloadCompleted(ReloadWeaponResultDto result)
    {
        ServiceLog.Info($"[Server] Reload: success={result.Success}, mag={result.CurrentMag}");
        _ecsProjection?.EnqueueReloadCompleted(result);
        return Task.CompletedTask;
    }

    public Task OnProjectileBroadcast(ProjectileEventDto projectile)
    {
        _ecsProjection?.EnqueueProjectile(projectile);
        OnProjectileReceived?.Invoke(projectile);
        return Task.CompletedTask;
    }

    public Task OnEntityOwnershipChanged(EntityOwnershipDto ownership)
    {
        ServiceLog.Info($"[Server] Ownership changed: entity={ownership.EntityNetworkId}, owner={ownership.OwnerId}");
        _ecsProjection?.EnqueueOwnership(ownership);
        OnOwnershipReceived?.Invoke(ownership);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 附近实体广播
    // ══════════════════════════════════════════════════════════════════

    public Task OnNearbyEntitiesUpdated(NearbyEntitiesDto nearby)
    {
        LastNearbyEntities = nearby;
        _ecsProjection?.EnqueueNearbyEntities(nearby);
        OnNearbyUpdated?.Invoke(nearby);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 玩法模式 & 技能/功能系统（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>模式切换结果。</summary>
    public event Action<ModeChangeResultDto>? OnModeChangeReceived;
    /// <summary>技能使用结果。</summary>
    public event Action<AbilityUseResultDto>? OnAbilityUseReceived;
    /// <summary>技能栏同步。</summary>
    public event Action<AbilityBarSyncDto>? OnAbilityBarSyncReceived;
    /// <summary>技能冷却更新。</summary>
    public event Action<Guid, string, float>? OnAbilityCooldownReceived;
    /// <summary>武器开火结果。</summary>
    public event Action<FireWeaponResultDto>? OnWeaponFireReceived;
    /// <summary>弹道广播（渲染用）。</summary>
    public event Action<ProjectileEventDto>? OnProjectileReceived;
    /// <summary>载具生成结果。</summary>
    public event Action<SpawnVehicleResultDto>? OnVehicleSpawnReceived;
    /// <summary>建筑放置结果。</summary>
    public event Action<PlaceBuildingResultDto>? OnBuildingPlaceReceived;
    /// <summary>建筑放置结果（含建造者）。</summary>
    public event Action<Guid, PlaceBuildingResultDto>? OnBuildingPlacedByPlayerReceived;
    /// <summary>火箭组装结果。</summary>
    public event Action<AssembleRocketResultDto>? OnRocketAssembleReceived;
    /// <summary>火箭发射结果。</summary>
    public event Action<LaunchRocketResultDto>? OnRocketLaunchReceived;
    /// <summary>实体归属变更。</summary>
    public event Action<EntityOwnershipDto>? OnOwnershipReceived;

    public Task OnModeChanged(ModeChangeResultDto result)
    {
        ServiceLog.Info($"[Server] Mode changed: success={result.Success}, mode={result.NewMode}");
        OnModeChangeReceived?.Invoke(result);
        return Task.CompletedTask;
    }

    public Task OnAbilityUsed(AbilityUseResultDto result)
    {
        ServiceLog.Info($"[Server] Ability used: {result.AbilityId}, success={result.Success}");
        OnAbilityUseReceived?.Invoke(result);
        return Task.CompletedTask;
    }

    public Task OnAbilityBarSynced(AbilityBarSyncDto syncData)
    {
        ServiceLog.Info($"[Server] Ability bar synced: {syncData.Abilities.Count} abilities");
        OnAbilityBarSyncReceived?.Invoke(syncData);
        return Task.CompletedTask;
    }

    public Task OnAbilityCooldownUpdated(Guid playerId, string abilityId, float cooldownRemaining)
    {
        OnAbilityCooldownReceived?.Invoke(playerId, abilityId, cooldownRemaining);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 服务端快照（SignalR 降级通道）
    // ══════════════════════════════════════════════════════════════════

    public Task OnServerSnapshot(byte[] snapshotFrame)
    {
        if (_snapshotApplier is null)
        {
            ServiceLog.Error("[ServerEventDispatcher] ❌ OnServerSnapshot received but _snapshotApplier is NULL!");
            return Task.CompletedTask;
        }

        //ServiceLog.Info($"[ServerEventDispatcher] 📦 SignalR snapshot received: {snapshotFrame.Length} bytes");
        _snapshotApplier.TryApply(snapshotFrame);
        return Task.CompletedTask;
    }
}
