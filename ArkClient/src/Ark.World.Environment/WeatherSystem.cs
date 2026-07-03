using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Environment;

/// <summary>
/// 天气系统 — 驱动天气状态转换、降水/风/雾的参数变化。
/// 基于群系权重 + 随机事件，支持渐变过渡。
/// </summary>
public sealed class WeatherSystem : IWorldSystem
{
    public string SystemId => "Weather";

    private readonly WeatherState _state;
    private readonly WorldTimeState _timeState;
    private WorldSeed _weatherSeed;
    private float _nextChangeTimer;
    private BiomeId _currentBiome = BiomeId.Plains;

    /// <summary>天气变更回调。</summary>
    public event Action<WeatherType, WeatherType>? OnWeatherChanged;

    /// <summary>当前天气快照。</summary>
    public WeatherState State => _state;

    public WeatherSystem(WeatherState state, WorldTimeState timeState)
    {
        _state = state;
        _timeState = timeState;
    }

    public void Initialize(WorldSeed seed)
    {
        _weatherSeed = seed.Derive("weather");
        _nextChangeTimer = WorldConstants.WeatherMinInterval;
        // 强制晴天（暂时禁用天气变化）
        ForceWeather(WeatherType.Clear);
    }

    /// <summary>通知天气系统当前玩家所在群系（影响天气权重）。</summary>
    public void SetCurrentBiome(BiomeId biome) => _currentBiome = biome;

    public void Update(float deltaTime)
    {
        // 暂时只保持晴天 — 跳过天气变化逻辑
        // 保留过渡机制以便将来恢复
    }

    public void Shutdown() { }

    /// <summary>强制设置天气（无过渡）。</summary>
    public void ForceWeather(WeatherType type)
    {
        var prev = _state.CurrentType;
        _state.CurrentType = type;
        _state.TargetType = type;
        _state.TransitionProgress = 1f;
        ApplyWeatherParameters(type);
        if (prev != type)
            OnWeatherChanged?.Invoke(prev, type);
    }

    /// <summary>开始天气过渡。</summary>
    public void TransitionTo(WeatherType target)
    {
        if (target == _state.CurrentType) return;
        var prev = _state.CurrentType;
        _state.TargetType = target;
        _state.TransitionProgress = 0f;
        _state.TimeSinceLastChange = 0;
        OnWeatherChanged?.Invoke(prev, target);
    }

    private void TryChangeWeather()
    {
        var biomeDef = BiomeRegistry.Get(_currentBiome);
        if (biomeDef == null || biomeDef.WeatherWeights.Count == 0) return;

        // 加权随机选择
        float total = 0;
        foreach (var w in biomeDef.WeatherWeights.Values) total += w;
        float roll = GetSeededRandom() * total;

        float acc = 0;
        foreach (var (type, weight) in biomeDef.WeatherWeights)
        {
            acc += weight;
            if (roll <= acc)
            {
                if (type != _state.CurrentType)
                    TransitionTo(type);
                return;
            }
        }
    }

    private void InterpolateWeather(float dt)
    {
        float t = _state.TransitionProgress;
        var target = WeatherState.FromType(_state.TargetType);

        _state.CloudCoverage = Mathf.Lerp(_state.CloudCoverage, target.CloudCoverage, t);
        _state.PrecipitationIntensity = Mathf.Lerp(_state.PrecipitationIntensity, target.PrecipitationIntensity, t);
        _state.WindSpeed = Mathf.Lerp(_state.WindSpeed, target.WindSpeed, t);
        _state.FogDensity = Mathf.Lerp(_state.FogDensity, target.FogDensity, t);
        _state.Temperature = Mathf.Lerp(_state.Temperature, target.Temperature, t);
        _state.Humidity = Mathf.Lerp(_state.Humidity, target.Humidity, t);
        _state.LightningFrequency = Mathf.Lerp(_state.LightningFrequency, target.LightningFrequency, t);
    }

    private void ApplyWeatherParameters(WeatherType type)
    {
        var defaults = WeatherState.FromType(type);
        _state.CloudCoverage = defaults.CloudCoverage;
        _state.PrecipitationIntensity = defaults.PrecipitationIntensity;
        _state.WindSpeed = defaults.WindSpeed;
        _state.FogDensity = defaults.FogDensity;
        _state.Temperature = defaults.Temperature;
        _state.Humidity = defaults.Humidity;
        _state.LightningFrequency = defaults.LightningFrequency;
    }

    private void ApplySeasonalEffects(float dt)
    {
        // 季节对温度的基线偏移
        float seasonTemp = _timeState.CurrentSeason switch
        {
            Season.Spring => 0f,
            Season.Summer => 8f,
            Season.Autumn => -2f,
            Season.Winter => -15f,
            _ => 0f
        };
        _state.Temperature = Mathf.Lerp(_state.Temperature, _state.Temperature + seasonTemp * 0.01f, dt);

        // 冬季积雪
        if (_timeState.CurrentSeason == Season.Winter && _state.PrecipitationIntensity > 0.1f)
            _state.SnowAccumulation += dt * _state.PrecipitationIntensity * 0.01f;
        else if (_state.SnowAccumulation > 0)
            _state.SnowAccumulation = MathF.Max(0, _state.SnowAccumulation - dt * 0.005f);
    }

    private float GetSeededRandom()
    {
        // 基于时间的确定性伪随机
        long hash = _weatherSeed.Value ^ (long)(_timeState.TotalElapsed * 1000);
        hash ^= hash >> 17;
        hash *= unchecked((long)0xbf58476d1ce4e5b9UL);
        hash ^= hash >> 31;
        return ((uint)(hash & 0x7FFFFFFF)) / (float)0x7FFFFFFF;
    }
}
