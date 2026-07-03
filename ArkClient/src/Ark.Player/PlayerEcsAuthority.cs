using Friflo.Engine.ECS;

namespace Ark.Player;

/// <summary>
/// 非 Node 的 ECS 结构变更授权门面：TpsPlayerController（CharacterBody3D 派生）
/// 通过它进行所有 AddComponent / RemoveComponent / AddTag / RemoveTag / CreateEntity
/// 写操作，使 Node 自身不再直接触碰 Friflo 的结构变更 API（满足 ARK005）。
///
/// 注意：该类自身不是 Node，因此可以安全地调用 Friflo 结构变更方法。
/// </summary>
internal sealed class PlayerEcsAuthority
{
    private readonly EntityStore _store;

    public PlayerEcsAuthority(EntityStore store)
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

    /// <summary>
    /// 创建一个新实体并附加单个组件（用于网络请求/命令实体）。
    /// </summary>
    public Entity CreateRequest<T>(in T component) where T : struct, IComponent
    {
        var entity = _store.CreateEntity();
        entity.AddComponent(component);
        return entity;
    }

    /// <summary>
    /// 在已存在的命令实体上写入命令组件。
    /// </summary>
    public void WriteCommand<T>(Entity commandEntity, in T component) where T : struct, IComponent
    {
        if (commandEntity.IsNull) return;
        commandEntity.AddComponent(component);
    }
}
