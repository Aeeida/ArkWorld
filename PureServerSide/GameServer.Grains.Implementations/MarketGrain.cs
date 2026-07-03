using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class MarketGrain(
    [PersistentState("market", "GameStore")] IPersistentState<MarketGrainInternalState> state,
    ILogger<MarketGrain> logger) : Grain, IMarketGrain
{
    public async Task<Guid> PlaceOrderAsync(Guid sellerId, string itemId, int quantity, decimal price, bool isBuyOrder)
    {
        var order = new MarketOrderGrainState
        {
            OrderId = Guid.NewGuid(),
            SellerId = sellerId,
            ItemId = itemId,
            Quantity = quantity,
            PricePerUnit = price,
            IsBuyOrder = isBuyOrder,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        state.State.Orders.Add(order);
        await state.WriteStateAsync();

        logger.LogInformation("Market order {OrderId} placed for {Item} x{Qty} at {Price}",
            order.OrderId, itemId, quantity, price);

        return order.OrderId;
    }

    public async Task<bool> CancelOrderAsync(Guid orderId)
    {
        var removed = state.State.Orders.RemoveAll(o => o.OrderId == orderId);
        if (removed > 0)
        {
            await state.WriteStateAsync();
            return true;
        }
        return false;
    }

    public Task<IReadOnlyList<MarketOrderGrainState>> GetOrdersAsync(string? itemFilter = null)
    {
        IReadOnlyList<MarketOrderGrainState> result = itemFilter is null
            ? state.State.Orders.AsReadOnly()
            : state.State.Orders.Where(o => o.ItemId == itemFilter).ToList().AsReadOnly();
        return Task.FromResult(result);
    }
}

[GenerateSerializer]
public sealed class MarketGrainInternalState
{
    [Id(0)] public List<MarketOrderGrainState> Orders { get; set; } = [];
}
