namespace Game.Shared.Events;

public sealed record MarketOrderPlacedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    string ItemId,
    decimal PricePerUnit,
    int Quantity,
    bool IsBuyOrder) : IGameEvent;

public sealed record MarketOrderFilledEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId) : IGameEvent;

public sealed record SovereigntyChangedEvent(
    Guid EventId,
    DateTime OccurredAt,
    string SolarSystemId,
    Guid? OldOwnerId,
    Guid NewOwnerId) : IGameEvent;
