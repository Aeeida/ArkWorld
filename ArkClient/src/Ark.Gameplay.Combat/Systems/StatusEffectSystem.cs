using System;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 状态效果系统 — 处理持续性增益/减益。
/// </summary>
public sealed class StatusEffectSystem
{
    private readonly EntityStore _store;
    private readonly DamageSystem _damageSystem;

    public StatusEffectSystem(EntityStore store, DamageSystem damageSystem)
    {
        _store = store;
        _damageSystem = damageSystem;
    }

    public void Update(float deltaTime, float gameTime)
    {
        // ── 1. 处理效果 tick，收集过期效果（不能在 query loop 内做 structural change）──
        var expired = new System.Collections.Generic.List<(int eid, StatusEffectType type)>();
        var healthUpdates = new System.Collections.Generic.List<(int eid, Health hp)>();
        var combatStatsUpdates = new System.Collections.Generic.List<(int eid, CombatStats stats)>();

        var query = _store.Query<StatusEffect>();

        foreach (var chunk in query.Chunks)
        {
            var effects  = chunk.Chunk1;
            var entities = chunk.Entities;

            for (int i = 0; i < entities.Length; i++)
            {
                ref var effect = ref effects.Span[i];
                int eid        = entities[i];
                var entity     = _store.GetEntityById(eid);

                effect.Duration -= deltaTime;

                if (effect.Duration <= 0)
                {
                    expired.Add((eid, (StatusEffectType)effect.EffectType));
                    continue;
                }

                switch ((StatusEffectType)effect.EffectType)
                {
                    case StatusEffectType.Burning:
                    case StatusEffectType.Poisoned:
                        var dmgType = (StatusEffectType)effect.EffectType == StatusEffectType.Burning
                            ? DamageType.Fire : DamageType.Poison;
                        _damageSystem.ApplyDamage(eid, effect.SourceEntityId,
                            effect.TickValue * deltaTime, dmgType);
                        break;

                    case StatusEffectType.Healing:
                        if (entity.TryGetComponent<Health>(out var hp))
                        {
                            hp.Current = Math.Min(hp.Current + effect.TickValue * deltaTime, hp.Max);
                            healthUpdates.Add((eid, hp));
                        }
                        break;

                    case StatusEffectType.Slowed:
                        if (entity.TryGetComponent<CombatStats>(out var slowStats))
                        {
                            slowStats.MoveSpeedMul = Math.Max(0.1f, 1f - effect.Intensity);
                            combatStatsUpdates.Add((eid, slowStats));
                        }
                        break;

                    case StatusEffectType.Haste:
                        if (entity.TryGetComponent<CombatStats>(out var hasteStats))
                        {
                            hasteStats.MoveSpeedMul = 1f + effect.Intensity;
                            combatStatsUpdates.Add((eid, hasteStats));
                        }
                        break;
                }
            }
        }

        // ── 2. query 外统一写回组件（closure pass）──
        foreach (var (eid, hp) in healthUpdates)
        {
            var entity = _store.GetEntityById(eid);
            if (!entity.IsNull)
                entity.AddComponent(hp);
        }

        foreach (var (eid, stats) in combatStatsUpdates)
        {
            var entity = _store.GetEntityById(eid);
            if (!entity.IsNull)
                entity.AddComponent(stats);
        }

        // ── 3. 在 query 外清理过期效果 ──
        foreach (var (eid, type) in expired)
        {
            var entity = _store.GetEntityById(eid);
            if (entity.IsNull) continue;
            RemoveEffectTags(entity, type);
            entity.RemoveComponent<StatusEffect>();
        }
    }

    public void ApplyEffect(int targetEntityId, int sourceEntityId,
        StatusEffectType effectType, float duration, float tickValue, float intensity = 1f)
    {
        var target = _store.GetEntityById(targetEntityId);
        if (target.IsNull) return;

        target.AddComponent(new StatusEffect
        {
            EffectType     = (byte)effectType,
            Duration       = duration,
            TickValue      = tickValue,
            Intensity      = intensity,
            SourceEntityId = sourceEntityId,
        });
        ApplyEffectTags(target, effectType);
    }

    private static void ApplyEffectTags(Entity entity, StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Stunned: entity.AddTag<Stunned>(); break;
            case StatusEffectType.Shield:  entity.AddTag<Shielded>(); break;
        }
    }

    private static void RemoveEffectTags(Entity entity, StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Stunned: entity.RemoveTag<Stunned>(); break;
            case StatusEffectType.Shield:  entity.RemoveTag<Shielded>(); break;
            case StatusEffectType.Slowed:
            case StatusEffectType.Haste:
                if (entity.TryGetComponent<CombatStats>(out var stats))
                {
                    stats.MoveSpeedMul = 1f;
                    entity.AddComponent(stats);
                }
                break;
        }
    }
}
