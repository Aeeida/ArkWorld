using GameServer.Application.Core;
using GameServer.Application.Features.Skills;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Skills;

public sealed class SkillsModule : IGameModule
{
    public string Name => "Skills";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(TrainSkillCommand));
    }
}
