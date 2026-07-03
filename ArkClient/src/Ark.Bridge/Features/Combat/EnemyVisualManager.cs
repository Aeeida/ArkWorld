using System.Collections.Generic;
using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Bridge.Features.Combat;

/// <summary>
/// 敌人视觉管理器 — 管理敌方小队的 Godot Node3D 节点。
///
/// 职责：
///   • 为每个敌方 ECS 实体创建角色节点（身体 + 头部 + 碰撞体）
///   • 委托 WeaponVisualSystem 挂载武器模型
///   • 每帧将 ECS WorldPosition 同步到 Node3D
///   • 死亡实体执行"倒地"视觉效果
/// </summary>
public partial class EnemyVisualManager : Node
{
    private EntityStore? _store;
    private WeaponVisualSystem? _weaponVisuals;
    private readonly List<(int eid, Node3D node)> _nodes = new();

    public void Initialize(EntityStore store, WeaponVisualSystem weaponVisuals)
    {
        _store         = store;
        _weaponVisuals = weaponVisuals;
    }

    /// <summary>
    /// 为一批敌方实体生成可视化角色节点（身体 + 碰撞体 + 武器）。
    /// </summary>
    public void SpawnEnemyNodes(IReadOnlyList<int> entityIds, int[] weaponDefIds)
    {
        if (_store == null) return;

        for (int i = 0; i < entityIds.Count; i++)
        {
            int eid = entityIds[i];
            var entity = _store.GetEntityById(eid);
            if (entity.IsNull) continue;
            if (!entity.TryGetComponent<WorldPosition>(out var pos)) continue;

            var root = new Node3D { Name = $"Enemy_{eid}" };
            root.Position = new Vector3(pos.X, pos.Y, pos.Z);
            AddChild(root);

            // 身体
            var bodyMesh = new BoxMesh { Size = new Vector3(0.6f, 1.7f, 0.4f) };
            bodyMesh.Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.7f, 0.15f, 0.15f),
                Metallic = 0.1f,
                Roughness = 0.8f,
            };
            var body = new MeshInstance3D { Mesh = bodyMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            body.Position = new Vector3(0, 0.85f, 0);
            root.AddChild(body);

            // 头部
            var headMesh = new SphereMesh { Radius = 0.2f, Height = 0.4f };
            headMesh.Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.55f, 0.1f, 0.1f),
                Metallic = 0.1f,
                Roughness = 0.8f,
            };
            var head = new MeshInstance3D { Mesh = headMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            head.Position = new Vector3(0, 1.9f, 0);
            root.AddChild(head);

            // 碰撞体
            var collider = new StaticBody3D { Name = $"EnemyCollider_{eid}" };
            collider.CollisionLayer = 4;
            collider.CollisionMask  = 0;
            var collShape = new CollisionShape3D();
            collShape.Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.7f };
            collShape.Position = new Vector3(0, 0.85f, 0);
            collider.AddChild(collShape);
            root.AddChild(collider);

            // 武器挂载
            if (_weaponVisuals != null)
            {
                int weaponDefId = weaponDefIds[i % weaponDefIds.Length];
                byte category = weaponDefId switch
                {
                    21 => 2,
                    50 => 5,
                    20 => 2,
                    _ => 2,
                };
                _weaponVisuals.AttachWeaponToCharacter(eid, root, category);
            }

            _nodes.Add((eid, root));
        }
    }

    /// <summary>每帧同步 ECS 位置到 Node3D（含死亡倒地）。</summary>
    public void SyncPositions()
    {
        if (_store == null) return;

        for (int i = _nodes.Count - 1; i >= 0; i--)
        {
            var (eid, node) = _nodes[i];
            var entity = _store.GetEntityById(eid);

            if (entity.IsNull || entity.Tags.Has<Dead>())
            {
                if (!entity.IsNull && entity.Tags.Has<Dead>() && node.Visible)
                {
                    if (entity.TryGetComponent<WorldPosition>(out var dp))
                        node.Position = new Vector3(dp.X, dp.Y, dp.Z);
                    node.RotationDegrees = new Vector3(90, node.RotationDegrees.Y, 0);
                }
                continue;
            }

            if (!entity.TryGetComponent<WorldPosition>(out var pos)) continue;
            node.Position = new Vector3(pos.X, pos.Y, pos.Z);
        }
    }
}
