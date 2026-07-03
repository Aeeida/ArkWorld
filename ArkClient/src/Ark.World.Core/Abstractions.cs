namespace Ark.World.Core;

/// <summary>
/// 世界层接口 — 多层叠加地形系统的每一层实现此接口。
///
/// 8 层叠加结构：
///   1. 宏观高度场 (Heightfield)
///   2. 体素密度 (Voxel) — 预留
///   3. 表面网格 (SurfaceMesh)
///   4. 材质细节 (Material)
///   5. 生态装饰 (Ecology)
///   6. 流体粒子 (Fluid) — 预留
///   7. 大气光照 (Atmosphere)
///   8. 事件历史 (EventHistory)
/// </summary>
public interface IWorldLayer
{
    /// <summary>层的唯一标识。</summary>
    string LayerId { get; }

    /// <summary>层的优先级（越小越先处理）。</summary>
    int Priority { get; }

    /// <summary>层是否启用。</summary>
    bool Enabled { get; set; }

    /// <summary>初始化层（绑定到世界种子）。</summary>
    void Initialize(WorldSeed seed);

    /// <summary>每帧更新。</summary>
    void Update(float deltaTime);

    /// <summary>清理资源。</summary>
    void Shutdown();
}

/// <summary>
/// 世界子系统接口 — 跨层的功能系统（天气、日夜循环等）。
/// </summary>
public interface IWorldSystem
{
    /// <summary>系统唯一标识。</summary>
    string SystemId { get; }

    /// <summary>初始化。</summary>
    void Initialize(WorldSeed seed);

    /// <summary>每帧更新。</summary>
    void Update(float deltaTime);

    /// <summary>清理。</summary>
    void Shutdown();
}

/// <summary>
/// 可视化世界层接口 — 具有 Godot 场景节点的层。
/// </summary>
public interface IVisualWorldLayer : IWorldLayer
{
    /// <summary>获取该层的根 Godot 节点（由 WorldManager 挂载到场景树）。</summary>
    Godot.Node3D? SceneRoot { get; }
}
