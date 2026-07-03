using Game.Shared.Core.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

namespace Ark.Networking.SignalR;

// ── 服务端 RPC 调用实现（对应 IGameServerApi 全部方法）────────────────

public sealed partial class SignalRClient
{
    // ══════════════════════════════════════════════════════════════════
    // 1. 认证与会话管理
    // ══════════════════════════════════════════════════════════════════

    public Task<LoginResultDto> LoginAsync(LoginCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<LoginResultDto>(nameof(LoginAsync), cmd, ct);

    public Task AuthenticateAsync(Guid playerId, CancellationToken ct = default)
        => Connection.InvokeCoreAsync("Authenticate", [playerId], ct);

    public Task<JoinWorldResultDto> JoinWorldAsync(JoinWorldCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<JoinWorldResultDto>(nameof(JoinWorldAsync), cmd, ct);

    public Task<LogoutResultDto> LogoutAsync(LogoutCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<LogoutResultDto>(nameof(LogoutAsync), cmd, ct);

    public Task SendGameMessageAsync(byte[] data, CancellationToken ct = default)
        => Connection.InvokeCoreAsync("SendGameMessage", [data], ct);

    // ══════════════════════════════════════════════════════════════════
    // 2. 核心角色与养成系统
    // ══════════════════════════════════════════════════════════════════

    public Task<CharacterCreateResultDto> CreateCharacterAsync(Guid accountId, string name, string faction, string characterClass, CancellationToken ct)
        => Connection.InvokeAsync<CharacterCreateResultDto>(nameof(CreateCharacterAsync), accountId, name, faction, characterClass, ct);

    public Task<PlayerDto?> GetCharacterAsync(Guid characterId, CancellationToken ct)
        => Connection.InvokeAsync<PlayerDto?>(nameof(GetCharacterAsync), characterId, ct);

    public Task<bool> GainExperienceAsync(Guid playerId, long amount, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(GainExperienceAsync), playerId, amount, ct);

    public Task<SkillTreeDto> GetSkillTreeAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<SkillTreeDto>(nameof(GetSkillTreeAsync), playerId, ct);

    public Task<TrainSkillResultDto> StartSkillTrainingAsync(StartTrainingCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<TrainSkillResultDto>(nameof(StartSkillTrainingAsync), cmd, ct);

    public Task<CancelTrainingResultDto> CancelSkillTrainingAsync(CancelTrainingCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<CancelTrainingResultDto>(nameof(CancelSkillTrainingAsync), cmd, ct);

    public Task<LevelUpResultDto> LevelUpAsync(LevelUpCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<LevelUpResultDto>(nameof(LevelUpAsync), cmd, ct);

    public Task<AttributeSetDto> GetAttributesAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<AttributeSetDto>(nameof(GetAttributesAsync), playerId, ct);

    // ══════════════════════════════════════════════════════════════════
    // 3. 库存与物品系统
    // ══════════════════════════════════════════════════════════════════

    public Task<bool> AddItemAsync(Guid playerId, string itemId, int quantity, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(AddItemAsync), playerId, itemId, quantity, ct);

    public Task<InventoryDto> GetInventoryAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<InventoryDto>(nameof(GetInventoryAsync), playerId, ct);

    public Task<ItemMoveResultDto> MoveItemAsync(ItemMoveCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<ItemMoveResultDto>(nameof(MoveItemAsync), cmd, ct);

    public Task<EquipResultDto> EquipItemAsync(EquipItemCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<EquipResultDto>(nameof(EquipItemAsync), cmd, ct);

    public Task<DropItemResultDto> DropItemAsync(DropItemCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<DropItemResultDto>(nameof(DropItemAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 4. 战斗与AI系统
    // ══════════════════════════════════════════════════════════════════

    public Task<CombatResultDto> StartCombatAsync(Guid attackerId, Guid defenderId, CancellationToken ct)
        => Connection.InvokeAsync<CombatResultDto>(nameof(StartCombatAsync), attackerId, defenderId, ct);

    public Task<AttackResultDto> AttackAsync(AttackCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<AttackResultDto>(nameof(AttackAsync), cmd, ct);

    public Task<UseSkillResultDto> UseSkillAsync(UseSkillCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<UseSkillResultDto>(nameof(UseSkillAsync), cmd, ct);

    public Task<BattleStatusDto> GetBattleStatusAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<BattleStatusDto>(nameof(GetBattleStatusAsync), playerId, ct);

    public Task<FleetBattleCommandResultDto> CommandFleetAttackAsync(CommandFleetAttackCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<FleetBattleCommandResultDto>(nameof(CommandFleetAttackAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 5. 世界与探索系统
    // ══════════════════════════════════════════════════════════════════

    public Task<bool> EnterWorldAsync(Guid playerId, string worldId, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(EnterWorldAsync), playerId, worldId, ct);

    public Task<bool> LeaveWorldAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(LeaveWorldAsync), playerId, ct);

    public Task<NavigationResultDto> NavigateToAsync(NavigationCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<NavigationResultDto>(nameof(NavigateToAsync), cmd, ct);

    public Task<ScanResultDto> ScanAreaAsync(ScanCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<ScanResultDto>(nameof(ScanAreaAsync), cmd, ct);

    public Task<ScanResultDto> ScanSystemAsync(Guid playerId, string solarSystemId, CancellationToken ct)
        => Connection.InvokeAsync<ScanResultDto>(nameof(ScanSystemAsync), playerId, solarSystemId, ct);

    public Task<CollectResourceResultDto> CollectResourceAsync(CollectResourceCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<CollectResourceResultDto>(nameof(CollectResourceAsync), cmd, ct);

    public Task<HarvestResultDto> HarvestResourceAsync(Guid playerId, string resourceNodeId, string solarSystemId, CancellationToken ct)
        => Connection.InvokeAsync<HarvestResultDto>(nameof(HarvestResourceAsync), playerId, resourceNodeId, solarSystemId, ct);

    // ══════════════════════════════════════════════════════════════════
    // 6. 经济与制造系统
    // ══════════════════════════════════════════════════════════════════

    public Task<Guid> PlaceMarketOrderAsync(Guid sellerId, string itemId, int quantity, decimal pricePerUnit, string stationId, bool isBuyOrder, CancellationToken ct)
        => Connection.InvokeAsync<Guid>(nameof(PlaceMarketOrderAsync), sellerId, itemId, quantity, pricePerUnit, stationId, isBuyOrder, ct);

    public Task<MarketOrdersDto> GetMarketOrdersAsync(string stationId, CancellationToken ct)
        => Connection.InvokeAsync<MarketOrdersDto>(nameof(GetMarketOrdersAsync), stationId, ct);

    public Task<IReadOnlyList<MarketOrderDto>> GetMarketOrdersByFilterAsync(string stationId, string? itemFilter, CancellationToken ct)
        => Connection.InvokeAsync<IReadOnlyList<MarketOrderDto>>(nameof(GetMarketOrdersByFilterAsync), stationId, itemFilter, ct);

    public Task<CreateOrderResultDto> CreateMarketOrderAsync(CreateMarketOrderCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<CreateOrderResultDto>(nameof(CreateMarketOrderAsync), cmd, ct);

    public Task<BuyOrderResultDto> BuyOrderAsync(BuyOrderCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<BuyOrderResultDto>(nameof(BuyOrderAsync), cmd, ct);

    public Task<StartCraftResultDto> StartCraftingAsync(StartCraftCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<StartCraftResultDto>(nameof(StartCraftingAsync), cmd, ct);

    public Task<CraftingJobDto> StartCraftingByBlueprintAsync(Guid playerId, string blueprintId, int quantity, CancellationToken ct)
        => Connection.InvokeAsync<CraftingJobDto>(nameof(StartCraftingByBlueprintAsync), playerId, blueprintId, quantity, ct);

    public Task<CraftingQueueDto> GetCraftingQueueAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<CraftingQueueDto>(nameof(GetCraftingQueueAsync), playerId, ct);

    // ══════════════════════════════════════════════════════════════════
    // 7. 舰队与主权系统
    // ══════════════════════════════════════════════════════════════════

    public Task<Guid> CreateFleetAsync(Guid leaderId, string fleetName, CancellationToken ct)
        => Connection.InvokeAsync<Guid>(nameof(CreateFleetAsync), leaderId, fleetName, ct);

    public Task<bool> JoinFleetAsync(Guid fleetId, Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(JoinFleetAsync), fleetId, playerId, ct);

    public Task<FleetDto?> GetFleetAsync(Guid fleetId, CancellationToken ct)
        => Connection.InvokeAsync<FleetDto?>(nameof(GetFleetAsync), fleetId, ct);

    public Task<FormFleetResultDto> FormFleetAsync(FormFleetCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<FormFleetResultDto>(nameof(FormFleetAsync), cmd, ct);

    public Task<CommandFleetResultDto> CommandFleetAsync(CommandFleetCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<CommandFleetResultDto>(nameof(CommandFleetAsync), cmd, ct);

    public Task<ClaimSovereigntyResultDto> ClaimSovereigntyAsync(ClaimSovereigntyCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<ClaimSovereigntyResultDto>(nameof(ClaimSovereigntyAsync), cmd, ct);

    public Task<IReadOnlyList<SovereigntyDto>> GetSovereigntyMapAsync(CancellationToken ct)
        => Connection.InvokeAsync<IReadOnlyList<SovereigntyDto>>(nameof(GetSovereigntyMapAsync), ct);

    public Task<BuildStructureResultDto> BuildStructureAsync(BuildStructureCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<BuildStructureResultDto>(nameof(BuildStructureAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 8. 公会与社交系统
    // ══════════════════════════════════════════════════════════════════

    public Task<Guid> CreateGuildAsync(Guid founderId, string guildName, CancellationToken ct)
        => Connection.InvokeAsync<Guid>(nameof(CreateGuildAsync), founderId, guildName, ct);

    public Task<CreateGuildResultDto> CreateGuildByCommandAsync(CreateGuildCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<CreateGuildResultDto>(nameof(CreateGuildByCommandAsync), cmd, ct);

    public Task<bool> JoinGuildAsync(Guid guildId, Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(JoinGuildAsync), guildId, playerId, ct);

    public Task<GuildDto?> GetGuildAsync(Guid guildId, CancellationToken ct)
        => Connection.InvokeAsync<GuildDto?>(nameof(GetGuildAsync), guildId, ct);

    public Task<GuildInfoDto> GetGuildInfoAsync(Guid guildId, CancellationToken ct)
        => Connection.InvokeAsync<GuildInfoDto>(nameof(GetGuildInfoAsync), guildId, ct);

    public Task<SendGuildChatResultDto> SendChatAsync(SendChatCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<SendGuildChatResultDto>(nameof(SendChatAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 9. 副本与实例系统
    // ══════════════════════════════════════════════════════════════════

    public Task<Guid> CreateInstanceAsync(string instanceTemplateId, Guid leaderId, CancellationToken ct)
        => Connection.InvokeAsync<Guid>(nameof(CreateInstanceAsync), instanceTemplateId, leaderId, ct);

    public Task<bool> JoinInstanceAsync(Guid instanceId, Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(JoinInstanceAsync), instanceId, playerId, ct);

    public Task<InstanceDto?> GetInstanceAsync(Guid instanceId, CancellationToken ct)
        => Connection.InvokeAsync<InstanceDto?>(nameof(GetInstanceAsync), instanceId, ct);

    public Task<EnterInstanceResultDto> EnterInstanceAsync(EnterInstanceCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<EnterInstanceResultDto>(nameof(EnterInstanceAsync), cmd, ct);

    public Task<InstanceStatusDto> GetInstanceStatusAsync(Guid instanceId, CancellationToken ct)
        => Connection.InvokeAsync<InstanceStatusDto>(nameof(GetInstanceStatusAsync), instanceId, ct);

    // ══════════════════════════════════════════════════════════════════
    // 10. 任务与叙事系统（含脚本系统）
    // ══════════════════════════════════════════════════════════════════

    public Task<QuestDto> AcceptQuestAsync(AcceptQuestCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<QuestDto>(nameof(AcceptQuestAsync), cmd, ct);

    public Task<AcceptQuestResultDto> AcceptQuestByIdAsync(Guid playerId, string questId, bool trackProgress, CancellationToken ct)
        => Connection.InvokeAsync<AcceptQuestResultDto>(nameof(AcceptQuestByIdAsync), playerId, questId, trackProgress, ct);

    public Task<QuestRewardDto> CompleteQuestAsync(Guid playerId, string questId, CancellationToken ct)
        => Connection.InvokeAsync<QuestRewardDto>(nameof(CompleteQuestAsync), playerId, questId, ct);

    public Task<SubmitQuestResultDto> SubmitQuestAsync(SubmitQuestCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<SubmitQuestResultDto>(nameof(SubmitQuestAsync), cmd, ct);

    public Task<IReadOnlyList<QuestDto>> GetActiveQuestsAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<IReadOnlyList<QuestDto>>(nameof(GetActiveQuestsAsync), playerId, ct);

    public Task<QuestProgressDto> GetQuestProgressAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<QuestProgressDto>(nameof(GetQuestProgressAsync), playerId, ct);

    public Task<StartScriptResultDto> StartScriptedNarrativeAsync(StartScriptCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<StartScriptResultDto>(nameof(StartScriptedNarrativeAsync), cmd, ct);

    public Task<DialogueChoiceResultDto> ChooseDialogueOptionAsync(ChooseDialogueCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<DialogueChoiceResultDto>(nameof(ChooseDialogueOptionAsync), cmd, ct);

    public Task<TriggerActivityResultDto> TriggerWorldActivityAsync(TriggerActivityCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<TriggerActivityResultDto>(nameof(TriggerWorldActivityAsync), cmd, ct);

    public Task<ScriptStatusDto> GetActiveScriptStatusAsync(Guid playerId, string scriptId, CancellationToken ct)
        => Connection.InvokeAsync<ScriptStatusDto>(nameof(GetActiveScriptStatusAsync), playerId, scriptId, ct);

    // ── Legacy scripting ──────────────────────────────────────────────

    public Task<ScriptResultDto> StartScriptAsync(Guid playerId, string scriptId, CancellationToken ct)
        => Connection.InvokeAsync<ScriptResultDto>(nameof(StartScriptAsync), playerId, scriptId, ct);

    public Task<ScriptResultDto> AdvanceScriptAsync(Guid playerId, string scriptId, CancellationToken ct)
        => Connection.InvokeAsync<ScriptResultDto>(nameof(AdvanceScriptAsync), playerId, scriptId, ct);

    public Task<DialogueDto> GetDialogueAsync(Guid playerId, string scriptId, CancellationToken ct)
        => Connection.InvokeAsync<DialogueDto>(nameof(GetDialogueAsync), playerId, scriptId, ct);

    public Task<ScriptResultDto> ChooseDialogueOptionLegacyAsync(Guid playerId, string scriptId, int optionIndex, CancellationToken ct)
        => Connection.InvokeAsync<ScriptResultDto>(nameof(ChooseDialogueOptionLegacyAsync), playerId, scriptId, optionIndex, ct);

    public Task<ScriptResultDto> AbortScriptAsync(Guid playerId, string scriptId, CancellationToken ct)
        => Connection.InvokeAsync<ScriptResultDto>(nameof(AbortScriptAsync), playerId, scriptId, ct);

    public Task<IReadOnlyList<ScriptStatusDto>> GetActiveScriptsAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<IReadOnlyList<ScriptStatusDto>>(nameof(GetActiveScriptsAsync), playerId, ct);

    public Task<IReadOnlyList<ActivityDto>> GetActiveActivitiesAsync(CancellationToken ct)
        => Connection.InvokeAsync<IReadOnlyList<ActivityDto>>(nameof(GetActiveActivitiesAsync), ct);

    // ══════════════════════════════════════════════════════════════════
    // 11. 成就/收藏/外观系统
    // ══════════════════════════════════════════════════════════════════

    public Task<AchievementDto> UnlockAchievementAsync(Guid playerId, string achievementId, CancellationToken ct)
        => Connection.InvokeAsync<AchievementDto>(nameof(UnlockAchievementAsync), playerId, achievementId, ct);

    public Task<AchievementProgressDto> GetAchievementsAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<AchievementProgressDto>(nameof(GetAchievementsAsync), playerId, ct);

    public Task<IReadOnlyList<AchievementDto>> GetAchievementListAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<IReadOnlyList<AchievementDto>>(nameof(GetAchievementListAsync), playerId, ct);

    public Task<UnlockCosmeticResultDto> UnlockCosmeticAsync(UnlockCosmeticCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<UnlockCosmeticResultDto>(nameof(UnlockCosmeticAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 12. 其他支撑功能
    // ══════════════════════════════════════════════════════════════════

    public Task<SendMailResultDto> SendMailAsync(SendMailCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<SendMailResultDto>(nameof(SendMailAsync), cmd, ct);

    public Task<GetMailDto> GetMailAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<GetMailDto>(nameof(GetMailAsync), playerId, ct);

    public Task<RespawnResultDto> RespawnAsync(RespawnCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<RespawnResultDto>(nameof(RespawnAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 13. 角色列表与完整创建（含队友/小队）
    // ══════════════════════════════════════════════════════════════════

    public Task<CharacterListDto> GetCharacterListAsync(Guid accountId, CancellationToken ct)
        => Connection.InvokeAsync<CharacterListDto>(nameof(GetCharacterListAsync), accountId, ct);

    public Task<CharacterCreateFullResultDto> CreateCharacterFullAsync(CreateCharacterFullCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<CharacterCreateFullResultDto>(nameof(CreateCharacterFullAsync), cmd, ct);

    public Task<SelectCharacterResultDto> SelectCharacterAsync(SelectCharacterCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<SelectCharacterResultDto>(nameof(SelectCharacterAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 14. 世界环境与地形（登录前预加载）
    // ══════════════════════════════════════════════════════════════════

    public Task<WorldEnvironmentDto> GetWorldEnvironmentAsync(string worldId, CancellationToken ct)
        => Connection.InvokeAsync<WorldEnvironmentDto>(nameof(GetWorldEnvironmentAsync), worldId, ct);

    public Task<IReadOnlyList<TerrainModificationDto>> GetTerrainModificationsAsync(string worldId, string zoneId, CancellationToken ct)
        => Connection.InvokeAsync<IReadOnlyList<TerrainModificationDto>>(nameof(GetTerrainModificationsAsync), worldId, zoneId, ct);

    // ══════════════════════════════════════════════════════════════════
    // 15. 队伍 / 小队查询
    // ══════════════════════════════════════════════════════════════════

    public Task<PartyInfoDto> GetPartyInfoAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<PartyInfoDto>(nameof(GetPartyInfoAsync), playerId, ct);

    public Task<NearbyEntitiesDto> GetNearbyEntitiesAsync(Guid playerId, float radius, CancellationToken ct)
        => Connection.InvokeAsync<NearbyEntitiesDto>(nameof(GetNearbyEntitiesAsync), playerId, radius, ct);

    // ══════════════════════════════════════════════════════════════════
    // 16. 载具（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public Task<VehicleActionResultDto> VehicleActionAsync(VehicleActionCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<VehicleActionResultDto>(nameof(VehicleActionAsync), cmd, ct);

    public Task<VehicleStateDto> GetVehicleStateAsync(Guid vehicleEntityId, CancellationToken ct)
        => Connection.InvokeAsync<VehicleStateDto>(nameof(GetVehicleStateAsync), vehicleEntityId, ct);

    public Task<bool> VehicleControlAsync(Guid playerId, Guid vehicleEntityId, float throttle, float steering, float brake, byte actionFlags, float turretYaw, float turretPitch, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(VehicleControlAsync), playerId, vehicleEntityId, throttle, steering, brake, actionFlags, turretYaw, turretPitch, ct);

    // ══════════════════════════════════════════════════════════════════
    // 17. 太空飞行（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public Task<SpaceActionResultDto> SpaceActionAsync(SpaceActionCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<SpaceActionResultDto>(nameof(SpaceActionAsync), cmd, ct);

    public Task<SpaceFlightStateDto> GetSpaceFlightStateAsync(Guid playerId, CancellationToken ct)
        => Connection.InvokeAsync<SpaceFlightStateDto>(nameof(GetSpaceFlightStateAsync), playerId, ct);

    public Task<bool> SpacecraftControlAsync(Guid playerId, Guid spacecraftId, float thrustX, float thrustY, float thrustZ, float rotX, float rotY, float rotZ, byte actionFlags, CancellationToken ct)
        => Connection.InvokeAsync<bool>(nameof(SpacecraftControlAsync), playerId, spacecraftId, thrustX, thrustY, thrustZ, rotX, rotY, rotZ, actionFlags, ct);

    // ══════════════════════════════════════════════════════════════════
    // 18. 基地建造（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public Task<PlaceBuildingResultDto> PlaceBuildingAsync(PlaceBuildingCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<PlaceBuildingResultDto>(nameof(PlaceBuildingAsync), cmd, ct);

    public Task<DestroyBuildingResultDto> DestroyBuildingAsync(DestroyBuildingCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<DestroyBuildingResultDto>(nameof(DestroyBuildingAsync), cmd, ct);

    public Task<UpgradeBuildingResultDto> UpgradeBuildingAsync(UpgradeBuildingCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<UpgradeBuildingResultDto>(nameof(UpgradeBuildingAsync), cmd, ct);

    public Task<BuildingListDto> GetAvailableBuildingTypesAsync(CancellationToken ct)
        => Connection.InvokeAsync<BuildingListDto>(nameof(GetAvailableBuildingTypesAsync), ct);

    // ══════════════════════════════════════════════════════════════════
    // 19. 武器/弹药/弹道（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public Task<FireWeaponResultDto> FireWeaponAsync(FireWeaponCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<FireWeaponResultDto>(nameof(FireWeaponAsync), cmd, ct);

    public Task<ReloadWeaponResultDto> ReloadWeaponAsync(ReloadWeaponCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<ReloadWeaponResultDto>(nameof(ReloadWeaponAsync), cmd, ct);

    public Task<SeatWeaponInteractResultDto> SeatWeaponInteractAsync(SeatWeaponInteractCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<SeatWeaponInteractResultDto>(nameof(SeatWeaponInteractAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 20. 载具生成（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public Task<SpawnVehicleResultDto> SpawnVehicleAsync(SpawnVehicleCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<SpawnVehicleResultDto>(nameof(SpawnVehicleAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 21. 火箭组装/发射（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public Task<AssembleRocketResultDto> AssembleRocketAsync(AssembleRocketCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<AssembleRocketResultDto>(nameof(AssembleRocketAsync), cmd, ct);

    public Task<LaunchRocketResultDto> LaunchRocketAsync(LaunchRocketCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<LaunchRocketResultDto>(nameof(LaunchRocketAsync), cmd, ct);

    // ══════════════════════════════════════════════════════════════════
    // 22. 玩法模式 & 技能/功能系统（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public Task<ModeChangeResultDto> ChangeModeAsync(ChangeModeCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<ModeChangeResultDto>(nameof(ChangeModeAsync), cmd, ct);

    public Task<AbilityUseResultDto> UseAbilityAsync(UseAbilityCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<AbilityUseResultDto>(nameof(UseAbilityAsync), cmd, ct);

    public Task<AbilityBarSyncDto> GetAbilityBarAsync(GetAbilityBarCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<AbilityBarSyncDto>(nameof(GetAbilityBarAsync), cmd, ct);

    public Task<AbilityBarSyncDto> GetAllAbilitiesAsync(GetAllAbilitiesCommandDto cmd, CancellationToken ct)
        => Connection.InvokeAsync<AbilityBarSyncDto>(nameof(GetAllAbilitiesAsync), cmd, ct);
}
