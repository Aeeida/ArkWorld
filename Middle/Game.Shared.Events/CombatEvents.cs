namespace Game.Shared.Events;

public sealed record CombatStartedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AttackerId,
    Guid DefenderId,
    string ZoneId) : IGameEvent;

public sealed record CombatEndedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid WinnerId,
    Guid LoserId) : IGameEvent;

public sealed record EntityDestroyedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid EntityId,
    Guid DestroyedById) : IGameEvent;
