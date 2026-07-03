using Game.Shared.Core.DTOs;

namespace Ark.Networking;

/// <summary>
/// 服务端推送事件的客户端接收接口。
/// SignalR 会自动将服务端 Clients.Caller/All.XXX 调用路由到此处注册的方法。
/// 实现方负责将事件分发到游戏系统（UI、ECS、音效等）。
/// </summary>
public interface IServerEventHandler
{
    // ══════════════════════════════════════════════════════════════════
    // 连接 / 会话
    // ══════════════════════════════════════════════════════════════════
    Task OnForceDisconnect(string reason);
    Task OnServerMessage(string message);

    // ══════════════════════════════════════════════════════════════════
    // 玩家状态
    // ══════════════════════════════════════════════════════════════════
    Task OnPlayerJoinedWorld(Guid playerId, string playerName, string worldId);
    Task OnPlayerLeftWorld(Guid playerId, string worldId);
    Task OnPlayerLevelUp(Guid playerId, int oldLevel, int newLevel);
    Task OnPlayerDied(Guid playerId, Guid? killerId, string zoneId);
    Task OnRespawned(RespawnResultDto result);

    // ══════════════════════════════════════════════════════════════════
    // 战斗
    // ══════════════════════════════════════════════════════════════════
    Task OnCombatStarted(Guid attackerId, Guid defenderId, string zoneId);
    Task OnCombatEnded(Guid winnerId, Guid loserId);
    Task OnEntityDestroyed(Guid entityId, Guid destroyedById);
    Task OnDamageReceived(AttackResultDto result);

    // ══════════════════════════════════════════════════════════════════
    // 库存
    // ══════════════════════════════════════════════════════════════════
    Task OnInventoryChanged(InventoryDto inventory);
    Task OnItemEquipped(EquipResultDto result);
    Task OnItemDropped(DropItemResultDto result);

    // ══════════════════════════════════════════════════════════════════
    // 经济 / 市场
    // ══════════════════════════════════════════════════════════════════
    Task OnMarketOrderPlaced(Guid orderId, string itemId, decimal pricePerUnit, int quantity, bool isBuyOrder);
    Task OnMarketOrderFilled(Guid orderId, Guid buyerId, Guid sellerId);

    // ══════════════════════════════════════════════════════════════════
    // 制造
    // ══════════════════════════════════════════════════════════════════
    Task OnCraftingStarted(Guid playerId, string blueprintId, int quantity);
    Task OnCraftingCompleted(Guid playerId, string blueprintId, int quantity);

    // ══════════════════════════════════════════════════════════════════
    // 技能训练
    // ══════════════════════════════════════════════════════════════════
    Task OnSkillTrainingCompleted(Guid playerId, string skillId, int newLevel);

    // ══════════════════════════════════════════════════════════════════
    // 舰队
    // ══════════════════════════════════════════════════════════════════
    Task OnFleetCreated(Guid fleetId, string fleetName, Guid leaderId);
    Task OnFleetMemberJoined(Guid fleetId, Guid playerId);
    Task OnFleetBattleStarted(Guid fleetId, string solarSystemId, int attackerCount, int defenderCount);
    Task OnFleetBattleEnded(Guid winningFleetId, Guid losingFleetId, string solarSystemId);

    // ══════════════════════════════════════════════════════════════════
    // 公会
    // ══════════════════════════════════════════════════════════════════
    Task OnGuildCreated(Guid guildId, string guildName, Guid founderId);
    Task OnGuildMemberJoined(Guid guildId, Guid playerId);

    // ══════════════════════════════════════════════════════════════════
    // 社交 / 聊天
    // ══════════════════════════════════════════════════════════════════
    Task OnChatReceived(Guid senderId, string channel, string content, DateTime timestamp);

    // ══════════════════════════════════════════════════════════════════
    // 副本
    // ══════════════════════════════════════════════════════════════════
    Task OnInstanceCreated(Guid instanceId, string templateId, string difficulty);
    Task OnInstanceCompleted(Guid instanceId, TimeSpan duration, int playerCount);

    // ══════════════════════════════════════════════════════════════════
    // 任务
    // ══════════════════════════════════════════════════════════════════
    Task OnQuestAccepted(Guid playerId, string questId);
    Task OnQuestCompleted(Guid playerId, string questId);
    Task OnQuestProgressUpdated(QuestProgressDto progress);

    // ══════════════════════════════════════════════════════════════════
    // 主权 / 建筑
    // ══════════════════════════════════════════════════════════════════
    Task OnSovereigntyChanged(string solarSystemId, Guid? oldOwnerId, Guid newOwnerId);
    Task OnStructurePlaced(string solarSystemId, Guid structureId, string structureType, Guid allianceId);
    Task OnStructureDestroyed(string solarSystemId, Guid structureId, string structureType);

    // ══════════════════════════════════════════════════════════════════
    // 探索 / 资源
    // ══════════════════════════════════════════════════════════════════
    Task OnResourceDiscovered(Guid playerId, string solarSystemId, string resourceType);
    Task OnResourceHarvested(Guid playerId, string solarSystemId, string resourceType, int quantity);

