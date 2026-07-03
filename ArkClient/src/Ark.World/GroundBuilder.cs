using Godot;

namespace Ark.World;

/// <summary>
/// 地面构建器 — 创建游戏世界的地面 StaticBody3D + 碰撞 + 网格。
/// </summary>
public static class GroundBuilder
{
    /// <summary>
    /// 构建地面节点并添加到父节点。
    /// </summary>
    public static StaticBody3D Build(Node parent, float size = 500f)
    {
        var ground = new StaticBody3D { Name = "Ground" };

        var collision = new CollisionShape3D();
        collision.Shape = new BoxShape3D { Size = new Vector3(size, 0.2f, size) };
        collision.Position = new Vector3(0, -0.1f, 0);
        ground.AddChild(collision);

        var mesh = new MeshInstance3D();
        mesh.Mesh = new BoxMesh { Size = new Vector3(size, 0.2f, size) };
        mesh.Position = new Vector3(0, -0.1f, 0);
        var mat = new StandardMaterial3D
        {
            AlbedoColor  = new Color(0.35f, 0.45f, 0.3f),
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        ((BoxMesh)mesh.Mesh).Material = mat;
        ground.AddChild(mesh);

        parent.AddChild(ground);
        return ground;
    }
}
