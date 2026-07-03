namespace Game.Shared.Events;

public sealed record PlayerLoggedInEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid PlayerId,
    string PlayerName) : IGameEvent;

public sealed record PlayerLoggedOutEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid PlayerId) : IGameEvent;

public sealed record PlayerJoinedWorldEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid PlayerId,
    string WorldId) : IGameEvent;

public sealed record PlayerLeftWorldEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid PlayerId,
    string WorldId) : IGameEvent;

public sealed record PlayerLevelUpEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid PlayerId,
    int OldLevel,
    int NewLevel) : IGameEvent;
