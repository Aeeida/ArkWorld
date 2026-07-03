using System;
using System.Collections.Generic;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 投射物碰撞检测系统 — 使用射线段检测避免高速弹穿透。
///
/// 职责：
///   • 每帧查询所有活跃投射物，构造上一帧→当前帧的射线段
///   • 射线段 vs 目标球体检测
///   • 投射物 Y vs 地形高度 → 地面命中（炮弹→爆炸+地形变形）
///   • 产出命中列表，交由 DamageSystem / ProjectileSystem 处理
///   • 对命中目标施加击退位移
///
/// 不依赖 Godot — 纯 ECS + System.Numerics。
/// </summary>
public sealed class ProjectileCollisionSystem
{
    private readonly EntityStore _store;
    private readonly ProjectileSystem _projectiles;
    private readonly DamageSystem _damage;
    private ITerrainQuery? _terrain;

    /// <summary>命中时触发（hitWorldPos）— 供视觉层订阅。</summary>
    public event Action<Vector3>? OnHit;

    /// <summary>命中时触发（hitWorldPos, explosionRadius）— 供视觉层订阅爆炸特效。</summary>
    public event Action<Vector3, float>? OnExplosion;

    public ProjectileCollisionSystem(EntityStore store, ProjectileSystem projectiles, DamageSystem damage)
    {
        _store       = store;
        _projectiles = projectiles;
        _damage      = damage;
    }

    /// <summary>注入地形查询 — 用于检测炮弹命中地面。</summary>
    public void SetTerrainQuery(ITerrainQuery terrain) => _terrain = terrain;

