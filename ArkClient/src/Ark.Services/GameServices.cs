using Ark.Abstractions;
using Ark.Networking;
using Ark.Services.Remote;
using Ark.Shared.Data;
using Friflo.Engine.ECS;
using Game.Shared.Core.DTOs;

namespace Ark.Services;

/// <summary>
/// 服务定位器 — 提供所有游戏服务的统一访问点。
/// 运行时仅支持 Network 模式；本地实现代码仅保留为非激活备用代码。
/// </summary>
public static class GameServices
{
    private const string DefaultServerAddress = "<SERVER_HOST>";
    private const int DefaultServerPort = 5000;
    private const float DefaultNearbyQueryRadius = 5120f;

    private static IGameWorld?          _gameWorld;
    private static INetworkService?     _network;
    private static ICombatService?      _combat;
    private static IBaseBuildingService? _baseBuilding;
    private static ISpaceService?       _space;
    private static ITerrainQuery?       _terrain;

    // ── 扩展远程服务 ──
    private static RemoteCharacterService? _remoteCharacter;
    private static RemoteEconomyService?   _remoteEconomy;
    private static RemoteFleetService?     _remoteFleet;
    private static RemoteGuildService?     _remoteGuild;
    private static RemoteScriptService?    _remoteScript;
    private static RemoteInventoryService? _remoteInventoryService;
    private static RemoteQuestService?     _remoteQuestService;
    private static RemoteWorldEcsCacheSystem? _remoteWorldEcsCache;
    private static Guid _remoteAccountId;
    private static Guid _remotePlayerId;
    private static string? _remoteSessionToken;

    private static bool _isNetworkMode;
    private static bool _isNetworkEnabled;
    private static NetworkManager? _networkManager;
    private static NetworkServiceBridge? _bridge;
    private static SnapshotApplier? _snapshotApplier;
    private static ServerEventDispatcher? _eventDispatcher;
    private static Remote.ServerEventEcsProjectionBuffer? _serverEventEcsProjection;

    public static event Action<string>? WorldPreparationStatusChanged;

    // ═══ 服务访问器 ═══

    public static IGameWorld World => _gameWorld 
        ?? throw new System.InvalidOperationException("GameWorld not initialized");

    public static INetworkService Network => _network 
        ?? throw new System.InvalidOperationException("NetworkService not initialized");

    public static ICombatService Combat => _combat 
        ?? throw new System.InvalidOperationException("CombatService not initialized");

    public static IBaseBuildingService BaseBuilding => _baseBuilding 
        ?? throw new System.InvalidOperationException("BaseBuildingService not initialized");

    public static ISpaceService Space => _space 
        ?? throw new System.InvalidOperationException("SpaceService not initialized");

    /// <summary>地形查询（可为 null — 非所有模式都有地形）。</summary>
    public static ITerrainQuery? Terrain => _terrain;

    /// <summary>世界初始化器 — 用于从服务端种子重建地形（可为 null）。</summary>
    public static IWorldInitializer? WorldInitializer { get; private set; }

    /// <summary>最近一次 JoinWorld 返回的出生坐标 X。</summary>
    public static float LastSpawnX { get; private set; }
    /// <summary>最近一次 JoinWorld 返回的出生坐标 Y。</summary>
    public static float LastSpawnY { get; private set; }
    /// <summary>最近一次 JoinWorld 返回的出生坐标 Z。</summary>
    public static float LastSpawnZ { get; private set; }
    /// <summary>最近一次 JoinWorld 返回的地形种子。</summary>
    public static long LastTerrainSeed { get; private set; }

    /// <summary>远程角色服务（角色列表/创建/选择，仅网络模式可用）。</summary>
    public static RemoteCharacterService? Character => _remoteCharacter;

    /// <summary>远程经济服务（市场/制造，仅网络模式可用）。</summary>
    public static RemoteEconomyService? Economy => _remoteEconomy;

    /// <summary>远程舰队服务（舰队/主权，仅网络模式可用）。</summary>
    public static RemoteFleetService? Fleet => _remoteFleet;

    /// <summary>远程公会/社交服务（仅网络模式可用）。</summary>
    public static RemoteGuildService? Guild => _remoteGuild;

    /// <summary>远程脚本/叙事服务（仅网络模式可用）。</summary>
    public static RemoteScriptService? Script => _remoteScript;

    public static bool IsNetworkMode => _isNetworkMode;

