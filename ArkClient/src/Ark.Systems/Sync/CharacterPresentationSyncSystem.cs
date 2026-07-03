using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;

namespace Ark.Systems.Sync;

/// <summary>
/// 角色表现层数据推送接收方接口。
/// 由 Godot 表现节点（CharacterBody3D 等）实现，<see cref="CharacterPresentationSyncSystem"/> 主动推送 ECS 缓存数据。
/// </summary>
public interface ICharacterPresentationReceiver
{
    /// <summary>角色绑定的 ECS 实体（必须有效）。</summary>
    Entity Entity { get; }

    /// <summary>每帧推送一次。组件缺失时对应字段保持默认值。</summary>
    void OnPresentationPushed(in CharacterPresentationFrame frame);
}

/// <summary>
/// 单帧推送给角色表现节点的快照（值类型，避免分配）。
/// </summary>
public readonly struct CharacterPresentationFrame
{
    public readonly bool HasWorldPose;
    public readonly WorldPosition WorldPosition;
    public readonly WorldRotation WorldRotation;

    public readonly bool HasSnapshot;
    public readonly RemoteSnapshotState Snapshot;

    public readonly bool HasEntityState;
    public readonly RemoteEntityState EntityState;

    public readonly bool HasAnimationState;
    public readonly RemoteAnimationState AnimationState;

    public CharacterPresentationFrame(
        bool hasWorldPose, WorldPosition pos, WorldRotation rot,
        bool hasSnapshot, RemoteSnapshotState snap,
        bool hasEntityState, RemoteEntityState ent,
        bool hasAnimationState, RemoteAnimationState anim)
    {
        HasWorldPose = hasWorldPose;
        WorldPosition = pos;
        WorldRotation = rot;
        HasSnapshot = hasSnapshot;
        Snapshot = snap;
        HasEntityState = hasEntityState;
        EntityState = ent;
        HasAnimationState = hasAnimationState;
        AnimationState = anim;
    }
}

/// <summary>
/// 角色表现层 Sync System — 单向地从 ECS 缓存推送给表现节点。
/// 取代 Node 自身每帧 <c>_entity.TryGetComponent</c> 拉取的反向耦合。
/// 用法：表现节点调用 <see cref="Register"/> 注册，析构 / _ExitTree 时调用 <see cref="Unregister"/>。
/// </summary>
public sealed class CharacterPresentationSyncSystem
{
    private readonly EntityStore _store;
    private readonly List<ICharacterPresentationReceiver> _receivers = new();

    public CharacterPresentationSyncSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Register(ICharacterPresentationReceiver receiver)
    {
        if (receiver is null) return;
        if (!_receivers.Contains(receiver))
            _receivers.Add(receiver);
    }

    public void Unregister(ICharacterPresentationReceiver receiver)
    {
        if (receiver is null) return;
        _receivers.Remove(receiver);
    }

    /// <summary>每帧调度一次（主线程）。</summary>
    public void Update()
    {
        for (int i = 0; i < _receivers.Count; i++)
        {
            var receiver = _receivers[i];
            var entity = receiver.Entity;
            if (entity.IsNull)
                continue;

            bool hasPos = entity.TryGetComponent<WorldPosition>(out var pos);
            bool hasRot = entity.TryGetComponent<WorldRotation>(out var rot);
            bool hasSnap = entity.TryGetComponent<RemoteSnapshotState>(out var snap);
            bool hasEnt = entity.TryGetComponent<RemoteEntityState>(out var ent);
            bool hasAnim = entity.TryGetComponent<RemoteAnimationState>(out var anim);

            var frame = new CharacterPresentationFrame(
                hasPos && hasRot, pos, rot,
                hasSnap, snap,
                hasEnt, ent,
                hasAnim, anim);

            try
            {
                receiver.OnPresentationPushed(frame);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CharacterPresentationSync] receiver threw: {ex.Message}");
            }
        }
    }
}
