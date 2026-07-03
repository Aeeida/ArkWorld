using GameServer.Application.Core;
using GameServer.Application.Features.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Inventory;

public sealed class InventoryModule : IGameModule
{
    public string Name => "Inventory";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(AddItemCommand));
    }
}
