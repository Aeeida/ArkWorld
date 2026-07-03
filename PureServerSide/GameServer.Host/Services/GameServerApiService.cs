using Game.Shared.Core.DTOs;
using Game.Shared.Core.Enums;
using Game.Shared.Core.Universe;
using GameServer.Api.Core;
using GameServer.Application.Core.CQRS;
using GameServer.Application.Features.Achievements;
using GameServer.Application.Features.Character;
using GameServer.Application.Features.Combat;
using GameServer.Application.Features.Crafting;
using GameServer.Application.Features.Economy;
using GameServer.Application.Features.Exploration;
using GameServer.Application.Features.Fleet;
using GameServer.Application.Features.Guild;
using GameServer.Application.Features.Instance;
using GameServer.Application.Features.Inventory;
using GameServer.Application.Features.Login;
using GameServer.Application.Features.Quests;
using GameServer.Application.Features.Scripting;
using GameServer.Application.Features.Skills;
using GameServer.Application.Features.Sovereignty;
using GameServer.Application.Features.World;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Host.Services;

public sealed class GameServerApiService(IMediator mediator, GameDbContext db) : IGameServerApi
{
    // ══════════════════════════════════════════════════════════════════
    // 1. 认证与会话管理
    // ══════════════════════════════════════════════════════════════════
    public async Task<LoginResultDto> LoginAsync(LoginCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new LoginCommand(cmd.AccountId, cmd.PasswordHash), ct);

    public async Task<JoinWorldResultDto> JoinWorldAsync(JoinWorldCommandDto cmd, CancellationToken ct)
    {
        // 1. 加载玩家，如果没有出生位置则从数据库分配一个
        var player = await db.Players.FindAsync([cmd.PlayerId], ct);
        if (player is null)
            return new JoinWorldResultDto(false, null, 0, "Player not found.");

        // 若玩家没有 CurrentWorldId（旧角色/出生点分配曾失败），在此补分配
        if (string.IsNullOrWhiteSpace(player.CurrentWorldId) || player.CurrentLocationId is null)
        {
            await AssignFallbackSpawnAsync(player, ct);
        }

        string worldId = cmd.SolarSystemId?.ToString()
                      ?? player.CurrentWorldId
                      ?? "default";

        var result = await mediator.Send(new JoinWorldCommand(cmd.PlayerId, worldId), ct);
        if (!result.Success)
            return result;

        // 2. 从位置数据库加载地形种子、生物群系和出生坐标
        // 重新加载 player 以获取可能刚分配的位置
        await db.Entry(player).ReloadAsync(ct);

        var location = player.CurrentLocationId is long locId
            ? await db.Locations.FindAsync([locId], ct)
            : null;

        // 按 Code/Name 匹配
        location ??= await db.Locations.FirstOrDefaultAsync(
            l => l.Code == worldId || l.Name == worldId, ct);

        // 兜底：取任意行星表面
        location ??= await db.Locations
            .Where(l => l.LocationType == LocationType.PlanetSurface)
            .FirstOrDefaultAsync(ct);

        long terrainSeed = 0;
        string? biomeId = null;
        byte weatherId = 0;
        float weatherIntensity = 0f;
        float timeOfDay = 9.5f;

        if (location is not null)
        {
            terrainSeed = location.TerrainSeed ?? location.Seed;
            biomeId = location.BiomeId;

            var envState = await db.WorldEnvironmentStates
                .FirstOrDefaultAsync(e => e.LocationId == location.Id, ct);

            if (envState is not null)
            {
                weatherId = envState.WeatherId;
                weatherIntensity = envState.WeatherIntensity;
                timeOfDay = envState.TimeOfDay;
            }
        }

        float spawnX = (float)player.LocalPositionX;
        float spawnY = (float)player.LocalPositionY;
        float spawnZ = (float)player.LocalPositionZ;

        return new JoinWorldResultDto(
            result.Success,
            result.WorldId,
            result.OnlinePlayerCount,
            result.ErrorMessage,
            TerrainSeed: terrainSeed,
            BiomeId: biomeId,
            WeatherId: weatherId,
            WeatherIntensity: weatherIntensity,
            TimeOfDay: timeOfDay,
            SpawnX: spawnX,
            SpawnY: spawnY,
            SpawnZ: spawnZ);
    }

