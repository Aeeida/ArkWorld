using System.Collections.Concurrent;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.Services.Remote;

/// <summary>
/// 线程安全网络视觉事件缓冲。
/// SignalR/TCP 线程先入队，主线程再刷入 ECS 事件实体。
/// </summary>
public sealed class NetworkVisualEventBuffer
{
    private readonly EntityStore _store;
    private readonly ConcurrentQueue<WeaponFireVisualEvent> _weaponFires = new();
    private readonly ConcurrentQueue<ProjectileHitVisualEvent> _hits = new();
    private readonly ConcurrentQueue<ExplosionVisualEvent> _explosions = new();

    public NetworkVisualEventBuffer(EntityStore store)
    {
        _store = store;
    }

    public void EnqueueWeaponFire(int shooterEntityId, int weaponDefId)
    {
        _weaponFires.Enqueue(new WeaponFireVisualEvent
        {
            ShooterEntityId = shooterEntityId,
            WeaponDefId = weaponDefId,
        });
    }

    public void EnqueueWeaponFire(int shooterEntityId, int weaponDefId, System.Numerics.Vector3 origin, System.Numerics.Vector3 direction)
    {
        _weaponFires.Enqueue(new WeaponFireVisualEvent
        {
            ShooterEntityId = shooterEntityId,
            WeaponDefId = weaponDefId,
            OriginX = origin.X,
            OriginY = origin.Y,
            OriginZ = origin.Z,
            DirX = direction.X,
            DirY = direction.Y,
            DirZ = direction.Z,
            HasExplicitTrajectory = 1,
        });
    }

    public void EnqueueHit(System.Numerics.Vector3 position)
    {
        _hits.Enqueue(new ProjectileHitVisualEvent { X = position.X, Y = position.Y, Z = position.Z });
    }

    public void EnqueueExplosion(System.Numerics.Vector3 position, float radius)
    {
        _explosions.Enqueue(new ExplosionVisualEvent { X = position.X, Y = position.Y, Z = position.Z, Radius = radius });
    }

    public void FlushToEcs()
    {
        while (_weaponFires.TryDequeue(out var fire))
            _store.CreateEntity().AddComponent(fire);

        while (_hits.TryDequeue(out var hit))
            _store.CreateEntity().AddComponent(hit);

        while (_explosions.TryDequeue(out var explosion))
            _store.CreateEntity().AddComponent(explosion);
    }
}
