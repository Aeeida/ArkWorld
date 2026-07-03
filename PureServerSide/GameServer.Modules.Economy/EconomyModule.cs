using GameServer.Application.Core;
using GameServer.Application.Features.Economy;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Economy;

public sealed class EconomyModule : IGameModule
{
    public string Name => "Economy";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(PlaceMarketOrderCommand));
    }
}