    // ══════════════════════════════════════════════════════════════════
    // 成就
    // ══════════════════════════════════════════════════════════════════
    Task OnAchievementUnlocked(Guid playerId, string achievementId, int points);

    // ══════════════════════════════════════════════════════════════════
    // 脚本 / 活动
    // ══════════════════════════════════════════════════════════════════
    Task OnScriptStarted(Guid playerId, string scriptId, int version);
    Task OnScriptCompleted(Guid playerId, string scriptId);
    Task OnDialogueUpdated(DialogueDto dialogue);
    Task OnActivityStarted(Guid activityId, string scriptId);
    Task OnActivityEnded(Guid activityId, string scriptId);

    // ══════════════════════════════════════════════════════════════════
    // 邮件
    // ══════════════════════════════════════════════════════════════════
    Task OnMailReceived(MailDto mail);

    // ══════════════════════════════════════════════════════════════════
    // 世界环境 / 地形（登录进入世界前预加载）
    // ══════════════════════════════════════════════════════════════════
    Task OnWorldEnvironmentReceived(WorldEnvironmentDto environment);
    Task OnTerrainModificationsReceived(IReadOnlyList<TerrainModificationDto> modifications);
    Task OnWeatherChanged(WeatherDto weather);
    Task OnTimeOfDayChanged(float timeOfDay, float timeScale);

    // ══════════════════════════════════════════════════════════════════
    // 角色列表 / 选择 / 创建结果
    // ══════════════════════════════════════════════════════════════════
    Task OnCharacterListReceived(CharacterListDto characterList);
    Task OnCharacterCreated(CharacterCreateFullResultDto result);
    Task OnCharacterSelected(SelectCharacterResultDto result);

    // ══════════════════════════════════════════════════════════════════
    // 队伍 / 小队
    // ══════════════════════════════════════════════════════════════════
    Task OnPartyUpdated(PartyInfoDto partyInfo);
    Task OnSquadMemberSpawned(Guid memberId, string name, string characterClass, double x, double y, double z);
    Task OnSquadMemberDied(Guid memberId, Guid? killerId);

    // ══════════════════════════════════════════════════════════════════
    // 载具（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════
    Task OnVehicleStateChanged(VehicleStateDto vehicleState);
    Task OnVehicleEntered(Guid playerId, Guid vehicleEntityId, int seatIndex);
    Task OnVehicleExited(Guid playerId, Guid vehicleEntityId);
    Task OnVehicleSpawned(SpawnVehicleResultDto result);

    // ══════════════════════════════════════════════════════════════════
    // 太空飞行（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════
    Task OnSpaceFlightStateChanged(SpaceFlightStateDto flightState);
    Task OnSpacePhaseChanged(Guid playerId, string phase);
    Task OnRocketAssembled(AssembleRocketResultDto result);
    Task OnRocketLaunched(LaunchRocketResultDto result);

    // ══════════════════════════════════════════════════════════════════
    // 建筑（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════
    Task OnBuildingPlacedByPlayer(Guid playerId, PlaceBuildingResultDto result);
    Task OnBuildingDestroyedByPlayer(Guid playerId, DestroyBuildingResultDto result);
    Task OnBuildingUpgraded(Guid buildingEntityId, int newLevel);

    // ══════════════════════════════════════════════════════════════════
    // 武器/弹药/弹道（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════
    Task OnWeaponFired(FireWeaponResultDto result);
    Task OnReloadCompleted(ReloadWeaponResultDto result);
    Task OnProjectileBroadcast(ProjectileEventDto projectile);
    Task OnEntityOwnershipChanged(EntityOwnershipDto ownership);

    // ══════════════════════════════════════════════════════════════════
    // 附近实体广播
    // ══════════════════════════════════════════════════════════════════
    Task OnNearbyEntitiesUpdated(NearbyEntitiesDto nearby);

    // ══════════════════════════════════════════════════════════════════
    // 玩法模式 & 技能/功能系统（服务端权威状态推送）
    // ══════════════════════════════════════════════════════════════════
    Task OnModeChanged(ModeChangeResultDto result);
    Task OnAbilityUsed(AbilityUseResultDto result);
    Task OnAbilityBarSynced(AbilityBarSyncDto syncData);
    Task OnAbilityCooldownUpdated(Guid playerId, string abilityId, float cooldownRemaining);

    // ══════════════════════════════════════════════════════════════════
    // 服务端快照（SignalR 降级通道）
    // ══════════════════════════════════════════════════════════════════
    /// <summary>
    /// 当 TCP 不可用时，WorldTickService 通过 SignalR 推送二进制快照帧。
    /// 帧格式与 TCP 快照完全一致：[1:packetId][8:tick][4:serverTime][N:entityStates]
    /// </summary>
    Task OnServerSnapshot(byte[] snapshotFrame);
}
