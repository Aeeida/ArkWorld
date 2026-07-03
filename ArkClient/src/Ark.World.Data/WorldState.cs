using Ark.World.Core;

namespace Ark.World.Data;

/// <summary>
/// 天气状态 — 当前世界天气的完整快照（可插值过渡）。
/// </summary>
public sealed class WeatherState
{
    /// <summary>当前天气类型。</summary>
    public WeatherType CurrentType { get; set; } = WeatherType.Clear;

    /// <summary>目标天气类型（过渡中）。</summary>
    public WeatherType TargetType { get; set; } = WeatherType.Clear;

    /// <summary>过渡进度 [0,1]。</summary>
    public float TransitionProgress { get; set; } = 1f;

    /// <summary>云量 [0,1]。</summary>
    public float CloudCoverage { get; set; }

    /// <summary>降水强度 [0,1]。</summary>
    public float PrecipitationIntensity { get; set; }

    /// <summary>风速 (m/s)。</summary>
    public float WindSpeed { get; set; }

    /// <summary>风向 (度, 0=北, 顺时针)。</summary>
    public float WindDirection { get; set; }

    /// <summary>雾密度 [0,1]。</summary>
    public float FogDensity { get; set; }

    /// <summary>雷电频率 [0,1]。</summary>
    public float LightningFrequency { get; set; }

    /// <summary>积雪厚度 (米)。</summary>
    public float SnowAccumulation { get; set; }

    /// <summary>温度 (°C)。</summary>
    public float Temperature { get; set; } = 20f;

    /// <summary>湿度 [0,1]。</summary>
    public float Humidity { get; set; } = 0.5f;

    /// <summary>距离上次天气变更的时间（秒）。</summary>
    public float TimeSinceLastChange { get; set; }

    /// <summary>从天气类型获取默认参数快照。</summary>
    public static WeatherState FromType(WeatherType type) => type switch
    {
        WeatherType.Clear => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 0.1f, Temperature = 22f, Humidity = 0.3f,
        },
        WeatherType.Cloudy => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 0.7f, Temperature = 18f, Humidity = 0.5f,
        },
        WeatherType.LightRain => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 0.8f, PrecipitationIntensity = 0.3f, Temperature = 15f, Humidity = 0.75f,
            FogDensity = 0.05f,
        },
        WeatherType.HeavyRain => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 0.95f, PrecipitationIntensity = 0.85f, WindSpeed = 8f,
            Temperature = 12f, Humidity = 0.9f, FogDensity = 0.15f,
        },
        WeatherType.Thunderstorm => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 1f, PrecipitationIntensity = 0.95f, WindSpeed = 15f,
            Temperature = 10f, Humidity = 0.95f, FogDensity = 0.1f, LightningFrequency = 0.5f,
        },
        WeatherType.Fog => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 0.6f, Temperature = 12f, Humidity = 0.85f, FogDensity = 0.6f,
        },
        WeatherType.Snow => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 0.85f, PrecipitationIntensity = 0.5f, Temperature = -5f,
            Humidity = 0.6f, FogDensity = 0.08f,
        },
        WeatherType.Sandstorm => new()
        {
            CurrentType = type, TargetType = type, TransitionProgress = 1f,
            CloudCoverage = 0.3f, WindSpeed = 25f, Temperature = 35f, Humidity = 0.1f,
            FogDensity = 0.7f,
        },
        _ => new() { CurrentType = type, TargetType = type, TransitionProgress = 1f }
    };
}

/// <summary>
/// 世界时间状态 — 日夜循环、季节、日历。
/// </summary>
public sealed class WorldTimeState
{
    /// <summary>世界总经过时间（秒，不受暂停影响）。</summary>
    public double TotalElapsed { get; set; }

    /// <summary>游戏内天数（从第 0 天开始）。</summary>
    public int Day { get; set; }

    /// <summary>当天归一化时间 [0,1)，0=午夜, 0.25=日出, 0.5=正午, 0.75=日落。</summary>
    public float NormalizedTimeOfDay { get; set; } = 0.3f; // 默认从早上开始

    /// <summary>当前时段。</summary>
    public DayPeriod Period { get; set; } = DayPeriod.Morning;

    /// <summary>当前季节。</summary>
    public Season CurrentSeason { get; set; } = Season.Spring;

    /// <summary>一个季节持续多少游戏天。</summary>
    public int DaysPerSeason { get; set; } = 15;

    /// <summary>太阳仰角（度，0=地平线，90=天顶）。</summary>
    public float SunElevation { get; set; }

    /// <summary>太阳方位角（度，0=北）。</summary>
    public float SunAzimuth { get; set; }

    /// <summary>月相 [0,1]，0=新月，0.5=满月。</summary>
    public float MoonPhase { get; set; }
}

/// <summary>
/// 修改日志条目 — 记录对世界的一次永久修改。
/// 用于 Delta 存储 + 回放。
/// </summary>
public readonly record struct ModificationEntry(
    double Timestamp,
    TerrainModType ModType,
    float PosX, float PosY, float PosZ,
    float Radius,
    float Intensity,
    string? Metadata = null);

/// <summary>
/// 修改日志 — 追加写入的世界修改历史记录。
/// </summary>
public sealed class ModificationLog
{
    private readonly List<ModificationEntry> _entries = [];

    public IReadOnlyList<ModificationEntry> Entries => _entries;

    public void Append(ModificationEntry entry) => _entries.Add(entry);

    /// <summary>获取指定区域内的所有修改。</summary>
    public IEnumerable<ModificationEntry> GetModificationsInRange(
        float centerX, float centerZ, float range)
    {
        float rangeSq = range * range;
        foreach (var e in _entries)
        {
            float dx = e.PosX - centerX;
            float dz = e.PosZ - centerZ;
            if (dx * dx + dz * dz <= rangeSq)
                yield return e;
        }
    }

    /// <summary>获取指定时间戳之后的修改。</summary>
    public IEnumerable<ModificationEntry> GetModificationsSince(double timestamp)
    {
        foreach (var e in _entries)
            if (e.Timestamp > timestamp)
                yield return e;
    }

    public void Trim(System.Func<ModificationEntry, bool> keepPredicate)
    {
        _entries.RemoveAll(entry => !keepPredicate(entry));
    }

    public void Clear() => _entries.Clear();
    public int Count => _entries.Count;
}
