using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IMarketGrain : IGrainWithStringKey
{
    Task<Guid> PlaceOrderAsync(Guid sellerId, string itemId, int quantity, decimal price, bool isBuyOrder);
    Task<bool> CancelOrderAsync(Guid orderId);
    Task<IReadOnlyList<MarketOrderGrainState>> GetOrdersAsync(string? itemFilter = null);
}

[GenerateSerializer]
public sealed class MarketOrderGrainState
{
    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public Guid SellerId { get; set; }
    [Id(2)] public string ItemId { get; set; } = string.Empty;
    [Id(3)] public int Quantity { get; set; }
    [Id(4)] public decimal PricePerUnit { get; set; }
    [Id(5)] public bool IsBuyOrder { get; set; }
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime ExpiresAt { get; set; }
}
