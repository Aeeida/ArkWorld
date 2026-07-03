using GameServer.Domain.Core;

namespace GameServer.Domain.Entities;

public class MarketOrder : AggregateRoot<Guid>
{
    public Guid SellerId { get; init; }
    public required string ItemId { get; init; }

    public int Quantity
    {
        get => field;
        set => field = value >= 0 ? value : throw new ArgumentException("Quantity cannot be negative.");
    }

    public decimal PricePerUnit
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentException("Price must be positive.");
    }

    public required string StationId { get; init; }
    public bool IsBuyOrder { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsFilled => Quantity <= 0;

    public static MarketOrder Create(
        Guid id, Guid sellerId, string itemId, int quantity,
        decimal pricePerUnit, string stationId, bool isBuyOrder, TimeSpan duration)
    {
        return new MarketOrder
        {
            Id = id,
            SellerId = sellerId,
            ItemId = itemId,
            Quantity = quantity,
            PricePerUnit = pricePerUnit,
            StationId = stationId,
            IsBuyOrder = isBuyOrder,
            ExpiresAt = DateTime.UtcNow + duration
        };
    }

    public int FillOrder(int amount)
    {
        var filled = Math.Min(Quantity, amount);
        Quantity -= filled;
        UpdatedAt = DateTime.UtcNow;
        return filled;
    }
}
