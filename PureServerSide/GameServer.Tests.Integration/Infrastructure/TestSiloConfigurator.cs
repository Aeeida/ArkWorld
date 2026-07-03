using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;

namespace GameServer.Tests.Integration.Infrastructure;

public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorageAsDefault();
        siloBuilder.AddMemoryGrainStorage("GameStore");
        siloBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IEventBus, NullEventBus>();
        });
    }
}

internal sealed class NullEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class =>
        Task.CompletedTask;
}
