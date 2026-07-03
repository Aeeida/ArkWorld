using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Services.Remote;

namespace Ark.Bridge.Features.Combat;

public partial class WeaponVisualSystem
{
    private const float BulletSpeed = 120f;
    private const float BulletMaxLifetime = 2f;
    private const float ShellSpeed = 40f;
    private const float ShellMaxLifetime = 4f;
    private const float MuzzleFlashTime = 0.06f;
    private const float ReloadAnimDuration = 1.8f;
    private const float MaintenanceAnimDuration = 1.6f;

    private EntityStore? _store;
    private WeaponVisualEcsAuthority? _ecsAuth;
    private RemoteWorldEcsCacheSystem? _remoteWorldEcsCache;

    // ═══ 线程安全队列 — 从 SignalR 线程入队，主线程出队处理 ═══
    private readonly ConcurrentQueue<Vector3> _pendingHits = new();
    private readonly ConcurrentQueue<(Vector3 Pos, float Radius)> _pendingExplosions = new();
    private readonly ConcurrentQueue<(int EntityId, int WeaponDefId)> _pendingFires = new();

    private sealed class WeaponAttachment
    {
        public int EntityId;
        public Node3D Root = null!;
        public MeshInstance3D Gun = null!;
        public MeshInstance3D Barrel = null!;
        public MeshInstance3D Muzzle = null!;
        public byte WeaponCategory;
        public float MuzzleTimer;
        public float ReloadTimer;
        public float MaintenanceTimer;
        public AudioStreamPlayer3D? MaintenanceAudio;
        public float MaintenanceAudioPhase;
        public Vector3 GunRestPos;
    }

    private readonly Dictionary<int, WeaponAttachment> _weapons = new();

    private sealed class BulletTrail
    {
        public MeshInstance3D Node = null!;
        public Vector3 Direction;
        public float Speed;
        public float Lifetime;
        public float MaxLifetime;
    }

    private readonly List<BulletTrail> _bullets = new();
    private readonly List<BulletTrail> _bulletPool = new();

    private sealed class VehicleVisual
    {
        public Node3D Root = null!;
        public MeshInstance3D Body = null!;
        public Node3D TurretPivot = null!;
        public MeshInstance3D Turret = null!;
        public MeshInstance3D Barrel = null!;
        public List<VehicleWeaponStation> Stations { get; } = [];
        public Rid PhysBody;
        public Rid PhysShape;
    }

    private sealed class VehicleWeaponStation
    {
        public int SeatIndex;
        public Node3D Pivot = null!;
        public MeshInstance3D Turret = null!;
        public MeshInstance3D Barrel = null!;
        public MeshInstance3D? BarrelMirror;
    }

    private readonly Dictionary<int, VehicleVisual> _vehicles = new();

    private sealed class HitFlash
    {
        public MeshInstance3D Sphere = null!;
        public float Timer;
    }

    private readonly List<HitFlash> _hitFlashes = new();
    private readonly List<HitFlash> _hitFlashPool = new();
    private static StandardMaterial3D? _hitFlashMat;

    private sealed class ExplosionEffect
    {
        public MeshInstance3D Sphere = null!;
        public float Timer;
        public float MaxTimer;
        public float MaxScale;
    }

    private readonly List<ExplosionEffect> _explosions = new();
    private static StandardMaterial3D? _explosionMat;
    private readonly List<int> _drainedEventEntities = new();
    private readonly Dictionary<int, RemotePresentationFeedbackState> _pendingPresentationFeedbackWrites = new();
    private readonly Dictionary<int, BuildingDamageFeedbackState> _pendingBuildingFeedbackWrites = new();

    private static StandardMaterial3D? _bulletMat;
    private static StandardMaterial3D? _shellMat;
    private static StandardMaterial3D? _muzzleMat;
    private static StandardMaterial3D? _gunMetalMat;
    private static StandardMaterial3D? _tankBodyMat;
    private static StandardMaterial3D? _tankTurretMat;

    public void Initialize(EntityStore store, RemoteWorldEcsCacheSystem? remoteWorldEcsCache = null)
    {
        _store = store;
        _ecsAuth = new WeaponVisualEcsAuthority(store);
        _remoteWorldEcsCache = remoteWorldEcsCache;
        EnsureMaterials();
    }

