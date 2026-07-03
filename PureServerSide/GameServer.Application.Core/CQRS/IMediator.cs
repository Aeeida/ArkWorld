using Cortex.Mediator.Notifications;
using Cortex.Mediator.Queries;

namespace GameServer.Application.Core.CQRS;

public interface IQuery<TResult> : Cortex.Mediator.Queries.IQuery<TResult>;

public interface IMediator
{
    Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task Send(ICommand command, CancellationToken ct = default);
    Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    Task Publish(INotification notification, CancellationToken ct = default);
}

internal sealed class CortexMediatorAdapter(Cortex.Mediator.IMediator mediator) : IMediator
{
    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        mediator.SendCommandAsync(command, ct);

    public Task Send(ICommand command, CancellationToken ct = default) =>
        mediator.SendCommandAsync(command, ct);

    public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        mediator.SendQueryAsync(query, ct);

    public Task Publish(INotification notification, CancellationToken ct = default) =>
        mediator.PublishAsync(notification, ct);
}
