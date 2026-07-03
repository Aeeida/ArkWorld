using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Application.Core;

public interface IGameModule
{
    string Name { get; }
    int Priority => 0;
    void RegisterServices(IServiceCollection services);
    Task StartAsync(IServiceProvider serviceProvider, CancellationToken ct = default) => Task.CompletedTask;
    Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
}
