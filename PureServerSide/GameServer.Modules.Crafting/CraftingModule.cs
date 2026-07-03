using GameServer.Application.Core;
using GameServer.Application.Features.Crafting;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Crafting;

public sealed class CraftingModule : IGameModule
{
    public string Name => "Crafting";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(StartCraftingCommand));
    }
}
