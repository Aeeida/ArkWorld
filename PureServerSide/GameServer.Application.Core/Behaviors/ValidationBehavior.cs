using FluentValidation;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Queries;

namespace GameServer.Application.Core.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IQueryPipelineBehavior<TRequest, TResponse>
    where TRequest : Cortex.Mediator.Queries.IQuery<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        QueryHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(request, cancellationToken);
        return await next();
    }

    private async Task ValidateAsync(TRequest request, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return;
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);
    }
}

public sealed class ValidationCommandBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : ICommandPipelineBehavior<TRequest, TResponse>
    where TRequest : Cortex.Mediator.Commands.ICommand<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var results = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = results
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count != 0)
                throw new ValidationException(failures);
        }

        return await next();
    }
}
