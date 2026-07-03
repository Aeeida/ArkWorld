using GameServer.Application.Core;
using GameServer.Application.Features.Quests;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Quests;

public sealed class QuestsModule : IGameModule
{
    public string Name => "Quests";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(AcceptQuestCommand));
    }
}
