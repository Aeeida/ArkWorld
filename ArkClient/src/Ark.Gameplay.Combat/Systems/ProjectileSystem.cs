using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 投射物系统 — 管理子弹/炮弹/火箭的生命周期。
/// </summary>
public sealed class ProjectileSystem
{
    private readonly EntityStore _store;
    private readonly ProjectileDefRegistry _projectileDefs;

    public event Action<int, int, Vector3>? OnProjectileHit;
    public event Action<int, Vector3, float>? OnProjectileExplode;

    public ProjectileSystem(EntityStore store, ProjectileDefRegistry projectileDefs)
    {
        _store = store;
        _projectileDefs = projectileDefs;
    }

    public void Update(float deltaTime)
    {
        // ── 1. 移动投射物，收集超距的 ID（不能在 query loop 内做 structural change）──
        var expired = new System.Collections.Generic.List<int>();

        var query = _store.Query<ProjectileData, WorldPosition, Velocity>()
            .AllTags(Tags.Get<Projectile>());

        foreach (var chunk in query.Chunks)
        {
            var projectiles = chunk.Chunk1;
            var positions   = chunk.Chunk2;
            var velocities  = chunk.Chunk3;
            var entities    = chunk.Entities;

            for (int i = 0; i < entities.Length; i++)
            {
                ref var proj = ref projectiles.Span[i];
                ref var pos  = ref positions.Span[i];
                ref var vel  = ref velocities.Span[i];

                if (proj.AffectedByGravity != 0)
                    vel.Y -= 9.81f * deltaTime;

                float speed = proj.Speed;
                pos.X += vel.X * speed * deltaTime;
                pos.Y += vel.Y * speed * deltaTime;
                pos.Z += vel.Z * speed * deltaTime;

                proj.TraveledDistance += speed * deltaTime;

                if (proj.TraveledDistance > proj.MaxRange)
                    expired.Add(entities[i]);
            }
        }

        // ── 2. 在 query 外标记待销毁 ──
        foreach (var eid in expired)
        {
            var e = _store.GetEntityById(eid);
            if (!e.IsNull) e.AddTag<PendingDestroy>();
        }

        CleanupDestroyedProjectiles();
    }

    public Entity SpawnProjectile(ProjectileSpawnRequest request)
    {
        var def = _projectileDefs.Get(request.ProjectileDefId);
        if (def == null) return default;

        var entity = _store.CreateEntity();

        entity.AddComponent(new ProjectileData
        {
            ProjectileDefId   = request.ProjectileDefId,
            OwnerEntityId     = request.OwnerEntityId,
            Damage            = request.DamageMultiplier * 10f,
            Speed             = def.Value.Speed,
            MaxRange          = def.Value.MaxRange,
            ExplosionRadius   = def.Value.ExplosionRadius,
            DamageType        = (byte)def.Value.DamageType,
            AffectedByGravity = (byte)(def.Value.Gravity > 0 ? 1 : 0),
        });

        var dir = Vector3.Normalize(request.Direction);
        entity.AddComponent(new WorldPosition { X = request.Origin.X, Y = request.Origin.Y, Z = request.Origin.Z });
        entity.AddComponent(new Velocity { X = dir.X, Y = dir.Y, Z = dir.Z, Speed = def.Value.Speed });
        entity.AddComponent(WorldRotation.Identity);
        entity.AddTag<Projectile>();

        switch (def.Value.Type)
        {
            case ProjectileType.Bullet:  entity.AddTag<BulletProjectile>(); break;
            case ProjectileType.Shell:   entity.AddTag<ShellProjectile>(); break;
            case ProjectileType.Rocket:  entity.AddTag<RocketProjectile>(); break;
            case ProjectileType.Grenade: entity.AddTag<ThrownProjectile>(); break;
        }

        return entity;
    }

    public void HandleHit(int projectileEntityId, int targetEntityId, Vector3 hitPosition, DamageSystem damageSystem)
    {
        var projEntity = _store.GetEntityById(projectileEntityId);
        if (projEntity.IsNull) return;
        if (!projEntity.TryGetComponent<ProjectileData>(out var proj)) return;

        OnProjectileHit?.Invoke(projectileEntityId, targetEntityId, hitPosition);

        if (proj.ExplosionRadius > 0)
            OnProjectileExplode?.Invoke(projectileEntityId, hitPosition, proj.ExplosionRadius);

        if (targetEntityId > 0)
        {
            damageSystem.ApplyDamage(targetEntityId, proj.OwnerEntityId,
                proj.Damage, (DamageType)proj.DamageType, HitZone.Body, false, hitPosition);
        }

        projEntity.AddTag<PendingDestroy>();
    }

    private void CleanupDestroyedProjectiles()
    {
        // 收集待销毁的投射物 ID
        var toDestroy = new System.Collections.Generic.List<int>();
        var query = _store.Query<ProjectileData>().AllTags(Tags.Get<PendingDestroy>());
        foreach (var chunk in query.Chunks)
        {
            var entities = chunk.Entities;
            for (int i = 0; i < entities.Length; i++)
                toDestroy.Add(entities[i]);
        }

        // 迭代外删除
        foreach (var eid in toDestroy)
        {
            var e = _store.GetEntityById(eid);
            if (!e.IsNull) e.DeleteEntity();
        }
    }
}
