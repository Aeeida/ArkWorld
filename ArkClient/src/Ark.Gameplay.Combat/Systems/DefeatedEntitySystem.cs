using System;
using Friflo.Engine.ECS;
using Ark.Abstractions;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 战败实体物理系统 — 对 Defeated 标记的实体施加重力、上抛和地面限制。
///
/// 职责：
///   • 爆炸物命中时添加上抛速度
///   • 每帧应用重力
///   • 落地后摩擦减速
///   • 使用地形高度（非 Y=0）确定地面位置
/// </summary>
public sealed class DefeatedEntitySystem
{
    private readonly EntityStore _store;
    private ITerrainQuery? _terrain;
    private const float Gravity = 15f;
    private const float GroundFriction = 0.9f;

    public DefeatedEntitySystem(EntityStore store, ITerrainQuery? terrain = null)
    {
        _store = store;
        _terrain = terrain;
    }

    /// <summary>设置地形查询（延迟注入，地形可能在系统之后初始化）。</summary>
    public void SetTerrainQuery(ITerrainQuery? terrain) => _terrain = terrain;

    public void Update(float dt)
    {
        var query = _store.Query<WorldPosition, Velocity>()
            .AllTags(Tags.Get<Defeated>());

        foreach (var chunk in query.Chunks)
        {
            var positions  = chunk.Chunk1;
            var velocities = chunk.Chunk2;
            var entities   = chunk.Entities;

            for (int i = 0; i < entities.Length; i++)
            {
                ref var pos = ref positions.Span[i];
                ref var vel = ref velocities.Span[i];

                var entity = _store.GetEntityById(entities[i]);

                // 查询当前位置的地形高度
                float groundY = _terrain?.SampleHeight(pos.X, pos.Z) ?? 0f;

                // 爆炸物命中 — 添加上抛速度
                if (entity.TryGetComponent<DamageBuffer>(out var dmgBuf) &&
                    dmgBuf.DamageType == (byte)DamageType.Explosive)
                {
                    vel.Y = 8f + (float)(Random.Shared.NextDouble() * 5);
                    vel.X = (float)(Random.Shared.NextDouble() * 6 - 3);
                    vel.Z = (float)(Random.Shared.NextDouble() * 6 - 3);
                }

                // 重力 + 地面限制（使用真实地形高度）
                if (pos.Y > groundY + 0.05f)
                {
                    vel.Y -= Gravity * dt;
                    pos.X += vel.X * dt;
                    pos.Y += vel.Y * dt;
                    pos.Z += vel.Z * dt;

                    // 重新查询移动后的地形高度
                    float newGroundY = _terrain?.SampleHeight(pos.X, pos.Z) ?? 0f;
                    if (pos.Y < newGroundY) pos.Y = newGroundY;
                }
                else
                {
                    vel.X *= GroundFriction;
                    vel.Z *= GroundFriction;
                    vel.Y = 0;
                    pos.Y = groundY;
                }
            }
        }
    }
}
