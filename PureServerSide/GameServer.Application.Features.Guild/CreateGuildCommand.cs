using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Guild;

public sealed record CreateGuildCommand(Guid FounderId, string GuildName) : ICommand<Guid>;

public sealed record JoinGuildCommand(Guid GuildId, Guid PlayerId) : ICommand<bool>;

public sealed record GetGuildQuery(Guid GuildId) : IQuery<GuildDto?>;

public sealed record CreateGuildWrappedCommand(Guid FounderId, string GuildName, string? Motd) : ICommand<CreateGuildResultDto>;

public sealed record GetGuildInfoQuery(Guid GuildId) : IQuery<GuildInfoDto>;

public sealed record SendChatCommand(Guid PlayerId, Guid GuildId, string Message) : ICommand<SendGuildChatResultDto>;

public sealed class CreateGuildHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<CreateGuildCommand, Guid>
{
    public async Task<Guid> Handle(CreateGuildCommand request, CancellationToken ct)
    {
        var guildId = Guid.NewGuid();
        var grain = grainFactory.GetGrain<IGuildGrain>(guildId);
        await grain.InitializeAsync(request.FounderId, request.GuildName);
        return guildId;
    }
}

public sealed class JoinGuildHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<JoinGuildCommand, bool>
{
    public async Task<bool> Handle(JoinGuildCommand request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IGuildGrain>(request.GuildId);
        return await grain.AddMemberAsync(request.PlayerId);

    }
}

public sealed class GetGuildHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetGuildQuery, GuildDto?>
{
    public async Task<GuildDto?> Handle(GetGuildQuery request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IGuildGrain>(request.GuildId);
        var state = await grain.GetStateAsync();
        if (string.IsNullOrEmpty(state.Name)) return null;
        return new GuildDto(request.GuildId, state.Name, state.FounderId, state.MemberIds.Count);
    }
}

public sealed class CreateGuildWrappedHandler(
    IGrainFactory grainFactory,
    ILogger<CreateGuildWrappedHandler> logger)
    : ICommandHandler<CreateGuildWrappedCommand, CreateGuildResultDto>
{
    public async Task<CreateGuildResultDto> Handle(CreateGuildWrappedCommand request, CancellationToken ct)
    {
        var guildId = Guid.NewGuid();
        var grain = grainFactory.GetGrain<IGuildGrain>(guildId);
        await grain.InitializeAsync(request.FounderId, request.GuildName);

        logger.LogInformation("Guild {GuildId} created by {FounderId}: {Name}",
            guildId, request.FounderId, request.GuildName);

        return new CreateGuildResultDto(true, guildId, null);
    }
}

public sealed class GetGuildInfoHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetGuildInfoQuery, GuildInfoDto>
{
    public async Task<GuildInfoDto> Handle(GetGuildInfoQuery request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IGuildGrain>(request.GuildId);
        var state = await grain.GetStateAsync();

        return new GuildInfoDto(
            request.GuildId,
            state.Name,
            state.FounderId,
            state.MemberIds.Count,
            DateTime.UtcNow,
            null);
    }
}

public sealed class SendChatHandler(
    ILogger<SendChatHandler> logger)
    : ICommandHandler<SendChatCommand, SendGuildChatResultDto>
{
    public Task<SendGuildChatResultDto> Handle(SendChatCommand request, CancellationToken ct)
    {
        // TODO: Delegate to IGuildGrain → broadcast via SignalR
        logger.LogInformation("Player {PlayerId} sent chat in guild {GuildId}", request.PlayerId, request.GuildId);
        return Task.FromResult(new SendGuildChatResultDto(true, null));
    }
}
