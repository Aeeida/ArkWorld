using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;
using Ark.Services;
using Ark.Services.Remote;
using Ark.Shared.Data;

namespace Ark.Bridge.Player;

/// <summary>
/// 远端玩家桥接器 — 将 <see cref="RemoteGameWorld"/> 中的远端实体桥接到 Godot 场景树。
///
/// 职责：
/// 1. 从远端世界快照缓存创建或移除远端展示节点
/// 2. 为每个远端玩家创建一个可见 Node3D（含简易胶囊体 Mesh）
/// 3. 每帧从 <see cref="RemoteGameWorld"/> 同步位置/朝向到场景节点
///
/// 使用方法：
///   在 GameBootstrap 中调用 <see cref="GameServices.CreateRemotePlayerBridge"/>
///   或手动 new RemotePlayerBridge(remoteWorld, localPlayerId) 并 AddChild。
/// </summary>
public sealed partial class RemotePlayerBridge : Node3D
{
    private EntityStore? _store;
    private RemoteAnimationEcsFlush? _animationEcsFlush;
    private RemoteWorldEcsCacheSystem? _remoteWorldEcsCache;
    private Action<int, System.Numerics.Vector3, Quaternion, int, byte>? _spawnTypedBuilding;
    private Action<int>? _removeBuilding;
    private Action<int, Node3D, byte>? _attachWeapon;
    private Action<int>? _detachWeapon;
    private const byte DefaultStarterWeaponCategory = 2;

    // snapshotEntityId → 场景节点
    private readonly Dictionary<int, Node3D> _remoteNodes = new();
    private readonly HashSet<int> _spawnedBuildings = new();
    private readonly HashSet<int> _seenSnapshotIds = new();
    private readonly List<int> _staleSnapshotIds = new();

    /// <summary>远端玩家实体产生时触发（entityId, networkId）。</summary>
    public event Action<int, System.Guid>? OnRemotePlayerSpawned;

    /// <summary>远端玩家实体移除时触发（entityId）。</summary>
    public event Action<int>? OnRemotePlayerRemoved;

    public int RemotePlayerCount => _remoteNodes.Count;

    public bool TryGetNode(int entityId, out Node3D? node) => _remoteNodes.TryGetValue(entityId, out node);

    public void Initialize(
        EntityStore store,
        RemoteWorldEcsCacheSystem? remoteWorldEcsCache,
        System.Guid localPlayerId,
        Action<int, System.Numerics.Vector3, Quaternion, int, byte>? spawnTypedBuilding,
        Action<int>? removeBuilding,
        Action<int, Node3D, byte>? attachWeapon,
        Action<int>? detachWeapon)
    {
        _store = store;
        _remoteWorldEcsCache = remoteWorldEcsCache;
        _spawnTypedBuilding = spawnTypedBuilding;
        _removeBuilding = removeBuilding;
        _attachWeapon = attachWeapon;
        _detachWeapon = detachWeapon;

        _animationEcsFlush = new RemoteAnimationEcsFlush(store);

        GD.Print($"[RemotePlayerBridge] Initialized, localPlayer={localPlayerId}");
    }

    public override void _ExitTree()
    {
        foreach (var node in _remoteNodes.Values)
            node.QueueFree();
        _remoteNodes.Clear();
        _spawnedBuildings.Clear();
    }