    /// <summary>网络层是否已启用。</summary>
    public static bool IsNetworkEnabled => _isNetworkEnabled;
    public static Guid RemoteAccountId => _remoteAccountId;
    public static Guid RemotePlayerId => _remotePlayerId;
    public static string? RemoteSessionToken => _remoteSessionToken;

    /// <summary>
    /// 网络管理器 — 提供 SignalR / TCP 底层访问。
    /// 在 Network 模式下自动创建。
    /// </summary>
    public static NetworkManager? NetworkManager => _networkManager;

    /// <summary>
    /// 服务端权威桥接器 — 在网络模式下拦截本地游戏动作路由到服务端。
    /// </summary>
    public static Remote.ServerAuthorityBridge? ServerBridge { get; set; }
    public static Remote.ServerEventEcsProjectionBuffer? ServerEventEcsProjection => _serverEventEcsProjection;

    public static void BindServerAuthorityBridge(Remote.ServerAuthorityBridge bridge)
    {
        ServerBridge = bridge;
        ServerBridge.PlayerId = _remotePlayerId;
    }

    public static void BindServerEventEcsProjection(Remote.ServerEventEcsProjectionBuffer projection)
    {
        _serverEventEcsProjection = projection;
        _eventDispatcher?.BindEcsProjection(projection);
    }

    // ═══ 初始化 ═══

    /// <summary>
    /// 初始化为纯 Network 模式（无本地服务）。
    /// 所有游戏状态来自服务端，客户端仅负责渲染/输入。
    /// </summary>
    public static void InitializeNetworkMode(string serverAddress, int port)
    {
        _isNetworkMode = true;

        // ── 网络层 ──
        _networkManager = new NetworkManager();
        _bridge = new NetworkServiceBridge(_networkManager);
        _network = _bridge;
        _isNetworkEnabled = true;

        // ── 远程世界 ──
        var remoteWorld = new RemoteGameWorld(_networkManager);
        _gameWorld = remoteWorld;

        // ── 快照解码 → RemoteGameWorld ──
        _snapshotApplier = new SnapshotApplier(remoteWorld);
        _networkManager.OnRawTcpMessage += data =>
        {
            _snapshotApplier.TryApply(data);
            return System.Threading.Tasks.Task.CompletedTask;
        };

        // ── 远程业务服务（初始 PlayerId = Empty，登录后更新）──
        var playerId = System.Guid.Empty;
        var remoteInventory = new RemoteInventoryService(_networkManager, playerId);
        var remoteQuest = new RemoteQuestService(_networkManager, playerId);

        _remoteInventoryService = remoteInventory;
        _remoteQuestService = remoteQuest;

        // ── 扩展远程服务 ──
        _remoteCharacter = new RemoteCharacterService(_networkManager, playerId);
        _remoteEconomy = new RemoteEconomyService(_networkManager, playerId);
        _remoteFleet = new RemoteFleetService(_networkManager, playerId);
        _remoteGuild = new RemoteGuildService(_networkManager, playerId);
        _remoteScript = new RemoteScriptService(_networkManager, playerId);

        // ── 服务端事件分发器 ──
        _eventDispatcher = new ServerEventDispatcher();
        _eventDispatcher.Bind(remoteWorld, remoteInventory, remoteQuest);
        _eventDispatcher.BindExtended(
            _remoteCharacter, _remoteEconomy, _remoteFleet, _remoteGuild, _remoteScript);
        _eventDispatcher.BindSnapshotApplier(_snapshotApplier);
        _networkManager.SetEventHandler(_eventDispatcher);
        _remoteAccountId = Guid.Empty;
        _remotePlayerId = Guid.Empty;

        ServiceLog.Info($"[GameServices] Initialized in NETWORK mode ({serverAddress}:{port})");
    }

    public static void SetRemoteAccountId(System.Guid accountId)
    {
        _remoteAccountId = accountId;
        _remoteCharacter?.SetAccountId(accountId);
        ServiceLog.Info($"[GameServices] Remote AccountId set: {accountId}");
    }

    /// <summary>
    /// 设置当前激活的远程玩家身份（登录后可为账号，占用角色后切为角色）。
    /// </summary>
    public static void SetRemotePlayerId(System.Guid playerId)
    {
        _remotePlayerId = playerId;

        if (_bridge is not null)
            _bridge.PlayerId = playerId;

        if (ServerBridge is not null)
            ServerBridge.PlayerId = playerId;

        if (_gameWorld is RemoteGameWorld remoteWorld)
            remoteWorld.SetLocalPlayer(playerId);

        _remoteInventoryService?.SetPlayerId(playerId);
        _remoteQuestService?.SetPlayerId(playerId);
        ServiceLog.Info($"[GameServices] Remote PlayerId set: {playerId}");
    }

