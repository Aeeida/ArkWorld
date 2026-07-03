using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Instance;

public sealed record CreateInstanceCommand(string InstanceTemplateId, Guid LeaderId) : ICommand<Guid>;

public sealed record JoinInstanceCommand(Guid InstanceId, Guid PlayerId) : ICommand<bool>;

public sealed record GetInstanceQuery(Guid InstanceId) : IQuery<InstanceDto?>;

public sealed record EnterInstanceCommand(Guid PlayerId, Guid InstanceId) : ICommand<EnterInstanceResultDto>;

public sealed record GetInstanceStatusQuery(Guid InstanceId) : IQuery<InstanceStatusDto>;

public sealed class CreateInstanceHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<CreateInstanceCommand, Guid>
{
    public async Task<Guid> Handle(CreateInstanceCommand request, CancellationToken ct)
    {
        var instanceId = Guid.NewGuid();
        var grain = grainFactory.GetGrain<IInstanceGrain>(instanceId);
        await grain.InitializeAsync(request.InstanceTemplateId, request.LeaderId);
        return instanceId;
    }
}

public sealed class JoinInstanceHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<JoinInstanceCommand, bool>
{
    public async Task<bool> Handle(JoinInstanceCommand request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IInstanceGrain>(request.InstanceId);
        return await grain.AddPlayerAsync(request.PlayerId);
    }
}

public sealed class GetInstanceHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetInstanceQuery, InstanceDto?>
{
    public async Task<InstanceDto?> Handle(GetInstanceQuery request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IInstanceGrain>(request.InstanceId);
        var state = await grain.GetStateAsync();
        if (string.IsNullOrEmpty(state.TemplateId)) return null;
        return new InstanceDto(request.InstanceId, state.TemplateId, state.Difficulty, state.PlayerIds.Count);
    }
}

public sealed class EnterInstanceHandler(
    IGrainFactory grainFactory,
    ILogger<EnterInstanceHandler> logger)
    : ICommandHandler<EnterInstanceCommand, EnterInstanceResultDto>
{
    public async Task<EnterInstanceResultDto> Handle(EnterInstanceCommand request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IInstanceGrain>(request.InstanceId);
        var joined = await grain.AddPlayerAsync(request.PlayerId);

        if (!joined)
            return new EnterInstanceResultDto(false, null, "Instance is full or not found");

        logger.LogInformation("Player {PlayerId} entered instance {InstanceId}",
            request.PlayerId, request.InstanceId);

        return new EnterInstanceResultDto(true, request.InstanceId, null);
    }
}

public sealed class GetInstanceStatusHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetInstanceStatusQuery, InstanceStatusDto>
{
    public async Task<InstanceStatusDto> Handle(GetInstanceStatusQuery request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IInstanceGrain>(request.InstanceId);
        var state = await grain.GetStateAsync();

        return new InstanceStatusDto(
            request.InstanceId,
            state.TemplateId,
            state.Difficulty,
            state.PlayerIds.Count,
            20,
            TimeSpan.Zero);
    }
}
