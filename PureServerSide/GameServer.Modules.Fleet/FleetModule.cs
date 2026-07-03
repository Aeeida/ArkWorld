using GameServer.Application.Core;
using GameServer.Application.Features.Fleet;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Fleet;

public sealed class FleetModule : IGameModule
{
    public string Name => "Fleet";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(CreateFleetCommand));
    }
}
