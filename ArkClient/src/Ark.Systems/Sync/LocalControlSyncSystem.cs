using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;

namespace Ark.Systems.Sync;

/// <summary>
/// 本地控制状态变更接收方接口。
/// 仅在 <see cref="LocalControlState"/> 关键字段变化时触发，避免每帧无谓回调。
/// </summary>
public interface ILocalControlReceiver
{
    /// <summary>承载 LocalControlState 的玩家实体（通常是本地玩家 ECS 实体）。</summary>
    Entity LocalEntity { get; }
    void OnLocalControlChanged(in LocalControlState previous, in LocalControlState current);
}

/// <summary>
/// 本地控制 Sync System — 监测 <see cref="LocalControlState"/> 关键字段变化并通知订阅者，
/// 用于驱动相机/输入模式切换，避免 Node 自轮询。
/// 比对变更检测：Mode/ControlSource/SeatType/InVehicle/BuildMode/HoverMode/EngineCutoff/MouseCaptured/ControlledSnapshotEntityId/ActiveNetworkId。
/// </summary>
public sealed class LocalControlSyncSystem
{
    private readonly EntityStore _store;
    private readonly List<Subscription> _subs = new();

    private struct Subscription
    {
        public ILocalControlReceiver Receiver;
        public LocalControlState LastSeen;
        public bool HasLastSeen;
    }

    public LocalControlSyncSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Register(ILocalControlReceiver receiver)
    {
        if (receiver is null) return;
        for (int i = 0; i < _subs.Count; i++)
            if (ReferenceEquals(_subs[i].Receiver, receiver))
                return;
        _subs.Add(new Subscription { Receiver = receiver });
    }

    public void Unregister(ILocalControlReceiver receiver)
    {
        if (receiver is null) return;
        for (int i = 0; i < _subs.Count; i++)
        {
            if (ReferenceEquals(_subs[i].Receiver, receiver))
            {
                _subs.RemoveAt(i);
                return;
            }
        }
    }

    public void Update()
    {
        for (int i = 0; i < _subs.Count; i++)
        {
            var sub = _subs[i];
            var entity = sub.Receiver.LocalEntity;
            if (entity.IsNull || !entity.TryGetComponent<LocalControlState>(out var current))
                continue;

            if (!sub.HasLastSeen)
            {
                sub.LastSeen = current;
                sub.HasLastSeen = true;
                _subs[i] = sub;
                try { sub.Receiver.OnLocalControlChanged(default, current); }
                catch (Exception ex) { GD.PrintErr($"[LocalControlSync] init throw: {ex.Message}"); }
                continue;
            }

            if (!StateChanged(sub.LastSeen, current))
                continue;

            var prev = sub.LastSeen;
            sub.LastSeen = current;
            _subs[i] = sub;
            try { sub.Receiver.OnLocalControlChanged(prev, current); }
            catch (Exception ex) { GD.PrintErr($"[LocalControlSync] receiver threw: {ex.Message}"); }
        }
    }

    private static bool StateChanged(in LocalControlState a, in LocalControlState b)
    {
        return a.Mode != b.Mode
            || a.ControlSource != b.ControlSource
            || a.SeatType != b.SeatType
            || a.SeatIndex != b.SeatIndex
            || a.InVehicle != b.InVehicle
            || a.BuildMode != b.BuildMode
            || a.HoverMode != b.HoverMode
            || a.EngineCutoff != b.EngineCutoff
            || a.MouseCaptured != b.MouseCaptured
            || a.ExternalControlLocked != b.ExternalControlLocked
            || a.ControlledSnapshotEntityId != b.ControlledSnapshotEntityId
            || a.ActiveNetworkId != b.ActiveNetworkId;
    }
}
