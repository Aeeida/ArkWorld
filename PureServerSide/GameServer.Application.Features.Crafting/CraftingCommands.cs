using Game.Shared.Core.DTOs;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Crafting;

// ── Commands ──────────────────────────────────────────────────────────

public sealed record StartCraftingCommand(
    Guid PlayerId, string BlueprintId, int Quantity) : ICommand<CraftingJobDto>;

public sealed record CancelCraftingCommand(Guid PlayerId, Guid JobId) : ICommand<bool>;

public sealed record GetBlueprintsQuery(Guid PlayerId) : IQuery<IReadOnlyList<BlueprintDto>>, ICacheableQuery
{
    public string CacheKey => $"blueprints:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public sealed record GetCraftingQueueQuery(Guid PlayerId) : IQuery<IReadOnlyList<CraftingJobDto>>;

public sealed record LearnBlueprintCommand(Guid PlayerId, string BlueprintId) : ICommand<bool>;

public sealed record StartCraftWrappedCommand(Guid PlayerId, string BlueprintId) : ICommand<StartCraftResultDto>;

public sealed record GetCraftingQueueWrappedQuery(Guid PlayerId) : IQuery<CraftingQueueDto>;

// ── Handlers ──────────────────────────────────────────────────────────

public sealed class StartCraftingHandler(
    IGrainFactory grainFactory,
    ILogger<StartCraftingHandler> logger)
    : ICommandHandler<StartCraftingCommand, CraftingJobDto>
{
    public async Task<CraftingJobDto> Handle(StartCraftingCommand request, CancellationToken ct)
    {
        var craftingGrain = grainFactory.GetGrain<ICraftingGrain>(request.PlayerId);
        var job = await craftingGrain.StartCraftingAsync(request.BlueprintId, request.Quantity);

        logger.LogInformation("Player {PlayerId} started crafting {BlueprintId} x{Qty}, job {JobId}",
            request.PlayerId, request.BlueprintId, request.Quantity, job.JobId);

        return new CraftingJobDto(job.JobId, job.BlueprintId, job.Quantity, job.Status, job.CompletesAt);
    }
}

public sealed class CancelCraftingHandler(
    IGrainFactory grainFactory,
    ILogger<CancelCraftingHandler> logger)
    : ICommandHandler<CancelCraftingCommand, bool>
{
    public async Task<bool> Handle(CancelCraftingCommand request, CancellationToken ct)
    {
        var craftingGrain = grainFactory.GetGrain<ICraftingGrain>(request.PlayerId);
        var result = await craftingGrain.CancelCraftingAsync(request.JobId);

        logger.LogInformation("Player {PlayerId} cancel crafting job {JobId}: {Result}",
            request.PlayerId, request.JobId, result);
        return result;
    }
}

public sealed class GetBlueprintsHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetBlueprintsQuery, IReadOnlyList<BlueprintDto>>
{
    public async Task<IReadOnlyList<BlueprintDto>> Handle(GetBlueprintsQuery request, CancellationToken ct)
    {
        var craftingGrain = grainFactory.GetGrain<ICraftingGrain>(request.PlayerId);
        var grainState = await craftingGrain.GetStateAsync();

        return grainState.LearnedBlueprints
            .Select(bp => new BlueprintDto(bp, bp, bp, 1, [], 300))
            .ToList();
    }
}

public sealed class GetCraftingQueueHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetCraftingQueueQuery, IReadOnlyList<CraftingJobDto>>
{
    public async Task<IReadOnlyList<CraftingJobDto>> Handle(GetCraftingQueueQuery request, CancellationToken ct)
    {
        var craftingGrain = grainFactory.GetGrain<ICraftingGrain>(request.PlayerId);
        var grainState = await craftingGrain.GetStateAsync();

        return grainState.ActiveJobs
            .Select(j => new CraftingJobDto(j.JobId, j.BlueprintId, j.Quantity, j.Status, j.CompletesAt))
            .ToList();
    }
}

public sealed class LearnBlueprintHandler(
    IGrainFactory grainFactory,
    ILogger<LearnBlueprintHandler> logger)
    : ICommandHandler<LearnBlueprintCommand, bool>
{
    public async Task<bool> Handle(LearnBlueprintCommand request, CancellationToken ct)
    {
        var craftingGrain = grainFactory.GetGrain<ICraftingGrain>(request.PlayerId);
        var result = await craftingGrain.LearnBlueprintAsync(request.BlueprintId);

        logger.LogInformation("Player {PlayerId} learn blueprint {BlueprintId}: {Result}",
            request.PlayerId, request.BlueprintId, result);
        return result;
    }
}

public sealed class StartCraftWrappedHandler(
    IGrainFactory grainFactory,
    ILogger<StartCraftWrappedHandler> logger)
    : ICommandHandler<StartCraftWrappedCommand, StartCraftResultDto>
{
    public async Task<StartCraftResultDto> Handle(StartCraftWrappedCommand request, CancellationToken ct)
    {
        var craftingGrain = grainFactory.GetGrain<ICraftingGrain>(request.PlayerId);
        var job = await craftingGrain.StartCraftingAsync(request.BlueprintId, 1);

        logger.LogInformation("Player {PlayerId} started crafting {BlueprintId}, job {JobId}",
            request.PlayerId, request.BlueprintId, job.JobId);

        return new StartCraftResultDto(true, job.JobId, job.CompletesAt, null);
    }
}

public sealed class GetCraftingQueueWrappedHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetCraftingQueueWrappedQuery, CraftingQueueDto>
{
    public async Task<CraftingQueueDto> Handle(GetCraftingQueueWrappedQuery request, CancellationToken ct)
    {
        var craftingGrain = grainFactory.GetGrain<ICraftingGrain>(request.PlayerId);
        var grainState = await craftingGrain.GetStateAsync();

        var jobs = grainState.ActiveJobs
            .Select(j => new CraftingJobDto(j.JobId, j.BlueprintId, j.Quantity, j.Status, j.CompletesAt))
            .ToList();

        return new CraftingQueueDto(request.PlayerId, jobs, 5);
    }
}
