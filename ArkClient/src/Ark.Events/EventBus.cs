namespace Ark.Events;

/// <summary>
/// 类型化事件总线 — 模块间解耦通信的核心基础设施。
///
/// 使用方式：
///   var bus = new EventBus();
///   bus.Subscribe&lt;DamageEvent&gt;(e => HandleDamage(e));
///   bus.Publish(new DamageEvent(...));
///
/// 设计原则：
///   • 发布者不知道订阅者的存在
///   • 同步分发（与 ECS 帧同步）
///   • 支持取消订阅（通过 IDisposable token）
/// </summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];

    /// <summary>订阅事件，返回取消订阅 token。</summary>
    public IDisposable Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = [];
            _handlers[type] = list;
        }
        list.Add(handler);
        return new Unsubscriber(() => list.Remove(handler));
    }

    /// <summary>发布事件到所有订阅者。</summary>
    public void Publish<T>(T @event)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is Action<T> handler)
                handler(@event);
        }
    }

    /// <summary>移除指定类型的所有订阅。</summary>
    public void ClearSubscriptions<T>()
    {
        _handlers.Remove(typeof(T));
    }

    /// <summary>移除所有订阅。</summary>
    public void ClearAll()
    {
        _handlers.Clear();
    }

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            _onDispose?.Invoke();
            _onDispose = null;
        }
    }
}
