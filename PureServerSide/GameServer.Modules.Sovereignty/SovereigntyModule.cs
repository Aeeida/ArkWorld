using GameServer.Application.Core;
using GameServer.Application.Features.Sovereignty;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Sovereignty;

public sealed class SovereigntyModule : IGameModule
{
    public string Name => "Sovereignty";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(ClaimSovereigntyCommand));
    }
}
