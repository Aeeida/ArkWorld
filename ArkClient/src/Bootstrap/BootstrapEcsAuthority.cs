using Friflo.Engine.ECS;

namespace Ark;

/// <summary>
/// 非 Node 的 ECS 结构变更授权门面：GameBootstrap 通过它进行所有
/// AddComponent / RemoveComponent / AddTag / RemoveTag / CreateEntity / DeleteEntity
/// 操作，避免 Node 派生类型直接触碰 Friflo 结构变更 API（满足 ARK005）。
/// </summary>
internal sealed class BootstrapEcsAuthority
{
    private readonly EntityStore _store;

    public BootstrapEcsAuthority(EntityStore store)
    {
        _store = store;
    }

    public EntityStore Store => _store;

    public void Write<T>(Entity entity, in T component) where T : struct, IComponent
    {
        if (entity.IsNull) return;
        entity.AddComponent(component);
    }

    public void Remove<T>(Entity entity) where T : struct, IComponent
    {
        if (entity.IsNull) return;
        entity.RemoveComponent<T>();
    }

    public void AddTag<T>(Entity entity) where T : struct, ITag
    {
        if (entity.IsNull) return;
        entity.AddTag<T>();
    }

    public void RemoveTag<T>(Entity entity) where T : struct, ITag
    {
        if (entity.IsNull) return;
        entity.RemoveTag<T>();
    }

    public Entity CreateRequest<T>(in T component) where T : struct, IComponent
    {
        var entity = _store.CreateEntity();
        entity.AddComponent(component);
        return entity;
    }

    public void DeleteById(int entityId)
    {
        var entity = _store.GetEntityById(entityId);
        if (!entity.IsNull)
            entity.DeleteEntity();
    }
}
