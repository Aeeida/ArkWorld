using Friflo.Engine.ECS;
using Godot;

namespace Ark.Ecs.Bridge;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║               Godot 桥接组件 — 持有 Godot 对象引用（仅主线程访问）                  ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Node3D 引用桥接 — 用于近景可交互实体。
/// ⚠️ 只能在主线程访问！
/// </summary>
public struct NodeRef : IComponent
{
    private ulong _instanceId;

    /// <summary>设置引用的 Node</summary>
    public void Set(Node3D node)
    {
        _instanceId = node?.GetInstanceId() ?? 0;
    }

    /// <summary>获取 Node（自动验证存活性）</summary>
    public Node3D? Get()
    {
        if (_instanceId == 0) return null;
        var obj = GodotObject.InstanceFromId(_instanceId);
        if (obj is Node3D node && GodotObject.IsInstanceValid(node))
            return node;
        _instanceId = 0; // 已释放，清空
        return null;
    }

    /// <summary>引用是否有效</summary>
    public bool IsValid => _instanceId != 0 &&
        GodotObject.IsInstanceValid(GodotObject.InstanceFromId(_instanceId));

    /// <summary>清空引用</summary>
    public void Clear() => _instanceId = 0;
}

/// <summary>
/// AnimationTree 引用 — 用于骨骼动画角色。
/// </summary>
public struct AnimationRef : IComponent
{
    private ulong _treeInstanceId;
    private ulong _playerInstanceId;

    public void SetTree(AnimationTree tree)
    {
        _treeInstanceId = tree?.GetInstanceId() ?? 0;
    }

    public void SetPlayer(AnimationPlayer player)
    {
        _playerInstanceId = player?.GetInstanceId() ?? 0;
    }

    public AnimationTree? GetTree()
    {
        if (_treeInstanceId == 0) return null;
        var obj = GodotObject.InstanceFromId(_treeInstanceId);
        return obj as AnimationTree;
    }

    public AnimationPlayer? GetPlayer()
    {
        if (_playerInstanceId == 0) return null;
        var obj = GodotObject.InstanceFromId(_playerInstanceId);
        return obj as AnimationPlayer;
    }
}

/// <summary>
/// 物理空间状态引用 — 用于物理查询。
/// </summary>
public struct PhysicsSpaceRef : IComponent
{
    private ulong _worldInstanceId;

    public void Set(World3D world)
    {
        _worldInstanceId = world?.GetInstanceId() ?? 0;
    }

    public PhysicsDirectSpaceState3D? GetSpaceState()
    {
        if (_worldInstanceId == 0) return null;
        var obj = GodotObject.InstanceFromId(_worldInstanceId);
        return (obj as World3D)?.DirectSpaceState;
    }
}

/// <summary>
/// 音频播放器引用。
/// </summary>
public struct AudioRef : IComponent
{
    private ulong _playerInstanceId;

    public void Set(AudioStreamPlayer3D player)
    {
        _playerInstanceId = player?.GetInstanceId() ?? 0;
    }

    public AudioStreamPlayer3D? Get()
    {
        if (_playerInstanceId == 0) return null;
        var obj = GodotObject.InstanceFromId(_playerInstanceId);
        return obj as AudioStreamPlayer3D;
    }
}

/// <summary>
/// 通用 Godot Rid 引用（用于 RenderingServer / PhysicsServer 资源）。
/// 注意：Godot 4.x 中 Rid 无法直接从 ulong 创建，需要通过 RidCache 管理。
/// </summary>
public struct RidRef : IComponent
{
    /// <summary>Rid 的唯一 Id（用于查找）</summary>
    public ulong RidValue;

    /// <summary>
    /// 获取 Rid。需要先通过 RidCache.Register 注册。
    /// </summary>
    public Rid GetRid() => RidCache.Get(RidValue);

    /// <summary>
    /// 从 Rid 创建引用并自动注册到缓存。
    /// </summary>
    public static RidRef From(Rid rid)
    {
        RidCache.Register(rid);
        return new() { RidValue = rid.Id };
    }

    public bool IsValid => RidValue != 0 && RidCache.Contains(RidValue);
}

/// <summary>
/// Rid 缓存 — 解决 Godot 4.x 中无法从 ulong 创建 Rid 的问题。
/// </summary>
public static class RidCache
{
    private static readonly System.Collections.Generic.Dictionary<ulong, Rid> _cache = new();

    public static void Register(Rid rid)
    {
        if (rid.Id != 0)
            _cache[rid.Id] = rid;
    }

    public static Rid Get(ulong id) => _cache.TryGetValue(id, out var rid) ? rid : default;

    public static bool Contains(ulong id) => _cache.ContainsKey(id);

    public static void Remove(ulong id) => _cache.Remove(id);

    public static void Clear() => _cache.Clear();
}
