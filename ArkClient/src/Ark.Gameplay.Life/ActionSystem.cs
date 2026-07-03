using Ark.Abstractions;
using Ark.Events;

namespace Ark.Gameplay.Life;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║    统一动作执行器 — ActionSystem.Execute(action, entityId)                    ║
// ║    所有人物日常动作走此入口，避免重复逻辑                                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 正在执行的动作实例。
/// </summary>
public sealed class ActiveAction
{
    public IGameAction Action { get; }
    public int EntityId { get; }
    public float Elapsed { get; set; }

    public ActiveAction(IGameAction action, int entityId)
    {
        Action = action;
        EntityId = entityId;
    }

    public bool IsComplete => Action.Duration <= 0 || Elapsed >= Action.Duration;
}

/// <summary>
/// 统一动作系统 — 管理动作的启动、计时、完成。
/// </summary>
public sealed class ActionSystem
{
    private readonly EventBus _eventBus;
    private readonly Dictionary<int, ActiveAction> _active = [];

    public ActionSystem(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>尝试为实体执行动作。</summary>
    public bool Execute(IGameAction action, int entityId)
    {
        if (!action.CanExecute(entityId)) return false;

        // 取消当前动作
        if (_active.TryGetValue(entityId, out var current))
            Cancel(entityId);

        action.Execute(entityId);
        _eventBus.Publish(new ActionStartedEvent(entityId, action.ActionId, action.Duration));

        if (action.Duration <= 0)
        {
            // 瞬时动作
            action.OnComplete(entityId);
            _eventBus.Publish(new ActionCompletedEvent(entityId, action.ActionId));
        }
        else
        {
            _active[entityId] = new ActiveAction(action, entityId);
        }
        return true;
    }

    /// <summary>取消实体当前动作。</summary>
    public void Cancel(int entityId)
    {
        _active.Remove(entityId);
    }

    /// <summary>每帧更新 — 推进所有活跃动作计时。</summary>
    public void Update(float deltaTime)
    {
        List<int>? completed = null;

        foreach (var (entityId, active) in _active)
        {
            active.Elapsed += deltaTime;
            if (active.IsComplete)
            {
                active.Action.OnComplete(entityId);
                _eventBus.Publish(new ActionCompletedEvent(entityId, active.Action.ActionId));
                (completed ??= []).Add(entityId);
            }
        }

        if (completed != null)
            foreach (var id in completed)
                _active.Remove(id);
    }

    /// <summary>实体是否正在执行动作。</summary>
    public bool IsActive(int entityId) => _active.ContainsKey(entityId);

    /// <summary>获取实体当前动作 ID（无则 null）。</summary>
    public string? GetCurrentActionId(int entityId) =>
        _active.TryGetValue(entityId, out var a) ? a.Action.ActionId : null;

    /// <summary>当前活跃动作数。</summary>
    public int ActiveCount => _active.Count;
}
