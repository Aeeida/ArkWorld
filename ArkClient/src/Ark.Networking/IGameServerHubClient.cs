using Game.Shared.Core.DTOs;

namespace Ark.Networking;

/// <summary>
/// 客户端通过 SignalR 调用服务端的统一接口。
/// 方法签名与服务端 IGameServerApi 完全对应。
/// 调用流程：Godot C#脚本 → IGameServerHubClient → SignalR Hub → 服务端业务层。
/// </summary>
public interface IGameServerHubClient
{
    // ══════════════════════════════════════════════════════════════════
    // 1. 认证与会话管理
    // ══════════════════════════════════════════════════════════════════
    Task<LoginResultDto> LoginAsync(LoginCommandDto cmd, CancellationToken ct = default);
    Task<JoinWorldResultDto> JoinWorldAsync(JoinWorldCommandDto cmd, CancellationToken ct = default);
    Task<LogoutResultDto> LogoutAsync(LogoutCommandDto cmd, CancellationToken ct = default);
    Task SendGameMessageAsync(byte[] data, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 2. 核心角色与养成系统
    // ══════════════════════════════════════════════════════════════════
    Task<CharacterCreateResultDto> CreateCharacterAsync(Guid accountId, string name, string faction, string characterClass, CancellationToken ct = default);
    Task<PlayerDto?> GetCharacterAsync(Guid characterId, CancellationToken ct = default);
    Task<bool> GainExperienceAsync(Guid playerId, long amount, CancellationToken ct = default);
    Task<SkillTreeDto> GetSkillTreeAsync(Guid playerId, CancellationToken ct = default);
    Task<TrainSkillResultDto> StartSkillTrainingAsync(StartTrainingCommandDto cmd, CancellationToken ct = default);
    Task<CancelTrainingResultDto> CancelSkillTrainingAsync(CancelTrainingCommandDto cmd, CancellationToken ct = default);
    Task<LevelUpResultDto> LevelUpAsync(LevelUpCommandDto cmd, CancellationToken ct = default);
    Task<AttributeSetDto> GetAttributesAsync(Guid playerId, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 3. 库存与物品系统
    // ══════════════════════════════════════════════════════════════════
    Task<bool> AddItemAsync(Guid playerId, string itemId, int quantity, CancellationToken ct = default);
    Task<InventoryDto> GetInventoryAsync(Guid playerId, CancellationToken ct = default);
    Task<ItemMoveResultDto> MoveItemAsync(ItemMoveCommandDto cmd, CancellationToken ct = default);
    Task<EquipResultDto> EquipItemAsync(EquipItemCommandDto cmd, CancellationToken ct = default);
    Task<DropItemResultDto> DropItemAsync(DropItemCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 4. 战斗与AI系统
    // ══════════════════════════════════════════════════════════════════
    Task<CombatResultDto> StartCombatAsync(Guid attackerId, Guid defenderId, CancellationToken ct = default);
    Task<AttackResultDto> AttackAsync(AttackCommandDto cmd, CancellationToken ct = default);
    Task<UseSkillResultDto> UseSkillAsync(UseSkillCommandDto cmd, CancellationToken ct = default);
    Task<BattleStatusDto> GetBattleStatusAsync(Guid playerId, CancellationToken ct = default);
    Task<FleetBattleCommandResultDto> CommandFleetAttackAsync(CommandFleetAttackCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 5. 世界与探索系统
    // ══════════════════════════════════════════════════════════════════
    Task<bool> EnterWorldAsync(Guid playerId, string worldId, CancellationToken ct = default);
    Task<bool> LeaveWorldAsync(Guid playerId, CancellationToken ct = default);
    Task<NavigationResultDto> NavigateToAsync(NavigationCommandDto cmd, CancellationToken ct = default);
    Task<ScanResultDto> ScanAreaAsync(ScanCommandDto cmd, CancellationToken ct = default);
    Task<ScanResultDto> ScanSystemAsync(Guid playerId, string solarSystemId, CancellationToken ct = default);
    Task<CollectResourceResultDto> CollectResourceAsync(CollectResourceCommandDto cmd, CancellationToken ct = default);
    Task<HarvestResultDto> HarvestResourceAsync(Guid playerId, string resourceNodeId, string solarSystemId, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 6. 经济与制造系统
    // ══════════════════════════════════════════════════════════════════
    Task<Guid> PlaceMarketOrderAsync(Guid sellerId, string itemId, int quantity, decimal pricePerUnit, string stationId, bool isBuyOrder, CancellationToken ct = default);
    Task<MarketOrdersDto> GetMarketOrdersAsync(string stationId, CancellationToken ct = default);
    Task<IReadOnlyList<MarketOrderDto>> GetMarketOrdersByFilterAsync(string stationId, string? itemFilter, CancellationToken ct = default);
    Task<CreateOrderResultDto> CreateMarketOrderAsync(CreateMarketOrderCommandDto cmd, CancellationToken ct = default);
    Task<BuyOrderResultDto> BuyOrderAsync(BuyOrderCommandDto cmd, CancellationToken ct = default);
    Task<StartCraftResultDto> StartCraftingAsync(StartCraftCommandDto cmd, CancellationToken ct = default);
    Task<CraftingJobDto> StartCraftingByBlueprintAsync(Guid playerId, string blueprintId, int quantity, CancellationToken ct = default);
    Task<CraftingQueueDto> GetCraftingQueueAsync(Guid playerId, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 7. 舰队与主权系统
    // ══════════════════════════════════════════════════════════════════
    Task<Guid> CreateFleetAsync(Guid leaderId, string fleetName, CancellationToken ct = default);
    Task<bool> JoinFleetAsync(Guid fleetId, Guid playerId, CancellationToken ct = default);
    Task<FleetDto?> GetFleetAsync(Guid fleetId, CancellationToken ct = default);
    Task<FormFleetResultDto> FormFleetAsync(FormFleetCommandDto cmd, CancellationToken ct = default);
    Task<CommandFleetResultDto> CommandFleetAsync(CommandFleetCommandDto cmd, CancellationToken ct = default);
    Task<ClaimSovereigntyResultDto> ClaimSovereigntyAsync(ClaimSovereigntyCommandDto cmd, CancellationToken ct = default);
    Task<IReadOnlyList<SovereigntyDto>> GetSovereigntyMapAsync(CancellationToken ct = default);
    Task<BuildStructureResultDto> BuildStructureAsync(BuildStructureCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 8. 公会与社交系统
    // ══════════════════════════════════════════════════════════════════
    Task<Guid> CreateGuildAsync(Guid founderId, string guildName, CancellationToken ct = default);
    Task<CreateGuildResultDto> CreateGuildByCommandAsync(CreateGuildCommandDto cmd, CancellationToken ct = default);
    Task<bool> JoinGuildAsync(Guid guildId, Guid playerId, CancellationToken ct = default);
    Task<GuildDto?> GetGuildAsync(Guid guildId, CancellationToken ct = default);
    Task<GuildInfoDto> GetGuildInfoAsync(Guid guildId, CancellationToken ct = default);
    Task<SendGuildChatResultDto> SendChatAsync(SendChatCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 9. 副本与实例系统
    // ══════════════════════════════════════════════════════════════════
    Task<Guid> CreateInstanceAsync(string instanceTemplateId, Guid leaderId, CancellationToken ct = default);
    Task<bool> JoinInstanceAsync(Guid instanceId, Guid playerId, CancellationToken ct = default);
    Task<InstanceDto?> GetInstanceAsync(Guid instanceId, CancellationToken ct = default);
    Task<EnterInstanceResultDto> EnterInstanceAsync(EnterInstanceCommandDto cmd, CancellationToken ct = default);
    Task<InstanceStatusDto> GetInstanceStatusAsync(Guid instanceId, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 10. 任务与叙事系统（含脚本系统）
    // ══════════════════════════════════════════════════════════════════
    Task<QuestDto> AcceptQuestAsync(AcceptQuestCommandDto cmd, CancellationToken ct = default);
    Task<AcceptQuestResultDto> AcceptQuestByIdAsync(Guid playerId, string questId, bool trackProgress, CancellationToken ct = default);
    Task<QuestRewardDto> CompleteQuestAsync(Guid playerId, string questId, CancellationToken ct = default);
    Task<SubmitQuestResultDto> SubmitQuestAsync(SubmitQuestCommandDto cmd, CancellationToken ct = default);
    Task<IReadOnlyList<QuestDto>> GetActiveQuestsAsync(Guid playerId, CancellationToken ct = default);
    Task<QuestProgressDto> GetQuestProgressAsync(Guid playerId, CancellationToken ct = default);
    Task<StartScriptResultDto> StartScriptedNarrativeAsync(StartScriptCommandDto cmd, CancellationToken ct = default);
    Task<DialogueChoiceResultDto> ChooseDialogueOptionAsync(ChooseDialogueCommandDto cmd, CancellationToken ct = default);
    Task<TriggerActivityResultDto> TriggerWorldActivityAsync(TriggerActivityCommandDto cmd, CancellationToken ct = default);
    Task<ScriptStatusDto> GetActiveScriptStatusAsync(Guid playerId, string scriptId, CancellationToken ct = default);

    // ── Legacy scripting ──────────────────────────────────────────────
    Task<ScriptResultDto> StartScriptAsync(Guid playerId, string scriptId, CancellationToken ct = default);
    Task<ScriptResultDto> AdvanceScriptAsync(Guid playerId, string scriptId, CancellationToken ct = default);
    Task<DialogueDto> GetDialogueAsync(Guid playerId, string scriptId, CancellationToken ct = default);
    Task<ScriptResultDto> ChooseDialogueOptionLegacyAsync(Guid playerId, string scriptId, int optionIndex, CancellationToken ct = default);
    Task<ScriptResultDto> AbortScriptAsync(Guid playerId, string scriptId, CancellationToken ct = default);
    Task<IReadOnlyList<ScriptStatusDto>> GetActiveScriptsAsync(Guid playerId, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityDto>> GetActiveActivitiesAsync(CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 11. 成就/收藏/外观系统
    // ══════════════════════════════════════════════════════════════════
    Task<AchievementDto> UnlockAchievementAsync(Guid playerId, string achievementId, CancellationToken ct = default);
    Task<AchievementProgressDto> GetAchievementsAsync(Guid playerId, CancellationToken ct = default);
    Task<IReadOnlyList<AchievementDto>> GetAchievementListAsync(Guid playerId, CancellationToken ct = default);
    Task<UnlockCosmeticResultDto> UnlockCosmeticAsync(UnlockCosmeticCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 12. 其他支撑功能
    // ══════════════════════════════════════════════════════════════════
    Task<SendMailResultDto> SendMailAsync(SendMailCommandDto cmd, CancellationToken ct = default);
    Task<GetMailDto> GetMailAsync(Guid playerId, CancellationToken ct = default);
    Task<RespawnResultDto> RespawnAsync(RespawnCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 13. 角色列表与完整创建（含队友/小队）
    // ══════════════════════════════════════════════════════════════════
    Task<CharacterListDto> GetCharacterListAsync(Guid accountId, CancellationToken ct = default);
    Task<CharacterCreateFullResultDto> CreateCharacterFullAsync(CreateCharacterFullCommandDto cmd, CancellationToken ct = default);
    Task<SelectCharacterResultDto> SelectCharacterAsync(SelectCharacterCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 14. 世界环境与地形（登录前预加载）
    // ══════════════════════════════════════════════════════════════════
    Task<WorldEnvironmentDto> GetWorldEnvironmentAsync(string worldId, CancellationToken ct = default);
    Task<IReadOnlyList<TerrainModificationDto>> GetTerrainModificationsAsync(string worldId, string zoneId, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 15. 队伍 / 小队查询
    // ══════════════════════════════════════════════════════════════════
    Task<PartyInfoDto> GetPartyInfoAsync(Guid playerId, CancellationToken ct = default);
    Task<NearbyEntitiesDto> GetNearbyEntitiesAsync(Guid playerId, float radius, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 16. 载具（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    Task<VehicleActionResultDto> VehicleActionAsync(VehicleActionCommandDto cmd, CancellationToken ct = default);
    Task<VehicleStateDto> GetVehicleStateAsync(Guid vehicleEntityId, CancellationToken ct = default);
    Task<bool> VehicleControlAsync(Guid playerId, Guid vehicleEntityId, float throttle, float steering, float brake, byte actionFlags, float turretYaw, float turretPitch, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 17. 太空飞行（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    Task<SpaceActionResultDto> SpaceActionAsync(SpaceActionCommandDto cmd, CancellationToken ct = default);
    Task<SpaceFlightStateDto> GetSpaceFlightStateAsync(Guid playerId, CancellationToken ct = default);
    Task<bool> SpacecraftControlAsync(Guid playerId, Guid spacecraftId, float thrustX, float thrustY, float thrustZ, float rotX, float rotY, float rotZ, byte actionFlags, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 18. 基地建造（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    Task<PlaceBuildingResultDto> PlaceBuildingAsync(PlaceBuildingCommandDto cmd, CancellationToken ct = default);
    Task<DestroyBuildingResultDto> DestroyBuildingAsync(DestroyBuildingCommandDto cmd, CancellationToken ct = default);
    Task<UpgradeBuildingResultDto> UpgradeBuildingAsync(UpgradeBuildingCommandDto cmd, CancellationToken ct = default);
    Task<BuildingListDto> GetAvailableBuildingTypesAsync(CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 19. 武器/弹药/弹道（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    Task<FireWeaponResultDto> FireWeaponAsync(FireWeaponCommandDto cmd, CancellationToken ct = default);
    Task<ReloadWeaponResultDto> ReloadWeaponAsync(ReloadWeaponCommandDto cmd, CancellationToken ct = default);
    Task<SeatWeaponInteractResultDto> SeatWeaponInteractAsync(SeatWeaponInteractCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 20. 载具生成（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    Task<SpawnVehicleResultDto> SpawnVehicleAsync(SpawnVehicleCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 21. 火箭组装/发射（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    Task<AssembleRocketResultDto> AssembleRocketAsync(AssembleRocketCommandDto cmd, CancellationToken ct = default);
    Task<LaunchRocketResultDto> LaunchRocketAsync(LaunchRocketCommandDto cmd, CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════════
    // 22. 玩法模式 & 技能/功能系统（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    Task<ModeChangeResultDto> ChangeModeAsync(ChangeModeCommandDto cmd, CancellationToken ct = default);
    Task<AbilityUseResultDto> UseAbilityAsync(UseAbilityCommandDto cmd, CancellationToken ct = default);
    Task<AbilityBarSyncDto> GetAbilityBarAsync(GetAbilityBarCommandDto cmd, CancellationToken ct = default);
    Task<AbilityBarSyncDto> GetAllAbilitiesAsync(GetAllAbilitiesCommandDto cmd, CancellationToken ct = default);
}
