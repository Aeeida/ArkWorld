using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Infrastructure.Messaging;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
}

public sealed class MassTransitEventBus(IPublishEndpoint publishEndpoint) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        await publishEndpoint.Publish(@event, ct);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddGameMessaging(
        this IServiceCollection services,
        string rabbitMqHost,
        params Type[] consumerAssemblyMarkers)
    {
        services.AddMassTransit(cfg =>
        {
            cfg.SetKebabCaseEndpointNameFormatter();

            foreach (var marker in consumerAssemblyMarkers)
                cfg.AddConsumers(marker.Assembly);

            cfg.UsingRabbitMq((context, bus) =>
            {
                bus.Host(rabbitMqHost);
                bus.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IEventBus, MassTransitEventBus>();
        return services;
    }
}