    public void Update(float gameTime)
    {
        var projQuery   = _store.Query<ProjectileData, WorldPosition, Velocity>()
            .AllTags(Tags.Get<Projectile>());
        var targetQuery = _store.Query<Health, WorldPosition, BoundingBox>();

        var hits = new List<(int projEid, int targetEid, Vector3 hitPos, bool isExplosive)>();

        foreach (var projChunk in projQuery.Chunks)
        {
            var projData     = projChunk.Chunk1;
            var projPos      = projChunk.Chunk2;
            var projVel      = projChunk.Chunk3;
            var projEntities = projChunk.Entities;

            for (int pi = 0; pi < projEntities.Length; pi++)
            {
                ref readonly var pd = ref projData.Span[pi];
                ref readonly var pp = ref projPos.Span[pi];
                ref readonly var pv = ref projVel.Span[pi];
                int projEid = projEntities[pi];

                var projEntity = _store.GetEntityById(projEid);
                if (projEntity.Tags.Has<PendingDestroy>()) continue;

                // 射线段：从估算的上一帧位置到当前位置
                float frameDist = pd.Speed * 0.017f; // ~1/60s
                float segLen    = MathF.Max(frameDist, 2f);
                float prevX = pp.X - pv.X * segLen;
                float prevY = pp.Y - pv.Y * segLen;
                float prevZ = pp.Z - pv.Z * segLen;

                // 炮弹体积更大 → 碰撞半径更大
                float hitRadius = pd.ExplosionRadius > 0
                    ? MathF.Max(pd.ExplosionRadius * 0.5f, 2.0f)
                    : 1.2f;
                bool isExplosive = pd.ExplosionRadius > 0;
                bool alreadyHit = false;

                foreach (var tgtChunk in targetQuery.Chunks)
                {
                    if (alreadyHit) break;
                    var tgtHp       = tgtChunk.Chunk1;
                    var tgtPos      = tgtChunk.Chunk2;
                    var tgtBbox     = tgtChunk.Chunk3;
                    var tgtEntities = tgtChunk.Entities;

                    for (int ti = 0; ti < tgtEntities.Length; ti++)
                    {
                        if (alreadyHit) break;
                        int targetEid = tgtEntities[ti];
                        if (targetEid == pd.OwnerEntityId) continue;

                        ref readonly var hp = ref tgtHp.Span[ti];
                        if (hp.IsDead) continue;

                        var targetEntity = _store.GetEntityById(targetEid);
                        if (targetEntity.Tags.Has<Dead>()) continue;

                        // 友方不打友方、敌方不打敌方（建筑/载具无此标签，总是可被命中）
                        var ownerEntity = _store.GetEntityById(pd.OwnerEntityId);
                        if (!ownerEntity.IsNull)
                        {
                            if (ownerEntity.Tags.Has<Friendly>() && targetEntity.Tags.Has<Friendly>()
                                && !targetEntity.Tags.Has<BuildingTag>()) continue;
                            if (ownerEntity.Tags.Has<Hostile>()  && targetEntity.Tags.Has<Hostile>())  continue;
                        }

                        ref readonly var tp = ref tgtPos.Span[ti];
                        ref readonly var bb = ref tgtBbox.Span[ti];

                        // 建筑用 AABB 碰撞检测（比球体更精确）
                        float dist;
                        if (targetEntity.Tags.Has<BuildingTag>() || targetEntity.Tags.Has<VehicleTag>())
                        {
                            dist = RaySegmentAABBDistance(
                                prevX, prevY, prevZ, pp.X, pp.Y, pp.Z,
                                tp.X + bb.MinX, tp.Y + bb.MinY, tp.Z + bb.MinZ,
                                tp.X + bb.MaxX, tp.Y + bb.MaxY, tp.Z + bb.MaxZ);
                        }
                        else
                        {
                            float cy = tp.Y + (bb.MaxY - bb.MinY) * 0.5f;
                            dist = RaySegmentPointDistance(
                                prevX, prevY, prevZ,
                                pp.X, pp.Y, pp.Z,
                                tp.X, cy, tp.Z);
                        }

                        if (dist <= hitRadius)
                        {
                            // 使用投射物当前位置作为命中点（贴近目标表面）
                            hits.Add((projEid, targetEid, new Vector3(pp.X, pp.Y, pp.Z), isExplosive));
                            alreadyHit = true;
                        }
                    }
                }

                // ── 未命中实体 → 检查地面碰撞（投射物 Y ≤ 地形高度）──
                if (!alreadyHit && _terrain != null)
                {
                    float terrainY = _terrain.SampleHeight(pp.X, pp.Z);
                    if (pp.Y <= terrainY)
                    {
                        var groundHitPos = new Vector3(pp.X, terrainY, pp.Z);
                        // targetEid = -1 表示命中地面（无实体目标）
                        hits.Add((projEid, -1, groundHitPos, isExplosive));
                    }
                }
            }
        }

        // 处理命中（在 query 外执行 structural change）
        foreach (var (projEid, targetEid, hitPos, isExplosive) in hits)
        {
            if (targetEid >= 0)
            {
                // 命中实体
                _projectiles.HandleHit(projEid, targetEid, hitPos, _damage);
                OnHit?.Invoke(hitPos);
            }
            else
            {
                // 命中地面（无实体目标）— 销毁投射物
                var projEntity = _store.GetEntityById(projEid);
                if (!projEntity.IsNull) projEntity.AddTag<PendingDestroy>();
                OnHit?.Invoke(hitPos);
            }

            if (isExplosive)
            {
                if (targetEid >= 0)
                    ApplyExplosion(projEid, hitPos);
                else
                {
                    // 地面爆炸 — 仅触发事件（地形破坏由 WorldEnvManager 通过 EventBus 处理）
                    var pe = _store.GetEntityById(projEid);
                    float r = !pe.IsNull && pe.TryGetComponent<ProjectileData>(out var pd2) ? pd2.ExplosionRadius : 3f;
                    OnExplosion?.Invoke(hitPos, r);
                }
            }
            else if (targetEid >= 0)
            {
                ApplyKnockback(projEid, targetEid);
            }
        }
    }

    private void ApplyKnockback(int projEid, int targetEid)
    {
        var targetEntity = _store.GetEntityById(targetEid);
        if (targetEntity.IsNull) return;

        // 载具 / 建筑不受子弹击退
        if (targetEntity.Tags.Has<VehicleTag>() || targetEntity.Tags.Has<BuildingTag>()) return;

        if (!targetEntity.TryGetComponent<WorldPosition>(out var tPos)) return;

        var projEntity = _store.GetEntityById(projEid);
        if (projEntity.IsNull) return;
        if (!projEntity.TryGetComponent<Velocity>(out var projVel)) return;

        // 结构完整性：低伤害无效
        if (projEntity.TryGetComponent<ProjectileData>(out var pd) &&
            targetEntity.TryGetComponent<StructuralIntegrity>(out var si))
        {
            if (pd.Damage < si.DamageThreshold) return; // 子弹打不动建筑
        }

        const float knockback = 0.4f;
        tPos.X += projVel.X * knockback;
        tPos.Z += projVel.Z * knockback;
        targetEntity.AddComponent(tPos);
    }

