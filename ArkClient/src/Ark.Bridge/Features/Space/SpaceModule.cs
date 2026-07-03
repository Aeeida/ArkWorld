using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;
using Ark.Shared.Data;

namespace Ark.Bridge.Features.Space;

/// <summary>
/// 太空飞行模块 — 火箭发射、轨道飞行、飞船操纵。
/// 实现 ISpaceService 接口供上层调用。
/// TODO: 实现完整太空系统
/// </summary>
public sealed class SpaceModule : ISpaceService
{
    private readonly EntityStore _store;
    private SpaceFlightPhase _flightPhase = SpaceFlightPhase.OnPad;

    public int CurrentSpacecraftId => -1;
    public SpaceFlightPhase FlightPhase => _flightPhase;
    public float Altitude => 0f;
    public float OrbitalVelocity => 0f;
    public float RemainingDeltaV => 0f;

    public event Action<SpaceFlightPhase>? OnPhaseChanged;
    public event Action<OrbitalEvent>? OnOrbitalEvent;

    public SpaceModule(EntityStore store)
    {
        _store = store;
    }

    public bool InitiateLaunch()
    {
        if (_flightPhase != SpaceFlightPhase.OnPad) return false;
        _flightPhase = SpaceFlightPhase.PreLaunch;
        OnPhaseChanged?.Invoke(_flightPhase);
        return true;
    }

    public bool Abort()
    {
        _flightPhase = SpaceFlightPhase.OnPad;
        OnPhaseChanged?.Invoke(_flightPhase);
        return true;
    }

    public bool PerformStaging()
    {
        // TODO: 委托给 VesselState.PerformStaging
        return false;
    }

    public void SendSpacecraftInput(SpacecraftInputData input)
    {
        // TODO: 处理飞船输入
    }

    public bool ConfigureSpacecraft(SpacecraftConfig config)
    {
        // TODO: 配置飞船
        return true;
    }

    public OrbitalParams GetOrbitalParams()
    {
        // TODO: 从当前飞行器状态计算
        return default;
    }

    public bool IsInStableOrbit()
    {
        // TODO: 委托给 OrbitalMechanics.IsStableOrbit
        return false;
    }

    /// <summary>每帧更新太空飞行状态</summary>
    public void UpdateSpaceFlight(float deltaTime)
    {
        // TODO: 更新飞行模拟
    }
}
