using Game.Shared.Core.DTOs;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Sovereignty;

// ── Commands ──────────────────────────────────────────────────────────

public sealed record ClaimSovereigntyCommand(
    Guid AllianceId, string SolarSystemId) : ICommand<SovereigntyDto>;

public sealed record ContestSovereigntyCommand(
    Guid AttackingAllianceId, string SolarSystemId) : ICommand<SovereigntyContestDto>;

public sealed record GetSovereigntyMapQuery()
    : IQuery<IReadOnlyList<SovereigntyDto>>, ICacheableQuery
{
    public string CacheKey => "sovereignty:map";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);
}

public sealed record PlaceStructureCommand(
    Guid AllianceId, string SolarSystemId, string StructureType) : ICommand<StructureDto>;

public sealed record DestroyStructureCommand(Guid StructureId, string SolarSystemId) : ICommand<bool>;

public sealed record SetTaxRateCommand(Guid AllianceId, string SolarSystemId, decimal TaxRate) : ICommand<bool>;

public sealed record ClaimSovereigntyWrappedCommand(
    Guid AllianceId, string SolarSystemId) : ICommand<ClaimSovereigntyResultDto>;

public sealed record BuildStructureCommand(
    Guid AllianceId, string SolarSystemId, string StructureType) : ICommand<BuildStructureResultDto>;

// ── Handlers ──────────────────────────────────────────────────────────

public sealed class ClaimSovereigntyHandler(
    IGrainFactory grainFactory,
    ILogger<ClaimSovereigntyHandler> logger)
    : ICommandHandler<ClaimSovereigntyCommand, SovereigntyDto>
{
    public async Task<SovereigntyDto> Handle(ClaimSovereigntyCommand request, CancellationToken ct)
    {
        var sovGrain = grainFactory.GetGrain<ISovereigntyGrain>(request.SolarSystemId);
        var claimed = await sovGrain.ClaimAsync(request.AllianceId);

        var grainState = await sovGrain.GetStateAsync();

        logger.LogInformation("Alliance {AllianceId} claim sovereignty over {SystemId}: {Result}",
            request.AllianceId, request.SolarSystemId, claimed);

        return new SovereigntyDto(
            grainState.SolarSystemId,
            grainState.OwnerAllianceId ?? Guid.Empty,
            grainState.ClaimedAt ?? DateTime.UtcNow,
            grainState.Status);
    }
}

public sealed class ContestSovereigntyHandler(
    IGrainFactory grainFactory,
    ILogger<ContestSovereigntyHandler> logger)
    : ICommandHandler<ContestSovereigntyCommand, SovereigntyContestDto>
{
    public async Task<SovereigntyContestDto> Handle(ContestSovereigntyCommand request, CancellationToken ct)
    {
        var sovGrain = grainFactory.GetGrain<ISovereigntyGrain>(request.SolarSystemId);
        var contestId = await sovGrain.ContestAsync(request.AttackingAllianceId);

        var grainState = await sovGrain.GetStateAsync();
        var contest = grainState.ActiveContests.Find(c => c.ContestId == contestId);

        logger.LogInformation("Alliance {AllianceId} contesting {SystemId}, contest {ContestId}",
            request.AttackingAllianceId, request.SolarSystemId, contestId);

        return new SovereigntyContestDto(
            contestId,
            request.SolarSystemId,
            request.AttackingAllianceId,
            contest?.Status ?? "InProgress",
            contest?.EndsAt ?? DateTime.UtcNow.AddHours(2));
    }
}

