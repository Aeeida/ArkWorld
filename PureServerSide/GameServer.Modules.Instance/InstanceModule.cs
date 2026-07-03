using GameServer.Application.Core;
using GameServer.Application.Features.Instance;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Instance;

public sealed class InstanceModule : IGameModule
{
    public string Name => "Instance";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(CreateInstanceCommand));
    }
}