    /// <summary>
    /// 爆炸处理 — AOE 伤害 + 击退/弹飞 + 结构完整性损坏。
    /// </summary>
    private void ApplyExplosion(int projEid, Vector3 hitPos)
    {
        var projEntity = _store.GetEntityById(projEid);
        if (projEntity.IsNull) return;
        if (!projEntity.TryGetComponent<ProjectileData>(out var pd)) return;

        float radius = pd.ExplosionRadius;
        float baseDamage = pd.Damage;

        OnExplosion?.Invoke(hitPos, radius);

        // 查询所有在爆炸范围内的有生命值实体
        var query = _store.Query<Health, WorldPosition, BoundingBox>();
        var affected = new List<(int eid, float dist)>();

        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk2;
            var entities  = chunk.Entities;
            for (int i = 0; i < entities.Length; i++)
            {
                int eid = entities[i];
                if (eid == pd.OwnerEntityId) continue;

                ref readonly var tp = ref positions.Span[i];
                float dx = tp.X - hitPos.X;
                float dy = tp.Y - hitPos.Y;
                float dz = tp.Z - hitPos.Z;
                float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

                if (dist <= radius)
                    affected.Add((eid, dist));
            }
        }

        // query 外应用伤害和力
        foreach (var (eid, dist) in affected)
        {
            var entity = _store.GetEntityById(eid);
            if (entity.IsNull) continue;

            float falloff = 1f - (dist / radius);
            falloff = MathF.Max(falloff, 0.1f);
            float aoeDamage = baseDamage * falloff;

            // 结构完整性检查
            if (entity.TryGetComponent<StructuralIntegrity>(out var si))
            {
                if (aoeDamage >= si.DamageThreshold)
                {
                    si.AccumulatedDamage += aoeDamage - si.DamageThreshold;
                    entity.AddComponent(si);
                }
                // 低于阈值的伤害被建筑无视
                if (aoeDamage < si.DamageThreshold) continue;
            }

            // 对生命值造成伤害
            _damage.ApplyDamage(eid, pd.OwnerEntityId, aoeDamage,
                (DamageType)pd.DamageType, HitZone.Body, false, hitPos);

            // 击退/弹飞
            if (entity.TryGetComponent<WorldPosition>(out var ePos))
            {
                float dx = ePos.X - hitPos.X;
                float dz = ePos.Z - hitPos.Z;
                float len = MathF.Sqrt(dx * dx + dz * dz);
                if (len > 0.01f)
                {
                    dx /= len; dz /= len;
                }
                else
                {
                    dx = 1; dz = 0;
                }

                // 角色弹飞，载具/建筑只水平晃动
                bool isHeavy = entity.Tags.Has<VehicleTag>() || entity.Tags.Has<BuildingTag>();
                float force = isHeavy ? 0.3f * falloff : 3.0f * falloff;

                ePos.X += dx * force;
                ePos.Z += dz * force;
                if (!isHeavy)
                    ePos.Y += 1.5f * falloff;
                entity.AddComponent(ePos);
            }
        }
    }

    /// <summary>计算射线段到点的最短距离。</summary>
    internal static float RaySegmentPointDistance(
        float ax, float ay, float az,
        float bx, float by, float bz,
        float px, float py, float pz)
    {
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float lenSq = dx * dx + dy * dy + dz * dz;
        if (lenSq < 0.0001f)
        {
            float ex = px - ax, ey = py - ay, ez = pz - az;
            return MathF.Sqrt(ex * ex + ey * ey + ez * ez);
        }

        float t = ((px - ax) * dx + (py - ay) * dy + (pz - az) * dz) / lenSq;
        t = Math.Clamp(t, 0f, 1f);

        float cx = ax + t * dx - px;
        float cy = ay + t * dy - py;
        float cz = az + t * dz - pz;
        return MathF.Sqrt(cx * cx + cy * cy + cz * cz);
    }

    /// <summary>射线段到 AABB 的最短距离（0 = 相交）。</summary>
    internal static float RaySegmentAABBDistance(
        float ax, float ay, float az,
        float bx, float by, float bz,
        float minX, float minY, float minZ,
        float maxX, float maxY, float maxZ)
    {
        // 找射线段上最近点到 AABB 的距离
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float lenSq = dx * dx + dy * dy + dz * dz;

        const int samples = 5;
        float bestDist = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float t = lenSq < 0.0001f ? 0f : (float)i / samples;
            float px = ax + t * dx;
            float py = ay + t * dy;
            float pz = az + t * dz;

            // 计算点到 AABB 的距离
            float cx = MathF.Max(minX - px, MathF.Max(0, px - maxX));
            float cy = MathF.Max(minY - py, MathF.Max(0, py - maxY));
            float cz = MathF.Max(minZ - pz, MathF.Max(0, pz - maxZ));
            float dist = MathF.Sqrt(cx * cx + cy * cy + cz * cz);

            if (dist < bestDist) bestDist = dist;
            if (bestDist <= 0) return 0;
        }

        return bestDist;
    }
}
