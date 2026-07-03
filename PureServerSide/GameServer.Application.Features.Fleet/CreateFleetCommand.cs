using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Fleet;

public sealed record CreateFleetCommand(Guid LeaderId, string FleetName) : ICommand<Guid>;

public sealed record JoinFleetCommand(Guid FleetId, Guid PlayerId) : ICommand<bool>;

public sealed record GetFleetQuery(Guid FleetId) : IQuery<FleetDto?>;

public sealed record FormFleetCommand(Guid LeaderId, string FleetName) : ICommand<FormFleetResultDto>;

public sealed record CommandFleetCommand(Guid FleetId, string CommandType, string? TargetId) : ICommand<CommandFleetResultDto>;

public sealed class CreateFleetHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<CreateFleetCommand, Guid>
{
    public async Task<Guid> Handle(CreateFleetCommand request, CancellationToken ct)
    {
        var fleetId = Guid.NewGuid();
        var grain = grainFactory.GetGrain<IFleetGrain>(fleetId);
        await grain.InitializeAsync(request.LeaderId, request.FleetName);
        return fleetId;
    }
}

public sealed class JoinFleetHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<JoinFleetCommand, bool>
{
    public async Task<bool> Handle(JoinFleetCommand request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IFleetGrain>(request.FleetId);
        return await grain.AddMemberAsync(request.PlayerId);
    }
}

public sealed class GetFleetHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetFleetQuery, FleetDto?>
{
    public async Task<FleetDto?> Handle(GetFleetQuery request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IFleetGrain>(request.FleetId);
        var state = await grain.GetStateAsync();
        if (string.IsNullOrEmpty(state.Name)) return null;
        return new FleetDto(request.FleetId, state.LeaderId, state.Name, state.MemberIds.Count);
    }
}

public sealed class FormFleetHandler(
    IGrainFactory grainFactory,
    ILogger<FormFleetHandler> logger)
    : ICommandHandler<FormFleetCommand, FormFleetResultDto>
{
    public async Task<FormFleetResultDto> Handle(FormFleetCommand request, CancellationToken ct)
    {
        var fleetId = Guid.NewGuid();
        var grain = grainFactory.GetGrain<IFleetGrain>(fleetId);
        await grain.InitializeAsync(request.LeaderId, request.FleetName);

        logger.LogInformation("Fleet {FleetId} formed by {LeaderId}", fleetId, request.LeaderId);
        return new FormFleetResultDto(true, fleetId, null);
    }
}

public sealed class CommandFleetHandler(
    IGrainFactory grainFactory,
    ILogger<CommandFleetHandler> logger)
    : ICommandHandler<CommandFleetCommand, CommandFleetResultDto>
{
    public async Task<CommandFleetResultDto> Handle(CommandFleetCommand request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IFleetGrain>(request.FleetId);
        var state = await grain.GetStateAsync();

        if (string.IsNullOrEmpty(state.Name))
            return new CommandFleetResultDto(false, request.FleetId, request.CommandType, "Fleet not found");

        logger.LogInformation("Fleet {FleetId} executing command {CommandType} target={TargetId}",
            request.FleetId, request.CommandType, request.TargetId);

        return new CommandFleetResultDto(true, request.FleetId, request.CommandType, null);
    }
}
