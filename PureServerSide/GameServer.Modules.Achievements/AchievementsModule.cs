using GameServer.Application.Core;
using GameServer.Application.Features.Achievements;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Achievements;

public sealed class AchievementsModule : IGameModule
{
    public string Name => "Achievements";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(UnlockAchievementCommand));
    }
}
