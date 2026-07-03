using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;

namespace Ark.Systems.Sync;

/// <summary>
/// 载具 HUD 数据推送接收方接口。
/// </summary>
public interface IVehicleHudReceiver
{
    Entity VehicleEntity { get; }
    void OnVehicleHudPushed(in VehicleHudFrame frame);
}

public readonly struct VehicleHudFrame
{
    public readonly bool HasRuntime;
    public readonly RemoteVehicleRuntimeState Runtime;
    public readonly bool HasCombat;
    public readonly RemoteCombatState Combat;
    public readonly bool HasOccupant;
    public readonly RemoteVehicleOccupantState Occupant;

    public VehicleHudFrame(
        bool hasRuntime, RemoteVehicleRuntimeState runtime,
        bool hasCombat, RemoteCombatState combat,
        bool hasOccupant, RemoteVehicleOccupantState occupant)
    {
        HasRuntime = hasRuntime;
        Runtime = runtime;
        HasCombat = hasCombat;
        Combat = combat;
        HasOccupant = hasOccupant;
        Occupant = occupant;
    }
}

/// <summary>
/// 载具 HUD Sync System — 从 ECS 推送给注册的 HUD/Widget 节点。
/// </summary>
public sealed class VehicleHudSyncSystem
{
    private readonly EntityStore _store;
    private readonly List<IVehicleHudReceiver> _receivers = new();

    public VehicleHudSyncSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Register(IVehicleHudReceiver receiver)
    {
        if (receiver is null) return;
        if (!_receivers.Contains(receiver))
            _receivers.Add(receiver);
    }

    public void Unregister(IVehicleHudReceiver receiver)
    {
        if (receiver is null) return;
        _receivers.Remove(receiver);
    }

    public void Update()
    {
        for (int i = 0; i < _receivers.Count; i++)
        {
            var receiver = _receivers[i];
            var entity = receiver.VehicleEntity;
            if (entity.IsNull)
                continue;

            bool hasRuntime = entity.TryGetComponent<RemoteVehicleRuntimeState>(out var runtime);
            bool hasCombat = entity.TryGetComponent<RemoteCombatState>(out var combat);
            bool hasOccupant = entity.TryGetComponent<RemoteVehicleOccupantState>(out var occupant);

            var frame = new VehicleHudFrame(hasRuntime, runtime, hasCombat, combat, hasOccupant, occupant);

            try
            {
                receiver.OnVehicleHudPushed(frame);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[VehicleHudSync] receiver threw: {ex.Message}");
            }
        }
    }
}
