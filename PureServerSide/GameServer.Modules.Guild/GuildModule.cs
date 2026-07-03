using GameServer.Application.Core;
using GameServer.Application.Features.Guild;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Guild;

public sealed class GuildModule : IGameModule
{
    public string Name => "Guild";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(CreateGuildCommand));
    }
}
