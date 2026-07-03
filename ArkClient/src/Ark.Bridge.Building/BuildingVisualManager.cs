using Godot;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Bridge.Features.BaseBuilding;

/// <summary>
/// 建筑视觉管理器 — 负责建筑的 Node3D 渲染、建造动画和地基变形。
/// 每个建筑对应一组 Node3D 子节点，独立于 MultiMesh 系统。
///
/// 建造动画：建筑从地面向上"生长"（Y 轴缩放 0→1），同时外观从骨架变为完整形态。
/// 地形拟合：在建筑地基下放置一块基础板，覆盖地面不平整部分。
/// </summary>
public partial class BuildingVisualManager : Node
{
    // ─── 每个建筑实体对应的视觉记录 ───
    private sealed class BuildingVisual
    {
        public Node3D           Root        = null!; // 根节点（持有所有子节点）
        public StaticBody3D     Collider    = null!; // 碰撞体
        public MeshInstance3D   Body        = null!; // 主体
        public MeshInstance3D   Roof        = null!; // 顶部装饰
        public MeshInstance3D   Foundation  = null!; // 地基贴片（地形拟合）
        public MeshInstance3D   Scaffold    = null!; // 建造脚手架（建造中显示）
        public MeshInstance3D   UpgradeBand = null!; // 升级环带
        public MeshInstance3D   UpgradeBeacon = null!; // 高等级信标
        public Node3D           UpgradePiecesRoot = null!; // 类型专属升级件根
        public MeshInstance3D   DamageShell = null!; // 战损外壳
        public MeshInstance3D   RepairAura = null!; // 修复光层
        public Node3D           DamageZonesRoot = null!; // 局部破损分区根
        public List<MeshInstance3D> DamageZones { get; } = [];
        public Node3D           DamageInstancesRoot = null!; // 持久命中簇根
        public List<Node3D>     DamageInstanceRoots { get; } = [];
        public StandardMaterial3D BodyMat   = null!;
        public StandardMaterial3D RoofMat   = null!;
        public StandardMaterial3D ScaffoldMat = null!;
        public StandardMaterial3D UpgradeBandMat = null!;
        public StandardMaterial3D UpgradeBeaconMat = null!;
        public StandardMaterial3D DamageShellMat = null!;
        public StandardMaterial3D RepairAuraMat = null!;
        public int              TypeId;
        public float            BuildTime;           // 来自 BuildingDef
        public bool             IsComplete;
        public byte             Level;
        public float            LastHealthRatio = 1f;
        public float            RepairPulse;
    }

    // entityId → visual
    private readonly Dictionary<int, BuildingVisual> _visuals = new();
    // collider instance id → entityId（反向查找，用于鼠标点击选中建筑）
    private readonly Dictionary<ulong, int> _colliderToEntity = new();
    private readonly Dictionary<int, System.Guid> _buildingOwners = new();
    private EntityStore? _store;
    private BuildingDamageEcsAuthority? _damageAuth;

