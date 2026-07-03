using GameServer.Application.Core;
using GameServer.Application.Features.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Modules.World;

public sealed class WorldModule : IGameModule
{
    public string Name => "World";
    public int Priority => 0; // 最先启动，确保宇宙在其他模块之前生成

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(EnterWorldCommand));
        services.AddSingleton<UniverseGenerator>();
        services.AddSingleton<WorldPopulationService>();
        services.AddSingleton<GameServer.Networking.Core.IWorldPopulationService>(sp => sp.GetRequiredService<WorldPopulationService>());
        services.AddScoped<SpawnPointService>();
        services.AddScoped<GameServer.Application.Core.ISpawnPointAssigner>(sp => sp.GetRequiredService<SpawnPointService>());
    }

    public async Task StartAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILogger<WorldModule>>();
        logger.LogInformation("WorldModule starting: generating universe...");

        var generator = services.GetRequiredService<UniverseGenerator>();
        await generator.EnsureUniverseGeneratedAsync(ct);

        logger.LogInformation("WorldModule started");
    }
}
