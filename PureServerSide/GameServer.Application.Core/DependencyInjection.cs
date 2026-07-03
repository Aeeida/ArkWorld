using Cortex.Mediator.DependencyInjection;
using FluentValidation;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Application.Core;

public sealed class ApplicationCoreAssemblyMarker;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationCore(this IServiceCollection services)
    {
        services.AddCortexMediator(
            [typeof(ApplicationCoreAssemblyMarker)],
            options =>
        {
            options.AddOpenCommandPipelineBehavior(typeof(LoggingCommandBehavior<,>));
            options.AddOpenQueryPipelineBehavior(typeof(LoggingBehavior<,>));
            options.AddOpenCommandPipelineBehavior(typeof(ValidationCommandBehavior<,>));
            options.AddOpenQueryPipelineBehavior(typeof(ValidationBehavior<,>));
            options.AddOpenQueryPipelineBehavior(typeof(CachingBehavior<,>));
            options.AddOpenCommandPipelineBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddScoped<IMediator, CortexMediatorAdapter>();
        services.AddValidatorsFromAssemblyContaining<ApplicationCoreAssemblyMarker>(includeInternalTypes: true);

        return services;
    }

    public static IServiceCollection AddFeatureHandlers(this IServiceCollection services, params Type[] assemblyMarkerTypes)
    {
        if (assemblyMarkerTypes.Length == 0)
        {
            return services;
        }

        services.AddCortexMediator(assemblyMarkerTypes, _ => { });

        foreach (var marker in assemblyMarkerTypes)
            services.AddValidatorsFromAssemblyContaining(marker, includeInternalTypes: true);

        return services;
    }
}
