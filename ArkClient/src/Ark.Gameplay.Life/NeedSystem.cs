using Ark.Configuration;
using Ark.Events;
using Ark.Shared.Data;

namespace Ark.Gameplay.Life;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║          角色需求状态 — 持有单个角色的 6 项需求值                                ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 角色需求状态容器 — 管理 Hunger/Energy/Bladder/Hygiene/Fun/Social。
/// 可独立于 ECS 使用，也可作为 ECS Component 的数据载体。
/// </summary>
public sealed class CharacterNeedState
{
    private readonly float[] _values = new float[NeedCount];
    private readonly float[] _decayRates = new float[NeedCount];

    public const int NeedCount = 6;

    public int EntityId { get; }

    public CharacterNeedState(int entityId)
    {
        EntityId = entityId;
        var cfg = GameConfig.Current.Life;
        float max = cfg.NeedMax;

        // 初始化：满值 + 配置衰减率
        Span<float> decays = [cfg.HungerDecay, cfg.EnergyDecay, cfg.BladderDecay,
                              cfg.HygieneDecay, cfg.FunDecay, cfg.SocialDecay];
        for (int i = 0; i < NeedCount; i++)
        {
            _values[i] = max;
            _decayRates[i] = decays[i];
        }
    }

    /// <summary>获取指定需求的当前值。</summary>
    public float Get(NeedCategory need) => _values[(int)need];

    /// <summary>直接设置需求值（clamp 到 [0, max]）。</summary>
    public void Set(NeedCategory need, float value)
    {
        float max = GameConfig.Current.Life.NeedMax;
        _values[(int)need] = Math.Clamp(value, 0f, max);
    }

    /// <summary>修改指定需求值（正=恢复，负=消耗）。</summary>
    public void Modify(NeedCategory need, float delta)
    {
        Set(need, _values[(int)need] + delta);
    }

    /// <summary>获取所有需求的只读快照。</summary>
    public ReadOnlySpan<float> GetAll() => _values;

    /// <summary>衰减率数组。</summary>
    public ReadOnlySpan<float> DecayRates => _decayRates;

    /// <summary>计算心情值（所有需求的加权平均 / 最大值）。</summary>
    public float CalculateMood()
    {
        float sum = 0;
        for (int i = 0; i < NeedCount; i++)
            sum += _values[i];
        return sum / NeedCount;
    }
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║    需求系统 — 纯逻辑，每帧衰减所有角色的需求并发布事件                            ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 需求衰减系统 — 纯函数式 + EventBus 通知。
/// 由上层（GameBootstrap 或 LifeModule）每帧调用 Update。
/// </summary>
public sealed class NeedSystem
{
    private readonly EventBus _eventBus;
    private readonly List<CharacterNeedState> _characters = [];

    public NeedSystem(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Register(CharacterNeedState state) => _characters.Add(state);
    public void Unregister(CharacterNeedState state) => _characters.Remove(state);

    /// <summary>每帧更新：衰减所有角色需求，触发临界事件。</summary>
    public void Update(float deltaTime, float timeScale = 1f)
    {
        float dt = deltaTime * timeScale;
        float threshold = GameConfig.Current.Life.NeedCriticalThreshold;

        foreach (var ch in _characters)
        {
            var decays = ch.DecayRates;
            for (int i = 0; i < CharacterNeedState.NeedCount; i++)
            {
                var need = (NeedCategory)i;
                float oldVal = ch.Get(need);
                float newVal = Math.Max(0, oldVal - decays[i] * dt);

                if (Math.Abs(newVal - oldVal) < 0.0001f) continue;

                ch.Set(need, newVal);
                _eventBus.Publish(new NeedChangedEvent(ch.EntityId, need.ToString(), oldVal, newVal));

                // 进入危险区
                if (oldVal >= threshold && newVal < threshold)
                    _eventBus.Publish(new NeedCriticalEvent(ch.EntityId, need.ToString(), newVal));
            }
        }
    }

    /// <summary>获取当前注册的角色数。</summary>
    public int CharacterCount => _characters.Count;
}