    // 脚手架材质（半透明黄色线框感）
    private static StandardMaterial3D MakeScaffoldMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor  = new Color(0.9f, 0.75f, 0.2f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            GrowAmount   = 0.02f,
        };
    }

    public void Initialize(EntityStore store)
    {
        _store = store;
        _damageAuth = new BuildingDamageEcsAuthority();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                         生成 / 销毁
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 新建筑放置时调用：创建所有视觉子节点。
    /// </summary>
    public void SpawnBuildingVisual(int entityId, Vector3 worldPos, Quaternion worldRot, int typeId, byte initialProgress = 100)
    {
        var def = BuildingDef.Get(typeId);
        if (def == null) return;
        if (_visuals.ContainsKey(entityId)) return;

        var v = new BuildingVisual
        {
            TypeId    = typeId,
            BuildTime = def.Value.BuildTime,
        };

        // ─── 根节点 ───
        v.Root = new Node3D { Name = $"Building_{entityId}" };
        v.Root.Position = worldPos;
        v.Root.Quaternion = worldRot;
        AddChild(v.Root);

        // ─── 地基贴片（地形拟合：略高于地面，覆盖建筑脚印）───
        {
            var foundMesh = new BoxMesh
            {
                Size = new Vector3(def.Value.Size.X + 0.4f, 0.12f, def.Value.Size.Z + 0.4f)
            };
            var foundMat = new StandardMaterial3D
            {
                AlbedoColor = BuildingDef.FoundationColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            foundMesh.Material = foundMat;
            v.Foundation = new MeshInstance3D { Mesh = foundMesh };
            v.Foundation.Position = new Vector3(0, 0.06f, 0);   // 略高于 Y=0
            v.Root.AddChild(v.Foundation);
        }

        // ─── 主体（建造中 Y 缩放从 0 开始生长）───
        {
            var size = def.Value.Size;
            var bodyMesh = new BoxMesh { Size = size };
            v.BodyMat = new StandardMaterial3D
            {
                AlbedoColor = def.Value.BodyColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            bodyMesh.Material = v.BodyMat;
            v.Body = new MeshInstance3D { Mesh = bodyMesh };
            v.Body.Position = new Vector3(0, size.Y * 0.5f, 0);
            v.Body.Scale    = new Vector3(1f, 0f, 1f); // 从 Y=0 开始
            v.Root.AddChild(v.Body);
        }

        // ─── 屋顶装饰块 ───
        {
            var size     = def.Value.Size;
            float roofH  = Mathf.Max(size.Y * 0.15f, 0.4f);
            var roofMesh = new BoxMesh
            {
                Size = new Vector3(size.X * 0.9f, roofH, size.Z * 0.9f)
            };
            var roofMat = new StandardMaterial3D
            {
                AlbedoColor = def.Value.RoofColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            v.RoofMat = roofMat;
            roofMesh.Material = roofMat;
            v.Roof = new MeshInstance3D { Mesh = roofMesh };
            v.Roof.Position = new Vector3(0, size.Y + roofH * 0.5f, 0);
            v.Roof.Scale    = new Vector3(1f, 0f, 1f);
            v.Root.AddChild(v.Roof);
        }

        // ─── 脚手架（建造中可见）───
        {
            var size = def.Value.Size;
            var scaffMesh = new BoxMesh { Size = size * 1.06f };
            v.ScaffoldMat = MakeScaffoldMaterial();
            scaffMesh.Material = v.ScaffoldMat;
            v.Scaffold = new MeshInstance3D { Mesh = scaffMesh };
            v.Scaffold.Position = new Vector3(0, size.Y * 0.5f, 0);
            v.Root.AddChild(v.Scaffold);
        }

        // ─── 升级环带 ───
        {
            var size = def.Value.Size;
            var bandMesh = new BoxMesh { Size = new Vector3(size.X * 1.04f, Mathf.Max(size.Y * 0.12f, 0.18f), size.Z * 1.04f) };
            v.UpgradeBandMat = new StandardMaterial3D
            {
                AlbedoColor = def.Value.RoofColor.Lightened(0.15f),
                Metallic = 0.35f,
                Roughness = 0.5f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            bandMesh.Material = v.UpgradeBandMat;
            v.UpgradeBand = new MeshInstance3D { Mesh = bandMesh, Visible = false };
            v.UpgradeBand.Position = new Vector3(0, size.Y * 0.58f, 0);
            v.Root.AddChild(v.UpgradeBand);
        }

        // ─── 高等级信标 ───
        {
            var beaconMesh = new CylinderMesh
            {
                TopRadius = 0.08f,
                BottomRadius = 0.12f,
                Height = Mathf.Max(def.Value.Size.Y * 0.35f, 0.8f),
            };
            v.UpgradeBeaconMat = new StandardMaterial3D
            {
                AlbedoColor = def.Value.RoofColor.Lightened(0.3f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = def.Value.RoofColor.Lightened(0.45f),
                EmissionEnergyMultiplier = 1.6f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            beaconMesh.Material = v.UpgradeBeaconMat;
            v.UpgradeBeacon = new MeshInstance3D { Mesh = beaconMesh, Visible = false };
            v.UpgradeBeacon.Position = new Vector3(0, def.Value.Size.Y + Mathf.Max(def.Value.Size.Y * 0.25f, 0.45f), 0);
            v.Root.AddChild(v.UpgradeBeacon);
        }

        v.UpgradePiecesRoot = new Node3D { Name = "UpgradePieces" };
        v.Root.AddChild(v.UpgradePiecesRoot);
        CreateTypeSpecificUpgradePieces(v, def.Value);
        CreateDamageVisuals(v, def.Value);

        // ─── 碰撞体（StaticBody3D + BoxShape3D）───
        {
            var size = def.Value.Size;
            v.Collider = new StaticBody3D { Name = $"Collider_{entityId}" };
            v.Collider.CollisionLayer = 1; // Layer 1 = 环境/建筑
            v.Collider.CollisionMask  = 0; // 不需要检测其他物体

            var shape = new CollisionShape3D();
            shape.Shape = new BoxShape3D { Size = size };
            shape.Position = new Vector3(0, size.Y * 0.5f, 0);
            v.Collider.AddChild(shape);
            v.Root.AddChild(v.Collider);
        }

        _visuals[entityId] = v;
        _colliderToEntity[v.Collider.GetInstanceId()] = entityId;

        ApplyConstructionVisual(v, initialProgress);
    }

    /// <summary>
    /// 建筑被拆除时：移除视觉节点。
    /// </summary>
    public void RemoveBuildingVisual(int entityId)
    {
        if (!_visuals.TryGetValue(entityId, out var v)) return;
        _colliderToEntity.Remove(v.Collider.GetInstanceId());
        _buildingOwners.Remove(entityId);
        v.Root.QueueFree();
        _visuals.Remove(entityId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                         每帧更新（建造动画）
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        if (_store == null) return;

        var query = _store.Query<Building, WorldPosition, WorldRotation, Health>().AllTags(Tags.Get<BuildingTag>());

        foreach (var chunk in query.Chunks)
        {
            var buildings = chunk.Chunk1;
            var positions = chunk.Chunk2;
            var rotations = chunk.Chunk3;
            var healths = chunk.Chunk4;
            for (int i = 0; i < chunk.Entities.Length; i++)
            {
                ref readonly var b     = ref buildings.Span[i];
                ref readonly var pos   = ref positions.Span[i];
                ref readonly var rot   = ref rotations.Span[i];
                ref readonly var health = ref healths.Span[i];
                int eid                = chunk.Entities[i];

                if (!_visuals.TryGetValue(eid, out var v)) continue;
                v.Root.Position = new Vector3(pos.X, pos.Y, pos.Z);
                v.Root.Quaternion = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
                ApplyConstructionVisual(v, b.ConstructionProgress);
                ApplyUpgradeVisual(v, b.Level, b.ConstructionProgress);
                BuildingDamageState? persistentDamage = null;
                BuildingDamageInstanceState? damageInstances = null;
                BuildingDamageFeedbackState? damageFeedback = null;
                var entity = _store.GetEntityById(eid);
                if (!entity.IsNull && entity.TryGetComponent<BuildingDamageState>(out var persistent))
                    persistentDamage = persistent;
                if (!entity.IsNull && entity.TryGetComponent<BuildingDamageInstanceState>(out var instances))
                    damageInstances = instances;
                if (!entity.IsNull && entity.TryGetComponent<BuildingDamageFeedbackState>(out var feedback))
                    damageFeedback = feedback;
                ApplyDamageVisual(entity, v, health, b.ConstructionProgress, (float)delta, persistentDamage, damageInstances, damageFeedback, _damageAuth);
            }
        }
    }

    private static void ApplyConstructionVisual(BuildingVisual v, byte constructionProgress)
    {
        float t = constructionProgress / 100f;
        float smoothT = Mathf.SmoothStep(0f, 1f, t);
        v.Body.Scale = new Vector3(1f, smoothT, 1f);
        v.Roof.Scale = new Vector3(1f, smoothT, 1f);

        float scaffAlpha = t < 0.95f ? 0.55f * (1f - t * 0.5f) : Mathf.Lerp(0.55f, 0f, (t - 0.95f) / 0.05f);
        v.ScaffoldMat.AlbedoColor = v.ScaffoldMat.AlbedoColor with { A = scaffAlpha };
        v.Scaffold.Visible = scaffAlpha > 0.01f;
        v.IsComplete = t >= 1f;
        v.BodyMat.ShadingMode = v.IsComplete
            ? BaseMaterial3D.ShadingModeEnum.PerPixel
            : BaseMaterial3D.ShadingModeEnum.Unshaded;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                         碰撞体 → 实体 ID 查找
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 通过碰撞体（StaticBody3D）反查建筑实体 ID。
    /// 返回 true 表示找到了对应的建筑。
    /// </summary>
    public bool TryGetEntityIdByCollider(GodotObject collider, out int entityId, out int typeId, out bool isComplete)
    {
        entityId = 0;
        typeId = 0;
        isComplete = false;

        if (collider is CollisionObject3D co &&
            _colliderToEntity.TryGetValue(co.GetInstanceId(), out entityId) &&
            _visuals.TryGetValue(entityId, out var v))
        {
            typeId = v.TypeId;
            isComplete = v.IsComplete;
            return true;
        }
        return false;
    }

    /// <summary>获取建筑根节点的世界位置。</summary>
    public Vector3 GetBuildingWorldPos(int entityId)
    {
        return _visuals.TryGetValue(entityId, out var v) ? v.Root.Position : Vector3.Zero;
    }

    public void SetBuildingOwner(int entityId, System.Guid ownerId)
    {
        _buildingOwners[entityId] = ownerId;
    }

    public bool TryGetBuildingOwner(int entityId, out System.Guid ownerId)
    {
        if (_store != null)
        {
            var entity = _store.GetEntityById(entityId);
            if (!entity.IsNull && entity.TryGetComponent<RemoteOwnershipState>(out var ownership) && ownership.OwnerId != System.Guid.Empty)
            {
                ownerId = ownership.OwnerId;
                return true;
            }
        }

        return _buildingOwners.TryGetValue(entityId, out ownerId);
    }

    public override void _ExitTree()
    {
        // 节点会随场景树一起清理，不需要手动 QueueFree
        _visuals.Clear();
        _colliderToEntity.Clear();
        _buildingOwners.Clear();
    }
}
