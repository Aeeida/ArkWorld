using Ark.Abstractions;
using Ark.Configuration;
using Ark.Shared.Data;

namespace Ark.Gameplay.Life.Actions;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║           标准日常动作 — 数据驱动，可从 JSON 扩展                                ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 日常动作基类 — 持有需求影响表和技能经验。
/// </summary>
public abstract class DailyAction : IGameAction
{
    public abstract string ActionId { get; }
    public abstract float Duration { get; }

    /// <summary>需求影响表：(NeedCategory, delta)。正=恢复，负=消耗。</summary>
    protected virtual (NeedCategory need, float delta)[] NeedEffects => [];

    /// <summary>技能经验：(SkillCategory, expAmount)。</summary>
    protected virtual (SkillCategory skill, float exp)? SkillGain => null;

    /// <summary>需求阈值前提条件 — 返回需求是否低于指定值才允许执行。</summary>
    protected virtual (NeedCategory need, float maxThreshold)? NeedPrecondition => null;

    private readonly Func<int, CharacterNeedState?> _needResolver;

    protected DailyAction(Func<int, CharacterNeedState?> needResolver)
    {
        _needResolver = needResolver;
    }

    public virtual bool CanExecute(int entityId)
    {
        var state = _needResolver(entityId);
        if (state == null) return false;

        if (NeedPrecondition is var (need, max))
            return state.Get(need) < max;

        return true;
    }

    public virtual void Execute(int entityId) { }

    public virtual void OnComplete(int entityId)
    {
        var state = _needResolver(entityId);
        if (state == null) return;

        foreach (var (need, delta) in NeedEffects)
            state.Modify(need, delta);
    }
}

/// <summary>吃饭动作。</summary>
public sealed class EatAction(Func<int, CharacterNeedState?> resolver) : DailyAction(resolver)
{
    public override string ActionId => "Eat";
    public override float Duration => GameConfig.Current.Life.EatDuration;
    protected override (NeedCategory need, float maxThreshold)? NeedPrecondition => (NeedCategory.Hunger, 70f);
    protected override (NeedCategory need, float delta)[] NeedEffects =>
        [(NeedCategory.Hunger, 80f), (NeedCategory.Fun, 10f)];
}

/// <summary>睡觉动作。</summary>
public sealed class SleepAction(Func<int, CharacterNeedState?> resolver) : DailyAction(resolver)
{
    public override string ActionId => "Sleep";
    public override float Duration => GameConfig.Current.Life.SleepDuration;
    protected override (NeedCategory need, float maxThreshold)? NeedPrecondition => (NeedCategory.Energy, 50f);
    protected override (NeedCategory need, float delta)[] NeedEffects =>
        [(NeedCategory.Energy, 100f)];
}

/// <summary>社交聊天动作。</summary>
public sealed class SocialChatAction(Func<int, CharacterNeedState?> resolver) : DailyAction(resolver)
{
    public override string ActionId => "Social_Chat";
    public override float Duration => GameConfig.Current.Life.SocialChatDuration;
    protected override (NeedCategory need, float maxThreshold)? NeedPrecondition => (NeedCategory.Social, 60f);
    protected override (NeedCategory need, float delta)[] NeedEffects =>
        [(NeedCategory.Social, 40f), (NeedCategory.Fun, 20f)];
}

/// <summary>火箭工厂工作动作 — 跨模块：获得金钱+工程技能。</summary>
public sealed class WorkRocketFactoryAction(Func<int, CharacterNeedState?> resolver) : DailyAction(resolver)
{
    public override string ActionId => "Work_RocketFactory";
    public override float Duration => GameConfig.Current.Life.WorkDuration;
    protected override (NeedCategory need, float delta)[] NeedEffects =>
        [(NeedCategory.Energy, -20f), (NeedCategory.Fun, -10f)];
    protected override (SkillCategory skill, float exp)? SkillGain =>
        (SkillCategory.RocketEngineering, 5f);
}
