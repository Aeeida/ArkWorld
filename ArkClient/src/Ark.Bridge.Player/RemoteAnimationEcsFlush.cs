using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Analyzers.Attributes;
using Ark.Ecs.Components;

namespace Ark.Bridge.Player;

/// <summary>
/// 远端玩家动画/反馈状态投影到 ECS 的授权写入器。
/// 拆出独立类后，Godot Node 端 (<see cref="RemotePlayerBridge"/>) 不再直接调用
/// <c>Entity.AddComponent</c>，满足 ARK005 / ECS005 ECS-FIRST 约束。
/// </summary>
[EcsAuthorityBridge]
internal sealed class RemoteAnimationEcsFlush
{
    private readonly EntityStore _store;

    public RemoteAnimationEcsFlush(EntityStore store)
    {
        _store = store;
    }

    public void Flush(
        Dictionary<int, RemoteAnimationState> pendingAnimation,
        Dictionary<int, RemotePresentationFeedbackState> pendingFeedback)
    {
        foreach (var (ecsEntityId, animation) in pendingAnimation)
        {
            var entity = _store.GetEntityById(ecsEntityId);
            if (!entity.IsNull)
                entity.AddComponent(animation);
        }
        pendingAnimation.Clear();

        foreach (var (ecsEntityId, feedback) in pendingFeedback)
        {
            var entity = _store.GetEntityById(ecsEntityId);
            if (!entity.IsNull)
                entity.AddComponent(feedback);
        }
        pendingFeedback.Clear();
    }
}
