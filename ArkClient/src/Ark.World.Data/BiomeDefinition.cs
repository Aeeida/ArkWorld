using Ark.World.Core;

namespace Ark.World.Data;

/// <summary>
/// 生物群系定义 — 描述一种群系的地形参数、植被密度、天气权重等。
/// 设计为不可变数据，注册后全局共享。
/// </summary>
public sealed record BiomeDefinition
{
    /// <summary>群系标识。</summary>
    public required BiomeId Id { get; init; }

    /// <summary>显示名称。</summary>
    public required string Name { get; init; }

    // ═══ 地形参数 ═══
    /// <summary>基础高度偏移（米）。</summary>
    public float BaseHeight { get; init; }
    /// <summary>高度振幅倍数。</summary>
    public float HeightAmplitude { get; init; } = 1f;
    /// <summary>噪声频率倍数。</summary>
    public float FrequencyScale { get; init; } = 1f;
    /// <summary>FBM 叠加层数。</summary>
    public int Octaves { get; init; } = 6;
    /// <summary>是否使用脊化噪声（山脉风格）。</summary>
    public bool UseRidgedNoise { get; init; }

    // ═══ 表面 ═══
    /// <summary>主地表颜色。</summary>
    public Godot.Color SurfaceColor { get; init; } = new(0.35f, 0.45f, 0.3f);
    /// <summary>陡坡颜色（岩石/泥土）。</summary>
    public Godot.Color SlopeColor { get; init; } = new(0.5f, 0.45f, 0.35f);
    /// <summary>坡度阈值（度数），超过此角度使用 SlopeColor。</summary>
    public float SlopeThresholdDeg { get; init; } = 35f;

    // ═══ 生态 ═══
    /// <summary>植被密度 [0,1]。</summary>
    public float VegetationDensity { get; init; }
    /// <summary>允许的植被类型 ID 列表。</summary>
    public string[] VegetationTypes { get; init; } = [];

    // ═══ 天气权重 ═══
    /// <summary>各天气类型的权重（未列出的=0）。</summary>
    public Dictionary<WeatherType, float> WeatherWeights { get; init; } = new();

    // ═══ 大气 ═══
    /// <summary>雾密度倍数。</summary>
    public float FogDensityMultiplier { get; init; } = 1f;
    /// <summary>环境光颜色调制。</summary>
    public Godot.Color AmbientTint { get; init; } = Godot.Colors.White;
}

/// <summary>
/// 群系注册表 — 全局群系定义仓库，种子无关。
/// </summary>
public static class BiomeRegistry
{
    private static readonly Dictionary<BiomeId, BiomeDefinition> _biomes = new();

    /// <summary>注册群系定义。</summary>
    public static void Register(BiomeDefinition biome)
    {
        _biomes[biome.Id] = biome;
    }

    /// <summary>获取群系定义。</summary>
    public static BiomeDefinition? Get(BiomeId id)
        => _biomes.GetValueOrDefault(id);

    /// <summary>所有已注册群系。</summary>
    public static IReadOnlyCollection<BiomeDefinition> All => _biomes.Values;

