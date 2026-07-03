using Godot;
using Ark.Systems.Sync;

namespace Ark.Render;

/// <summary>
/// MultiMesh 组注册器 — 创建占位 Mesh 并注册到 MultiMeshSyncSystem。
///
/// 管理 MultiMeshInstance3D 节点的创建和 Mesh 工厂。
/// </summary>
public static class MeshGroupRegistry
{
    /// <summary>
    /// 注册默认的 3 个 MultiMesh 组（NPC、Monster、Environment）。
    /// </summary>
    public static void RegisterDefaults(Node parent, MultiMeshSyncSystem syncSystem)
    {
        Register(parent, syncSystem, 0, "NpcGroup",         5000);
        Register(parent, syncSystem, 1, "MonsterGroup",     10000);
        Register(parent, syncSystem, 2, "EnvironmentGroup", 20000);
    }

    /// <summary>
    /// 注册单个 MultiMesh 组。
    /// </summary>
    public static void Register(Node parent, MultiMeshSyncSystem syncSystem, int groupId, string name, int maxInstances)
    {
        var instance = new MultiMeshInstance3D { Name = name };
        instance.Multimesh = new MultiMesh { Mesh = CreatePlaceholderMesh(groupId) };
        parent.AddChild(instance);
        syncSystem.RegisterGroup(groupId, instance, maxInstances);
    }

    /// <summary>
    /// 按组 ID 创建占位 Mesh（不同颜色/尺寸）。
    /// </summary>
    public static Mesh CreatePlaceholderMesh(int groupId) => groupId switch
    {
        0 => CreateColoredBox(new Color(0.2f, 0.8f, 0.2f), new Vector3(0.6f, 1.6f, 0.6f)),
        1 => CreateColoredBox(new Color(0.8f, 0.2f, 0.2f), new Vector3(0.8f, 1.2f, 0.8f)),
        2 => CreateColoredBox(new Color(0.3f, 0.6f, 0.3f), new Vector3(0.4f, 1.0f, 0.4f)),
        _ => CreateColoredBox(new Color(0.5f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f)),
    };

    /// <summary>
    /// 创建带颜色的 BoxMesh。
    /// </summary>
    public static BoxMesh CreateColoredBox(Color color, Vector3 size)
    {
        var mesh = new BoxMesh { Size = size };
        mesh.Material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        return mesh;
    }
}