    /// <summary>
    /// 为缺少出生点的旧角色补分配位置（取安全度最高的行星表面 + 散布）。
    /// </summary>
    private async Task AssignFallbackSpawnAsync(Domain.Entities.Player player, CancellationToken ct)
    {
        // 优先找带 SpawnPoint 元数据的表面
        var surface = await db.Locations
            .Where(l => l.LocationType == LocationType.PlanetSurface
                     && l.MetadataJson != null)
            .OrderByDescending(l => l.SecurityLevel)
            .FirstOrDefaultAsync(ct);

        // 兜底：任意行星表面
        surface ??= await db.Locations
            .Where(l => l.LocationType == LocationType.PlanetSurface)
            .FirstOrDefaultAsync(ct);

        if (surface is null) return;

        player.CurrentLocationId = surface.Id;
        player.CurrentWorldId = surface.Code;

        // 简单散布避免堆叠
        var rng = new Random(player.Id.GetHashCode());
        var angle = rng.NextDouble() * Math.PI * 2;
        var dist = 5 + rng.NextDouble() * 45;
        player.LocalPositionX = dist * Math.Cos(angle);
        player.LocalPositionY = 0;
        player.LocalPositionZ = dist * Math.Sin(angle);

        // 查找祖先 SolarSystem
        if (!string.IsNullOrEmpty(surface.HierarchyPath))
        {
            foreach (var idStr in surface.HierarchyPath.Split('/').Reverse())
            {
                if (!long.TryParse(idStr, out var ancestorId)) continue;
                var ancestor = await db.Locations.FindAsync([ancestorId], ct);
                if (ancestor?.LocationType == LocationType.SolarSystem)
                {
                    player.SolarSystemId = ancestor.Id;
                    break;
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<LogoutResultDto> LogoutAsync(LogoutCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new LogoutCommand(cmd.PlayerId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 2. 核心角色与养成系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<CharacterCreateResultDto> CreateCharacterAsync(
        Guid accountId, string name, string faction, string characterClass, CancellationToken ct) =>
        await mediator.Send(new CreateCharacterCommand(accountId, name, faction, characterClass), ct);

    public async Task<PlayerDto?> GetCharacterAsync(Guid characterId, CancellationToken ct) =>
        await mediator.Send(new GetCharacterQuery(characterId), ct);

    public async Task<bool> GainExperienceAsync(Guid playerId, long amount, CancellationToken ct) =>
        await mediator.Send(new GainExperienceCommand(playerId, amount), ct);

    public async Task<SkillTreeDto> GetSkillTreeAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetFullSkillTreeQuery(playerId), ct);

    public async Task<TrainSkillResultDto> StartSkillTrainingAsync(StartTrainingCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new StartSkillTrainingCommand(cmd.PlayerId, cmd.SkillId.ToString()), ct);

    public async Task<CancelTrainingResultDto> CancelSkillTrainingAsync(CancelTrainingCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new CancelTrainingCommand(cmd.PlayerId), ct);

    public async Task<LevelUpResultDto> LevelUpAsync(LevelUpCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new LevelUpCommand(cmd.PlayerId), ct);

    public async Task<AttributeSetDto> GetAttributesAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetAttributesQuery(playerId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 3. 库存与物品系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<bool> AddItemAsync(Guid playerId, string itemId, int quantity, CancellationToken ct) =>
        await mediator.Send(new AddItemCommand(playerId, itemId, quantity), ct);

    public async Task<InventoryDto> GetInventoryAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetFullInventoryQuery(playerId), ct);

    public async Task<ItemMoveResultDto> MoveItemAsync(ItemMoveCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new MoveItemCommand(cmd.PlayerId, cmd.ItemInstanceId.ToString(), cmd.SlotIndex ?? 0, cmd.SlotIndex ?? 0), ct);

    public async Task<EquipResultDto> EquipItemAsync(EquipItemCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new EquipItemCommand(cmd.PlayerId, cmd.ItemInstanceId.ToString(), cmd.EquipSlot.ToString()), ct);

    public async Task<DropItemResultDto> DropItemAsync(DropItemCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new DropItemCommand(cmd.PlayerId, cmd.ItemInstanceId.ToString(), cmd.Quantity), ct);

    // ══════════════════════════════════════════════════════════════════
    // 4. 战斗与AI系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<CombatResultDto> StartCombatAsync(Guid attackerId, Guid defenderId, CancellationToken ct) =>
        await mediator.Send(new StartCombatCommand(attackerId, defenderId), ct);

    public async Task<AttackResultDto> AttackAsync(AttackCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new AttackCommand(cmd.PlayerId, cmd.TargetId, cmd.SkillId?.ToString()), ct);

    public async Task<UseSkillResultDto> UseSkillAsync(UseSkillCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new UseSkillCommand(cmd.PlayerId, cmd.SkillId.ToString(), cmd.Targets.FirstOrDefault()), ct);

    public async Task<BattleStatusDto> GetBattleStatusAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetBattleStatusQuery(playerId), ct);

    public async Task<FleetBattleCommandResultDto> CommandFleetAttackAsync(CommandFleetAttackCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new CommandFleetAttackCommand(cmd.FleetId, cmd.TargetId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 5. 世界与探索系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<bool> EnterWorldAsync(Guid playerId, string worldId, CancellationToken ct) =>
        await mediator.Send(new EnterWorldCommand(playerId, worldId), ct);

    public async Task<bool> LeaveWorldAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new LeaveWorldCommand(playerId), ct);

    public async Task<NavigationResultDto> NavigateToAsync(NavigationCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new NavigateToCommand(cmd.PlayerId, cmd.TargetSolarSystemId.ToString(), 0, 0, 0), ct);

    public async Task<ScanResultDto> ScanAreaAsync(ScanCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new ScanAreaCommand(cmd.PlayerId, cmd.Coordinates.ToString()), ct);

    public async Task<ScanResultDto> ScanSystemAsync(Guid playerId, string solarSystemId, CancellationToken ct) =>
        await mediator.Send(new ScanSystemCommand(playerId, solarSystemId), ct);

    public async Task<CollectResourceResultDto> CollectResourceAsync(CollectResourceCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new CollectResourceCommand(cmd.PlayerId, cmd.ResourceNodeId.ToString(), string.Empty), ct);

    public async Task<HarvestResultDto> HarvestResourceAsync(Guid playerId, string resourceNodeId, string solarSystemId, CancellationToken ct) =>
        await mediator.Send(new HarvestResourceCommand(playerId, resourceNodeId, solarSystemId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 6. 经济与制造系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<Guid> PlaceMarketOrderAsync(
        Guid sellerId, string itemId, int quantity, decimal pricePerUnit,
        string stationId, bool isBuyOrder, CancellationToken ct) =>
        await mediator.Send(new PlaceMarketOrderCommand(sellerId, itemId, quantity, pricePerUnit, stationId, isBuyOrder), ct);

    public async Task<MarketOrdersDto> GetMarketOrdersAsync(string stationId, CancellationToken ct) =>
        await mediator.Send(new GetMarketOrdersWrappedQuery(stationId), ct);

    public async Task<IReadOnlyList<MarketOrderDto>> GetMarketOrdersAsync(string stationId, string? itemFilter, CancellationToken ct) =>
        await mediator.Send(new GetMarketOrdersQuery(stationId, itemFilter), ct);

    public async Task<CreateOrderResultDto> CreateMarketOrderAsync(CreateMarketOrderCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new CreateMarketOrderCommand(cmd.PlayerId, cmd.ItemId.ToString(), cmd.Quantity, cmd.PricePerUnit, string.Empty, cmd.OrderType == OrderType.Buy), ct);

    public async Task<BuyOrderResultDto> BuyOrderAsync(BuyOrderCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new BuyOrderCommand(cmd.PlayerId, cmd.OrderId, cmd.Quantity), ct);

    public async Task<StartCraftResultDto> StartCraftingAsync(StartCraftCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new StartCraftWrappedCommand(cmd.PlayerId, cmd.BlueprintId.ToString()), ct);

    public async Task<CraftingJobDto> StartCraftingAsync(Guid playerId, string blueprintId, int quantity, CancellationToken ct) =>
        await mediator.Send(new StartCraftingCommand(playerId, blueprintId, quantity), ct);

    public async Task<CraftingQueueDto> GetCraftingQueueAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetCraftingQueueWrappedQuery(playerId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 7. 舰队与主权系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<Guid> CreateFleetAsync(Guid leaderId, string fleetName, CancellationToken ct) =>
        await mediator.Send(new CreateFleetCommand(leaderId, fleetName), ct);

    public async Task<bool> JoinFleetAsync(Guid fleetId, Guid playerId, CancellationToken ct) =>
        await mediator.Send(new JoinFleetCommand(fleetId, playerId), ct);

    public async Task<FleetDto?> GetFleetAsync(Guid fleetId, CancellationToken ct) =>
        await mediator.Send(new GetFleetQuery(fleetId), ct);

    public async Task<FormFleetResultDto> FormFleetAsync(FormFleetCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new FormFleetCommand(cmd.LeaderId, cmd.FleetName ?? string.Empty), ct);

    public async Task<CommandFleetResultDto> CommandFleetAsync(CommandFleetCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new CommandFleetCommand(cmd.FleetId, cmd.CommandType.ToString(), null), ct);

    public async Task<ClaimSovereigntyResultDto> ClaimSovereigntyAsync(ClaimSovereigntyCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new ClaimSovereigntyWrappedCommand(cmd.AllianceId ?? cmd.PlayerId, cmd.SolarSystemId.ToString()), ct);

    public async Task<IReadOnlyList<SovereigntyDto>> GetSovereigntyMapAsync(CancellationToken ct) =>
        await mediator.Send(new GetSovereigntyMapQuery(), ct);

    public async Task<BuildStructureResultDto> BuildStructureAsync(BuildStructureCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new BuildStructureCommand(cmd.PlayerId, cmd.SolarSystemId.ToString(), cmd.StructureType.ToString()), ct);

    // ══════════════════════════════════════════════════════════════════
    // 8. 公会与社交系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<Guid> CreateGuildAsync(Guid founderId, string guildName, CancellationToken ct) =>
        await mediator.Send(new CreateGuildCommand(founderId, guildName), ct);

    public async Task<CreateGuildResultDto> CreateGuildAsync(CreateGuildCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new CreateGuildWrappedCommand(cmd.PlayerId, cmd.GuildName, cmd.Description), ct);

    public async Task<bool> JoinGuildAsync(Guid guildId, Guid playerId, CancellationToken ct) =>
        await mediator.Send(new JoinGuildCommand(guildId, playerId), ct);

    public async Task<GuildDto?> GetGuildAsync(Guid guildId, CancellationToken ct) =>
        await mediator.Send(new GetGuildQuery(guildId), ct);

    public async Task<GuildInfoDto> GetGuildInfoAsync(Guid guildId, CancellationToken ct) =>
        await mediator.Send(new GetGuildInfoQuery(guildId), ct);

    public async Task<SendGuildChatResultDto> SendChatAsync(SendChatCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new SendChatCommand(cmd.PlayerId, cmd.TargetId ?? Guid.Empty, cmd.Message), ct);

    // ══════════════════════════════════════════════════════════════════
    // 9. 副本与实例系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<Guid> CreateInstanceAsync(string instanceTemplateId, Guid leaderId, CancellationToken ct) =>
        await mediator.Send(new CreateInstanceCommand(instanceTemplateId, leaderId), ct);

    public async Task<bool> JoinInstanceAsync(Guid instanceId, Guid playerId, CancellationToken ct) =>
        await mediator.Send(new JoinInstanceCommand(instanceId, playerId), ct);

    public async Task<InstanceDto?> GetInstanceAsync(Guid instanceId, CancellationToken ct) =>
        await mediator.Send(new GetInstanceQuery(instanceId), ct);

    public async Task<EnterInstanceResultDto> EnterInstanceAsync(EnterInstanceCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new EnterInstanceCommand(cmd.PlayerId, cmd.InstanceTemplateId), ct);

    public async Task<InstanceStatusDto> GetInstanceStatusAsync(Guid instanceId, CancellationToken ct) =>
        await mediator.Send(new GetInstanceStatusQuery(instanceId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 10. 任务与叙事系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<QuestDto> AcceptQuestAsync(AcceptQuestCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new AcceptQuestCommand(cmd.PlayerId, cmd.QuestId.ToString()), ct);

    public async Task<AcceptQuestResultDto> AcceptQuestAsync(Guid playerId, string questId, bool trackProgress, CancellationToken ct) =>
        await mediator.Send(new AcceptQuestWrappedCommand(playerId, questId), ct);

    public async Task<QuestRewardDto> CompleteQuestAsync(Guid playerId, string questId, CancellationToken ct) =>
        await mediator.Send(new CompleteQuestCommand(playerId, questId), ct);

    public async Task<SubmitQuestResultDto> SubmitQuestAsync(SubmitQuestCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new SubmitQuestCommand(cmd.PlayerId, cmd.QuestId.ToString()), ct);

    public async Task<IReadOnlyList<QuestDto>> GetActiveQuestsAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetActiveQuestsQuery(playerId), ct);

    public async Task<QuestProgressDto> GetQuestProgressAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetQuestProgressQuery(playerId), ct);

    public async Task<StartScriptResultDto> StartScriptedNarrativeAsync(StartScriptCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new StartScriptedNarrativeCommand(cmd.PlayerId, cmd.ScriptId.ToString()), ct);

    public async Task<DialogueChoiceResultDto> ChooseDialogueOptionAsync(ChooseDialogueCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new ChooseDialogueWrappedCommand(cmd.PlayerId, cmd.ScriptInstanceId.ToString(), cmd.ChoiceIndex), ct);

    public async Task<TriggerActivityResultDto> TriggerWorldActivityAsync(TriggerActivityCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new TriggerWorldActivityCommand(cmd.ActivityId.ToString(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null), ct);

    public async Task<ScriptStatusDto> GetActiveScriptStatusAsync(Guid playerId, string scriptId, CancellationToken ct) =>
        await mediator.Send(new GetActiveScriptStatusQuery(playerId, scriptId), ct);

    // ── Legacy scripting ─────────────────────────────────────────────
    public async Task<ScriptResultDto> StartScriptAsync(Guid playerId, string scriptId, CancellationToken ct) =>
        await mediator.Send(new StartScriptCommand(playerId, scriptId), ct);

    public async Task<ScriptResultDto> AdvanceScriptAsync(Guid playerId, string scriptId, CancellationToken ct) =>
        await mediator.Send(new AdvanceScriptCommand(playerId, scriptId), ct);

    public async Task<DialogueDto> GetDialogueAsync(Guid playerId, string scriptId, CancellationToken ct) =>
        await mediator.Send(new GetDialogueQuery(playerId, scriptId), ct);

    public async Task<ScriptResultDto> ChooseDialogueOptionAsync(Guid playerId, string scriptId, int optionIndex, CancellationToken ct) =>
        await mediator.Send(new ChooseDialogueOptionCommand(playerId, scriptId, optionIndex), ct);

    public async Task<ScriptResultDto> AbortScriptAsync(Guid playerId, string scriptId, CancellationToken ct) =>
        await mediator.Send(new AbortScriptCommand(playerId, scriptId), ct);

    public async Task<IReadOnlyList<ScriptStatusDto>> GetActiveScriptsAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetActiveScriptsQuery(playerId), ct);

    public async Task<IReadOnlyList<ActivityDto>> GetActiveActivitiesAsync(CancellationToken ct) =>
        await mediator.Send(new GetActiveActivitiesQuery(), ct);

    // ══════════════════════════════════════════════════════════════════
    // 11. 成就/收藏/外观系统
    // ══════════════════════════════════════════════════════════════════
    public async Task<AchievementDto> UnlockAchievementAsync(Guid playerId, string achievementId, CancellationToken ct) =>
        await mediator.Send(new UnlockAchievementCommand(playerId, achievementId), ct);

    public async Task<AchievementProgressDto> GetAchievementsAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetAchievementProgressQuery(playerId), ct);

    public async Task<IReadOnlyList<AchievementDto>> GetAchievementListAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetAchievementsQuery(playerId), ct);

    public async Task<UnlockCosmeticResultDto> UnlockCosmeticAsync(UnlockCosmeticCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new UnlockCosmeticCommand(cmd.PlayerId, cmd.CosmeticId.ToString()), ct);

    // ══════════════════════════════════════════════════════════════════
    // 12. 其他支撑功能
    // ══════════════════════════════════════════════════════════════════
    public async Task<SendMailResultDto> SendMailAsync(SendMailCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new SendMailCommand(cmd.PlayerId, cmd.RecipientId, cmd.Title, cmd.Content), ct);

    public async Task<GetMailDto> GetMailAsync(Guid playerId, CancellationToken ct) =>
        await mediator.Send(new GetMailQuery(playerId), ct);

    public async Task<RespawnResultDto> RespawnAsync(RespawnCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new RespawnCommand(cmd.PlayerId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 13. 角色列表与完整创建（含队友/小队）
    // ══════════════════════════════════════════════════════════════════
    public async Task<CharacterListDto> GetCharacterListAsync(Guid accountId, CancellationToken ct) =>
        await mediator.Send(new GetCharacterListQuery(accountId), ct);

    public async Task<CharacterCreateFullResultDto> CreateCharacterFullAsync(CreateCharacterFullCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new CreateCharacterFullCommand(cmd), ct);

    public async Task<SelectCharacterResultDto> SelectCharacterAsync(SelectCharacterCommandDto cmd, CancellationToken ct) =>
        await mediator.Send(new SelectCharacterCommand(cmd.PlayerId, cmd.CharacterId), ct);

    // ══════════════════════════════════════════════════════════════════
    // 14. 世界环境与地形（登录前预加载）
    // ══════════════════════════════════════════════════════════════════
    public async Task<WorldEnvironmentDto> GetWorldEnvironmentAsync(string worldId, CancellationToken ct)
    {
        // 尝试从数据库加载世界环境
        var location = await db.Locations
            .FirstOrDefaultAsync(l => l.Code == worldId || l.Name == worldId, ct);

        if (location is null)
        {
            // 尝试找一个默认的行星表面
            location = await db.Locations
                .Where(l => l.LocationType == LocationType.PlanetSurface)
                .FirstOrDefaultAsync(ct);
        }

        if (location is not null)
        {
            var envState = await db.WorldEnvironmentStates
                .FirstOrDefaultAsync(e => e.LocationId == location.Id, ct);

            var terrainMods = await db.TerrainModifications
                .Where(t => t.LocationId == location.Id)
                .Select(t => new TerrainModificationDto(t.CenterX, t.CenterZ, t.RadiusX, t.RadiusZ, t.TargetHeight, t.ModificationType))
                .ToListAsync(ct);

            // 加载世界物体生成列表
            var spawns = await db.WorldSpawns
                .Where(s => s.LocationId == location.Id && s.IsActive)
                .Select(s => new WorldSpawnDto(
                    s.SpawnType,
                    s.TemplateId,
                    s.DisplayName,
                    s.LocalX,
                    s.LocalY,
                    s.LocalZ,
                    s.Rotation,
                    s.Level,
                    s.IsActive,
                    s.SpawnType != "player",
                    s.MetadataJson))
                .ToListAsync(ct);

            // 构建位置路径（人类可读）
            var locationPath = await BuildLocationPathAsync(location, ct);

            // 查找祖先 SolarSystem ID
            long solarSystemId = 0;
            if (!string.IsNullOrEmpty(location.HierarchyPath))
            {
                var ancestorIds = location.HierarchyPath.Split('/').Select(long.Parse).Reverse();
                foreach (var aid in ancestorIds)
                {
                    var ancestor = await db.Locations.FindAsync([aid], ct);
                    if (ancestor?.LocationType == LocationType.SolarSystem)
                    {
                        solarSystemId = ancestor.Id;
                        break;
                    }
                }
            }

            return new WorldEnvironmentDto(
                TerrainSeed: location.TerrainSeed ?? location.Seed,
                TerrainModifications: terrainMods,
                Weather: new WeatherDto(
                    envState?.WeatherId ?? 0,
                    envState?.WeatherIntensity ?? 0.2f,
                    envState?.WindX ?? 0, envState?.WindY ?? 0, envState?.WindZ ?? 0,
                    envState?.FogDensity ?? 0.05f,
                    envState?.Temperature ?? 22f),
                TimeOfDay: envState?.TimeOfDay ?? 9.5f,
                TimeScale: envState?.TimeScale ?? 1f,
                BiomeId: location.BiomeId ?? "temperate",
                WorldId: worldId,
                GravityMultiplier: location.GravityMultiplier)
            {
                LocationId = location.Id,
                SolarSystemId = solarSystemId,
                LocationPath = locationPath,
                WorldSpawns = spawns,
                CosmicEnvironment = envState is not null ? new CosmicEnvironmentDto(
                    location.Id,
                    envState.ActiveHazards,
                    envState.RadiationLevel,
                    location.GravityMultiplier,
                    envState.Temperature,
                    location.AtmosphereDensity,
                    envState.WeatherId,
                    envState.WeatherIntensity,
                    envState.WindX, envState.WindY, envState.WindZ) : null
            };
        }

        // 回退到默认值（尚未生成宇宙时）
        return new WorldEnvironmentDto(
            TerrainSeed: worldId.GetHashCode(StringComparison.Ordinal),
            TerrainModifications: [],
            Weather: new WeatherDto(0, 0.2f, 0f, 0f, 0f, 0.05f, 22f),
            TimeOfDay: 9.5f,
            TimeScale: 1f,
            BiomeId: "temperate",
            WorldId: worldId,
            GravityMultiplier: 1f);
    }

    public async Task<IReadOnlyList<TerrainModificationDto>> GetTerrainModificationsAsync(string worldId, string zoneId, CancellationToken ct)
    {
        var location = await db.Locations
            .FirstOrDefaultAsync(l => l.Code == worldId || l.Name == worldId, ct);

        if (location is null)
            return [];

        var query = db.TerrainModifications.Where(t => t.LocationId == location.Id);
        if (!string.IsNullOrWhiteSpace(zoneId))
            query = query.Where(t => t.ChunkKey == null || t.ChunkKey == zoneId);

        return await query
            .OrderBy(t => t.SequenceTick)
            .Select(t => new TerrainModificationDto(t.CenterX, t.CenterZ, t.RadiusX, t.RadiusZ, t.TargetHeight, t.ModificationType, t.ChunkKey, t.SequenceTick, t.MetadataJson))
            .ToListAsync(ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // 15. 队伍 / 小队查询
    // ══════════════════════════════════════════════════════════════════
    public Task<PartyInfoDto> GetPartyInfoAsync(Guid playerId, CancellationToken ct) =>
        Task.FromResult(new PartyInfoDto(
            LeaderId: playerId,
            Members:
            [
                new PartyMemberDto(
                    MemberId: playerId,
                    Name: "Leader",
                    CharacterClass: "Ranger",
                    Level: 1,
                    Health: 100,
                    MaxHealth: 100,
                    IsAlive: true,
                    Role: "Leader",
                    IsAI: false)
            ],
            MaxSize: 4));

    public Task<NearbyEntitiesDto> GetNearbyEntitiesAsync(Guid playerId, float radius, CancellationToken ct) =>
        Task.FromResult(new NearbyEntitiesDto(
            Entities: [],
            QueryRadius: radius,
            ZoneId: "default"));

    // ══════════════════════════════════════════════════════════════════
    // 16. 载具（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    public Task<VehicleActionResultDto> VehicleActionAsync(VehicleActionCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<VehicleStateDto> GetVehicleStateAsync(Guid vehicleEntityId, CancellationToken ct) =>
        throw new NotImplementedException();

    // ══════════════════════════════════════════════════════════════════
    // 17. 太空飞行（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    public Task<SpaceActionResultDto> SpaceActionAsync(SpaceActionCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<SpaceFlightStateDto> GetSpaceFlightStateAsync(Guid playerId, CancellationToken ct) =>
        throw new NotImplementedException();

    // ══════════════════════════════════════════════════════════════════
    // 18. 基地建造（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    public Task<PlaceBuildingResultDto> PlaceBuildingAsync(PlaceBuildingCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<DestroyBuildingResultDto> DestroyBuildingAsync(DestroyBuildingCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<UpgradeBuildingResultDto> UpgradeBuildingAsync(UpgradeBuildingCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<BuildingListDto> GetAvailableBuildingTypesAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    // ══════════════════════════════════════════════════════════════════
    // 19. 武器/弹药/弹道（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    public Task<FireWeaponResultDto> FireWeaponAsync(FireWeaponCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<ReloadWeaponResultDto> ReloadWeaponAsync(ReloadWeaponCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    // ══════════════════════════════════════════════════════════════════
    // 20. 载具生成（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    public Task<SpawnVehicleResultDto> SpawnVehicleAsync(SpawnVehicleCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    // ══════════════════════════════════════════════════════════════════
    // 21. 火箭组装/发射（服务端权威）
    // ══════════════════════════════════════════════════════════════════
    public Task<AssembleRocketResultDto> AssembleRocketAsync(AssembleRocketCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<LaunchRocketResultDto> LaunchRocketAsync(LaunchRocketCommandDto cmd, CancellationToken ct) =>
        throw new NotImplementedException();

    // ══════════════════════════════════════════════════════════════════
    // Internal helpers
    // ══════════════════════════════════════════════════════════════════

    private async Task<string> BuildLocationPathAsync(GameServer.Domain.Entities.LocationNode node, CancellationToken ct)
    {
        var segments = new List<string> { node.Name };
        if (!string.IsNullOrEmpty(node.HierarchyPath))
        {
            var ids = node.HierarchyPath.Split('/').Select(long.Parse).ToArray();
            var ancestors = await db.Locations
                .Where(l => ids.Contains(l.Id))
                .OrderBy(l => l.Depth)
                .Select(l => l.Name)
                .ToListAsync(ct);
            segments.InsertRange(0, ancestors);
        }
        return string.Join(" / ", segments);
    }
}
