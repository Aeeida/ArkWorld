using Cortex.Mediator.Commands;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Core.Behaviors;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : ICommandPipelineBehavior<TRequest, TResponse>
    where TRequest : Cortex.Mediator.Commands.ICommand<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting transaction for {Request}", typeof(TRequest).Name);
        var response = await next();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogDebug("Transaction committed for {Request}", typeof(TRequest).Name);

        return response;
    }
}
