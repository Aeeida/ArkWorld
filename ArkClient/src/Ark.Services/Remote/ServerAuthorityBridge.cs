using System;
using System.Numerics;
using Ark.Ecs.Components;
using Ark.Shared.Data;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 服务端权威桥接器 — 在网络模式下拦截本地游戏动作，
/// 将其路由到服务端验证。
///
/// 职责：
///   • 建筑放置/销毁 → PlaceBuildingCommandDto
///   • 武器开火/换弹 → FireWeaponCommandDto / ReloadWeaponCommandDto
///   • 载具生成/进出 → SpawnVehicleCommandDto / VehicleActionCommandDto
///   • 火箭组装/发射 → AssembleRocketCommandDto / LaunchRocketCommandDto
///   • 离散权威请求统一走 ECS 命令 → 本桥接器 → SignalR RPC
///
/// 设计原则：
///   客户端可做预测（Ghost 预览、本地射击视觉），但实际状态一律以服务端为准。
/// </summary>
public sealed class ServerAuthorityBridge
{
    private readonly Networking.NetworkManager _network;

    public System.Guid PlayerId { get; set; }

    public ServerAuthorityBridge(
        Networking.NetworkManager network,
        System.Guid playerId)
    {
        _network = network;
        PlayerId = playerId;
    }

    // ═══════════════════════════════════════════════════════════════════
    //                     客户端动作 → 服务端命令
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 请求放置建筑 — 替代本地 BaseBuildingModule.PlaceBuildingAt()。
    /// </summary>
    public void RequestPlaceBuilding(int buildingTypeId, Vector3 position, float rotationY)
    {
        _ = _network.SignalR.PlaceBuildingAsync(new PlaceBuildingCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            BuildingTypeId: buildingTypeId,
            X: position.X,
            Y: position.Y,
            Z: position.Z,
            RotationY: rotationY
        ), System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// 请求开火 — 替代本地 CombatGameplayModule.TryFire()。
    /// </summary>
    public void RequestFireWeapon(int weaponDefId, Vector3 origin, Vector3 direction)
    {
        if (weaponDefId <= 0)
            return;

        _ = _network.SignalR.FireWeaponAsync(new FireWeaponCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            WeaponDefId: weaponDefId,
            OriginX: origin.X,
            OriginY: origin.Y,
            OriginZ: origin.Z,
            DirX: direction.X,
            DirY: direction.Y,
            DirZ: direction.Z
        ), System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// 请求换弹 — 替代本地 CombatGameplayModule.TryReload()。
    /// </summary>
    public void RequestReload(int weaponDefId)
    {
        if (weaponDefId <= 0)
            return;

        _ = _network.SignalR.ReloadWeaponAsync(new ReloadWeaponCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            WeaponDefId: weaponDefId
        ), System.Threading.CancellationToken.None);
    }

    public void RequestSeatWeaponFaultClear()
    {
        _ = _network.SignalR.SeatWeaponInteractAsync(new SeatWeaponInteractCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            ActionKind: 0
        ), System.Threading.CancellationToken.None);
    }

    public void RequestSeatWeaponMaintenance()
    {
        _ = _network.SignalR.SeatWeaponInteractAsync(new SeatWeaponInteractCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            ActionKind: 1
        ), System.Threading.CancellationToken.None);
    }

    public bool RequestVehicleAction(NetworkVehicleActionKind actionKind, int snapshotVehicleEntityId, int seatIndex = 0)
    {
        if (!TryResolveVehicleNetworkId(snapshotVehicleEntityId, out var vehicleNetworkId))
        {
            ServiceLog.Error($"[VehicleDebug][Bridge] reject action={actionKind} snapshotVehicle={snapshotVehicleEntityId} seat={seatIndex} reason=network_id_not_found");
            return false;
        }

        ServiceLog.Info($"[VehicleDebug][Bridge] send action={actionKind} snapshotVehicle={snapshotVehicleEntityId} networkVehicle={vehicleNetworkId} seat={seatIndex}");
        SendVehicleAction(vehicleNetworkId, actionKind, seatIndex);
        return true;
    }

    public bool RequestVehicleInput(int snapshotVehicleEntityId, float throttle, float steering, float brake, byte actionFlags, float turretYaw, float turretPitch)
    {
        if (!TryResolveVehicleNetworkId(snapshotVehicleEntityId, out var vehicleNetworkId))
            return false;

        _ = _network.SignalR.VehicleControlAsync(
            PlayerId,
            vehicleNetworkId,
            throttle,
            steering,
            brake,
            actionFlags,
            turretYaw,
            turretPitch,
            System.Threading.CancellationToken.None);
        return true;
    }

    /// <summary>
    /// 请求生成载具 — 替代本地 CombatGameplayModule.SpawnVehicle()。
    /// </summary>
    public void RequestSpawnVehicle(int vehicleDefId, Vector3 spawnPos, System.Guid? factoryId = null)
    {
        _ = _network.SignalR.SpawnVehicleAsync(new SpawnVehicleCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            VehicleDefId: vehicleDefId,
            SpawnX: spawnPos.X,
            SpawnY: spawnPos.Y,
            SpawnZ: spawnPos.Z,
            FactoryBuildingId: factoryId
        ), System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// 请求组装火箭。
    /// </summary>
    public void RequestAssembleRocket(System.Guid launchPadEntityId, string rocketConfigJson)
    {
        _ = _network.SignalR.AssembleRocketAsync(new AssembleRocketCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            LaunchPadEntityId: launchPadEntityId,
            RocketConfigJson: rocketConfigJson
        ), System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// 请求发射火箭。
    /// </summary>
    public void RequestLaunchRocket(System.Guid rocketEntityId)
    {
        _ = _network.SignalR.LaunchRocketAsync(new LaunchRocketCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            RocketEntityId: rocketEntityId
        ), System.Threading.CancellationToken.None);
    }

    public void RequestSpacecraftInput(System.Guid spacecraftNetworkId, Vector3 thrust, Vector3 rotation, byte actionFlags)
    {
        if (spacecraftNetworkId == System.Guid.Empty)
            return;

        _ = _network.SignalR.SpacecraftControlAsync(
            PlayerId,
            spacecraftNetworkId,
            thrust.X,
            thrust.Y,
            thrust.Z,
            rotation.X,
            rotation.Y,
            rotation.Z,
            actionFlags,
            System.Threading.CancellationToken.None);
    }

    private void SendVehicleAction(System.Guid vehicleNetworkId, NetworkVehicleActionKind actionKind, int seatIndex)
    {
        string action = actionKind switch
        {
            NetworkVehicleActionKind.Enter => "enter",
            NetworkVehicleActionKind.Exit => "exit",
            NetworkVehicleActionKind.SwitchSeat => "switch_seat",
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(action))
            return;

        _ = _network.SignalR.VehicleActionAsync(new VehicleActionCommandDto(
            PlayerId: PlayerId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            VehicleEntityId: vehicleNetworkId,
            Action: action,
            SeatIndex: actionKind == NetworkVehicleActionKind.Exit ? null : seatIndex
        ), System.Threading.CancellationToken.None);
    }

    private static bool TryResolveVehicleNetworkId(int snapshotVehicleEntityId, out System.Guid vehicleNetworkId)
    {
        if (GameServices.RemoteWorldEcsCache?.TryGetNetworkId(snapshotVehicleEntityId, out vehicleNetworkId) == true)
            return true;

        vehicleNetworkId = System.Guid.Empty;
        return false;
    }
}
