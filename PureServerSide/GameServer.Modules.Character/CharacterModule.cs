using GameServer.Application.Core;
using GameServer.Application.Features.Character;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Character;

public sealed class CharacterModule : IGameModule
{
    public string Name => "Character";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(CreateCharacterCommand));
    }
}