    public override void _Process(double delta)
    {
        if (_store == null) return;

        _seenSnapshotIds.Clear();

        var query = _store.Query<RemoteEntityState, WorldPosition, WorldRotation>();
        foreach (var chunk in query.Chunks)
        {
            var states = chunk.Chunk1;
            var positions = chunk.Chunk2;
            var rotations = chunk.Chunk3;

            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var state = ref states.Span[i];
                if (state.IsLocalPlayer != 0)
                    continue;

                ref readonly var pos = ref positions.Span[i];
                ref readonly var rot = ref rotations.Span[i];
                int snapshotEntityId = state.SnapshotEntityId;
                _seenSnapshotIds.Add(snapshotEntityId);

                if ((EntityType)state.EntityType == EntityType.Building && _spawnTypedBuilding is not null && state.TypeId > 0)
                {
                    if (_spawnedBuildings.Add(snapshotEntityId))
                    {
                        byte initialProgress = 100;
                        var entity = _store.GetEntityById(chunk.Entities[i]);
                        if (!entity.IsNull && entity.TryGetComponent<Building>(out var building))
                            initialProgress = building.ConstructionProgress;

                        _spawnTypedBuilding(
                            snapshotEntityId,
                            new System.Numerics.Vector3(pos.X, pos.Y, pos.Z),
                            NormalizeQuaternion(new Quaternion(rot.X, rot.Y, rot.Z, rot.W)),
                            state.TypeId,
                            initialProgress);
                        GD.Print($"[RemotePlayerBridge] Spawned typed building: id={snapshotEntityId}, typeId={state.TypeId}");
                    }
                    continue;
                }

                if (!_remoteNodes.TryGetValue(snapshotEntityId, out var node))
                {
                    node = CreateRemoteEntityNode(state, pos, rot);
                    _remoteNodes[snapshotEntityId] = node;
                    CallDeferred(MethodName.AddChild, node);

                    var entityType = (EntityType)state.EntityType;
                    if (entityType == EntityType.Player || entityType == EntityType.RemotePlayer)
                    {
                        _attachWeapon?.Invoke(snapshotEntityId, node, ResolveWeaponCategory(snapshotEntityId));
                        OnRemotePlayerSpawned?.Invoke(snapshotEntityId, state.NetworkId);
                    }

                    GD.Print($"[RemotePlayerBridge] Spawned remote entity: id={snapshotEntityId}, networkId={state.NetworkId}, type={entityType}");
                }

                SyncNode(chunk.Entities[i], snapshotEntityId, node, state, pos, rot, delta);
            }
        }

        _staleSnapshotIds.Clear();
        foreach (var snapshotEntityId in _remoteNodes.Keys)
        {
            if (!_seenSnapshotIds.Contains(snapshotEntityId))
                _staleSnapshotIds.Add(snapshotEntityId);
        }
        foreach (var snapshotEntityId in _spawnedBuildings)
        {
            if (!_seenSnapshotIds.Contains(snapshotEntityId) && !_staleSnapshotIds.Contains(snapshotEntityId))
                _staleSnapshotIds.Add(snapshotEntityId);
        }

        foreach (var snapshotEntityId in _staleSnapshotIds)
            RemovePresentation(snapshotEntityId);