    /// <summary>服务端事件分发器（供系统消息等非玩法 UI 订阅）。</summary>
    public static ServerEventDispatcher? EventDispatcher => _eventDispatcher;
    public static RemoteWorldEcsCacheSystem? RemoteWorldEcsCache => _remoteWorldEcsCache;

    /// <summary>
    /// 清理所有服务。
    /// </summary>
    public static void Shutdown()
    {
        (_network as System.IDisposable)?.Dispose();
        if (_networkManager is not null)
        {
            _ = _networkManager.DisposeAsync();
            _networkManager = null;
        }
        _isNetworkMode = false;
        _isNetworkEnabled = false;
        _bridge = null;
        _snapshotApplier = null;
        _eventDispatcher = null;
        _serverEventEcsProjection = null;
        _gameWorld = null;
        _network   = null;
        _combat    = null;
        _baseBuilding = null;
        _space     = null;
        _terrain   = null;
        _remoteCharacter = null;
        _remoteEconomy = null;
        _remoteFleet = null;
        _remoteGuild = null;
        _remoteScript = null;
        _remoteInventoryService = null;
        _remoteQuestService = null;
        _remoteWorldEcsCache = null;
        _remoteAccountId = Guid.Empty;
        _remotePlayerId = Guid.Empty;

        ServiceLog.Info("[GameServices] Shutdown");
    }

    // ═══ 注册自定义服务（用于测试/Mock）═══

    public static void RegisterCombat(ICombatService service) => _combat = service;
    public static void RegisterBaseBuilding(IBaseBuildingService service) => _baseBuilding = service;
    public static void RegisterSpace(ISpaceService service) => _space = service;
    public static void RegisterTerrain(ITerrainQuery service) => _terrain = service;
    public static void RegisterRemoteWorldEcsCache(RemoteWorldEcsCacheSystem? cache) => _remoteWorldEcsCache = cache;

    /// <summary>注册世界初始化器（由 GameBootstrap 调用，与 RegisterTerrain 一起使用）。</summary>
    public static void RegisterWorldInitializer(IWorldInitializer initializer) => WorldInitializer = initializer;

    // ═══ 登录/进入世界前预加载流程 ═══

    /// <summary>
    /// 完整的网络模式登录流程：
    /// 1. 连接服务端 → 2. 登录 → 3. 设置PlayerId → 4. 拉取角色列表
    /// </summary>
    public static async System.Threading.Tasks.Task<LoginResultDto?> LoginRemoteAsync(
        string serverAddress, int port,
        string accountId, string passwordHash,
        string deviceId, string clientVersion)
    {
        if (_networkManager is null || _bridge is null)
        {
            ServiceLog.Error("[GameServices] Not in network mode");
            return null;
        }

        // 1. 建立连接
        if (_networkManager.ConnectionState != Networking.NetworkConnectionState.Connected)
        {
            var connected = await _network!.ConnectAsync(serverAddress, port);
            if (!connected)
            {
                ServiceLog.Error("[GameServices] Connection failed");
                return null;
            }
        }

        // 2. 登录
        var loginCmd = new LoginCommandDto(
            System.Guid.Empty, System.Guid.NewGuid(), System.DateTime.UtcNow,
            accountId, passwordHash, deviceId, clientVersion);
        var loginResult = await _networkManager.SignalR.LoginAsync(loginCmd, System.Threading.CancellationToken.None);

        if (loginResult.Success && loginResult.Player is not null)
        {
            _remoteSessionToken = loginResult.Token;
            await _networkManager.SignalR.AuthenticateAsync(loginResult.Player.Id, System.Threading.CancellationToken.None);
            SetRemoteAccountId(loginResult.Player.Id);
            SetRemotePlayerId(loginResult.Player.Id);

            // 4. 拉取角色列表
            if (_remoteCharacter is not null)
                await _remoteCharacter.FetchCharacterListAsync();

            ServiceLog.Info($"[GameServices] Login successful: {loginResult.Player.Name}");
        }
        else
        {
            _remoteSessionToken = null;
            ServiceLog.Error($"[GameServices] Login failed: {loginResult.ErrorMessage}");
        }

        return loginResult;
    }

