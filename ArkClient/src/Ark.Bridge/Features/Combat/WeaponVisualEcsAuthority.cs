using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.Bridge.Features.Combat;

/// <summary>
/// 非 Node 的 ECS 结构变更授权门面：WeaponVisualSystem 持有它，所有
/// AddComponent / DeleteEntity 写操作通过它进行（满足 ARK005）。
/// </summary>
internal sealed class WeaponVisualEcsAuthority
{
    private readonly EntityStore _store;

    public WeaponVisualEcsAuthority(EntityStore store)
    {
        _store = store;
    }

    public void FlushFeedback(
        IReadOnlyDictionary<int, RemotePresentationFeedbackState> presentation,
        IReadOnlyDictionary<int, BuildingDamageFeedbackState> building)
    {
        foreach (var (entityId, feedback) in presentation)
        {
            var entity = _store.GetEntityById(entityId);
            if (!entity.IsNull)
                entity.AddComponent(feedback);
        }

        foreach (var (entityId, feedback) in building)
        {
            var entity = _store.GetEntityById(entityId);
            if (!entity.IsNull)
                entity.AddComponent(feedback);
        }
    }

    public void DeleteEntities(IReadOnlyList<int> entityIds)
    {
        foreach (var entityId in entityIds)
        {
            var entity = _store.GetEntityById(entityId);
            if (!entity.IsNull)
                entity.DeleteEntity();
        }
    }
}
