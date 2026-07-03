using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 近战系统 — 处理格斗、挥刀、格挡。
/// </summary>
public sealed class MeleeSystem
{
    private readonly EntityStore _store;
    private readonly DamageSystem _damageSystem;

    public event Action<int, int>? OnMeleeHit;
    public event Action<int, int>? OnBlocked;

    public MeleeSystem(EntityStore store, DamageSystem damageSystem)
    {
        _store = store;
        _damageSystem = damageSystem;
    }

    public void PerformMeleeAttack(
        int attackerEntityId, Vector3 attackerPos, Vector3 attackerForward,
        float attackRange = 2.5f, float attackAngle = 90f)
    {
        var attacker = _store.GetEntityById(attackerEntityId);
        if (attacker.IsNull) return;
        if (attacker.Tags.Has<Stunned>() || attacker.Tags.Has<Defeated>()) return;

        attacker.AddTag<MeleeAttacking>();

        float baseDamage = 10f;
        float critChance = 0.05f;
        float critMul    = 1.5f;
        if (attacker.TryGetComponent<CombatStats>(out var stats))
        {
            baseDamage = stats.AttackPower;
            critChance = stats.CritChance;
            critMul    = stats.CritMultiplier;
        }

        if (attacker.TryGetComponent<WeaponState>(out var weapon) &&
            weapon.Category == (byte)WeaponCategory.Melee)
            baseDamage *= 1.5f;

        float halfAngle = attackAngle * 0.5f * (MathF.PI / 180f);
        var fwdNorm = Vector3.Normalize(new Vector3(attackerForward.X, 0, attackerForward.Z));

        // ── 收集命中目标（不能在 query loop 内做 structural change）──
        var hits = new System.Collections.Generic.List<(int targetEid, float damage, bool isCrit, Vector3 hitPos)>();

        var targetQuery = _store.Query<WorldPosition, Health>();

        foreach (var chunk in targetQuery.Chunks)
        {
            var positions = chunk.Chunk1;
            var entities  = chunk.Entities;

            for (int i = 0; i < entities.Length; i++)
            {
                int targetEid   = entities[i];
                if (targetEid == attackerEntityId) continue;

                var targetEntity = _store.GetEntityById(targetEid);
                if (targetEntity.Tags.Has<Dead>()) continue;

                ref readonly var targetPos = ref positions.Span[i];
                var toTarget = new Vector3(targetPos.X - attackerPos.X, 0, targetPos.Z - attackerPos.Z);
                float dist = toTarget.Length();

                if (dist > attackRange || dist < 0.01f) continue;

                var toTargetNorm = toTarget / dist;
                float dot = Vector3.Dot(fwdNorm, toTargetNorm);
                if (dot < MathF.Cos(halfAngle)) continue;

                float dmg = baseDamage;

                // 格挡检测
                if (targetEntity.Tags.Has<Blocking>())
                {
                    if (targetEntity.TryGetComponent<WorldRotation>(out var targetRot))
                    {
                        var targetQuat = new Quaternion(targetRot.X, targetRot.Y, targetRot.Z, targetRot.W);
                        var targetFwd = Vector3.Transform(-Vector3.UnitZ, targetQuat);
                        targetFwd.Y = 0;
                        targetFwd = Vector3.Normalize(targetFwd);
                        if (Vector3.Dot(targetFwd, -toTargetNorm) > 0.5f)
                        {
                            OnBlocked?.Invoke(targetEid, attackerEntityId);
                            dmg *= 0.2f;
                        }
                    }
                }

                bool isCrit = Random.Shared.NextSingle() < critChance;
                float finalDamage = isCrit ? dmg * critMul : dmg;

                hits.Add((targetEid, finalDamage, isCrit,
                    new Vector3(targetPos.X, targetPos.Y + 1f, targetPos.Z)));
            }
        }

        // ── 在 query 外应用伤害 ──
        foreach (var (targetEid, damage, isCrit, hitPos) in hits)
        {
            _damageSystem.ApplyDamage(targetEid, attackerEntityId,
                damage, DamageType.Physical, HitZone.Body, isCrit, hitPos);
            OnMeleeHit?.Invoke(attackerEntityId, targetEid);
        }
    }

    public void StartBlocking(int entityId)
    {
        var entity = _store.GetEntityById(entityId);
        if (!entity.IsNull) entity.AddTag<Blocking>();
    }

    public void StopBlocking(int entityId)
    {
        var entity = _store.GetEntityById(entityId);
        if (!entity.IsNull) entity.RemoveTag<Blocking>();
    }
}
