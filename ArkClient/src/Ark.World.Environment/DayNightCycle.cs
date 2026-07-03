using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Environment;

/// <summary>
/// 日夜循环系统 — 驱动太阳/月亮位置、光照颜色/强度、天空色调。
/// 输出到 WorldTimeState，由 AtmosphereController 消费应用到 Godot 节点。
/// </summary>
public sealed class DayNightCycle : IWorldSystem
{
    public string SystemId => "DayNight";

    private readonly WorldTimeState _timeState;
    private DayPeriod _lastPeriod;

    /// <summary>时间变更回调（可连接到 EventBus）。</summary>
    public event Action<DayPeriod, DayPeriod>? OnPeriodChanged;

    /// <summary>季节变更回调。</summary>
    public event Action<Season, Season>? OnSeasonChanged;

    public DayNightCycle(WorldTimeState timeState)
    {
        _timeState = timeState;
        _lastPeriod = _timeState.Period;
    }

    public void Initialize(WorldSeed seed)
    {
        // 可用种子偏移初始时间
        _timeState.NormalizedTimeOfDay = 0.3f; // 早晨
    }

    public void Update(float deltaTime)
    {
        // 推进时间
        float dayAdvance = deltaTime / WorldConstants.DayDurationSeconds;
        _timeState.NormalizedTimeOfDay += dayAdvance;

        if (_timeState.NormalizedTimeOfDay >= 1f)
        {
            _timeState.NormalizedTimeOfDay -= 1f;
            _timeState.Day++;

            // 季节检查
            int seasonIndex = (_timeState.Day / _timeState.DaysPerSeason) % 4;
            var newSeason = (Season)seasonIndex;
            if (newSeason != _timeState.CurrentSeason)
            {
                var prev = _timeState.CurrentSeason;
                _timeState.CurrentSeason = newSeason;
                OnSeasonChanged?.Invoke(prev, newSeason);
            }
        }

        _timeState.TotalElapsed += deltaTime;

        // 计算太阳位置
        float t = _timeState.NormalizedTimeOfDay;
        _timeState.SunElevation = CalculateSunElevation(t);
        _timeState.SunAzimuth = t * 360f; // 简化：一天转一圈

        // 月相
        _timeState.MoonPhase = (_timeState.Day % 30) / 30f;

        // 更新时段
        var newPeriod = ClassifyPeriod(t);
        if (newPeriod != _lastPeriod)
        {
            _timeState.Period = newPeriod;
            OnPeriodChanged?.Invoke(_lastPeriod, newPeriod);
            _lastPeriod = newPeriod;
        }
    }

    public void Shutdown() { }

    /// <summary>获取当前太阳方向向量（用于 DirectionalLight3D）。</summary>
    public Vector3 GetSunDirection()
    {
        float elevRad = Mathf.DegToRad(_timeState.SunElevation);
        float azimRad = Mathf.DegToRad(_timeState.SunAzimuth);
        return new Vector3(
            MathF.Cos(elevRad) * MathF.Sin(azimRad),
            MathF.Sin(elevRad),
            MathF.Cos(elevRad) * MathF.Cos(azimRad)
        ).Normalized();
    }

    /// <summary>获取太阳颜色（基于仰角）— 永不全黑。</summary>
    public Color GetSunColor()
    {
        float elev = _timeState.SunElevation;
        if (elev < 0f)
        {
            // "夜间" → 暖黄昏色（不再是深蓝/黑）
            float f = MathF.Max(0, (elev + 30f) / 30f); // -30→0 映射到 0→1
            return new Color(0.8f, 0.4f, 0.15f).Lerp(new Color(0.9f, 0.55f, 0.25f), f);
        }
        if (elev < 5f) // 黎明/黄昏
            return new Color(1f, 0.5f, 0.2f).Lerp(Colors.White, MathF.Max(0, elev / 5f));
        return Colors.White.Lerp(new Color(1f, 0.98f, 0.9f), MathF.Min(1f, elev / 60f));
    }

    /// <summary>获取太阳能量（基于仰角）— 最低不低于 0.3（黄昏级别）。</summary>
    public float GetSunEnergy()
    {
        float elev = _timeState.SunElevation;
        if (elev < -5f) return 0.3f; // "夜间" → 黄昏亮度（不再接近 0）
        if (elev < 0f) return Mathf.Lerp(0.3f, 0.5f, (elev + 5f) / 5f);
        return Mathf.Lerp(0.5f, 1.2f, MathF.Min(1f, elev / 45f));
    }

    /// <summary>获取环境光强度 — 始终保持可见。</summary>
    public float GetAmbientEnergy()
    {
        float elev = _timeState.SunElevation;
        if (elev < -10f) return 0.35f; // "夜间" → 足够看清环境
        if (elev < 0f) return Mathf.Lerp(0.35f, 0.5f, (elev + 10f) / 10f);
        return Mathf.Lerp(0.5f, 0.8f, MathF.Min(1f, elev / 30f));
    }

    /// <summary>获取天空颜色 — 永不全黑，最暗为深蓝黄昏。</summary>
    public Color GetSkyColor()
    {
        float elev = _timeState.SunElevation;
        if (elev < -10f) return new Color(0.15f, 0.12f, 0.2f);        // "夜间" → 深紫蓝（非纯黑）
        if (elev < 0f)                                                  // 黄昏/黎明
        {
            float f = (elev + 10f) / 10f;
            return new Color(0.15f, 0.12f, 0.2f).Lerp(new Color(0.6f, 0.3f, 0.15f), f);
        }
        if (elev < 15f)                                                 // 日出后
        {
            float f = elev / 15f;
            return new Color(0.6f, 0.3f, 0.15f).Lerp(new Color(0.4f, 0.6f, 0.9f), f);
        }
        return new Color(0.4f, 0.6f, 0.9f); // 白天蓝天
    }

    private static float CalculateSunElevation(float normalizedTime)
    {
        // 正弦曲线：0.25=日出(0°), 0.5=正午(max), 0.75=日落(0°)
        float angle = (normalizedTime - WorldConstants.SunriseNormalized) / (WorldConstants.SunsetNormalized - WorldConstants.SunriseNormalized);
        if (angle < 0 || angle > 1)
        {
            // 夜间 → 太阳在地平线以下
            return -30f + 20f * MathF.Sin(normalizedTime * MathF.PI * 2);
        }
        return MathF.Sin(angle * MathF.PI) * 70f; // 最大仰角 70°
    }

    private static DayPeriod ClassifyPeriod(float t) => t switch
    {
        < 0.21f => DayPeriod.Night,
        < 0.27f => DayPeriod.Dawn,
        < 0.40f => DayPeriod.Morning,
        < 0.55f => DayPeriod.Noon,
        < 0.70f => DayPeriod.Afternoon,
        < 0.77f => DayPeriod.Dusk,
        < 0.90f => DayPeriod.Night,
        _ => DayPeriod.Midnight,
    };
}
