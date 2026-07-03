using Cortex.Mediator.Commands;
using Cortex.Mediator.Queries;

namespace GameServer.Application.Core.CQRS;

public interface ICommandHandler<in TCommand, TResult> : Cortex.Mediator.Commands.ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;

public interface ICommandHandler<in TCommand> : Cortex.Mediator.Commands.ICommandHandler<TCommand>
    where TCommand : ICommand;

public interface IQueryHandler<in TQuery, TResult> : Cortex.Mediator.Queries.IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;
