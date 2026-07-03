using GameServer.Domain.Core;
using Cortex.Mediator.Notifications;

namespace GameServer.Domain.Events;

public sealed record PlayerCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid PlayerId,
    string PlayerName,
    string Faction) : IDomainEvent, INotification;

public sealed record PlayerLeveledUpEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid PlayerId,
    int OldLevel,
    int NewLevel) : IDomainEvent, INotification;

public sealed record CombatStartedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AttackerId,
    Guid DefenderId) : IDomainEvent, INotification;

public sealed record CombatDamageAppliedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AttackerId,
    Guid DefenderId,
    double Damage) : IDomainEvent, INotification;

public sealed record MarketOrderCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    string ItemId,
    decimal Price,
    int Quantity,
    bool IsBuyOrder) : IDomainEvent, INotification;

public sealed record MarketOrderFilledEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    int QuantityFilled) : IDomainEvent, INotification;

public sealed record GuildCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid GuildId,
    string GuildName,
    Guid FounderId) : IDomainEvent, INotification;

public sealed record FleetCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid FleetId,
    string FleetName,
    Guid LeaderId) : IDomainEvent, INotification;

public sealed record InstanceCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid InstanceId,
    string TemplateId,
    string Difficulty) : IDomainEvent, INotification;

public sealed record ShipDestroyedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ShipId,
    Guid OwnerId,
    Guid DestroyedById) : IDomainEvent, INotification;
