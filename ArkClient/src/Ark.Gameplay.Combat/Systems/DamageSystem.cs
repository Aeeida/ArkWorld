using System;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 伤害系统 — 处理 DamageBuffer，计算减免，更新 Health，触发事件。
/// </summary>
public sealed class DamageSystem
{
    private readonly EntityStore _store;

    public event Action<DamageResult>? OnDamageApplied;
    public event Action<KillEvent>? OnKill;

    public DamageSystem(EntityStore store)
    {
        _store = store;
    }

    public void Update(float gameTime)
    {
        // ── 1. 读取伤害数据，收集结果（不能在 query loop 内做 structural change）──
        var killed  = new System.Collections.Generic.List<(int eid, int sourceEid)>();
        var damaged = new System.Collections.Generic.List<int>();

        var query = _store.Query<Health, DamageBuffer>();

        foreach (var chunk in query.Chunks)
        {
            var healths  = chunk.Chunk1;
            var buffers  = chunk.Chunk2;
            var entities = chunk.Entities;

            for (int i = 0; i < entities.Length; i++)
            {
                ref var hp           = ref healths.Span[i];
                ref readonly var dmg = ref buffers.Span[i];
                int eid              = entities[i];
                var entity           = _store.GetEntityById(eid);

                if (hp.IsDead) continue;
                if (entity.Tags.Has<Invulnerable>()) continue;

                // 护甲减免
                float armorAbsorbed = 0f;
                if (entity.TryGetComponent<Armor>(out var armor))
                {
                    float reduction = dmg.DamageType switch
                    {
                        (byte)DamageType.Physical  => armor.PhysicalReduction,
                        (byte)DamageType.Energy    => armor.EnergyReduction,
                        (byte)DamageType.Explosive => (armor.PhysicalReduction + armor.EnergyReduction) * 0.5f,
                        _ => 0f,
                    };
                    armorAbsorbed = dmg.RawDamage * Math.Clamp(reduction, 0f, 0.9f);
                }

                // 战斗属性减免
                float reductionMul = 1f;
                if (entity.TryGetComponent<CombatStats>(out var stats))
                    reductionMul = stats.DamageReductionMul;

                float zoneMul = dmg.HitZone switch
                {
                    (byte)HitZone.Head => 2.0f,
                    (byte)HitZone.Limb => 0.75f,
                    _ => 1.0f,
                };

                float critMul = dmg.IsCrit != 0 ? 1.5f : 1.0f;
                float finalDmg = Math.Max((dmg.RawDamage - armorAbsorbed) * zoneMul * critMul * reductionMul, 0f);

                hp.Current -= finalDmg;
                hp.LastDamageTime = gameTime;

                bool isKill = hp.Current <= 0;
                if (isKill) hp.Current = 0;

                OnDamageApplied?.Invoke(new DamageResult(
                    dmg.SourceEntityId, eid,
                    dmg.RawDamage, finalDmg, armorAbsorbed,
                    (DamageType)dmg.DamageType, (HitZone)dmg.HitZone,
                    dmg.IsCrit != 0, isKill,
                    new System.Numerics.Vector3(dmg.HitX, dmg.HitY, dmg.HitZ)));

                if (isKill)
                    killed.Add((eid, dmg.SourceEntityId));
                else
                    damaged.Add(eid);
            }
        }

        // ── 2. 在 query 外应用 structural change ──
        foreach (var (eid, sourceEid) in killed)
        {
            var e = _store.GetEntityById(eid);
            if (!e.IsNull)
            {
                e.AddTag<Defeated>();
                e.AddTag<Dead>();
            }
            OnKill?.Invoke(new KillEvent(sourceEid, eid, 0, 0f));
        }

        foreach (var eid in damaged)
        {
            var e = _store.GetEntityById(eid);
            if (!e.IsNull) e.AddTag<TakingDamage>();
        }

        // ── 3. 清理 TakingDamage 标记 ──
        CleanupTags<TakingDamage>();

        // ── 4. 清理已死亡实体的 DamageBuffer ──
        CleanupDeadBuffers();
    }

    /// <summary>收集 → 迭代外移除 TakingDamage + DamageBuffer。</summary>
    private void CleanupTags<T>() where T : struct, ITag
    {
        var ids = new System.Collections.Generic.List<int>();
        var q = _store.Query<DamageBuffer>().AllTags(Tags.Get<T>());
        foreach (var chunk in q.Chunks)
        {
            var ents = chunk.Entities;
            for (int i = 0; i < ents.Length; i++)
                ids.Add(ents[i]);
        }
        foreach (var eid in ids)
        {
            var e = _store.GetEntityById(eid);
            if (e.IsNull) continue;
            e.RemoveComponent<DamageBuffer>();
            e.RemoveTag<T>();
        }
    }

    /// <summary>收集 → 迭代外移除 Dead 实体的 DamageBuffer。</summary>
    private void CleanupDeadBuffers()
    {
        var ids = new System.Collections.Generic.List<int>();
        var q = _store.Query<DamageBuffer>().AllTags(Tags.Get<Dead>());
        foreach (var chunk in q.Chunks)
        {
            var ents = chunk.Entities;
            for (int i = 0; i < ents.Length; i++)
                ids.Add(ents[i]);
        }
        foreach (var eid in ids)
        {
            var e = _store.GetEntityById(eid);
            if (!e.IsNull) e.RemoveComponent<DamageBuffer>();
        }
    }

    /// <summary>对目标实体施加伤害。</summary>
    public void ApplyDamage(int targetEntityId, int sourceEntityId, float rawDamage,
        DamageType damageType, HitZone hitZone = HitZone.Body, bool isCrit = false,
        System.Numerics.Vector3 hitPosition = default)
    {
        var target = _store.GetEntityById(targetEntityId);
        if (target.IsNull) return;
        if (!target.HasComponent<Health>()) return;

        target.AddComponent(new DamageBuffer
        {
            SourceEntityId = sourceEntityId,
            RawDamage      = rawDamage,
            DamageType     = (byte)damageType,
            IsCrit         = (byte)(isCrit ? 1 : 0),
            HitZone        = (byte)hitZone,
            HitX = hitPosition.X, HitY = hitPosition.Y, HitZ = hitPosition.Z,
        });
    }
}
