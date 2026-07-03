using Cortex.Mediator.Notifications;

namespace Game.Shared.Events;

public interface IGameEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventName => GetType().Name;
}
