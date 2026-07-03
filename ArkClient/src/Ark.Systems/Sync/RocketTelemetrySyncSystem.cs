using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;

namespace Ark.Systems.Sync;

/// <summary>
/// 火箭遥测推送接收方接口（Phase 3 取代 Bootstrap.UpdateNetworkRocketTelemetry 直接 TryGetComponent）。
/// 接收方提供当前要追踪的火箭 ECS 实体（IsNull 表示当前无追踪目标，跳过）。
/// </summary>
public interface IRocketTelemetryReceiver
{
    Entity RocketEntity { get; }
    void OnRocketTelemetryPushed(in RocketTelemetryFrame frame);
}

/// <summary>
/// 火箭权威 ECS 状态聚合帧 — 纯数据。客户端预测覆写由调用方在 receiver 内自行融合。
/// </summary>
public readonly struct RocketTelemetryFrame
{
    public readonly bool HasPosition;
    public readonly WorldPosition Position;

    public readonly bool HasRotation;
    public readonly WorldRotation Rotation;

    public readonly bool HasSnapshot;
    public readonly RemoteSnapshotState Snapshot;

    public RocketTelemetryFrame(
        bool hasPosition, WorldPosition position,
        bool hasRotation, WorldRotation rotation,
        bool hasSnapshot, RemoteSnapshotState snapshot)
    {
        HasPosition = hasPosition;
        Position = position;
        HasRotation = hasRotation;
        Rotation = rotation;
        HasSnapshot = hasSnapshot;
        Snapshot = snapshot;
    }

    public bool HasAuthoritativePose => HasPosition && HasRotation && HasSnapshot;
}

/// <summary>
/// 火箭遥测 Sync System — 推送权威 ECS 位姿/快照给注册的发射控制面板等表现层。
/// </summary>
public sealed class RocketTelemetrySyncSystem
{
    private readonly EntityStore _store;
    private readonly List<IRocketTelemetryReceiver> _receivers = new();

    public RocketTelemetrySyncSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Register(IRocketTelemetryReceiver receiver)
    {
        if (receiver is null) return;
        if (!_receivers.Contains(receiver))
            _receivers.Add(receiver);
    }

    public void Unregister(IRocketTelemetryReceiver receiver)
    {
        if (receiver is null) return;
        _receivers.Remove(receiver);
    }

    public void Update()
    {
        for (int i = 0; i < _receivers.Count; i++)
        {
            var receiver = _receivers[i];
            var entity = receiver.RocketEntity;
            if (entity.IsNull)
                continue;

            bool hasPosition = entity.TryGetComponent<WorldPosition>(out var position);
            bool hasRotation = entity.TryGetComponent<WorldRotation>(out var rotation);
            bool hasSnapshot = entity.TryGetComponent<RemoteSnapshotState>(out var snapshot);

            var frame = new RocketTelemetryFrame(
                hasPosition, position,
                hasRotation, rotation,
                hasSnapshot, snapshot);

            try
            {
                receiver.OnRocketTelemetryPushed(frame);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[RocketTelemetrySync] receiver threw: {ex.Message}");
            }
        }
    }
}
