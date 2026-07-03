namespace Ark.Events;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                   日常养成（Life）领域事件                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>需求值变更事件（单个需求）。</summary>
public readonly record struct NeedChangedEvent(
    int EntityId, string NeedId, float OldValue, float NewValue) : IGameEvent;

/// <summary>需求进入危险区事件（低于阈值）。</summary>
public readonly record struct NeedCriticalEvent(
    int EntityId, string NeedId, float Value) : IGameEvent;

/// <summary>角色动作开始事件。</summary>
public readonly record struct ActionStartedEvent(
    int EntityId, string ActionId, float Duration) : IGameEvent;

/// <summary>角色动作完成事件。</summary>
public readonly record struct ActionCompletedEvent(
    int EntityId, string ActionId) : IGameEvent;

/// <summary>技能经验获得事件。</summary>
public readonly record struct SkillExpGainedEvent(
    int EntityId, string SkillId, float Amount, float NewLevel) : IGameEvent;

/// <summary>心情变更事件。</summary>
public readonly record struct MoodChangedEvent(
    int EntityId, float OldMood, float NewMood) : IGameEvent;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                   太空飞行（Space）领域事件                                     ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>零件安装事件。</summary>
public readonly record struct PartAttachedEvent(
    int VesselEntityId, int PartDefId, int StageIndex) : IGameEvent;

/// <summary>火箭发射事件。</summary>
public readonly record struct LaunchEvent(int VesselEntityId) : IGameEvent;

/// <summary>分级事件。</summary>
public readonly record struct StageSeparationEvent(
    int VesselEntityId, int StageIndex) : IGameEvent;

/// <summary>入轨成功事件。</summary>
public readonly record struct OrbitInsertionEvent(
    int VesselEntityId, float Apoapsis, float Periapsis,
    float Eccentricity) : IGameEvent;

/// <summary>任务成功事件（发射→入轨完整链）。</summary>
public readonly record struct MissionSuccessEvent(
    int VesselEntityId, float ReputationReward,
    float ScienceReward) : IGameEvent;

/// <summary>任务失败事件。</summary>
public readonly record struct MissionFailedEvent(
    int VesselEntityId, string Reason) : IGameEvent;

/// <summary>时间加速变更事件。</summary>
public readonly record struct TimeWarpChangedEvent(
    float OldScale, float NewScale) : IGameEvent;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                   跨模块集成事件                                               ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>零件解锁事件（人物工作完成 → 火箭模块可用新零件）。</summary>
public readonly record struct PartUnlockedEvent(int PartDefId) : IGameEvent;

/// <summary>资源变更事件。</summary>
public readonly record struct ResourceChangedEvent(
    string ResourceId, float OldAmount, float NewAmount) : IGameEvent;