    public void HandleWeaponFired(int entityId, int weaponDefId)
    {
        if (_store == null) return;

        var entity = ResolveEntity(entityId);
        if (entity.IsNull) return;
        if (!entity.TryGetComponent<WorldPosition>(out var pos)) return;

        var dir = new Vector3(0, 0, -1);
        if (entity.TryGetComponent<WorldRotation>(out var rot))
        {
            var quat = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
            dir = quat * new Vector3(0, 0, -1);
        }

        bool isVehicleWeapon = entity.Tags.Has<VehicleTag>();
        var origin = isVehicleWeapon
            ? new Vector3(pos.X, pos.Y + 2.0f, pos.Z) + dir * 3.5f
            : new Vector3(pos.X, pos.Y + 1.2f, pos.Z) + dir * 0.8f;

        if (_weapons.TryGetValue(entityId, out var att))
        {
            att.Muzzle.Visible = true;
            att.MuzzleTimer = MuzzleFlashTime;
        }

        if (isVehicleWeapon)
            SpawnShellTrail(origin, dir);
        else
            SpawnBulletTrail(origin, dir);

        if (entity.TryGetComponent<WeaponState>(out var ws) && ws.IsReloading != 0)
            OnReloadStarted(entityId);
    }

    private Entity ResolveEntity(int entityId)
    {
        if (_store == null)
            return default;

        var entity = _store.GetEntityById(entityId);
        if (!entity.IsNull)
            return entity;

        if (_remoteWorldEcsCache != null && _remoteWorldEcsCache.TryGetEcsEntityId(entityId, out var ecsEntityId))
            return _store.GetEntityById(ecsEntityId);

        return default;
    }

    private bool TryGetWeaponAttachment(int entityId, out WeaponAttachment? attachment)
    {
        if (_weapons.TryGetValue(entityId, out var directAttachment))
        {
            attachment = directAttachment;
            return true;
        }

        if (_remoteWorldEcsCache != null && _remoteWorldEcsCache.TryGetEcsEntityId(entityId, out var ecsEntityId) && _weapons.TryGetValue(ecsEntityId, out var mappedAttachment))
        {
            attachment = mappedAttachment;
            return true;
        }

        attachment = null;
        return false;
    }

    /// <summary>线程安全 — 从任何线程调用，主线程处理。</summary>
    public void HandleProjectileHit(System.Numerics.Vector3 hitPos)
    {
        _pendingHits.Enqueue(new Vector3(hitPos.X, hitPos.Y, hitPos.Z));
    }

    /// <summary>线程安全 — 从任何线程调用，主线程处理。</summary>
    public void HandleExplosion(System.Numerics.Vector3 hitPos, float radius)
    {
        _pendingExplosions.Enqueue((new Vector3(hitPos.X, hitPos.Y, hitPos.Z), radius));
    }

    /// <summary>线程安全 — 从任何线程调用，主线程处理。</summary>
    public void HandleWeaponFiredThreadSafe(int entityId, int weaponDefId)
    {
        _pendingFires.Enqueue((entityId, weaponDefId));
    }

    private void SpawnExplosionEffect(Vector3 position, float radius)
    {
        if (!IsInsideTree()) return;

        var mesh = new SphereMesh { Radius = 0.5f, Height = 1.0f };
        mesh.Material = _explosionMat;
        var sphere = new MeshInstance3D { Mesh = mesh };
        AddChild(sphere);
        if (sphere.IsInsideTree())
            sphere.GlobalPosition = position;
        else
            sphere.Position = position;

        _explosions.Add(new ExplosionEffect
        {
            Sphere = sphere,
            Timer = 0f,
            MaxTimer = 0.8f,
            MaxScale = radius * 0.8f,
        });

        var ringMesh = new SphereMesh { Radius = 0.3f, Height = 0.6f };
        var ringMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.3f, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        ringMesh.Material = ringMat;
        var ring = new MeshInstance3D { Mesh = ringMesh };
        AddChild(ring);
        if (ring.IsInsideTree())
            ring.GlobalPosition = position;
        else
            ring.Position = position;

        _explosions.Add(new ExplosionEffect
        {
            Sphere = ring,
            Timer = 0f,
            MaxTimer = 0.5f,
            MaxScale = radius * 1.5f,
        });
    }

    private static void EnsureMaterials()
    {
        _bulletMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.95f, 0.3f),
            Emission = new Color(1f, 0.9f, 0.2f),
            EmissionEnergyMultiplier = 3f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _shellMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.5f, 0.1f),
            Emission = new Color(1f, 0.4f, 0f),
            EmissionEnergyMultiplier = 5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _muzzleMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.1f, 0.9f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.7f, 0f),
            EmissionEnergyMultiplier = 5f,
        };
        _gunMetalMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.25f, 0.28f),
            Metallic = 0.8f,
            Roughness = 0.4f,
        };
        _tankBodyMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.32f, 0.36f, 0.28f),
            Metallic = 0.6f,
            Roughness = 0.55f,
        };
        _tankTurretMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.32f, 0.24f),
            Metallic = 0.7f,
            Roughness = 0.45f,
        };
        _hitFlashMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.1f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.15f, 0f),
            EmissionEnergyMultiplier = 4f,
        };
        _explosionMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.6f, 0.1f, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.4f, 0f),
            EmissionEnergyMultiplier = 8f,
        };
    }
}
