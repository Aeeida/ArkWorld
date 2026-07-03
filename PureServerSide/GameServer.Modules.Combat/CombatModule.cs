using GameServer.Application.Core;
using GameServer.Application.Features.Combat;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Combat;

public sealed class CombatModule : IGameModule
{
    public string Name => "Combat";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(StartCombatCommand));
        // 玩法模式切换 & 技能/功能系统
        services.AddFeatureHandlers(typeof(ChangeModeCommand));
    }
}
