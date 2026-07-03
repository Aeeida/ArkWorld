using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Abstractions;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Vehicle;

/// <summary>
/// 载具地形贴附系统 — 独立于玩家控制器，每帧将所有地面载具 Y 坐标校正到地形表面。
///
/// 规则：
///   • 地面载具（Car, Tank, AntiAir）→ 始终贴附地形
///   • 飞行器（Plane, Helicopter, Rocket）→ 不贴附（飞行中由物理/控制系统管理）
///   • 船（Boat）→ 不贴附（由水面系统管理）
///
/// 这是载具的固有属性，与驾驶者身份（玩家/AI/敌人）无关。
/// </summary>
public sealed class VehicleTerrainSystem
{
    private readonly EntityStore _store;
    private ITerrainQuery? _terrain;

    public VehicleTerrainSystem(EntityStore store)
    {
        _store = store;
    }

    /// <summary>设置地形查询（延迟注入）。</summary>
    public void SetTerrainQuery(ITerrainQuery? terrain) => _terrain = terrain;

    /// <summary>
    /// 每帧调用 — 将地面载具 Y 坐标校正到地形表面。
    /// 飞行器只做最低高度保护（不强制落地），确保不穿过地形。
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_terrain == null) return;

        var query = _store.Query<VehicleState, WorldPosition>()
            .AllTags(Tags.Get<VehicleTag>());

        foreach (var chunk in query.Chunks)
        {
            var vehicles  = chunk.Chunk1;
            var positions = chunk.Chunk2;

            for (int i = 0; i < chunk.Length; i++)
            {
                ref var vehicle = ref vehicles.Span[i];
                ref var pos = ref positions.Span[i];

                var type = (VehicleType)vehicle.VehicleType;
                float terrainY = _terrain.SampleHeight(pos.X, pos.Z);

                if (IsGroundVehicle(type))
                {
                    // 地面载具：始终贴附地形
                    pos.Y = terrainY;
                }
                else if (IsAirVehicle(type))
                {
                    // 飞行器：只做最低高度保护（悬停/不穿地）
                    if (pos.Y < terrainY)
                        pos.Y = terrainY;
                }
            }
        }
    }

    /// <summary>判断载具类型是否为地面载具。</summary>
    public static bool IsGroundVehicle(VehicleType type)
        => type is VehicleType.Car or VehicleType.Tank or VehicleType.AntiAir;

    /// <summary>判断载具类型是否为飞行器。</summary>
    public static bool IsAirVehicle(VehicleType type)
        => type is VehicleType.Plane;
}
