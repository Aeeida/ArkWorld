using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Gameplay.Squad;

/// <summary>
/// 玩家实体原型 — 创建具有完整战斗/小队属性的玩家 ECS 实体。
/// </summary>
public static class PlayerArchetype
{
    /// <summary>
    /// 确保玩家实体拥有所有必需的组件和标签。
    /// 如果已有组件则不覆盖。
    /// </summary>
    public static Entity EnsureComplete(EntityStore store, Entity entity)
    {
        if (entity.IsNull)
        {
            entity = store.CreateEntity();
        }

        if (!entity.HasComponent<WorldPosition>())
            entity.AddComponent(new WorldPosition { X = 0, Y = 1, Z = 0 });

        if (!entity.HasComponent<WorldRotation>())
            entity.AddComponent(new WorldRotation { W = 1 });

        if (!entity.HasComponent<Velocity>())
            entity.AddComponent(new Velocity());

        if (!entity.HasComponent<MoveInput>())
            entity.AddComponent(new MoveInput());

        if (!entity.HasComponent<Health>())
            entity.AddComponent(new Health { Current = 100, Max = 100, RegenRate = 5f, RegenDelay = 3f });

        if (!entity.HasComponent<CombatStats>())
            entity.AddComponent(new CombatStats
            {
                AttackPower       = 10,
                DamageReductionMul = 1f,
                CritChance        = 0.05f,
                CritMultiplier    = 1.5f,
                AttackSpeedMul    = 1f,
                MoveSpeedMul      = 1f,
            });

        if (!entity.HasComponent<BoundingBox>())
            entity.AddComponent(new BoundingBox
            {
                MinX = -0.4f, MinY = 0, MinZ = -0.4f,
                MaxX =  0.4f, MaxY = 1.8f, MaxZ = 0.4f,
            });

        if (!entity.HasComponent<CombatTarget>())
            entity.AddComponent(CombatTarget.None);

        if (!entity.Tags.Has<LocalPlayer>()) entity.AddTag<LocalPlayer>();
        if (!entity.Tags.Has<HasNode>())     entity.AddTag<HasNode>();
        if (!entity.Tags.Has<Friendly>())    entity.AddTag<Friendly>();

        return entity;
    }
}
