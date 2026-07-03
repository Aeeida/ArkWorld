using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 生命值系统 — 处理生命恢复。
/// </summary>
public sealed class HealthSystem
{
    private readonly EntityStore _store;

    public HealthSystem(EntityStore store)
    {
        _store = store;
    }

    public void Update(float deltaTime, float gameTime)
    {
        var regenQuery = _store.Query<Health>();

        foreach (var chunk in regenQuery.Chunks)
        {
            var healths = chunk.Chunk1;

            for (int i = 0; i < chunk.Length; i++)
            {
                ref var hp = ref healths.Span[i];

                if (hp.IsDead) continue;
                if (hp.Current >= hp.Max) continue;
                if (hp.RegenRate <= 0) continue;
                if (gameTime - hp.LastDamageTime < hp.RegenDelay) continue;

                hp.Current = Math.Min(hp.Current + hp.RegenRate * deltaTime, hp.Max);
            }
        }
    }
}
