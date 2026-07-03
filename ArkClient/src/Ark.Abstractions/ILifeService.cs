using Ark.Shared.Data;

namespace Ark.Abstractions;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                日常养成服务接口 — Life Module 的公共契约                         ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 日常养成服务 — 查询角色需求、执行日常动作、读取技能等级。
/// </summary>
public interface ILifeService
{
    // ═══ 需求 ═══
    float GetNeed(int entityId, NeedCategory need);
    float GetMood(int entityId);
    void ModifyNeed(int entityId, NeedCategory need, float delta);

    // ═══ 技能 ═══
    float GetSkillLevel(int entityId, SkillCategory skill);
    void AddSkillExp(int entityId, SkillCategory skill, float amount);

    // ═══ 动作 ═══
    bool ExecuteAction(int entityId, string actionId);
    bool IsPerformingAction(int entityId);
    string? GetCurrentAction(int entityId);

    // ═══ 事件 ═══
    event Action<int, NeedCategory, float>? OnNeedCritical;
    event Action<int, string>? OnActionCompleted;
}
