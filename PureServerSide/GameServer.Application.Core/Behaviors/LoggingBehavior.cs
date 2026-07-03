using System.Diagnostics;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Core.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IQueryPipelineBehavior<TRequest, TResponse>
    where TRequest : Cortex.Mediator.Queries.IQuery<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        QueryHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return await LogQueryAsync(next);
    }

    private async Task<TResponse> LogQueryAsync(QueryHandlerDelegate<TResponse> next)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling query {RequestName}", requestName);

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > 500)
        {
            logger.LogWarning("Long running query: {RequestName} ({Elapsed}ms)",
                requestName, sw.ElapsedMilliseconds);
        }
        else
        {
            logger.LogInformation("Handled query {RequestName} in {Elapsed}ms",
                requestName, sw.ElapsedMilliseconds);
        }

        return response;
    }
}

public sealed class LoggingCommandBehavior<TRequest, TResponse>(
    ILogger<LoggingCommandBehavior<TRequest, TResponse>> logger)
    : ICommandPipelineBehavior<TRequest, TResponse>
    where TRequest : Cortex.Mediator.Commands.ICommand<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling command {RequestName}", requestName);

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > 500)
        {
            logger.LogWarning("Long running command: {RequestName} ({Elapsed}ms)",
                requestName, sw.ElapsedMilliseconds);
        }
        else
        {
            logger.LogInformation("Handled command {RequestName} in {Elapsed}ms",
                requestName, sw.ElapsedMilliseconds);
        }

        return response;
    }
}
