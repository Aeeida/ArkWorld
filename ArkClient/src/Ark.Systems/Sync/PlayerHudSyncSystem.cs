using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;

namespace Ark.Systems.Sync;

/// <summary>
/// 玩家 HUD 数据推送接收方接口（Phase 3 取代 Bootstrap 直接 TryGetComponent）。
/// </summary>
public interface IPlayerHudReceiver
{
    Entity PlayerEntity { get; }
    void OnPlayerHudPushed(in PlayerHudFrame frame);
}

/// <summary>
/// 玩家相关 ECS 组件聚合帧 — 由 <see cref="PlayerHudSyncSystem"/> 每帧推送。
/// </summary>
public readonly struct PlayerHudFrame
{
    public readonly bool HasLocalControl;
    public readonly LocalControlState LocalControl;

    public readonly bool HasAmmo;
    public readonly AmmoState Ammo;

    public readonly bool HasMountedWeapon;
    public readonly MountedWeaponRuntimeState MountedWeapon;

    public readonly bool HasRemoteAnimation;
    public readonly RemoteAnimationState RemoteAnimation;

    public readonly bool HasRemoteInventory;
    public readonly RemoteInventoryState RemoteInventory;

    public readonly bool HasRemoteQuest;
    public readonly RemoteQuestState RemoteQuest;

    public readonly bool HasRemoteWorldService;
    public readonly RemoteWorldServiceState RemoteWorldService;

    public PlayerHudFrame(
        bool hasLocalControl, LocalControlState localControl,
        bool hasAmmo, AmmoState ammo,
        bool hasMountedWeapon, MountedWeaponRuntimeState mountedWeapon,
        bool hasRemoteAnimation, RemoteAnimationState remoteAnimation,
        bool hasRemoteInventory, RemoteInventoryState remoteInventory,
        bool hasRemoteQuest, RemoteQuestState remoteQuest,
        bool hasRemoteWorldService, RemoteWorldServiceState remoteWorldService)
    {
        HasLocalControl = hasLocalControl;
        LocalControl = localControl;
        HasAmmo = hasAmmo;
        Ammo = ammo;
        HasMountedWeapon = hasMountedWeapon;
        MountedWeapon = mountedWeapon;
        HasRemoteAnimation = hasRemoteAnimation;
        RemoteAnimation = remoteAnimation;
        HasRemoteInventory = hasRemoteInventory;
        RemoteInventory = remoteInventory;
        HasRemoteQuest = hasRemoteQuest;
        RemoteQuest = remoteQuest;
        HasRemoteWorldService = hasRemoteWorldService;
        RemoteWorldService = remoteWorldService;
    }
}

/// <summary>
/// 玩家 HUD Sync System — 从 ECS 推送给注册的 HUD/PerfHud/Bootstrap orchestrator。
/// </summary>
public sealed class PlayerHudSyncSystem
{
    private readonly EntityStore _store;
    private readonly List<IPlayerHudReceiver> _receivers = new();

    public PlayerHudSyncSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Register(IPlayerHudReceiver receiver)
    {
        if (receiver is null) return;
        if (!_receivers.Contains(receiver))
            _receivers.Add(receiver);
    }

    public void Unregister(IPlayerHudReceiver receiver)
    {
        if (receiver is null) return;
        _receivers.Remove(receiver);
    }

    public void Update()
    {
        for (int i = 0; i < _receivers.Count; i++)
        {
            var receiver = _receivers[i];
            var entity = receiver.PlayerEntity;
            if (entity.IsNull)
                continue;

            bool hasLocalControl = entity.TryGetComponent<LocalControlState>(out var localControl);
            bool hasAmmo = entity.TryGetComponent<AmmoState>(out var ammo);
            bool hasMountedWeapon = entity.TryGetComponent<MountedWeaponRuntimeState>(out var mountedWeapon);
            bool hasRemoteAnimation = entity.TryGetComponent<RemoteAnimationState>(out var remoteAnimation);
            bool hasRemoteInventory = entity.TryGetComponent<RemoteInventoryState>(out var remoteInventory);
            bool hasRemoteQuest = entity.TryGetComponent<RemoteQuestState>(out var remoteQuest);
            bool hasRemoteWorldService = entity.TryGetComponent<RemoteWorldServiceState>(out var remoteWorldService);

            var frame = new PlayerHudFrame(
                hasLocalControl, localControl,
                hasAmmo, ammo,
                hasMountedWeapon, mountedWeapon,
                hasRemoteAnimation, remoteAnimation,
                hasRemoteInventory, remoteInventory,
                hasRemoteQuest, remoteQuest,
                hasRemoteWorldService, remoteWorldService);

            try
            {
                receiver.OnPlayerHudPushed(frame);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[PlayerHudSync] receiver threw: {ex.Message}");
            }
        }
    }
}
