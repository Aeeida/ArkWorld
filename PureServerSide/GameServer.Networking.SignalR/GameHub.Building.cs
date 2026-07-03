using System.Numerics;
using Game.Shared.Core.DTOs;
using GameLayer.Building;
using GameLayer.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR;

public sealed partial class GameHub
{
    // ══════════════════════════════════════════════════════════════════
    // 基地建造（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public async Task<PlaceBuildingResultDto> PlaceBuildingAsync(PlaceBuildingCommandDto cmd)
    {
        var building = serviceProvider.GetRequiredService<BuildingManager>();
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        var worldId = session?.CurrentWorldId ?? "default";

        var position = new Vector3((float)cmd.X, (float)cmd.Y, (float)cmd.Z);
        var rotation = Quaternion.CreateFromYawPitchRoll(cmd.RotationY, 0, 0);

        var entityId = building.PlaceBuilding(cmd.PlayerId, cmd.BuildingTypeId, position, rotation, worldId);
        if (entityId is null)
            return new PlaceBuildingResultDto(false, null, null, "Cannot place building at this location.");

        var entity = world.Entities.GetById(entityId.Value);
        var result = new PlaceBuildingResultDto(true, entityId, entity?.NetworkId, null);

        // Broadcast to all clients in the same world
        if (session?.CurrentWorldId is not null)
            await Clients.Group(session.CurrentWorldId).SendAsync("OnBuildingPlacedByPlayer", cmd.PlayerId, result);

        logger.LogInformation("Building placed: EntityId={EntityId}, WorldId={WorldId}, Player={Player}", entityId, worldId, cmd.PlayerId);
        return result;
    }

    public async Task<DestroyBuildingResultDto> DestroyBuildingAsync(DestroyBuildingCommandDto cmd)
    {
        var building = serviceProvider.GetRequiredService<BuildingManager>();
        var world = serviceProvider.GetRequiredService<ServerWorldState>();

        var entity = world.Entities.GetByNetworkId(cmd.BuildingEntityId);
        if (entity is null)
            return new DestroyBuildingResultDto(false, "Building not found.");

        var success = building.DestroyBuilding(entity.Id, cmd.PlayerId);
        return new DestroyBuildingResultDto(success, success ? null : "Not authorized to destroy this building.");
    }

    public async Task<UpgradeBuildingResultDto> UpgradeBuildingAsync(UpgradeBuildingCommandDto cmd)
    {
        var building = serviceProvider.GetRequiredService<BuildingManager>();
        var world = serviceProvider.GetRequiredService<ServerWorldState>();

        var entity = world.Entities.GetByNetworkId(cmd.BuildingEntityId);
        if (entity is null)
            return new UpgradeBuildingResultDto(false, null, "Building not found.");

        var success = building.UpgradeBuilding(entity.Id, cmd.PlayerId);
        return new UpgradeBuildingResultDto(success, success ? 2 : null, success ? null : "Cannot upgrade building.");
    }

    public Task<BuildingListDto> GetAvailableBuildingTypesAsync()
    {
        var building = serviceProvider.GetRequiredService<BuildingManager>();
        var types = building.GetAvailableBuildingTypes();
        var dtos = types.Select(t => new BuildingTypeInfoDto(t.TypeId, t.Name, "General", 5, [])).ToList();
        return Task.FromResult(new BuildingListDto(dtos));
    }
}
