using GameServer.Api.Core;
using GameServer.Application.Core;
using GameServer.Host.Services;
using GameServer.Infrastructure.Caching;
using GameServer.Infrastructure.Messaging;
using GameServer.Infrastructure.Monitoring;
using GameServer.Infrastructure.Persistence;
using GameServer.Modules.Character;
using GameServer.Modules.Combat;
using GameServer.Modules.Economy;
using GameServer.Modules.Fleet;
using GameServer.Modules.Guild;
using GameServer.Modules.Instance;
using GameServer.Modules.Inventory;
using GameServer.Modules.Achievements;
using GameServer.Modules.Crafting;
using GameServer.Modules.Exploration;
using GameServer.Modules.Login;
using GameServer.Modules.Quests;
using GameServer.Modules.Skills;
using GameServer.Modules.Scripting;
using GameServer.Modules.Sovereignty;
using GameServer.Modules.World;
using GameServer.Networking.SignalR;
using GameServer.Networking.SignalR.Consumers;
using GameServer.Networking.Transport;
using GameLayer.Core;
using GameLayer.Combat;
using GameLayer.Inventory;
using GameLayer.Vehicle;
using GameLayer.Quest;
using GameLayer.Building;
using GameLayer.WorldTick;

namespace GameServer.Host;

public class Program
{
    private const float RemoteVisibilityZoneSize = 50000f;

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        // ── Orleans Silo ──────────────────────────────────────────────
        builder.Host.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            silo.AddMemoryGrainStorageAsDefault();
            silo.AddMemoryGrainStorage("GameStore");
        });

        // ── Application Core (Cortex.Mediator + Behaviors) ───────────
        builder.Services.AddApplicationCore();

        // ── Game Modules (auto-scan + register) ──────────────────────
        IGameModule[] modules =
        [
            new LoginModule(),
            new CharacterModule(),
            new WorldModule(),
            new CombatModule(),
            new InventoryModule(),
            new EconomyModule(),
            new FleetModule(),
            new GuildModule(),
            new InstanceModule(),
            new SkillsModule(),
            new QuestsModule(),
            new CraftingModule(),
            new ExplorationModule(),
            new SovereigntyModule(),
            new AchievementsModule(),
            new ScriptingModule()
        ];

        // Sort by priority, register each
        foreach (var module in modules.OrderBy(m => m.Priority))
        {
            module.RegisterServices(builder.Services);
        }

        builder.Services.AddSingleton<IReadOnlyList<IGameModule>>(modules);

        // ── Infrastructure ───────────────────────────────────────────
        var pgConn = configuration.GetConnectionString("GameDb")
                     ?? "Host=<DB_HOST>;Database=<DB_NAME>;Username=<DB_USER>;Password=<DB_PASSWORD>";
        builder.Services.AddPersistence(pgConn);

        var redisConn = configuration.GetConnectionString("Redis") ?? "<REDIS_HOST>:6379";
        builder.Services.AddGameCaching(redisConn);

        var rabbitHost = configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq://<MQ_HOST>";
        builder.Services.AddGameMessaging(rabbitHost, typeof(PlayerEventConsumer));

        builder.Services.AddGameMonitoring();

        // ── Networking ───────────────────────────────────────────────
        builder.Services.AddSignalRNetworking();
        builder.Services.AddTcpTransport();
        builder.Services.AddSingleton<HighFreqMessageRouter>();
        builder.Services.AddSingleton<GameServer.Networking.Core.IRealtimeMessageRouter>(sp => sp.GetRequiredService<HighFreqMessageRouter>());

        // ── GameLayer: Server-Side Game Logic ────────────────────────
        builder.Services.AddSingleton(_ => new ServerWorldState(RemoteVisibilityZoneSize));

        builder.Services.AddSingleton<WeaponRegistry>(sp =>
        {
            var reg = new WeaponRegistry();
            reg.SeedDefaults();
            return reg;
        });
        builder.Services.AddSingleton<CombatSessionManager>();

        builder.Services.AddSingleton<ItemRegistry>(sp =>
        {
            var reg = new ItemRegistry();
            reg.SeedDefaults();
            return reg;
        });
        builder.Services.AddSingleton<InventoryManager>();

        builder.Services.AddSingleton<VehicleManager>(sp =>
            new VehicleManager(
                sp.GetRequiredService<ServerWorldState>().Entities,
                sp.GetRequiredService<WeaponRegistry>()));

        builder.Services.AddSingleton<QuestDefinitionRegistry>(sp =>
        {
            var reg = new QuestDefinitionRegistry();
            reg.SeedDefaults();
            return reg;
        });
        builder.Services.AddSingleton<QuestManager>();

        builder.Services.AddSingleton<BuildingTypeRegistry>(sp =>
        {
            var reg = new BuildingTypeRegistry();
            reg.SeedDefaults();
            return reg;
        });
        builder.Services.AddSingleton<BuildingManager>(sp =>
            new BuildingManager(
                sp.GetRequiredService<ServerWorldState>().Entities,
                sp.GetRequiredService<BuildingTypeRegistry>(),
                sp.GetRequiredService<ServerWorldState>().Zones));
        builder.Services.AddSingleton<IBuildingDamagePersistenceSink, BuildingDamagePersistenceSink>();

        builder.Services.AddHostedService<WorldTickService>();

        // ── Unified API ──────────────────────────────────────────────
        builder.Services.AddScoped<IGameServerApi, GameServerApiService>();

        // ── Health Checks ────────────────────────────────────────────
        builder.Services.AddHealthChecks();

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            await TerrainModificationSchemaBootstrapper.EnsureCompatibleSchemaAsync(dbContext, app.Logger);
        }

        // ── High-Frequency TCP Router (resolves singleton to wire event handlers) ──
        _ = app.Services.GetRequiredService<HighFreqMessageRouter>();

        // ── Start TCP Transport Server ───────────────────────────────
        // TCP 传输使用独立端口（默认 5002），避免与 Kestrel HTTP(5001)/HTTPS(5000) 冲突
        var tcpServer = app.Services.GetRequiredService<TcpTransportServer>();
        var tcpPort = int.TryParse(configuration["TcpTransport:Port"], out var tp) ? tp : 5002;
        await tcpServer.StartAsync(new System.Net.IPEndPoint(System.Net.IPAddress.Any, tcpPort));
        app.Logger.LogInformation("TCP Transport started on port {Port}", tcpPort);

        // ── Module StartAsync ────────────────────────────────────────
        foreach (var module in modules.OrderBy(m => m.Priority))
        {
            await module.StartAsync(app.Services);
            app.Logger.LogInformation("Module '{Module}' started", module.Name);
        }

        // ── Middleware Pipeline ──────────────────────────────────────
        app.UseHealthChecks("/health");
        app.MapHub<GameHub>("/gamehub");

        // ── Minimal API Endpoints ────────────────────────────────────
        app.MapGet("/", () => "MMORPG GameServer is running.");

        app.MapGet("/api/status", () => new
        {
            Status = "Online",
            Modules = modules.Select(m => m.Name).ToArray(),
            Timestamp = DateTime.UtcNow
        });

        // ── Character API ─────────────────────────────────────────────
        app.MapPost("/api/login", async (LoginRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.LoginAsync(new Game.Shared.Core.DTOs.LoginCommandDto(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow, req.Username, req.PasswordHash, "web", "1.0"), ct));

        app.MapPost("/api/character/create", async (CreateCharacterRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CreateCharacterAsync(req.AccountId, req.Name, req.Faction, req.CharacterClass, ct));

        app.MapPost("/api/character/create-full", async (Game.Shared.Core.DTOs.CreateCharacterFullCommandDto req, IGameServerApi api, CancellationToken ct) =>
            await api.CreateCharacterFullAsync(req, ct));

        app.MapGet("/api/character/list/{accountId:guid}", async (Guid accountId, IGameServerApi api, CancellationToken ct) =>
            await api.GetCharacterListAsync(accountId, ct));

        app.MapPost("/api/character/select", async (SelectCharacterRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.SelectCharacterAsync(new Game.Shared.Core.DTOs.SelectCharacterCommandDto(req.AccountId, Guid.NewGuid(), DateTime.UtcNow, req.CharacterId), ct));

        app.MapGet("/api/character/{id:guid}", async (Guid id, IGameServerApi api, CancellationToken ct) =>
            await api.GetCharacterAsync(id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        // ── World API ──────────────────────────────────────────────────
        app.MapPost("/api/world/enter", async (WorldActionRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.EnterWorldAsync(req.PlayerId, req.WorldId!, ct));

        app.MapPost("/api/world/leave", async (WorldLeaveRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.LeaveWorldAsync(req.PlayerId, ct));

        // ── Combat API ─────────────────────────────────────────────────
        app.MapPost("/api/combat/start", async (CombatRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.StartCombatAsync(req.AttackerId, req.DefenderId, ct));

        // ── Economy API ─────────────────────────────────────────────────
        app.MapPost("/api/market/order", async (MarketOrderRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.PlaceMarketOrderAsync(req.SellerId, req.ItemId, req.Quantity, req.PricePerUnit, req.StationId, req.IsBuyOrder, ct));

        // ── Fleet API ───────────────────────────────────────────────────
        app.MapPost("/api/fleet/create", async (FleetCreateRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CreateFleetAsync(req.LeaderId, req.FleetName, ct));

        // ── Guild API ───────────────────────────────────────────────────
        app.MapPost("/api/guild/create", async (GuildCreateRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CreateGuildAsync(new Game.Shared.Core.DTOs.CreateGuildCommandDto(req.FounderId, Guid.NewGuid(), DateTime.UtcNow, req.GuildName, "TAG"), ct));

        // ── Instance API ────────────────────────────────────────────────
        app.MapPost("/api/instance/create", async (InstanceCreateRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CreateInstanceAsync(req.TemplateId, req.LeaderId, ct));

        // ── Skills API ──────────────────────────────────────────────────
        app.MapPost("/api/skills/train", async (SkillTrainRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.StartSkillTrainingAsync(new Game.Shared.Core.DTOs.StartTrainingCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.SkillId), 1), ct));

        app.MapPost("/api/skills/cancel", async (SkillCancelRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CancelSkillTrainingAsync(new Game.Shared.Core.DTOs.CancelTrainingCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Empty), ct));

        // ── Quests API ──────────────────────────────────────────────────
        app.MapPost("/api/quests/accept", async (QuestAcceptRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.AcceptQuestAsync(new Game.Shared.Core.DTOs.AcceptQuestCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.QuestId)), ct));

        app.MapPost("/api/quests/complete", async (QuestCompleteRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CompleteQuestAsync(req.PlayerId, req.QuestId, ct));

        // ── Crafting API ────────────────────────────────────────────────
        app.MapPost("/api/crafting/start", async (CraftingStartRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.StartCraftingAsync(req.PlayerId, req.BlueprintId, req.Quantity, ct));

        // ── Exploration API ─────────────────────────────────────────────
        app.MapPost("/api/exploration/scan", async (ExplorationScanRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.ScanSystemAsync(req.PlayerId, req.SolarSystemId, ct));

        app.MapPost("/api/exploration/harvest", async (ExplorationHarvestRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.HarvestResourceAsync(req.PlayerId, req.ResourceNodeId, req.SolarSystemId, ct));

        // ── Sovereignty API ─────────────────────────────────────────────
        app.MapPost("/api/sovereignty/claim", async (SovereigntyClaimRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.ClaimSovereigntyAsync(new Game.Shared.Core.DTOs.ClaimSovereigntyCommandDto(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.SolarSystemId), req.AllianceId), ct));

        // ── Achievements API ────────────────────────────────────────────
        app.MapGet("/api/achievements/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetAchievementsAsync(playerId, ct));

        // ── Scripting / Narrative API ────────────────────────────────────
        app.MapPost("/api/script/start", async (ScriptStartRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.StartScriptAsync(req.PlayerId, req.ScriptId, ct));

        app.MapPost("/api/script/advance", async (ScriptAdvanceRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.AdvanceScriptAsync(req.PlayerId, req.ScriptId, ct));

        app.MapPost("/api/script/dialogue/choose", async (DialogueChoiceRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.ChooseDialogueOptionAsync(req.PlayerId, req.ScriptId, req.OptionIndex, ct));

        app.MapGet("/api/script/dialogue/{playerId:guid}/{scriptId}", async (Guid playerId, string scriptId, IGameServerApi api, CancellationToken ct) =>
            await api.GetDialogueAsync(playerId, scriptId, ct));

        app.MapPost("/api/script/abort", async (ScriptAbortRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.AbortScriptAsync(req.PlayerId, req.ScriptId, ct));

        app.MapGet("/api/script/active/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetActiveScriptsAsync(playerId, ct));

        app.MapGet("/api/activities", async (IGameServerApi api, CancellationToken ct) =>
            await api.GetActiveActivitiesAsync(ct));

        // ── Skills API (additional) ─────────────────────────────────────
        app.MapGet("/api/skills/tree/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetSkillTreeAsync(playerId, ct));

        app.MapPost("/api/skills/training/start", async (SkillTrainingStartRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.StartSkillTrainingAsync(new Game.Shared.Core.DTOs.StartTrainingCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.SkillId), 1), ct));

        app.MapPost("/api/skills/training/cancel/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.CancelSkillTrainingAsync(new Game.Shared.Core.DTOs.CancelTrainingCommandDto(playerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Empty), ct));

        app.MapPost("/api/player/levelup/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.LevelUpAsync(new Game.Shared.Core.DTOs.LevelUpCommandDto(playerId, Guid.NewGuid(), DateTime.UtcNow, Game.Shared.Core.Enums.AttributeType.Strength), ct));

        app.MapGet("/api/player/attributes/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetAttributesAsync(playerId, ct));

        // ── Inventory API ────────────────────────────────────────────────
        app.MapGet("/api/inventory/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetInventoryAsync(playerId, ct));

        app.MapPost("/api/inventory/move", async (ItemMoveRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.MoveItemAsync(new Game.Shared.Core.DTOs.ItemMoveCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Empty, Guid.Empty, Guid.Parse(req.ItemId), 1, req.ToSlot), ct));

        app.MapPost("/api/inventory/equip", async (EquipItemRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.EquipItemAsync(new Game.Shared.Core.DTOs.EquipItemCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.ItemId), Enum.Parse<Game.Shared.Core.Enums.EquipSlot>(req.Slot)), ct));

        app.MapPost("/api/inventory/drop", async (DropItemRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.DropItemAsync(new Game.Shared.Core.DTOs.DropItemCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.ItemId), req.Quantity), ct));

        // ── Combat API (additional) ──────────────────────────────────────
        app.MapPost("/api/combat/attack", async (AttackRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.AttackAsync(new Game.Shared.Core.DTOs.AttackCommandDto(req.AttackerId, Guid.NewGuid(), DateTime.UtcNow, req.TargetId, req.SkillId is not null ? Guid.Parse(req.SkillId) : null), ct));

        app.MapPost("/api/combat/useskill", async (UseSkillRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.UseSkillAsync(new Game.Shared.Core.DTOs.UseSkillCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.SkillId), req.TargetId.HasValue ? [req.TargetId.Value] : []), ct));

        app.MapGet("/api/combat/status/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetBattleStatusAsync(playerId, ct));

        app.MapPost("/api/combat/fleet/attack", async (FleetAttackRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CommandFleetAttackAsync(new Game.Shared.Core.DTOs.CommandFleetAttackCommandDto(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow, req.FleetId, req.TargetFleetId), ct));

        // ── Navigation API ───────────────────────────────────────────────
        app.MapPost("/api/world/navigate", async (NavigateRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.NavigateToAsync(new Game.Shared.Core.DTOs.NavigationCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.TargetSolarSystemId)), ct));

        app.MapPost("/api/world/scan", async (ScanAreaRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.ScanAreaAsync(new Game.Shared.Core.DTOs.ScanCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Game.Shared.Core.Enums.ScanType.Radar, 100f, new Game.Shared.Core.DTOs.Vector3Dto(0, 0, 0)), ct));

        app.MapPost("/api/world/collect", async (CollectResourceRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CollectResourceAsync(new Game.Shared.Core.DTOs.CollectResourceCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.ResourceNodeId)), ct));

        // ── Economy API (additional) ─────────────────────────────────────
        app.MapPost("/api/market/create", async (MarketOrderRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CreateMarketOrderAsync(new Game.Shared.Core.DTOs.CreateMarketOrderCommandDto(req.SellerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.ItemId), req.Quantity, req.PricePerUnit, req.IsBuyOrder ? Game.Shared.Core.Enums.OrderType.Buy : Game.Shared.Core.Enums.OrderType.Sell), ct));

        app.MapPost("/api/market/buy", async (BuyOrderRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.BuyOrderAsync(new Game.Shared.Core.DTOs.BuyOrderCommandDto(req.BuyerId, Guid.NewGuid(), DateTime.UtcNow, req.OrderId, req.Quantity), ct));

        // ── Fleet API (additional) ───────────────────────────────────────
        app.MapPost("/api/fleet/form", async (FleetCreateRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.FormFleetAsync(new Game.Shared.Core.DTOs.FormFleetCommandDto(req.LeaderId, Guid.NewGuid(), DateTime.UtcNow, [], req.LeaderId, req.FleetName), ct));

        app.MapPost("/api/fleet/command", async (FleetCommandRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.CommandFleetAsync(new Game.Shared.Core.DTOs.CommandFleetCommandDto(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow, req.FleetId, Enum.Parse<Game.Shared.Core.Enums.FleetCommandType>(req.CommandType), new Game.Shared.Core.DTOs.Vector3Dto(0, 0, 0)), ct));

        // ── Sovereignty API (additional) ─────────────────────────────────
        app.MapPost("/api/sovereignty/structure/build", async (StructureBuildRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.BuildStructureAsync(new Game.Shared.Core.DTOs.BuildStructureCommandDto(req.AllianceId, Guid.NewGuid(), DateTime.UtcNow, Enum.Parse<Game.Shared.Core.Enums.StructureType>(req.StructureType), Guid.Parse(req.SolarSystemId), new Game.Shared.Core.DTOs.Vector3Dto(0, 0, 0)), ct));

        // ── Guild API (additional) ───────────────────────────────────────
        app.MapGet("/api/guild/{guildId:guid}/info", async (Guid guildId, IGameServerApi api, CancellationToken ct) =>
            await api.GetGuildInfoAsync(guildId, ct));

        app.MapPost("/api/guild/chat", async (GuildChatRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.SendChatAsync(new Game.Shared.Core.DTOs.SendChatCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Game.Shared.Core.Enums.ChatChannel.Guild, req.Message, req.GuildId), ct));

        // ── Instance API (additional) ────────────────────────────────────
        app.MapPost("/api/instance/enter", async (InstanceEnterRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.EnterInstanceAsync(new Game.Shared.Core.DTOs.EnterInstanceCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, req.InstanceId), ct));

        app.MapGet("/api/instance/{instanceId:guid}/status", async (Guid instanceId, IGameServerApi api, CancellationToken ct) =>
            await api.GetInstanceStatusAsync(instanceId, ct));

        // ── Quest API (additional) ───────────────────────────────────────
        app.MapPost("/api/quests/submit", async (QuestCompleteRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.SubmitQuestAsync(new Game.Shared.Core.DTOs.SubmitQuestCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.QuestId)), ct));

        app.MapGet("/api/quests/progress/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetQuestProgressAsync(playerId, ct));

        // ── Scripting API (additional) ───────────────────────────────────
        app.MapPost("/api/script/narrative/start", async (ScriptStartRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.StartScriptedNarrativeAsync(new Game.Shared.Core.DTOs.StartScriptCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.ScriptId)), ct));

        app.MapPost("/api/script/dialogue/choice", async (DialogueChoiceRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.ChooseDialogueOptionAsync(new Game.Shared.Core.DTOs.ChooseDialogueCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.ScriptId), req.OptionIndex, Guid.Empty), ct));

        app.MapPost("/api/script/activity/trigger", async (TriggerActivityRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.TriggerWorldActivityAsync(new Game.Shared.Core.DTOs.TriggerActivityCommandDto(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.ScriptId)), ct));

        app.MapGet("/api/script/status/{playerId:guid}/{scriptId}", async (Guid playerId, string scriptId, IGameServerApi api, CancellationToken ct) =>
            await api.GetActiveScriptStatusAsync(playerId, scriptId, ct));

        // ── Achievement API (additional) ──────────────────────────────────
        app.MapGet("/api/achievements/progress/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetAchievementsAsync(playerId, ct));

        app.MapPost("/api/cosmetic/unlock", async (UnlockCosmeticRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.UnlockCosmeticAsync(new Game.Shared.Core.DTOs.UnlockCosmeticCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow, Guid.Parse(req.CosmeticId)), ct));

        // ── Mail API ─────────────────────────────────────────────────────
        app.MapPost("/api/mail/send", async (SendMailRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.SendMailAsync(new Game.Shared.Core.DTOs.SendMailCommandDto(req.SenderId, Guid.NewGuid(), DateTime.UtcNow, req.RecipientId, req.Subject, req.Body), ct));

        app.MapGet("/api/mail/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.GetMailAsync(playerId, ct));

        // ── Respawn API ──────────────────────────────────────────────────
        app.MapPost("/api/player/respawn/{playerId:guid}", async (Guid playerId, IGameServerApi api, CancellationToken ct) =>
            await api.RespawnAsync(new Game.Shared.Core.DTOs.RespawnCommandDto(playerId, Guid.NewGuid(), DateTime.UtcNow, Game.Shared.Core.Enums.RespawnType.Nearest), ct));

        // ── Join World API ───────────────────────────────────────────────
        app.MapPost("/api/world/join", async (JoinWorldRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.JoinWorldAsync(new Game.Shared.Core.DTOs.JoinWorldCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow), ct));

        // ── Logout API ───────────────────────────────────────────────────
        app.MapPost("/api/logout", async (LogoutRequest req, IGameServerApi api, CancellationToken ct) =>
            await api.LogoutAsync(new Game.Shared.Core.DTOs.LogoutCommandDto(req.PlayerId, Guid.NewGuid(), DateTime.UtcNow), ct));

        // ── Graceful Shutdown
        var lifetime = app.Lifetime;
        lifetime.ApplicationStopping.Register(() =>
        {
            foreach (var module in modules.OrderByDescending(m => m.Priority))
                module.StopAsync(default).GetAwaiter().GetResult();
        });

        await app.RunAsync();
    }
}

// ── Minimal API Request Models ──────────────────────────────────────
public sealed record LoginRequest(string Username, string PasswordHash);
public sealed record CreateCharacterRequest(Guid AccountId, string Name, string Faction, string CharacterClass);
public sealed record SelectCharacterRequest(Guid AccountId, Guid CharacterId);
public sealed record WorldActionRequest(Guid PlayerId, string? WorldId);
public sealed record WorldLeaveRequest(Guid PlayerId);
public sealed record CombatRequest(Guid AttackerId, Guid DefenderId);
public sealed record MarketOrderRequest(Guid SellerId, string ItemId, int Quantity, decimal PricePerUnit, string StationId, bool IsBuyOrder);
public sealed record FleetCreateRequest(Guid LeaderId, string FleetName);
public sealed record GuildCreateRequest(Guid FounderId, string GuildName);
public sealed record InstanceCreateRequest(string TemplateId, Guid LeaderId);
public sealed record SkillTrainRequest(Guid PlayerId, string SkillId);
public sealed record SkillCancelRequest(Guid PlayerId);
public sealed record QuestAcceptRequest(Guid PlayerId, string QuestId);
public sealed record QuestCompleteRequest(Guid PlayerId, string QuestId);
public sealed record CraftingStartRequest(Guid PlayerId, string BlueprintId, int Quantity);
public sealed record ExplorationScanRequest(Guid PlayerId, string SolarSystemId);
public sealed record ExplorationHarvestRequest(Guid PlayerId, string ResourceNodeId, string SolarSystemId);
public sealed record SovereigntyClaimRequest(Guid AllianceId, string SolarSystemId);
public sealed record ScriptStartRequest(Guid PlayerId, string ScriptId);
public sealed record ScriptAdvanceRequest(Guid PlayerId, string ScriptId);
public sealed record DialogueChoiceRequest(Guid PlayerId, string ScriptId, int OptionIndex);
public sealed record ScriptAbortRequest(Guid PlayerId, string ScriptId);

// ── New Request Models ──────────────────────────────────────────────
public sealed record SkillTrainingStartRequest(Guid PlayerId, string SkillId);
public sealed record ItemMoveRequest(Guid PlayerId, string ItemId, int FromSlot, int ToSlot);
public sealed record EquipItemRequest(Guid PlayerId, string ItemId, string Slot);
public sealed record DropItemRequest(Guid PlayerId, string ItemId, int Quantity);
public sealed record AttackRequest(Guid AttackerId, Guid TargetId, string? SkillId);
public sealed record UseSkillRequest(Guid PlayerId, string SkillId, Guid? TargetId);
public sealed record FleetAttackRequest(Guid FleetId, Guid TargetFleetId);
public sealed record NavigateRequest(Guid PlayerId, string TargetSolarSystemId, double X, double Y, double Z);
public sealed record ScanAreaRequest(Guid PlayerId, string SolarSystemId);
public sealed record CollectResourceRequest(Guid PlayerId, string ResourceNodeId, string SolarSystemId);
public sealed record BuyOrderRequest(Guid BuyerId, Guid OrderId, int Quantity);
public sealed record FleetCommandRequest(Guid FleetId, string CommandType, string? TargetId);
public sealed record StructureBuildRequest(Guid AllianceId, string SolarSystemId, string StructureType);
public sealed record GuildChatRequest(Guid PlayerId, Guid GuildId, string Message);
public sealed record InstanceEnterRequest(Guid PlayerId, Guid InstanceId);
public sealed record TriggerActivityRequest(string ScriptId, DateTime StartsAt, DateTime EndsAt, string? TargetZone);
public sealed record UnlockCosmeticRequest(Guid PlayerId, string CosmeticId);
public sealed record SendMailRequest(Guid SenderId, Guid RecipientId, string Subject, string Body);
public sealed record JoinWorldRequest(Guid PlayerId, string WorldId);
public sealed record LogoutRequest(Guid PlayerId);