    /// <summary>
    /// 选择角色后的世界准备流程：
    /// 1. 认证角色 → 2. 获取世界环境（地形种子、天气等）→ 3. 加入世界 → 4. 同步队伍/周围实体 → 5. 拉取库存/任务/队伍
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> PrepareWorldAsync(
        System.Guid characterId, string worldId = "default")
    {
        if (_networkManager is null)
        {
            ServiceLog.Error("[GameServices] Not in network mode");
            PublishWorldPreparationStatus("进入世界失败: 网络未初始化");
            return false;
        }

        try
        {
            if (_gameWorld is not RemoteGameWorld remoteWorld)
            {
                ServiceLog.Error("[GameServices] RemoteGameWorld unavailable");
                PublishWorldPreparationStatus("进入世界失败: 远程世界对象不可用");
                return false;
            }

            _eventDispatcher?.ResetWorldEntryState();
            remoteWorld.BeginWorldEntry(characterId);

            // ── 1. 认证角色 ──
            PublishWorldPreparationStatus("正在认证角色...");
            try
            {
                SetRemotePlayerId(characterId);
                await _networkManager.SignalR.AuthenticateAsync(characterId, System.Threading.CancellationToken.None);
            }
            catch (System.Exception ex)
            {
                ServiceLog.Error($"[GameServices] SignalR auth failed: {ex.Message}");
                PublishWorldPreparationStatus($"进入世界失败: 角色认证失败 - {ex.Message}");
                return false;
            }

            // ── 2. 预加载世界环境（地形种子、天气等） ──
            PublishWorldPreparationStatus("正在生成环境 / 地形...");
            try
            {
                var env = await _networkManager.SignalR.GetWorldEnvironmentAsync(worldId, System.Threading.CancellationToken.None);
                ServiceLog.Info($"[GameServices] World env loaded: seed={env.TerrainSeed}, biome={env.BiomeId}");

                if (_eventDispatcher is not null)
                    await _eventDispatcher.OnWorldEnvironmentReceived(env);

                // ── 应用服务端地形种子到 WorldEnvironmentManager ──
                LastTerrainSeed = env.TerrainSeed;
                if (WorldInitializer is not null)
                {
                    WorldInitializer.ReinitializeWithSeed(env.TerrainSeed);
                    WorldInitializer.ApplyServerTimeOfDay(env.TimeOfDay);
                    ServiceLog.Info($"[GameServices] World reinitialized with server seed={env.TerrainSeed}, time={env.TimeOfDay}");

                    if (!WorldInitializer.IsInitialized)
                    {
                        ServiceLog.Error("[GameServices] WorldInitializer failed to initialize from server seed");
                        PublishWorldPreparationStatus("进入世界失败: 服务端地形未初始化");
                        return false;
                    }
                }
                else
                {
                    ServiceLog.Error("[GameServices] WorldInitializer not registered");
                    PublishWorldPreparationStatus("进入世界失败: GameBootstrap 未注册 WorldInitializer");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                ServiceLog.Error($"[GameServices] GetWorldEnvironment failed: {ex.Message}");
                PublishWorldPreparationStatus($"进入世界失败: 获取世界环境失败 - {ex.Message}");
                return false;
            }

            // ── 3. 加入世界 ──
            PublishWorldPreparationStatus("正在加入世界实例...");
            try
            {
                var joinCmd = new JoinWorldCommandDto(
                    characterId, System.Guid.NewGuid(), System.DateTime.UtcNow);
                var joinResult = await _networkManager.SignalR.JoinWorldAsync(joinCmd, System.Threading.CancellationToken.None);

                if (!joinResult.Success)
                {
                    ServiceLog.Error($"[GameServices] JoinWorld failed: {joinResult.ErrorMessage}");
                    PublishWorldPreparationStatus($"进入世界失败: 加入世界被拒绝 - {joinResult.ErrorMessage}");
                    return false;
                }

                // 保存服务端出生坐标，供 GameBootstrap.PlacePlayerOnTerrain 使用
                LastSpawnX = joinResult.SpawnX;
                LastSpawnY = joinResult.SpawnY;
                LastSpawnZ = joinResult.SpawnZ;

                // 如果 JoinWorld 返回了不同的地形种子，优先使用
                if (joinResult.TerrainSeed != 0 && joinResult.TerrainSeed != LastTerrainSeed)
                {
                    LastTerrainSeed = joinResult.TerrainSeed;
                    if (WorldInitializer is null)
                    {
                        PublishWorldPreparationStatus("进入世界失败: WorldInitializer 丢失");
                        return false;
                    }

                    WorldInitializer.ReinitializeWithSeed(joinResult.TerrainSeed);
                    ServiceLog.Info($"[GameServices] Terrain seed updated from JoinWorld: {joinResult.TerrainSeed}");
                }

                ServiceLog.Info($"[GameServices] Joined world: {joinResult.WorldId}, {joinResult.OnlinePlayerCount} online, spawn=({joinResult.SpawnX:F1},{joinResult.SpawnY:F1},{joinResult.SpawnZ:F1})");
            }
            catch (System.Exception ex)
            {
                ServiceLog.Error($"[GameServices] JoinWorld exception: {ex.Message}");
                PublishWorldPreparationStatus($"进入世界失败: 加入世界异常 - {ex.Message}");
                return false;
            }

            // ── 3b. 高频同步通道（当前固定使用 SignalR）──
            PublishWorldPreparationStatus("正在切换到 SignalR 高频同步...");
            var tcpConnected = await _networkManager.ConnectTcpAsync(
                characterId, System.Threading.CancellationToken.None);
            ServiceLog.Info($"[GameServices] High-frequency channel: {(tcpConnected ? "TCP" : "SignalR-only")}");

            // ── 4. 同步队伍信息 ──
            PublishWorldPreparationStatus("正在同步队伍信息...");
            try
            {
                var partyInfo = await _networkManager.SignalR.GetPartyInfoAsync(characterId, System.Threading.CancellationToken.None);
                await _eventDispatcher!.OnPartyUpdated(partyInfo);
            }
            catch (System.Exception ex)
            {
                ServiceLog.Error($"[GameServices] GetPartyInfo failed: {ex.Message}");
                PublishWorldPreparationStatus($"进入世界失败: 同步队伍失败 - {ex.Message}");
                return false;
            }

            // ── 5. 同步周围实体 ──
            PublishWorldPreparationStatus("正在同步周围实体...");
            try
            {
                var nearby = await _networkManager.SignalR.GetNearbyEntitiesAsync(
                    characterId,
                    DefaultNearbyQueryRadius,
                    System.Threading.CancellationToken.None);
                await _eventDispatcher!.OnNearbyEntitiesUpdated(nearby);
            }
            catch (System.Exception ex)
            {
                ServiceLog.Error($"[GameServices] GetNearbyEntities failed: {ex.Message}");
                PublishWorldPreparationStatus($"进入世界失败: 同步周围实体失败 - {ex.Message}");
                return false;
            }

            // ── 6. 拉取其他初始数据 ──
            _remoteInventoryService?.RefreshFromServer();

            _remoteQuestService?.RefreshFromServer();

            _remoteEconomy?.FetchCraftingQueue();
            _remoteFleet?.FetchSovereigntyMap();
            _remoteGuild?.FetchMail();

            // ── 7. 等待首帧世界快照 ──
            PublishWorldPreparationStatus("正在等待首帧世界快照...");
            var snapshotReady = await WaitForRemoteWorldReadyAsync(remoteWorld, System.TimeSpan.FromSeconds(10));
            if (!snapshotReady)
            {
                ServiceLog.Error("[GameServices] Timed out waiting for initial world snapshot");
                PublishWorldPreparationStatus("进入世界失败: 首帧世界快照超时");
                return false;
            }

            if (WorldInitializer is null || !WorldInitializer.IsInitialized)
            {
                ServiceLog.Error("[GameServices] Blocking entry: world is still not initialized from server seed");
                PublishWorldPreparationStatus("进入世界失败: 世界仍未按服务端种子初始化");
                return false;
            }

            PublishWorldPreparationStatus("世界已就绪，正在进入...");

            return true;
        }
        catch (System.Exception ex)
        {
            ServiceLog.Error($"[GameServices] PrepareWorld failed: {ex.Message}");
            PublishWorldPreparationStatus($"进入世界失败: {ex.Message}");
            return false;
        }
    }

    private static void PublishWorldPreparationStatus(string status)
    {
        ServiceLog.Info($"[GameServices] {status}");
        WorldPreparationStatusChanged?.Invoke(status);
    }

    private static async System.Threading.Tasks.Task<bool> WaitForRemoteWorldReadyAsync(
        RemoteGameWorld remoteWorld,
        System.TimeSpan timeout)
    {
        var deadline = System.DateTime.UtcNow + timeout;
        while (System.DateTime.UtcNow < deadline)
        {
            if (remoteWorld.IsLoaded && remoteWorld.LocalPlayerId != 0)
                return true;

            await System.Threading.Tasks.Task.Delay(50);
        }

        return remoteWorld.IsLoaded && remoteWorld.LocalPlayerId != 0;
    }
}