public sealed class GetSovereigntyMapHandler
    : IQueryHandler<GetSovereigntyMapQuery, IReadOnlyList<SovereigntyDto>>
{
    public Task<IReadOnlyList<SovereigntyDto>> Handle(
        GetSovereigntyMapQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SovereigntyDto>>([]);
}

public sealed class PlaceStructureHandler(
    IGrainFactory grainFactory,
    ILogger<PlaceStructureHandler> logger)
    : ICommandHandler<PlaceStructureCommand, StructureDto>
{
    public async Task<StructureDto> Handle(PlaceStructureCommand request, CancellationToken ct)
    {
        var sovGrain = grainFactory.GetGrain<ISovereigntyGrain>(request.SolarSystemId);
        var structureId = await sovGrain.PlaceStructureAsync(request.AllianceId, request.StructureType);

        logger.LogInformation("Alliance {AllianceId} placed {StructureType} in {SystemId}: {StructureId}",
            request.AllianceId, request.StructureType, request.SolarSystemId, structureId);

        return new StructureDto(
            structureId,
            request.StructureType,
            request.SolarSystemId,
            request.AllianceId,
            100.0,
            "Online");
    }
}

public sealed class DestroyStructureHandler(
    IGrainFactory grainFactory,
    ILogger<DestroyStructureHandler> logger)
    : ICommandHandler<DestroyStructureCommand, bool>
{
    public async Task<bool> Handle(DestroyStructureCommand request, CancellationToken ct)
    {
        var sovGrain = grainFactory.GetGrain<ISovereigntyGrain>(request.SolarSystemId);
        var result = await sovGrain.DestroyStructureAsync(request.StructureId);

        logger.LogInformation("Structure {StructureId} destroyed in {SystemId}: {Result}",
            request.StructureId, request.SolarSystemId, result);
        return result;
    }
}

public sealed class SetTaxRateHandler(
    IGrainFactory grainFactory,
    ILogger<SetTaxRateHandler> logger)
    : ICommandHandler<SetTaxRateCommand, bool>
{
    public async Task<bool> Handle(SetTaxRateCommand request, CancellationToken ct)
    {
        var sovGrain = grainFactory.GetGrain<ISovereigntyGrain>(request.SolarSystemId);
        var result = await sovGrain.SetTaxRateAsync(request.AllianceId, request.TaxRate);

        logger.LogInformation("Alliance {AllianceId} set tax rate to {TaxRate} in {SystemId}: {Result}",
            request.AllianceId, request.TaxRate, request.SolarSystemId, result);
        return result;
    }
}

public sealed class ClaimSovereigntyWrappedHandler(
    IGrainFactory grainFactory,
    ILogger<ClaimSovereigntyWrappedHandler> logger)
    : ICommandHandler<ClaimSovereigntyWrappedCommand, ClaimSovereigntyResultDto>
{
    public async Task<ClaimSovereigntyResultDto> Handle(ClaimSovereigntyWrappedCommand request, CancellationToken ct)
    {
        var sovGrain = grainFactory.GetGrain<ISovereigntyGrain>(request.SolarSystemId);
        var claimed = await sovGrain.ClaimAsync(request.AllianceId);

        logger.LogInformation("Alliance {AllianceId} claim sovereignty {SystemId}: {Result}",
            request.AllianceId, request.SolarSystemId, claimed);

        return new ClaimSovereigntyResultDto(claimed, request.SolarSystemId, request.AllianceId,
            claimed ? null : "Failed to claim sovereignty");
    }
}

public sealed class BuildStructureHandler(
    IGrainFactory grainFactory,
    ILogger<BuildStructureHandler> logger)
    : ICommandHandler<BuildStructureCommand, BuildStructureResultDto>
{
    public async Task<BuildStructureResultDto> Handle(BuildStructureCommand request, CancellationToken ct)
    {
        var sovGrain = grainFactory.GetGrain<ISovereigntyGrain>(request.SolarSystemId);
        var structureId = await sovGrain.PlaceStructureAsync(request.AllianceId, request.StructureType);

        logger.LogInformation("Alliance {AllianceId} building {StructureType} in {SystemId}: {StructureId}",
            request.AllianceId, request.StructureType, request.SolarSystemId, structureId);

        return new BuildStructureResultDto(true, structureId, request.StructureType, null);
    }
}
