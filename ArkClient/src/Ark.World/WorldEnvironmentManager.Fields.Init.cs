using Godot;
using Ark.Abstractions;
using Ark.Events;
using Ark.World.Core;
using Ark.World.Data;
using Ark.World.Terrain;
using Ark.World.Environment;

namespace Ark.World;

public sealed partial class WorldEnvironmentManager
{
    private WorldSeed _seed;
    private EventBus? _eventBus;

    private WorldTimeState _timeState = new();
    private WeatherState _weatherState = new();
    private ModificationLog _modLog = new();
    private BiomeSampler? _biomeSampler;

    private HeightfieldGenerator? _heightGen;
    private ChunkManager? _chunkManager;
    private FarTerrainPlane? _farTerrain;
    private SkyDome? _skyDome;
    private PlanetRenderer? _planet;
    private DayNightCycle? _dayNight;
    private WeatherSystem? _weather;
    private AtmosphereController? _atmosphere;
    private PrecipitationRenderer? _precipitation;
    private VegetationSpawner? _vegetation;
    private EnvironmentSceneDecorator? _decorator;

    private bool _initialized;
    private bool _serverSeedProvided; // 服务端种子是否已到达
    private BiomeId _playerBiome;
    private bool _paused;
    private EnvironmentPreset _currentPreset = EnvironmentPreset.Natural;

    public EnvironmentPreset CurrentPreset => _currentPreset;
    public WorldSeed Seed => _seed;
    public WorldTimeState TimeState => _timeState;
    public WeatherState WeatherState => _weatherState;
    public ChunkManager? Terrain => _chunkManager;
    public int LoadedChunkCount => _chunkManager?.LoadedChunkCount ?? 0;
    public WeatherSystem? Weather => _weather;
    public AtmosphereController? Atmosphere => _atmosphere;
    public DayNightCycle? DayNight => _dayNight;
    public bool IsInitialized => _initialized;

    /// <summary>
    /// ⚠️ 网络模式下禁止直接调用！
    /// 必须通过 <see cref="ReinitializeWithSeed(long)"/> 使用服务端种子初始化。
    /// 如果 GameBootstrap 仍用 WorldSeed(42) 调用此方法，会抛出异常暴露调用点。
    /// </summary>
    public void InitializeWorld(WorldSeed seed, EventBus? eventBus = null)
    {
        // ── 激进诊断：拦截硬编码种子 ──
        if (Ark.Services.GameServices.IsNetworkMode && !_serverSeedProvided)
        {
            GD.PrintErr($"[WorldEnvManager] ❌ BLOCKED: InitializeWorld(seed={seed.Value}) called in NETWORK mode before server seed arrived!");
            GD.PrintErr("[WorldEnvManager] ❌ This means GameBootstrap is still using local WorldSeed(42).");
            GD.PrintErr("[WorldEnvManager] ❌ The world will NOT initialize until ReinitializeWithSeed is called with server data.");
            // 保存 eventBus 以便后续 ReinitializeWithSeed 使用
            _eventBus = eventBus;
            return;
        }

        if (_initialized)
        {
            GD.Print($"[WorldEnvManager] Already initialized with seed={_seed.Value}, ignoring seed={seed.Value}");
            return;
        }

        _seed = seed;
        _eventBus = eventBus;

        GD.Print($"[WorldEnvManager] Initializing with {seed}");

        BiomeRegistry.RegisterDefaults();
        _biomeSampler = new BiomeSampler(seed);

        _heightGen = new HeightfieldGenerator(seed, _biomeSampler);
        _chunkManager = new ChunkManager(_heightGen, _modLog);
        _chunkManager.Initialize(seed);
        AddChild(_chunkManager.SceneRoot);
        GD.Print("[WorldEnvManager] Terrain: ChunkManager initialized");

        _farTerrain = new FarTerrainPlane(_heightGen, _biomeSampler);
        AddChild(_farTerrain.SceneRoot);

        _skyDome = new SkyDome();
        _skyDome.Initialize();
        AddChild(_skyDome.SceneRoot);

        _planet = new PlanetRenderer();
        _planet.Initialize();
        AddChild(_planet.SceneRoot);

        _dayNight = new DayNightCycle(_timeState);
        _dayNight.Initialize(seed);
        _dayNight.OnPeriodChanged += (prev, next) =>
        {
            _eventBus?.Publish(new TimeOfDayChangedEvent(prev, next));
            GD.Print($"[WorldEnvManager] Time: {prev} → {next}");
        };
        _dayNight.OnSeasonChanged += (prev, next) =>
        {
            _eventBus?.Publish(new SeasonChangedEvent(prev, next));
            GD.Print($"[WorldEnvManager] Season: {prev} → {next}");
        };

        _weather = new WeatherSystem(_weatherState, _timeState);
        _weather.Initialize(seed);
        _weather.OnWeatherChanged += (prev, next) =>
        {
            _eventBus?.Publish(new WeatherChangedEvent(prev, next));
            GD.Print($"[WorldEnvManager] Weather: {prev} → {next}");
        };

        _atmosphere = new AtmosphereController(_dayNight, _weather);
        _atmosphere.Initialize(seed);
        AddChild(_atmosphere.SceneRoot);

        _precipitation = new PrecipitationRenderer(_weatherState);
        _precipitation.Initialize(seed);
        AddChild(_precipitation.SceneRoot);

        _vegetation = new VegetationSpawner();
        _vegetation.Initialize(seed);
        AddChild(_vegetation.SceneRoot);

        _chunkManager.OnChunkLoaded += chunk => _vegetation?.SpawnForChunk(chunk);
        _chunkManager.OnChunkUnloaded += coord => _vegetation?.UnloadForChunk(coord);

        _decorator = new EnvironmentSceneDecorator();
        _decorator.Initialize(seed);
        AddChild(_decorator.SceneRoot);

        _eventBus?.Subscribe<ExplosionEvent>(OnExplosion);

        _initialized = true;
        GD.Print("[WorldEnvManager] World initialized");
    }
}
