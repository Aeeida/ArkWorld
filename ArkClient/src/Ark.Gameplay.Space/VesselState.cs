using System.Numerics;
using Ark.Configuration;
using Ark.Events;
using Ark.Shared.Data;

namespace Ark.Gameplay.Space;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║    飞行器状态 — 管理零件、分级、推力、燃料                                       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 飞行器运行时状态 — 持有零件列表和实时飞行参数。
/// </summary>
public sealed class VesselState
{
    public int EntityId { get; }

    // ── 零件 & 分级 ──
    private readonly List<RocketPartDef> _parts = [];
    private readonly List<StageDef> _stages = [];
    private int _currentStageIndex;

    // ── 飞行状态 ──
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public SpaceFlightPhase Phase { get; set; } = SpaceFlightPhase.OnPad;

    public VesselState(int entityId)
    {
        EntityId = entityId;
    }

    public void AddPart(RocketPartDef part) => _parts.Add(part);
    public IReadOnlyList<RocketPartDef> Parts => _parts;
    public IReadOnlyList<StageDef> Stages => _stages;
    public int CurrentStageIndex => _currentStageIndex;

    public void DefineStage(int index, int[] partIds)
    {
        _stages.Add(new StageDef(index, partIds));
    }

    /// <summary>总质量（所有零件干质量 + 剩余燃料，简化模型）。</summary>
    public float TotalMass()
    {
        float m = 0;
        foreach (var p in _parts)
            m += p.DryMass + p.FuelCapacity; // 简化：fuel mass = capacity
        return m;
    }

    /// <summary>干质量（零件干质量总和）。</summary>
    public float DryMass()
    {
        float m = 0;
        foreach (var p in _parts)
            m += p.DryMass;
        return m;
    }

    /// <summary>当前级总推力。</summary>
    public float StageThrust()
    {
        if (_currentStageIndex >= _stages.Count) return 0;
        var stage = _stages[_currentStageIndex];
        float thrust = 0;
        foreach (int pid in stage.PartIds)
        {
            var part = _parts.FirstOrDefault(p => p.PartId == pid);
            thrust += part.MaxThrust;
        }
        return thrust;
    }

    /// <summary>
    /// 计算整船 ΔV（齐奥尔科夫斯基，平均 ISP）。
    /// </summary>
    public float CalculateDeltaV()
    {
        float wet = TotalMass();
        float dry = DryMass();
        if (dry <= 0 || wet <= dry) return 0;

        // 推力加权平均 ISP
        float totalThrust = 0, weightedIsp = 0;
        foreach (var p in _parts)
        {
            if (p.MaxThrust > 0)
            {
                totalThrust += p.MaxThrust;
                weightedIsp += p.MaxThrust * p.ISP;
            }
        }
        float avgIsp = totalThrust > 0 ? weightedIsp / totalThrust : 0;
        return OrbitalMechanics.TsiolkovskyDeltaV(avgIsp, wet, dry);
    }

    /// <summary>执行分级 — 丢弃当前级零件，推进到下一级。</summary>
    public bool PerformStaging(EventBus? eventBus = null)
    {
        if (_currentStageIndex >= _stages.Count) return false;

        var stage = _stages[_currentStageIndex];
        // 移除当前级零件
        _parts.RemoveAll(p => stage.PartIds.Contains(p.PartId));

        eventBus?.Publish(new StageSeparationEvent(EntityId, _currentStageIndex));
        _currentStageIndex++;
        return true;
    }

    /// <summary>当前高度（基于 Position.Y，简化）。</summary>
    public float Altitude => MathF.Max(0, Position.Y);
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║    物理模拟器 — 每步推进飞行器状态（推力 + 重力 + 大气阻力）                     ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 飞行物理模拟器 — 逐步推进，支持时间加速。
/// </summary>
public sealed class FlightSimulator
{
    private readonly EventBus _eventBus;

    public FlightSimulator(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>模拟单步物理。</summary>
    public void Step(VesselState vessel, ThrustCommand cmd, float dt)
    {
        if (vessel.Phase == SpaceFlightPhase.OnPad) return;

        float mass = vessel.TotalMass();
        if (mass <= 0) return;

        float alt = vessel.Altitude;

        // 推力
        float thrust = vessel.StageThrust() * cmd.Throttle;
        Vector3 thrustForce = cmd.Direction * thrust;

        // 重力（简化为向下）
        float g = OrbitalMechanics.GravityAt(alt);
        Vector3 gravity = new(0, -g * mass, 0);

        // 大气阻力（简化）
        float density = OrbitalMechanics.AtmosphereDensity(alt);
        float speed = vessel.Velocity.Length();
        float drag = 0.5f * density * speed * speed * 0.2f; // Cd*A ≈ 0.2
        Vector3 dragForce = speed > 0.01f
            ? -Vector3.Normalize(vessel.Velocity) * drag
            : Vector3.Zero;

        // 合力 → 加速度 → 积分
        Vector3 accel = (thrustForce + gravity + dragForce) / mass;
        vessel.Velocity += accel * dt;
        vessel.Position += vessel.Velocity * dt;

        // 地面碰撞
        if (vessel.Position.Y < 0)
        {
            vessel.Position = new Vector3(vessel.Position.X, 0, vessel.Position.Z);
            vessel.Velocity = Vector3.Zero;
        }
    }
}
