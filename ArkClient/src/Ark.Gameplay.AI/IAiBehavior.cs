namespace Ark.Gameplay.AI;

/// <summary>
/// AI 状态枚举 — 有限状态机的基础状态。
/// </summary>
public enum AiState : byte
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Flee,
    FollowLeader,
    EnterVehicle,
    Dead,
}

/// <summary>
/// AI 行为接口 — 所有 AI 行为节点应实现。
/// TODO: 实现行为树、寻路决策、视野检测。
/// </summary>
public interface IAiBehavior
{
    /// <summary>当前 AI 状态。</summary>
    AiState CurrentState { get; }

    /// <summary>每帧更新 AI 决策。</summary>
    void Update(float deltaTime);

    /// <summary>强制切换状态。</summary>
    void ForceState(AiState state);
}
