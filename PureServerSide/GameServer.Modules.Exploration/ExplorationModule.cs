using GameServer.Application.Core;
using GameServer.Application.Features.Exploration;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Exploration;

public sealed class ExplorationModule : IGameModule
{
    public string Name => "Exploration";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(ScanSystemCommand));
    }
}
