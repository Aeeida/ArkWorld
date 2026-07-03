using Ark.Events;

namespace Ark.World.Core;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                    世界/地形/环境事件 — 通过 EventBus 传播                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>地形修改事件 — 爆炸、挖掘、建造等永久改变地形。</summary>
public readonly record struct TerrainModifiedEvent(
    float PosX, float PosY, float PosZ,
    float Radius,
    TerrainModType ModType,
    float Intensity) : IGameEvent;

/// <summary>地形修改类型。</summary>
public enum TerrainModType : byte
{
    /// <summary>挖掘/破坏（降低高度）。</summary>
    Dig,
    /// <summary>填充/建造（增加高度）。</summary>
    Fill,
    /// <summary>平整。</summary>
    Flatten,
    /// <summary>爆炸（球形挖除+碎片）。</summary>
    Explosion,
    /// <summary>侵蚀（自然风化）。</summary>
    Erosion,
}

/// <summary>区块加载完成事件。</summary>
public readonly record struct ChunkLoadedEvent(ChunkCoord Coord) : IGameEvent;

/// <summary>区块卸载事件。</summary>
public readonly record struct ChunkUnloadedEvent(ChunkCoord Coord) : IGameEvent;

/// <summary>天气变更事件。</summary>
public readonly record struct WeatherChangedEvent(
    WeatherType PreviousWeather,
    WeatherType NewWeather) : IGameEvent;

/// <summary>天气类型。</summary>
public enum WeatherType : byte
{
    Clear,
    Cloudy,
    LightRain,
    HeavyRain,
    Thunderstorm,
    Fog,
    Snow,
    Sandstorm,
    AcidRain,
    RadiationStorm,
}

/// <summary>时间段变更事件（日出、正午、日落、深夜）。</summary>
public readonly record struct TimeOfDayChangedEvent(
    DayPeriod PreviousPeriod,
    DayPeriod NewPeriod) : IGameEvent;

/// <summary>一天的时段。</summary>
public enum DayPeriod : byte
{
    Dawn,
    Morning,
    Noon,
    Afternoon,
    Dusk,
    Night,
    Midnight,
}

/// <summary>季节变更事件。</summary>
public readonly record struct SeasonChangedEvent(Season PreviousSeason, Season NewSeason) : IGameEvent;

/// <summary>季节。</summary>
public enum Season : byte
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

/// <summary>生物群系进入事件。</summary>
public readonly record struct BiomeEnteredEvent(BiomeId NewBiome, BiomeId PreviousBiome) : IGameEvent;

/// <summary>环境切换事件。</summary>
public readonly record struct EnvironmentSwitchedEvent(EnvironmentPreset Preset) : IGameEvent;

/// <summary>
/// 环境预设 — F7~F12 一键切换整个世界场景风格。
/// </summary>
public enum EnvironmentPreset : byte
{
    /// <summary>自然混合（默认 — 噪声驱动的多群系）。</summary>
    Natural,
    /// <summary>F7: 美丽野外（开阔草原、山脉、湖泊、季节花海）。</summary>
    BeautifulWild,
    /// <summary>F8: 险恶森林（茂密植被、动态雾气、陷阱地形）。</summary>
    DarkForest,
    /// <summary>F9: 恐怖地下城（多层洞穴、崩塌结构、幽暗光影）。</summary>
    HorrorDungeon,
    /// <summary>F10: 现代城市（高楼林立、动态交通、霓虹夜景）。</summary>
    ModernCity,
    /// <summary>F11: 废墟考古（崩塌遗迹、沙尘覆盖、历史层叠）。</summary>
    RuinArchaeology,
    /// <summary>F12: 神秘天空（浮空岛、云海平台、风暴漩涡）。</summary>
    MysticSky,
    /// <summary>Shift+F12: 太空宇宙（零重力陨石带、行星表面）。</summary>
    SpaceUniverse,
}
