using Game.Shared.Events;
using GameServer.Networking.Core;
using GameServer.Networking.SignalR;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR.Consumers;

public sealed class PlayerEventConsumer(
    IHubContext<GameHub> hubContext,
    ISessionManager sessionManager,
    ILogger<PlayerEventConsumer> logger)
    : IConsumer<PlayerJoinedWorldEvent>,
      IConsumer<PlayerLeftWorldEvent>,
      IConsumer<PlayerLevelUpEvent>,
      IConsumer<PlayerDiedEvent>
{
    public async Task Consume(ConsumeContext<PlayerJoinedWorldEvent> context)
    {
        var e = context.Message;
        logger.LogInformation("Broadcasting PlayerJoinedWorld: {PlayerId} → {WorldId}", e.PlayerId, e.WorldId);
        await hubContext.Clients.All.SendAsync("PlayerJoinedWorld", e);
    }

    public async Task Consume(ConsumeContext<PlayerLeftWorldEvent> context)
    {
        var e = context.Message;
        await hubContext.Clients.All.SendAsync("PlayerLeftWorld", e);
    }

    public async Task Consume(ConsumeContext<PlayerLevelUpEvent> context)
    {
        var e = context.Message;
        await hubContext.Clients.All.SendAsync("PlayerLevelUp", e);
    }

    public async Task Consume(ConsumeContext<PlayerDiedEvent> context)
    {
        var e = context.Message;
        await hubContext.Clients.All.SendAsync("PlayerDied", e);
    }
}

public sealed class MarketEventConsumer(
    IHubContext<GameHub> hubContext,
    ILogger<MarketEventConsumer> logger)
    : IConsumer<MarketOrderPlacedEvent>,
      IConsumer<MarketOrderFilledEvent>
{
    public async Task Consume(ConsumeContext<MarketOrderPlacedEvent> context)
    {
        var e = context.Message;
        logger.LogInformation("Broadcasting MarketOrderPlaced: {OrderId}", e.OrderId);
        await hubContext.Clients.All.SendAsync("MarketOrderPlaced", e);
    }

    public async Task Consume(ConsumeContext<MarketOrderFilledEvent> context)
    {
        var e = context.Message;
        await hubContext.Clients.All.SendAsync("MarketOrderFilled", e);
    }
}
