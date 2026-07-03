using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.World;

public sealed record EnterWorldCommand(Guid PlayerId, string WorldId) : ICommand<bool>;

public sealed record LeaveWorldCommand(Guid PlayerId) : ICommand<bool>;

public sealed record NavigateToCommand(Guid PlayerId, string TargetSolarSystemId, double X, double Y, double Z) : ICommand<NavigationResultDto>;

public sealed record ScanAreaCommand(Guid PlayerId, string SolarSystemId) : ICommand<ScanResultDto>;

public sealed record CollectResourceCommand(Guid PlayerId, string ResourceNodeId, string SolarSystemId) : ICommand<CollectResourceResultDto>;

public sealed class EnterWorldHandler(
    IGrainFactory grainFactory,
    ILogger<EnterWorldHandler> logger)
    : ICommandHandler<EnterWorldCommand, bool>
{
    public async Task<bool> Handle(EnterWorldCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var currentWorld = await playerGrain.GetWorldAsync();
        if (currentWorld is not null)
        {
            var oldSystem = grainFactory.GetGrain<ISolarSystemGrain>(currentWorld);
            await oldSystem.PlayerLeftAsync(request.PlayerId);
        }

        await playerGrain.SetWorldAsync(request.WorldId);
        var solarSystem = grainFactory.GetGrain<ISolarSystemGrain>(request.WorldId);
        await solarSystem.PlayerEnteredAsync(request.PlayerId);

        logger.LogInformation("Player {PlayerId} entered world {WorldId}", request.PlayerId, request.WorldId);
        return true;
    }
}

public sealed class LeaveWorldHandler(
    IGrainFactory grainFactory,
    ILogger<LeaveWorldHandler> logger)
    : ICommandHandler<LeaveWorldCommand, bool>
{
    public async Task<bool> Handle(LeaveWorldCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var currentWorld = await playerGrain.GetWorldAsync();
        if (currentWorld is null) return false;

        var solarSystem = grainFactory.GetGrain<ISolarSystemGrain>(currentWorld);
        await solarSystem.PlayerLeftAsync(request.PlayerId);
        await playerGrain.SetWorldAsync(null);

        logger.LogInformation("Player {PlayerId} left world {WorldId}", request.PlayerId, currentWorld);
        return true;
    }
}

public sealed class NavigateToHandler(
    IGrainFactory grainFactory,
    ILogger<NavigateToHandler> logger)
    : ICommandHandler<NavigateToCommand, NavigationResultDto>
{
    public async Task<NavigationResultDto> Handle(NavigateToCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var currentWorld = await playerGrain.GetWorldAsync();

        if (currentWorld is null)
            return new NavigationResultDto(false, request.TargetSolarSystemId, 0, 0, "Player not in a world");

        // Calculate jump count based on system distance (simplified)
        var jumpCount = 1;
        var travelTime = jumpCount * 30.0;

        // Move player to new system
        var oldSystem = grainFactory.GetGrain<ISolarSystemGrain>(currentWorld);
        await oldSystem.PlayerLeftAsync(request.PlayerId);

        await playerGrain.SetWorldAsync(request.TargetSolarSystemId);
        var newSystem = grainFactory.GetGrain<ISolarSystemGrain>(request.TargetSolarSystemId);
        await newSystem.PlayerEnteredAsync(request.PlayerId);

        logger.LogInformation("Player {PlayerId} navigated from {Old} to {New}",
            request.PlayerId, currentWorld, request.TargetSolarSystemId);

        return new NavigationResultDto(true, request.TargetSolarSystemId, travelTime, jumpCount, null);
    }
}

public sealed class ScanAreaHandler(
    IGrainFactory grainFactory,
    ILogger<ScanAreaHandler> logger)
    : ICommandHandler<ScanAreaCommand, ScanResultDto>
{
    public async Task<ScanResultDto> Handle(ScanAreaCommand request, CancellationToken ct)
    {
        var explorationGrain = grainFactory.GetGrain<IExplorationGrain>(request.SolarSystemId);
        var scanResult = await explorationGrain.ScanAsync(request.PlayerId);

        logger.LogInformation("Player {PlayerId} scanned area {SystemId}", request.PlayerId, request.SolarSystemId);

        return new ScanResultDto(
            request.SolarSystemId,
            scanResult.PlayerCount,
            scanResult.ResourceNodes
                .Select(n => new ExplorationSiteDto(n.NodeId, n.ResourceType, "Normal", n.X, n.Y, n.Z))
                .ToList(),
            scanResult.AnomalyIds);
    }
}

public sealed class CollectResourceHandler(
    IGrainFactory grainFactory,
    ILogger<CollectResourceHandler> logger)
    : ICommandHandler<CollectResourceCommand, CollectResourceResultDto>
{
    public async Task<CollectResourceResultDto> Handle(CollectResourceCommand request, CancellationToken ct)
    {
        var explorationGrain = grainFactory.GetGrain<IExplorationGrain>(request.SolarSystemId);
        var result = await explorationGrain.HarvestResourceAsync(request.PlayerId, request.ResourceNodeId);

        logger.LogInformation("Player {PlayerId} collected resource {NodeId}: {Qty}",
            request.PlayerId, request.ResourceNodeId, result.QuantityHarvested);

        return new CollectResourceResultDto(
            true, result.ResourceType, result.QuantityHarvested, result.NodeDepleted, null);
    }
}
