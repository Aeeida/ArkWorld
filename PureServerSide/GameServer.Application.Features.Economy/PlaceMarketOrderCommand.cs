using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Application.Core.Behaviors;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Economy;

public sealed record PlaceMarketOrderCommand(
    Guid SellerId, string ItemId, int Quantity,
    decimal PricePerUnit, string StationId, bool IsBuyOrder) : ICommand<Guid>;

public sealed record GetMarketOrdersQuery(string StationId, string? ItemFilter = null)
    : IQuery<IReadOnlyList<MarketOrderDto>>, ICacheableQuery
{
    public string CacheKey => $"market:{StationId}:{ItemFilter ?? "all"}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);
}

public sealed record GetMarketOrdersWrappedQuery(string StationId)
    : IQuery<MarketOrdersDto>, ICacheableQuery
{
    public string CacheKey => $"market:wrapped:{StationId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);
}

public sealed record CreateMarketOrderCommand(
    Guid SellerId, string ItemId, int Quantity,
    decimal PricePerUnit, string StationId, bool IsBuyOrder) : ICommand<CreateOrderResultDto>;

public sealed record BuyOrderCommand(Guid BuyerId, Guid OrderId, int Quantity) : ICommand<BuyOrderResultDto>;

public sealed record SendMailCommand(Guid SenderId, Guid RecipientId, string Subject, string Body) : ICommand<SendMailResultDto>;

public sealed record GetMailQuery(Guid PlayerId) : IQuery<GetMailDto>;

public sealed class PlaceMarketOrderHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<PlaceMarketOrderCommand, Guid>
{
    public async Task<Guid> Handle(PlaceMarketOrderCommand request, CancellationToken ct)
    {
        var marketGrain = grainFactory.GetGrain<IMarketGrain>(request.StationId);
        return await marketGrain.PlaceOrderAsync(
            request.SellerId, request.ItemId, request.Quantity,
            request.PricePerUnit, request.IsBuyOrder);
    }
}

public sealed class GetMarketOrdersHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetMarketOrdersQuery, IReadOnlyList<MarketOrderDto>>
{
    public async Task<IReadOnlyList<MarketOrderDto>> Handle(GetMarketOrdersQuery request, CancellationToken ct)
    {
        var marketGrain = grainFactory.GetGrain<IMarketGrain>(request.StationId);
        var orders = await marketGrain.GetOrdersAsync(request.ItemFilter);
        return orders.Select(o => new MarketOrderDto(
            o.OrderId, o.SellerId, o.ItemId, o.Quantity,
            o.PricePerUnit, request.StationId, o.IsBuyOrder, o.ExpiresAt)).ToList();
    }
}

public sealed class GetMarketOrdersWrappedHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetMarketOrdersWrappedQuery, MarketOrdersDto>
{
    public async Task<MarketOrdersDto> Handle(GetMarketOrdersWrappedQuery request, CancellationToken ct)
    {
        var marketGrain = grainFactory.GetGrain<IMarketGrain>(request.StationId);
        var orders = await marketGrain.GetOrdersAsync(null);
        var dtos = orders.Select(o => new MarketOrderDto(
            o.OrderId, o.SellerId, o.ItemId, o.Quantity,
            o.PricePerUnit, request.StationId, o.IsBuyOrder, o.ExpiresAt)).ToList();
        return new MarketOrdersDto(request.StationId, dtos, dtos.Count);
    }
}

public sealed class CreateMarketOrderHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<CreateMarketOrderCommand, CreateOrderResultDto>
{
    public async Task<CreateOrderResultDto> Handle(CreateMarketOrderCommand request, CancellationToken ct)
    {
        var marketGrain = grainFactory.GetGrain<IMarketGrain>(request.StationId);
        var orderId = await marketGrain.PlaceOrderAsync(
            request.SellerId, request.ItemId, request.Quantity,
            request.PricePerUnit, request.IsBuyOrder);
        return new CreateOrderResultDto(true, orderId, null);
    }
}

public sealed class BuyOrderHandler(
    IGrainFactory grainFactory,
    ILogger<BuyOrderHandler> logger)
    : ICommandHandler<BuyOrderCommand, BuyOrderResultDto>
{
    public async Task<BuyOrderResultDto> Handle(BuyOrderCommand request, CancellationToken ct)
    {
        // TODO: Implement order matching through IMarketGrain
        logger.LogInformation("Buyer {BuyerId} attempting to buy order {OrderId} qty={Qty}",
            request.BuyerId, request.OrderId, request.Quantity);
        return new BuyOrderResultDto(true, request.OrderId, request.Quantity, 0m, null);
    }
}

public sealed class SendMailHandler : ICommandHandler<SendMailCommand, SendMailResultDto>
{
    public Task<SendMailResultDto> Handle(SendMailCommand request, CancellationToken ct)
    {
        // TODO: Delegate to IMailGrain
        var mailId = Guid.NewGuid();
        return Task.FromResult(new SendMailResultDto(true, mailId, null));
    }
}

public sealed class GetMailHandler : IQueryHandler<GetMailQuery, GetMailDto>
{
    public Task<GetMailDto> Handle(GetMailQuery request, CancellationToken ct)
    {
        // TODO: Fetch from IMailGrain
        return Task.FromResult(new GetMailDto(request.PlayerId, [], 0));
    }
}