        FlushDeferredEcsStateWrites();
        PumpAnimationStreaming(delta);
    }

    /// <summary>
    /// 创建远端玩家的简易可视节点 — 胶囊体 + 名字标签。
    /// GameBootstrap 可通过 OnRemotePlayerSpawned 事件替换为完整角色模型。
    /// </summary>
    private void SyncNode(int ecsEntityId, int snapshotEntityId, Node3D node, RemoteEntityState state, WorldPosition pos, WorldRotation rot, double delta)
    {
        if (!IsInstanceValid(node) || !node.IsInsideTree())
            return;

        if (TrySyncSeatAttachedPresentation(ecsEntityId, snapshotEntityId, node, state, rot, delta))
            return;

        EnsurePresentationParent(node, this);
        ApplyStandingPresentationPose(node, delta);
        ApplyAnimationStatePose(ecsEntityId, node, delta);
        ApplyPresentationFeedback(ecsEntityId, node, delta);

        var targetPos = new Vector3(pos.X, pos.Y, pos.Z);
        if (state.AttachToTerrain != 0 && GameServices.Terrain is not null)
            targetPos.Y = GameServices.Terrain.SampleHeight(targetPos.X, targetPos.Z);
        node.Position = node.Position.Lerp(targetPos, (float)(10.0 * delta));

        var currentRotation = NormalizeQuaternion(node.Quaternion);
        var targetRotation = NormalizeQuaternion(new Quaternion(rot.X, rot.Y, rot.Z, rot.W));
        node.Quaternion = currentRotation.Slerp(targetRotation, Mathf.Clamp((float)(8.0 * delta), 0f, 1f));
        node.Visible = state.IsAlive != 0;
    }

    private bool TrySyncSeatAttachedPresentation(int ecsEntityId, int snapshotEntityId, Node3D node, RemoteEntityState state, WorldRotation rot, double delta)
    {
        if (_store is null)
            return false;

        var entity = _store.GetEntityById(ecsEntityId);
        if (entity.IsNull || !entity.TryGetComponent<VehicleSeat>(out var seat))
            return false;

        if (!_remoteNodes.TryGetValue(seat.VehicleEntityId, out var vehicleNode))
            return false;

        bool hasMountedWeapon = entity.TryGetComponent<RemoteVehicleOccupantState>(out var runtime) && runtime.HasMountedWeapon != 0;
        var seatType = runtime.CurrentSeatType != 0 || seat.SeatType == (byte)SeatType.Driver
            ? (SeatType)(runtime.CurrentSeatType != 0 ? runtime.CurrentSeatType : seat.SeatType)
            : (SeatType)seat.SeatType;
        int seatIndex = runtime.CurrentSeatIndex >= 0 ? runtime.CurrentSeatIndex : seat.SeatIndex;
        var seatParent = ResolveSeatPresentationParent(vehicleNode, seatIndex, seatType, hasMountedWeapon);

        EnsurePresentationParent(node, seatParent);

        var targetLocalPos = new Vector3(seat.OffsetX, seat.OffsetY, seat.OffsetZ);
        if (seatParent is Node3D seatParentNode && seatParentNode != vehicleNode)
            targetLocalPos -= seatParentNode.Position;
        node.Position = node.Position.Lerp(targetLocalPos, (float)(12.0 * delta));

        var localRotation = NormalizeQuaternion(node.Quaternion);
        node.Quaternion = localRotation.Slerp(Quaternion.Identity, Mathf.Clamp((float)(10.0 * delta), 0f, 1f));
        ApplySeatedPresentationPose(node, seatType, hasMountedWeapon, GetVehiclePresentationType(vehicleNode), GetSeatAimRotationDegrees(entity, seatParent), delta);
        ApplyAnimationStatePose(ecsEntityId, node, delta);
        ApplyPresentationFeedback(ecsEntityId, node, delta);
        node.Visible = state.IsAlive != 0;
        node.SetMeta("seat_vehicle_id", seat.VehicleEntityId);
        node.SetMeta("seat_snapshot_id", snapshotEntityId);
        return true;
    }

    private static void EnsurePresentationParent(Node3D node, Node targetParent)
    {
        if (node.GetParent() == targetParent)
            return;

        node.Reparent(targetParent, true);
    }

    private void RemovePresentation(int snapshotEntityId)
    {
        _removeBuilding?.Invoke(snapshotEntityId);
        _spawnedBuildings.Remove(snapshotEntityId);
        _detachWeapon?.Invoke(snapshotEntityId);

        if (_remoteNodes.Remove(snapshotEntityId, out var node))
        {
            node.QueueFree();
            OnRemotePlayerRemoved?.Invoke(snapshotEntityId);
            GD.Print($"[RemotePlayerBridge] Removed remote entity: id={snapshotEntityId}");
        }
    }

    /// <summary>
    /// 创建远端玩家的简易可视节点 — 胶囊体 + 名字标签。
    /// GameBootstrap 可通过 OnRemotePlayerSpawned 事件替换为完整角色模型。
    /// </summary>
    private static Node3D CreateRemoteEntityNode(RemoteEntityState entity, WorldPosition pos, WorldRotation rot)
    {
        var root = new Node3D();
        var entityType = ToEntityType(entity);
        root.Name = $"{entityType}_{entity.NetworkId.ToString("N")[..8]}";
        root.SetMeta("entity_id", entity.SnapshotEntityId);
        root.SetMeta("entity_type", entityType.ToString());
        root.SetMeta("display_name", BuildDisplayName(entity));
        root.SetMeta("is_hostile", entityType == EntityType.Monster);
        var spawnPos = new Vector3(pos.X, pos.Y, pos.Z);
        if (entity.AttachToTerrain != 0 && GameServices.Terrain is not null)
            spawnPos.Y = GameServices.Terrain.SampleHeight(spawnPos.X, spawnPos.Z);
        root.Position = spawnPos;
        root.Quaternion = NormalizeQuaternion(new Quaternion(rot.X, rot.Y, rot.Z, rot.W));

        var visual = CreateVisualFor(entity);
        visual.Name = "Visual";
        root.AddChild(visual);
        var collider = CreateColliderFor(entity);
        if (collider != null)
        {
            collider.Name = "Collider";
            root.AddChild(collider);
        }

        // 名字标签（3D Label）— 弹丸不加标签
        if (entityType != EntityType.Projectile)
        {
            var label = new Label3D();
            label.Name = "NameLabel";
            label.Text = entityType.ToString();
            label.Position = new Vector3(0, 2.1f, 0);
            label.FontSize = 24;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(0.8f, 0.9f, 1.0f);
            root.AddChild(label);
        }

        return root;
    }

    private static Node3D CreateVisualFor(RemoteEntityState entity)
    {
        var entityType = ToEntityType(entity);
        if (entityType == EntityType.Vehicle)
            return CreateVehicleVisual(entity.TypeId);
        if (entityType == EntityType.Spacecraft)
            return CreateSpacecraftVisual();
        if (entityType is EntityType.Player or EntityType.RemotePlayer or EntityType.Npc)
            return CreateSegmentedCharacterVisual(entityType);

        var meshInstance = new MeshInstance3D();
        meshInstance.Mesh = CreateMeshFor(entityType);
        meshInstance.Position = MeshOffsetFor(entityType);
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        var material = new StandardMaterial3D();
        material.AlbedoColor = ColorFor(entityType);
        material.Metallic = 0.1f;
        material.Roughness = 0.8f;
        if (entityType == EntityType.Projectile)
        {
            material.EmissionEnabled = true;
            material.Emission = ColorFor(entityType);
            material.EmissionEnergyMultiplier = 3f;
        }
        meshInstance.MaterialOverride = material;
        return meshInstance;
    }

    private static Node3D CreateSegmentedCharacterVisual(EntityType entityType)
    {
        var root = new Node3D { Name = "CharacterRig" };

        var lowerBody = new MeshInstance3D
        {
            Name = "LowerBody",
            Mesh = new CapsuleMesh { Radius = 0.36f, Height = 1.14f },
            Position = new Vector3(0, 0.72f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        lowerBody.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = ColorFor(entityType).Darkened(0.1f),
            Metallic = 0.08f,
            Roughness = 0.82f,
        };
        root.AddChild(lowerBody);

        var upperPivot = new Node3D { Name = "UpperBodyPivot", Position = new Vector3(0, 1.1f, 0) };
        root.AddChild(upperPivot);
        var upperBody = new MeshInstance3D
        {
            Name = "UpperBody",
            Mesh = new BoxMesh { Size = new Vector3(0.72f, 0.72f, 0.34f) },
            Position = new Vector3(0, 0.18f, -0.02f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        upperBody.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = ColorFor(entityType),
            Metallic = 0.1f,
            Roughness = 0.74f,
        };
        upperPivot.AddChild(upperBody);

        var headPivot = new Node3D { Name = "HeadPivot", Position = new Vector3(0, 0.58f, -0.04f) };
        upperPivot.AddChild(headPivot);
        var head = new MeshInstance3D
        {
            Name = "Head",
            Mesh = new SphereMesh { Radius = 0.24f, Height = 0.48f },
            Position = new Vector3(0, 0.18f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        head.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = ColorFor(entityType).Lightened(0.12f),
            Metallic = 0.04f,
            Roughness = 0.88f,
        };
        headPivot.AddChild(head);
        return root;
    }

    private static Node3D? CreateColliderFor(RemoteEntityState entity)
    {
        var entityType = ToEntityType(entity);
        CollisionShape3D? shape = entityType switch
        {
            EntityType.Player or EntityType.RemotePlayer or EntityType.Npc or EntityType.Monster
                => new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f }, Position = new Vector3(0, 0.95f, 0) },
            EntityType.Vehicle
                => new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4f, 2.5f, 6f) }, Position = new Vector3(0, 1.25f, 0) },
            EntityType.Spacecraft
                => new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 1f, Height = 6f }, Position = new Vector3(0, 3f, 0) },
            _ => null,
        };

        if (shape == null)
            return null;

        var body = new StaticBody3D();
        body.Name = $"Collider_{entity.SnapshotEntityId}";
        body.CollisionLayer = entityType == EntityType.Monster ? 4u : 2u;
        body.CollisionMask = 0;
        body.SetMeta("entity_id", entity.SnapshotEntityId);
        body.SetMeta("entity_type", entityType.ToString());
        body.SetMeta("display_name", BuildDisplayName(entity));
        body.SetMeta("is_hostile", entityType == EntityType.Monster);
        body.AddChild(shape);
        return body;
    }

    private static string BuildDisplayName(RemoteEntityState entity) => ToEntityType(entity) switch
    {
        EntityType.Npc => $"NPC #{entity.SnapshotEntityId}",
        EntityType.Monster => $"Monster #{entity.SnapshotEntityId}",
        EntityType.Player or EntityType.RemotePlayer => $"Player #{entity.SnapshotEntityId}",
        EntityType.Vehicle => $"Vehicle #{entity.TypeId}",
        EntityType.Spacecraft => "Rocket",
        _ => ToEntityType(entity).ToString()
    };

    private byte ResolveWeaponCategory(int snapshotEntityId)
    {
        if (_store == null || _remoteWorldEcsCache == null)
            return DefaultStarterWeaponCategory;

        if (!_remoteWorldEcsCache.TryGetEcsEntityId(snapshotEntityId, out var ecsEntityId))
            return DefaultStarterWeaponCategory;

        var entity = _store.GetEntityById(ecsEntityId);
        if (entity.IsNull)
            return DefaultStarterWeaponCategory;

        if (entity.TryGetComponent<WeaponState>(out var weaponState) && weaponState.WeaponDefId > 0)
            return weaponState.Category;

        if (entity.TryGetComponent<RemoteCombatState>(out var remoteCombatState) && remoteCombatState.HasWeapon != 0)
            return remoteCombatState.WeaponCategory;

        return DefaultStarterWeaponCategory;
    }

    private static EntityType ToEntityType(RemoteEntityState entity) => (EntityType)entity.EntityType;

    private static Node3D CreateVehicleVisual(int vehicleDefId)
    {
        var root = new Node3D { Name = $"VehicleDef_{vehicleDefId}" };
        switch (vehicleDefId)
        {
            case 1:
                BuildOffroadVehicle(root);
                break;
            case 2:
                BuildTankVehicle(root);
                break;
            case 3:
                BuildAntiAirVehicle(root);
                break;
            case 4:
                BuildAircraftVehicle(root);
                break;
            case 5:
                BuildBoatVehicle(root);
                break;
            default:
                BuildFallbackVehicle(root);
                break;
        }

        return root;
    }

    private static Node3D CreateSpacecraftVisual()
    {
        var root = new Node3D { Name = "SpacecraftVisual" };
        AddMesh(root, new CylinderMesh { TopRadius = 0.8f, BottomRadius = 1.0f, Height = 6.0f }, new Vector3(0, 3.0f, 0), new Color(0.86f, 0.86f, 0.92f));
        AddMesh(root, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.8f, Height = 1.5f }, new Vector3(0, 6.75f, 0), new Color(0.94f, 0.32f, 0.24f));
        AddMesh(root, new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.95f, Height = 0.7f }, new Vector3(0, 0.0f, 0), new Color(0.3f, 0.3f, 0.35f));

        // 引擎火焰粒子效果
        var flame = new GpuParticles3D
        {
            Name = "ThrustFlame",
            Amount = 200,
            Lifetime = 0.5f,
            SpeedScale = 2.0f,
            Position = new Vector3(0, -0.5f, 0),
            Emitting = true,
            DrawOrder = GpuParticles3D.DrawOrderEnum.Lifetime,
        };
        flame.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 10f,
            InitialVelocityMin = 8f,
            InitialVelocityMax = 16f,
            Gravity = new Vector3(0, -2f, 0),
            ScaleMin = 0.3f,
            ScaleMax = 0.7f,
            Color = new Color(1f, 0.6f, 0.1f),
        };
        flame.DrawPass1 = new QuadMesh { Size = new Vector2(0.5f, 0.5f) };
        root.AddChild(flame);

        // 烟雾尾迹
        var smoke = new GpuParticles3D
        {
            Name = "SmokeTrail",
            Amount = 100,
            Lifetime = 2.5f,
            SpeedScale = 1.0f,
            Position = new Vector3(0, -1.5f, 0),
            Emitting = true,
        };
        smoke.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 20f,
            InitialVelocityMin = 2f,
            InitialVelocityMax = 6f,
            Gravity = new Vector3(0, 0.5f, 0),
            ScaleMin = 0.8f,
            ScaleMax = 3.0f,
            Color = new Color(0.6f, 0.6f, 0.6f, 0.4f),
        };
        smoke.DrawPass1 = new QuadMesh { Size = new Vector2(1.5f, 1.5f) };
        root.AddChild(smoke);

        // 引擎发光
        var thrustLight = new OmniLight3D
        {
            Position = new Vector3(0, -0.5f, 0),
            LightColor = new Color(1f, 0.7f, 0.3f),
            LightEnergy = 3f,
            OmniRange = 12f,
        };
        root.AddChild(thrustLight);

        return root;
    }

    private static void BuildOffroadVehicle(Node3D root)
    {
        AddMesh(root, new BoxMesh { Size = new Vector3(2.8f, 0.8f, 4.0f) }, new Vector3(0, 0.8f, 0), new Color(0.32f, 0.42f, 0.18f));
        AddMesh(root, new BoxMesh { Size = new Vector3(2.0f, 0.7f, 1.8f) }, new Vector3(0, 1.45f, -0.2f), new Color(0.22f, 0.28f, 0.14f));
        AddWheel(root, new Vector3(-1.35f, 0.55f, -1.35f));
        AddWheel(root, new Vector3(1.35f, 0.55f, -1.35f));
        AddWheel(root, new Vector3(-1.35f, 0.55f, 1.35f));
        AddWheel(root, new Vector3(1.35f, 0.55f, 1.35f));
    }

    private static void BuildTankVehicle(Node3D root)
    {
        AddMesh(root, new BoxMesh { Size = new Vector3(3.2f, 0.9f, 5.4f) }, new Vector3(0, 0.8f, 0), new Color(0.28f, 0.38f, 0.22f));
        AddMesh(root, new BoxMesh { Size = new Vector3(0.45f, 0.55f, 5.5f) }, new Vector3(-1.75f, 0.35f, 0), new Color(0.14f, 0.14f, 0.12f));
        AddMesh(root, new BoxMesh { Size = new Vector3(0.45f, 0.55f, 5.5f) }, new Vector3(1.75f, 0.35f, 0), new Color(0.14f, 0.14f, 0.12f));
        AddTurretStation(root, 1, new Vector3(0, 1.1f, -0.3f), new BoxMesh { Size = new Vector3(2.0f, 0.65f, 2.2f) }, new Vector3(0, 0.35f, 0), new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.1f, Height = 3.2f }, new Vector3(0, 0.35f, -2.25f), new Color(0.34f, 0.44f, 0.26f));
        AddTurretStation(root, 2, new Vector3(0, 1.75f, 0.55f), new BoxMesh { Size = new Vector3(0.55f, 0.35f, 0.55f) }, new Vector3(0, 0.18f, 0), new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.05f, Height = 1.6f }, new Vector3(0, 0.2f, -0.95f), new Color(0.38f, 0.46f, 0.28f));
    }

    private static void BuildAntiAirVehicle(Node3D root)
    {
        AddMesh(root, new BoxMesh { Size = new Vector3(2.8f, 0.8f, 4.6f) }, new Vector3(0, 0.8f, 0), new Color(0.25f, 0.34f, 0.22f));
        AddTurretStation(root, 0, new Vector3(0, 1.15f, -0.1f), new BoxMesh { Size = new Vector3(1.5f, 0.55f, 1.5f) }, new Vector3(0, 0.2f, 0), new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 2.4f }, new Vector3(-0.35f, 0.35f, -1.2f), new Color(0.42f, 0.48f, 0.3f), mirrorBarrelOffset: new Vector3(0.35f, 0.35f, -1.2f));
        AddWheel(root, new Vector3(-1.2f, 0.55f, -1.25f));
        AddWheel(root, new Vector3(1.2f, 0.55f, -1.25f));
        AddWheel(root, new Vector3(-1.2f, 0.55f, 1.25f));
        AddWheel(root, new Vector3(1.2f, 0.55f, 1.25f));
    }

    private static void BuildAircraftVehicle(Node3D root)
    {
        AddMesh(root, new CapsuleMesh { Radius = 0.45f, Height = 3.8f }, new Vector3(0, 1.0f, 0), new Color(0.52f, 0.56f, 0.62f), new Vector3(90, 0, 0));
        AddMesh(root, new BoxMesh { Size = new Vector3(6.0f, 0.12f, 1.4f) }, new Vector3(0, 1.0f, 0), new Color(0.38f, 0.44f, 0.55f));
        AddMesh(root, new BoxMesh { Size = new Vector3(2.3f, 0.1f, 0.8f) }, new Vector3(0, 1.45f, 1.45f), new Color(0.38f, 0.44f, 0.55f));
        AddMesh(root, new BoxMesh { Size = new Vector3(0.16f, 1.0f, 0.7f) }, new Vector3(0, 1.7f, 1.6f), new Color(0.34f, 0.4f, 0.52f));
        AddTurretStation(root, 0, new Vector3(0, 1.0f, -1.2f), new SphereMesh { Radius = 0.12f, Height = 0.24f }, new Vector3(0, 0.02f, 0), new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 1.1f }, new Vector3(0, 0.0f, -0.7f), new Color(0.46f, 0.5f, 0.58f));
    }

    private static void BuildBoatVehicle(Node3D root)
    {
        AddMesh(root, new BoxMesh { Size = new Vector3(2.6f, 1.0f, 5.5f) }, new Vector3(0, 0.7f, 0), new Color(0.24f, 0.36f, 0.6f));
        AddMesh(root, new BoxMesh { Size = new Vector3(1.6f, 1.0f, 1.6f) }, new Vector3(0, 1.55f, -0.2f), new Color(0.82f, 0.84f, 0.88f));
        AddMesh(root, new BoxMesh { Size = new Vector3(0.18f, 1.2f, 0.18f) }, new Vector3(0, 2.1f, 1.4f), new Color(0.72f, 0.72f, 0.78f));
        AddTurretStation(root, 1, new Vector3(0, 1.65f, 0.6f), new BoxMesh { Size = new Vector3(0.85f, 0.4f, 0.9f) }, new Vector3(0, 0.12f, 0), new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.08f, Height = 2.0f }, new Vector3(0, 0.18f, -1.3f), new Color(0.78f, 0.8f, 0.84f));
    }

    private static void BuildFallbackVehicle(Node3D root)
    {
        AddMesh(root, new BoxMesh { Size = new Vector3(4f, 1.8f, 2.2f) }, new Vector3(0, 0.9f, 0), new Color(0.4f, 0.6f, 0.8f));
    }

    private static void AddWheel(Node3D root, Vector3 position)
    {
        AddMesh(root, new CylinderMesh { TopRadius = 0.38f, BottomRadius = 0.38f, Height = 0.35f }, position, new Color(0.08f, 0.08f, 0.08f), new Vector3(90, 0, 0));
    }

    private static void AddTurretStation(Node3D root, int seatIndex, Mesh turretMesh, Vector3 turretPos, Mesh barrelMesh, Vector3 barrelPos, Color color)
    {
        AddTurretStation(root, seatIndex, turretPos, turretMesh, Vector3.Zero, barrelMesh, barrelPos, color);
    }

    private static void AddTurretStation(Node3D root, int seatIndex, Vector3 pivotPosition, Mesh turretMesh, Vector3 turretLocalPosition, Mesh barrelMesh, Vector3 barrelLocalPosition, Color color, Vector3? mirrorBarrelOffset = null)
    {
        var pivot = new Node3D { Name = $"TurretPivot_Seat{seatIndex}", Position = pivotPosition };
        if (seatIndex == 1)
            pivot.Name = "TurretPivot";
        root.AddChild(pivot);

        AddMesh(pivot, turretMesh, turretLocalPosition, color);
        AddMesh(pivot, barrelMesh, barrelLocalPosition, color, new Vector3(90, 0, 0));
        if (mirrorBarrelOffset.HasValue)
            AddMesh(pivot, barrelMesh.Duplicate() as Mesh ?? barrelMesh, mirrorBarrelOffset.Value, color, new Vector3(90, 0, 0));
    }

    private static void AddMesh(Node3D root, Mesh mesh, Vector3 position, Color color, Vector3? rotationDegrees = null)
    {
        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Metallic = 0.18f,
                Roughness = 0.75f,
            }
        };

        if (rotationDegrees.HasValue)
            meshInstance.RotationDegrees = rotationDegrees.Value;

        root.AddChild(meshInstance);
    }

    private static Mesh CreateMeshFor(Ark.Shared.Data.EntityType type) => type switch
    {
        Ark.Shared.Data.EntityType.Player or Ark.Shared.Data.EntityType.RemotePlayer => new CapsuleMesh { Radius = 0.35f, Height = 1.8f },
        Ark.Shared.Data.EntityType.Npc => new CapsuleMesh { Radius = 0.3f, Height = 1.7f },
        Ark.Shared.Data.EntityType.Monster => new SphereMesh { Radius = 0.7f, Height = 1.4f },
        Ark.Shared.Data.EntityType.Building => new BoxMesh { Size = new Vector3(3f, 2f, 3f) },
        Ark.Shared.Data.EntityType.Vehicle => new BoxMesh { Size = new Vector3(4f, 1.8f, 2.2f) },
        Ark.Shared.Data.EntityType.Spacecraft => new CylinderMesh { TopRadius = 0.8f, BottomRadius = 1.5f, Height = 6f },
        Ark.Shared.Data.EntityType.Projectile => new SphereMesh { Radius = 0.15f, Height = 0.3f },
        Ark.Shared.Data.EntityType.Environment => new BoxMesh { Size = new Vector3(2f, 3f, 2f) },
        Ark.Shared.Data.EntityType.GroundItem => new BoxMesh { Size = new Vector3(0.6f, 0.4f, 0.6f) },
        _ => new BoxMesh { Size = Vector3.One }
    };

    private static Vector3 MeshOffsetFor(Ark.Shared.Data.EntityType type) => type switch
    {
        Ark.Shared.Data.EntityType.Building => new Vector3(0, 1f, 0),
        Ark.Shared.Data.EntityType.Vehicle => new Vector3(0, 0.9f, 0),
        Ark.Shared.Data.EntityType.Spacecraft => new Vector3(0, 3f, 0),
        Ark.Shared.Data.EntityType.Projectile => Vector3.Zero,
        Ark.Shared.Data.EntityType.Environment => new Vector3(0, 1.5f, 0),
        Ark.Shared.Data.EntityType.GroundItem => new Vector3(0, 0.2f, 0),
        Ark.Shared.Data.EntityType.Monster => new Vector3(0, 0.7f, 0),
        _ => new Vector3(0, 0.9f, 0)
    };

    private static Color ColorFor(Ark.Shared.Data.EntityType type) => type switch
    {
        Ark.Shared.Data.EntityType.Player or Ark.Shared.Data.EntityType.RemotePlayer => new Color(0.2f, 0.5f, 1.0f),
        Ark.Shared.Data.EntityType.Npc => new Color(0.3f, 0.9f, 0.4f),
        Ark.Shared.Data.EntityType.Monster => new Color(0.95f, 0.25f, 0.25f),
        Ark.Shared.Data.EntityType.Building => new Color(0.7f, 0.6f, 0.45f),
        Ark.Shared.Data.EntityType.Vehicle => new Color(0.4f, 0.6f, 0.8f),
        Ark.Shared.Data.EntityType.Spacecraft => new Color(0.85f, 0.85f, 0.95f),
        Ark.Shared.Data.EntityType.Projectile => new Color(1.0f, 0.6f, 0.1f),
        Ark.Shared.Data.EntityType.Environment => new Color(0.65f, 0.8f, 0.35f),
        Ark.Shared.Data.EntityType.GroundItem => new Color(0.9f, 0.8f, 0.2f),
        _ => new Color(0.8f, 0.8f, 0.8f)
    };

    private static Quaternion NormalizeQuaternion(Quaternion q)
    {
        var lengthSq = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        if (!float.IsFinite(lengthSq) || lengthSq < 1e-6f)
            return Quaternion.Identity;

        var invLength = 1.0f / Mathf.Sqrt(lengthSq);
        return new Quaternion(q.X * invLength, q.Y * invLength, q.Z * invLength, q.W * invLength);
    }
}
