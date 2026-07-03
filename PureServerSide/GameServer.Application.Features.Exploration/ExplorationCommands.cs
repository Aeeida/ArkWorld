using Game.Shared.Core.DTOs;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Exploration;

// ── Commands ──────────────────────────────────────────────────────────

public sealed record ScanSystemCommand(Guid PlayerId, string SolarSystemId) : ICommand<ScanResultDto>;

public sealed record HarvestResourceCommand(
    Guid PlayerId, string ResourceNodeId, string SolarSystemId) : ICommand<HarvestResultDto>;

public sealed record GetExplorationSitesQuery(string SolarSystemId)
    : IQuery<IReadOnlyList<ExplorationSiteDto>>, ICacheableQuery
{
    public string CacheKey => $"exploration:{SolarSystemId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);
}

public sealed record TriggerDynamicEventCommand(string SolarSystemId, string EventType) : ICommand<Guid>;

// ── Handlers ──────────────────────────────────────────────────────────

public sealed class ScanSystemHandler(
    IGrainFactory grainFactory,
    ILogger<ScanSystemHandler> logger)
    : ICommandHandler<ScanSystemCommand, ScanResultDto>
{
    public async Task<ScanResultDto> Handle(ScanSystemCommand request, CancellationToken ct)
    {
        var explorationGrain = grainFactory.GetGrain<IExplorationGrain>(request.SolarSystemId);
        var scanResult = await explorationGrain.ScanAsync(request.PlayerId);

        // Trigger achievement progress for scanning
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        await achievementGrain.IncrementProgressAsync("explorer", 1);

        logger.LogInformation("Player {PlayerId} scanned system {SystemId}: {NodeCount} resources, {AnomalyCount} anomalies",
            request.PlayerId, request.SolarSystemId, scanResult.ResourceNodes.Count, scanResult.AnomalyIds.Count);

        return new ScanResultDto(
            request.SolarSystemId,
            scanResult.PlayerCount,
            scanResult.ResourceNodes
                .Select(n => new ExplorationSiteDto(n.NodeId, n.ResourceType, "Normal", n.X, n.Y, n.Z))
                .ToList(),
            scanResult.AnomalyIds);
    }
}

public sealed class HarvestResourceHandler(
    IGrainFactory grainFactory,
    ILogger<HarvestResourceHandler> logger)
    : ICommandHandler<HarvestResourceCommand, HarvestResultDto>
{
    public async Task<HarvestResultDto> Handle(HarvestResourceCommand request, CancellationToken ct)
    {
        var explorationGrain = grainFactory.GetGrain<IExplorationGrain>(request.SolarSystemId);
        var result = await explorationGrain.HarvestResourceAsync(request.PlayerId, request.ResourceNodeId);

        logger.LogInformation("Player {PlayerId} harvested {Qty} {Resource} from {NodeId}",
            request.PlayerId, result.QuantityHarvested, result.ResourceType, request.ResourceNodeId);

        return new HarvestResultDto(
            result.ResourceNodeId,
            result.ResourceType,
            result.QuantityHarvested,
            result.NodeDepleted);
    }
}

public sealed class GetExplorationSitesHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetExplorationSitesQuery, IReadOnlyList<ExplorationSiteDto>>
{
    public async Task<IReadOnlyList<ExplorationSiteDto>> Handle(
        GetExplorationSitesQuery request, CancellationToken ct)
    {
        var explorationGrain = grainFactory.GetGrain<IExplorationGrain>(request.SolarSystemId);
        var grainState = await explorationGrain.GetStateAsync();

        return grainState.Sites
            .Select(s => new ExplorationSiteDto(s.SiteId, s.Type, s.Difficulty, s.X, s.Y, s.Z))
            .ToList();
    }
}

public sealed class TriggerDynamicEventHandler(
    IGrainFactory grainFactory,
    ILogger<TriggerDynamicEventHandler> logger)
    : ICommandHandler<TriggerDynamicEventCommand, Guid>
{
    public async Task<Guid> Handle(TriggerDynamicEventCommand request, CancellationToken ct)
    {
        var explorationGrain = grainFactory.GetGrain<IExplorationGrain>(request.SolarSystemId);
        var eventId = await explorationGrain.TriggerDynamicEventAsync(request.EventType);

        logger.LogInformation("Dynamic event {EventType} triggered in {SystemId}: {EventId}",
            request.EventType, request.SolarSystemId, eventId);
        return eventId;
    }
}
