using System.Numerics;
using Game.Shared.Core.DTOs;
using GameLayer.Core;
using GameLayer.Vehicle;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR;

public sealed partial class GameHub
{
    // ══════════════════════════════════════════════════════════════════
    // 载具生成 / 控制 / 上下车（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public async Task<SpawnVehicleResultDto> SpawnVehicleAsync(SpawnVehicleCommandDto cmd)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var vehicles = serviceProvider.GetRequiredService<VehicleManager>();
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        var worldId = session?.CurrentWorldId ?? "default";

        var position = new Vector3((float)cmd.SpawnX, (float)cmd.SpawnY, (float)cmd.SpawnZ);

        // 从服务端载具定义注册表查找，回退到默认单座
        var def = vehicles.GetDef(cmd.VehicleDefId);
        var vehicleName = def?.Name ?? $"Vehicle_{cmd.VehicleDefId}";
        var maxHealth = def?.MaxHealth ?? 500f;

        var entity = world.SpawnEntity(System.Guid.NewGuid(), worldId, ServerEntityType.Vehicle, position, maxHealth,
            vehicleName, cmd.VehicleDefId);
        entity.AttachToTerrain = !def.HasValue || def.Value.Type is VehicleType.Car or VehicleType.Tank or VehicleType.AntiAir;

        if (def.HasValue)
        {
            vehicles.RegisterVehicle(entity.Id, def.Value);
        }
        else
        {
            vehicles.RegisterVehicle(entity.Id, new VehicleDef(
                cmd.VehicleDefId, vehicleName, VehicleType.Car,
                500f, 60f, 10f, 2f, 100f, 0.5f,
                [new VehicleSeatDef(0, SeatType.Driver, Vector3.Zero, false, 0)]));
        }

        var result = new SpawnVehicleResultDto(true, entity.Id, entity.NetworkId, cmd.VehicleDefId,
            cmd.SpawnX, cmd.SpawnY, cmd.SpawnZ, null);

        if (session?.CurrentWorldId is not null)
            await Clients.Group(session.CurrentWorldId).SendAsync("OnVehicleSpawned", result);

        logger.LogInformation("Vehicle spawned: EntityId={EntityId}, DefId={DefId}, Seats={Seats}, Player={Player}",
            entity.Id, cmd.VehicleDefId, def?.Seats.Length ?? 1, cmd.PlayerId);
        return result;
    }

    public Task<bool> VehicleControlAsync(System.Guid playerId, System.Guid vehicleEntityId, float throttle, float steering, float brake, byte actionFlags, float turretYaw, float turretPitch)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var vehicles = serviceProvider.GetRequiredService<VehicleManager>();
        var vehicleEntity = world.Entities.GetByNetworkId(vehicleEntityId);
        if (vehicleEntity is null)
            return Task.FromResult(false);

        var success = vehicles.TryApplyControl(playerId, vehicleEntity.Id, throttle, steering, brake, actionFlags, turretYaw, turretPitch);
        return Task.FromResult(success);
    }

    public async Task<VehicleActionResultDto> VehicleActionAsync(VehicleActionCommandDto cmd)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var vehicles = serviceProvider.GetRequiredService<VehicleManager>();
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        var worldId = session?.CurrentWorldId ?? "default";

        switch (cmd.Action)
        {
            case "enter":
            {
                var vehicleEntity = world.Entities.GetByNetworkId(cmd.VehicleEntityId);
                if (vehicleEntity is null)
                    return new VehicleActionResultDto(false, cmd.VehicleEntityId, cmd.Action, cmd.SeatIndex, "Vehicle not found.");

                var preferredSeat = cmd.SeatIndex ?? 0;
                if (!vehicles.EnterVehicle(cmd.PlayerId, vehicleEntity.Id, preferredSeat))
                    return new VehicleActionResultDto(false, cmd.VehicleEntityId, cmd.Action, preferredSeat, "Unable to enter vehicle.");

                var seat = vehicles.GetPlayerVehicle(cmd.PlayerId)?.SeatIndex ?? preferredSeat;
                if (session?.CurrentWorldId is not null)
                    await Clients.Group(session.CurrentWorldId).SendAsync("OnVehicleEntered", cmd.PlayerId, cmd.VehicleEntityId, seat);

                logger.LogInformation("Vehicle enter: VehicleId={VehicleId}, Player={Player}, Seat={Seat}", vehicleEntity.Id, cmd.PlayerId, seat);
                return new VehicleActionResultDto(true, cmd.VehicleEntityId, cmd.Action, seat, null);
            }

            case "exit":
            {
                var current = vehicles.GetPlayerVehicle(cmd.PlayerId);
                if (current is null)
                    return new VehicleActionResultDto(false, cmd.VehicleEntityId, cmd.Action, null, "Player is not in a vehicle.");

                var vehicleEntity = world.Entities.GetById(current.Value.VehicleId);
                if (vehicleEntity is null || !vehicles.ExitVehicle(cmd.PlayerId))
                    return new VehicleActionResultDto(false, cmd.VehicleEntityId, cmd.Action, null, "Unable to exit vehicle.");

                if (session?.CurrentWorldId is not null)
                    await Clients.Group(session.CurrentWorldId).SendAsync("OnVehicleExited", cmd.PlayerId, vehicleEntity.NetworkId);

                logger.LogInformation("Vehicle exit: VehicleId={VehicleId}, Player={Player}", vehicleEntity.Id, cmd.PlayerId);
                return new VehicleActionResultDto(true, vehicleEntity.NetworkId, cmd.Action, null, null);
            }

            case "switch_seat":
            {
                var current = vehicles.GetPlayerVehicle(cmd.PlayerId);
                if (current is null)
                    return new VehicleActionResultDto(false, cmd.VehicleEntityId, cmd.Action, cmd.SeatIndex, "Player is not in a vehicle.");

                var vehicleEntity = world.Entities.GetById(current.Value.VehicleId);
                var seatIndex = cmd.SeatIndex ?? current.Value.SeatIndex;
                if (vehicleEntity is null || !vehicles.SwitchSeat(cmd.PlayerId, seatIndex))
                    return new VehicleActionResultDto(false, cmd.VehicleEntityId, cmd.Action, seatIndex, "Unable to switch seat.");

                if (session?.CurrentWorldId is not null)
                    await Clients.Group(session.CurrentWorldId).SendAsync("OnVehicleEntered", cmd.PlayerId, vehicleEntity.NetworkId, seatIndex);

                logger.LogInformation("Vehicle seat switch: VehicleId={VehicleId}, Player={Player}, Seat={Seat}", vehicleEntity.Id, cmd.PlayerId, seatIndex);
                return new VehicleActionResultDto(true, vehicleEntity.NetworkId, cmd.Action, seatIndex, null);
            }
        }

        return new VehicleActionResultDto(false, cmd.VehicleEntityId, cmd.Action, cmd.SeatIndex, "Unknown vehicle action.");
    }
}
