using Game.Shared.Core.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Ark.Networking.SignalR;

// ── 服务端推送事件注册 ───────────────────────────────────────────────

public sealed partial class SignalRClient
{
    private void RegisterEventHandlers(IServerEventHandler handler)
    {
        // 清除旧订阅
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        var conn = Connection;

        // 连接 / 会话
        _subscriptions.Add(conn.On<string>(nameof(handler.OnForceDisconnect), handler.OnForceDisconnect));
        _subscriptions.Add(conn.On<string>(nameof(handler.OnServerMessage), handler.OnServerMessage));

        // 玩家状态
        _subscriptions.Add(conn.On<Guid, string, string>(nameof(handler.OnPlayerJoinedWorld), handler.OnPlayerJoinedWorld));
        _subscriptions.Add(conn.On<Guid, string>(nameof(handler.OnPlayerLeftWorld), handler.OnPlayerLeftWorld));
        _subscriptions.Add(conn.On<Guid, int, int>(nameof(handler.OnPlayerLevelUp), handler.OnPlayerLevelUp));
        _subscriptions.Add(conn.On<Guid, Guid?, string>(nameof(handler.OnPlayerDied), handler.OnPlayerDied));
        _subscriptions.Add(conn.On<RespawnResultDto>(nameof(handler.OnRespawned), handler.OnRespawned));

        // 战斗
        _subscriptions.Add(conn.On<Guid, Guid, string>(nameof(handler.OnCombatStarted), handler.OnCombatStarted));
        _subscriptions.Add(conn.On<Guid, Guid>(nameof(handler.OnCombatEnded), handler.OnCombatEnded));
        _subscriptions.Add(conn.On<Guid, Guid>(nameof(handler.OnEntityDestroyed), handler.OnEntityDestroyed));
        _subscriptions.Add(conn.On<AttackResultDto>(nameof(handler.OnDamageReceived), handler.OnDamageReceived));

        // 库存
        _subscriptions.Add(conn.On<InventoryDto>(nameof(handler.OnInventoryChanged), handler.OnInventoryChanged));
        _subscriptions.Add(conn.On<EquipResultDto>(nameof(handler.OnItemEquipped), handler.OnItemEquipped));
        _subscriptions.Add(conn.On<DropItemResultDto>(nameof(handler.OnItemDropped), handler.OnItemDropped));

        // 经济 / 市场
        _subscriptions.Add(conn.On<Guid, string, decimal, int, bool>(nameof(handler.OnMarketOrderPlaced), handler.OnMarketOrderPlaced));
        _subscriptions.Add(conn.On<Guid, Guid, Guid>(nameof(handler.OnMarketOrderFilled), handler.OnMarketOrderFilled));

        // 制造
        _subscriptions.Add(conn.On<Guid, string, int>(nameof(handler.OnCraftingStarted), handler.OnCraftingStarted));
        _subscriptions.Add(conn.On<Guid, string, int>(nameof(handler.OnCraftingCompleted), handler.OnCraftingCompleted));

        // 技能训练
        _subscriptions.Add(conn.On<Guid, string, int>(nameof(handler.OnSkillTrainingCompleted), handler.OnSkillTrainingCompleted));

        // 舰队
        _subscriptions.Add(conn.On<Guid, string, Guid>(nameof(handler.OnFleetCreated), handler.OnFleetCreated));
        _subscriptions.Add(conn.On<Guid, Guid>(nameof(handler.OnFleetMemberJoined), handler.OnFleetMemberJoined));
        _subscriptions.Add(conn.On<Guid, string, int, int>(nameof(handler.OnFleetBattleStarted), handler.OnFleetBattleStarted));
        _subscriptions.Add(conn.On<Guid, Guid, string>(nameof(handler.OnFleetBattleEnded), handler.OnFleetBattleEnded));

        // 公会
        _subscriptions.Add(conn.On<Guid, string, Guid>(nameof(handler.OnGuildCreated), handler.OnGuildCreated));
        _subscriptions.Add(conn.On<Guid, Guid>(nameof(handler.OnGuildMemberJoined), handler.OnGuildMemberJoined));

        // 聊天
        _subscriptions.Add(conn.On<Guid, string, string, DateTime>(nameof(handler.OnChatReceived), handler.OnChatReceived));

        // 副本
        _subscriptions.Add(conn.On<Guid, string, string>(nameof(handler.OnInstanceCreated), handler.OnInstanceCreated));
        _subscriptions.Add(conn.On<Guid, TimeSpan, int>(nameof(handler.OnInstanceCompleted), handler.OnInstanceCompleted));

        // 任务
        _subscriptions.Add(conn.On<Guid, string>(nameof(handler.OnQuestAccepted), handler.OnQuestAccepted));
        _subscriptions.Add(conn.On<Guid, string>(nameof(handler.OnQuestCompleted), handler.OnQuestCompleted));
        _subscriptions.Add(conn.On<QuestProgressDto>(nameof(handler.OnQuestProgressUpdated), handler.OnQuestProgressUpdated));

        // 主权 / 建筑
        _subscriptions.Add(conn.On<string, Guid?, Guid>(nameof(handler.OnSovereigntyChanged), handler.OnSovereigntyChanged));
        _subscriptions.Add(conn.On<string, Guid, string, Guid>(nameof(handler.OnStructurePlaced), handler.OnStructurePlaced));
        _subscriptions.Add(conn.On<string, Guid, string>(nameof(handler.OnStructureDestroyed), handler.OnStructureDestroyed));

        // 探索 / 资源
        _subscriptions.Add(conn.On<Guid, string, string>(nameof(handler.OnResourceDiscovered), handler.OnResourceDiscovered));
        _subscriptions.Add(conn.On<Guid, string, string, int>(nameof(handler.OnResourceHarvested), handler.OnResourceHarvested));

        // 成就
        _subscriptions.Add(conn.On<Guid, string, int>(nameof(handler.OnAchievementUnlocked), handler.OnAchievementUnlocked));

        // 脚本 / 活动
        _subscriptions.Add(conn.On<Guid, string, int>(nameof(handler.OnScriptStarted), handler.OnScriptStarted));
        _subscriptions.Add(conn.On<Guid, string>(nameof(handler.OnScriptCompleted), handler.OnScriptCompleted));
        _subscriptions.Add(conn.On<DialogueDto>(nameof(handler.OnDialogueUpdated), handler.OnDialogueUpdated));
        _subscriptions.Add(conn.On<Guid, string>(nameof(handler.OnActivityStarted), handler.OnActivityStarted));
        _subscriptions.Add(conn.On<Guid, string>(nameof(handler.OnActivityEnded), handler.OnActivityEnded));

        // 邮件
        _subscriptions.Add(conn.On<MailDto>(nameof(handler.OnMailReceived), handler.OnMailReceived));

        // 世界环境 / 地形
        _subscriptions.Add(conn.On<WorldEnvironmentDto>(nameof(handler.OnWorldEnvironmentReceived), handler.OnWorldEnvironmentReceived));
        _subscriptions.Add(conn.On<IReadOnlyList<TerrainModificationDto>>(nameof(handler.OnTerrainModificationsReceived), handler.OnTerrainModificationsReceived));
        _subscriptions.Add(conn.On<WeatherDto>(nameof(handler.OnWeatherChanged), handler.OnWeatherChanged));
        _subscriptions.Add(conn.On<float, float>(nameof(handler.OnTimeOfDayChanged), handler.OnTimeOfDayChanged));

        // 角色列表 / 选择 / 创建
        _subscriptions.Add(conn.On<CharacterListDto>(nameof(handler.OnCharacterListReceived), handler.OnCharacterListReceived));
        _subscriptions.Add(conn.On<CharacterCreateFullResultDto>(nameof(handler.OnCharacterCreated), handler.OnCharacterCreated));
        _subscriptions.Add(conn.On<SelectCharacterResultDto>(nameof(handler.OnCharacterSelected), handler.OnCharacterSelected));

        // 队伍 / 小队
        _subscriptions.Add(conn.On<PartyInfoDto>(nameof(handler.OnPartyUpdated), handler.OnPartyUpdated));
        _subscriptions.Add(conn.On<Guid, string, string, double, double, double>(nameof(handler.OnSquadMemberSpawned), handler.OnSquadMemberSpawned));
        _subscriptions.Add(conn.On<Guid, Guid?>(nameof(handler.OnSquadMemberDied), handler.OnSquadMemberDied));

        // 载具
        _subscriptions.Add(conn.On<VehicleStateDto>(nameof(handler.OnVehicleStateChanged), handler.OnVehicleStateChanged));
        _subscriptions.Add(conn.On<Guid, Guid, int>(nameof(handler.OnVehicleEntered), handler.OnVehicleEntered));
        _subscriptions.Add(conn.On<Guid, Guid>(nameof(handler.OnVehicleExited), handler.OnVehicleExited));
        _subscriptions.Add(conn.On<SpawnVehicleResultDto>(nameof(handler.OnVehicleSpawned), handler.OnVehicleSpawned));

        // 太空飞行
        _subscriptions.Add(conn.On<SpaceFlightStateDto>(nameof(handler.OnSpaceFlightStateChanged), handler.OnSpaceFlightStateChanged));
        _subscriptions.Add(conn.On<Guid, string>(nameof(handler.OnSpacePhaseChanged), handler.OnSpacePhaseChanged));
        _subscriptions.Add(conn.On<AssembleRocketResultDto>(nameof(handler.OnRocketAssembled), handler.OnRocketAssembled));
        _subscriptions.Add(conn.On<LaunchRocketResultDto>(nameof(handler.OnRocketLaunched), handler.OnRocketLaunched));

        // 建筑
        _subscriptions.Add(conn.On<Guid, PlaceBuildingResultDto>(nameof(handler.OnBuildingPlacedByPlayer), handler.OnBuildingPlacedByPlayer));
        _subscriptions.Add(conn.On<Guid, DestroyBuildingResultDto>(nameof(handler.OnBuildingDestroyedByPlayer), handler.OnBuildingDestroyedByPlayer));
        _subscriptions.Add(conn.On<Guid, int>(nameof(handler.OnBuildingUpgraded), handler.OnBuildingUpgraded));

        // 武器/弹药/弹道
        _subscriptions.Add(conn.On<FireWeaponResultDto>(nameof(handler.OnWeaponFired), handler.OnWeaponFired));
        _subscriptions.Add(conn.On<ReloadWeaponResultDto>(nameof(handler.OnReloadCompleted), handler.OnReloadCompleted));
        _subscriptions.Add(conn.On<ProjectileEventDto>(nameof(handler.OnProjectileBroadcast), handler.OnProjectileBroadcast));
        _subscriptions.Add(conn.On<EntityOwnershipDto>(nameof(handler.OnEntityOwnershipChanged), handler.OnEntityOwnershipChanged));

        // 附近实体广播
        _subscriptions.Add(conn.On<NearbyEntitiesDto>(nameof(handler.OnNearbyEntitiesUpdated), handler.OnNearbyEntitiesUpdated));

        // 玩法模式 & 技能/功能系统
        _subscriptions.Add(conn.On<ModeChangeResultDto>(nameof(handler.OnModeChanged), handler.OnModeChanged));
        _subscriptions.Add(conn.On<AbilityUseResultDto>(nameof(handler.OnAbilityUsed), handler.OnAbilityUsed));
        _subscriptions.Add(conn.On<AbilityBarSyncDto>(nameof(handler.OnAbilityBarSynced), handler.OnAbilityBarSynced));
        _subscriptions.Add(conn.On<Guid, string, float>(nameof(handler.OnAbilityCooldownUpdated), handler.OnAbilityCooldownUpdated));

        // 服务端快照（SignalR 降级通道，当 TCP 不可用时 WorldTickService 通过此推送）
        _subscriptions.Add(conn.On<byte[]>("OnServerSnapshot", handler.OnServerSnapshot));

        _logger?.LogInformation("Registered {Count} server event handlers", _subscriptions.Count);
    }
}