    /// <summary>注册默认群系集合。</summary>
    public static void RegisterDefaults()
    {
        Register(new BiomeDefinition
        {
            Id = BiomeId.Plains, Name = "草原",
            BaseHeight = 2f, HeightAmplitude = 0.1f, FrequencyScale = 0.8f, Octaves = 4,
            SurfaceColor = new Godot.Color(0.3f, 0.55f, 0.2f),
            VegetationDensity = 0.4f,
            VegetationTypes = ["grass", "flower", "bush"],
            WeatherWeights = new() { [WeatherType.Clear] = 0.5f, [WeatherType.Cloudy] = 0.3f, [WeatherType.LightRain] = 0.2f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Forest, Name = "森林",
            BaseHeight = 3f, HeightAmplitude = 0.15f, FrequencyScale = 1.0f, Octaves = 5,
            SurfaceColor = new Godot.Color(0.2f, 0.4f, 0.15f),
            SlopeColor = new Godot.Color(0.35f, 0.3f, 0.2f),
            VegetationDensity = 0.85f,
            VegetationTypes = ["tree_oak", "tree_pine", "bush", "fern", "mushroom"],
            FogDensityMultiplier = 2f,
            WeatherWeights = new() { [WeatherType.Cloudy] = 0.3f, [WeatherType.LightRain] = 0.3f, [WeatherType.Fog] = 0.3f, [WeatherType.HeavyRain] = 0.1f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Mountain, Name = "山脉",
            BaseHeight = 15f, HeightAmplitude = 0.5f, FrequencyScale = 0.6f, Octaves = 6,
            UseRidgedNoise = true,
            SurfaceColor = new Godot.Color(0.45f, 0.42f, 0.38f),
            SlopeColor = new Godot.Color(0.55f, 0.52f, 0.48f),
            SlopeThresholdDeg = 25f,
            VegetationDensity = 0.15f,
            VegetationTypes = ["tree_pine", "rock"],
            WeatherWeights = new() { [WeatherType.Clear] = 0.3f, [WeatherType.Snow] = 0.3f, [WeatherType.Fog] = 0.2f, [WeatherType.Thunderstorm] = 0.2f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Desert, Name = "沙漠",
            BaseHeight = 3f, HeightAmplitude = 0.12f, FrequencyScale = 0.5f, Octaves = 3,
            SurfaceColor = new Godot.Color(0.76f, 0.7f, 0.5f),
            SlopeColor = new Godot.Color(0.65f, 0.55f, 0.35f),
            VegetationDensity = 0.05f,
            VegetationTypes = ["cactus", "dead_bush"],
            FogDensityMultiplier = 0.2f,
            WeatherWeights = new() { [WeatherType.Clear] = 0.7f, [WeatherType.Sandstorm] = 0.2f, [WeatherType.Cloudy] = 0.1f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Ocean, Name = "海洋",
            BaseHeight = -5f, HeightAmplitude = 0.05f, FrequencyScale = 1f, Octaves = 3,
            SurfaceColor = new Godot.Color(0.15f, 0.35f, 0.55f),
            VegetationDensity = 0f,
            WeatherWeights = new() { [WeatherType.Clear] = 0.3f, [WeatherType.Cloudy] = 0.3f, [WeatherType.HeavyRain] = 0.2f, [WeatherType.Thunderstorm] = 0.2f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Swamp, Name = "沼泽",
            BaseHeight = 0f, HeightAmplitude = 0.06f, FrequencyScale = 1.2f, Octaves = 4,
            SurfaceColor = new Godot.Color(0.25f, 0.35f, 0.2f),
            VegetationDensity = 0.6f,
            VegetationTypes = ["dead_tree", "reed", "lily"],
            FogDensityMultiplier = 3f,
            AmbientTint = new Godot.Color(0.7f, 0.8f, 0.6f),
            WeatherWeights = new() { [WeatherType.Fog] = 0.5f, [WeatherType.LightRain] = 0.3f, [WeatherType.HeavyRain] = 0.2f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Tundra, Name = "冻原",
            BaseHeight = 5f, HeightAmplitude = 0.15f, FrequencyScale = 0.7f, Octaves = 4,
            SurfaceColor = new Godot.Color(0.75f, 0.78f, 0.8f),
            VegetationDensity = 0.08f,
            VegetationTypes = ["dead_bush", "ice_crystal"],
            WeatherWeights = new() { [WeatherType.Snow] = 0.5f, [WeatherType.Clear] = 0.2f, [WeatherType.Fog] = 0.3f },
        });

        // ── 特殊场景群系 ──

        Register(new BiomeDefinition
        {
            Id = BiomeId.Cave, Name = "恐怖地下城",
            BaseHeight = -5f, HeightAmplitude = 0.3f, FrequencyScale = 1.5f, Octaves = 5,
            UseRidgedNoise = true,
            SurfaceColor = new Godot.Color(0.18f, 0.15f, 0.12f),
            SlopeColor = new Godot.Color(0.12f, 0.1f, 0.08f),
            SlopeThresholdDeg = 20f,
            VegetationDensity = 0.3f,
            VegetationTypes = ["mushroom", "crystal", "moss", "stalactite"],
            FogDensityMultiplier = 5f,
            AmbientTint = new Godot.Color(0.4f, 0.5f, 0.3f),
            WeatherWeights = new() { [WeatherType.Fog] = 0.8f, [WeatherType.AcidRain] = 0.2f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.City, Name = "现代城市",
            BaseHeight = 1f, HeightAmplitude = 0.02f, FrequencyScale = 0.3f, Octaves = 2,
            SurfaceColor = new Godot.Color(0.4f, 0.4f, 0.42f),
            SlopeColor = new Godot.Color(0.35f, 0.35f, 0.38f),
            VegetationDensity = 0.1f,
            VegetationTypes = ["street_tree", "hedge", "planter"],
            FogDensityMultiplier = 0.5f,
            AmbientTint = new Godot.Color(0.9f, 0.88f, 0.95f),
            WeatherWeights = new() { [WeatherType.Clear] = 0.4f, [WeatherType.Cloudy] = 0.3f, [WeatherType.LightRain] = 0.2f, [WeatherType.Fog] = 0.1f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Ruins, Name = "废墟考古",
            BaseHeight = 3f, HeightAmplitude = 0.2f, FrequencyScale = 0.8f, Octaves = 4,
            SurfaceColor = new Godot.Color(0.6f, 0.55f, 0.42f),
            SlopeColor = new Godot.Color(0.5f, 0.45f, 0.3f),
            SlopeThresholdDeg = 28f,
            VegetationDensity = 0.2f,
            VegetationTypes = ["dead_bush", "vine", "rubble", "column"],
            FogDensityMultiplier = 1.5f,
            AmbientTint = new Godot.Color(0.85f, 0.8f, 0.65f),
            WeatherWeights = new() { [WeatherType.Sandstorm] = 0.3f, [WeatherType.Clear] = 0.4f, [WeatherType.Cloudy] = 0.3f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.SkyIsland, Name = "神秘天空",
            BaseHeight = 20f, HeightAmplitude = 0.6f, FrequencyScale = 1.2f, Octaves = 5,
            UseRidgedNoise = true,
            SurfaceColor = new Godot.Color(0.5f, 0.7f, 0.55f),
            SlopeColor = new Godot.Color(0.6f, 0.65f, 0.7f),
            VegetationDensity = 0.5f,
            VegetationTypes = ["cloud_tree", "sky_vine", "crystal_flower"],
            FogDensityMultiplier = 0.3f,
            AmbientTint = new Godot.Color(0.8f, 0.85f, 1f),
            WeatherWeights = new() { [WeatherType.Clear] = 0.5f, [WeatherType.Cloudy] = 0.3f, [WeatherType.Thunderstorm] = 0.2f },
        });
        Register(new BiomeDefinition
        {
            Id = BiomeId.Space, Name = "太空宇宙",
            BaseHeight = 0f, HeightAmplitude = 0.5f, FrequencyScale = 0.4f, Octaves = 4,
            UseRidgedNoise = true,
            SurfaceColor = new Godot.Color(0.25f, 0.22f, 0.2f),
            SlopeColor = new Godot.Color(0.15f, 0.12f, 0.1f),
            VegetationDensity = 0f,
            FogDensityMultiplier = 0f,
            AmbientTint = new Godot.Color(0.3f, 0.3f, 0.4f),
            WeatherWeights = new() { [WeatherType.RadiationStorm] = 0.3f, [WeatherType.Clear] = 0.7f },
        });
    }
}
