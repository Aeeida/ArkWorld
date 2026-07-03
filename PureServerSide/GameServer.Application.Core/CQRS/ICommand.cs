using Cortex.Mediator.Commands;

namespace GameServer.Application.Core.CQRS;

public interface ICommand<TResult> : Cortex.Mediator.Commands.ICommand<TResult>;

public interface ICommand : Cortex.Mediator.Commands.ICommand;
