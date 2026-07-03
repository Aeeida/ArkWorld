using GameServer.Application.Core;
using GameServer.Application.Features.Scripting;
using GameServer.Modules.Scripting.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Scripting;

public sealed class ScriptingModule : IGameModule
{
    public string Name => "Scripting";
    public int Priority => 100; // Load after all game modules

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(StartScriptCommand));

        services.AddSingleton<ScriptHotReloadService>();
        services.AddSingleton<ActivityCalendarService>();
        services.AddSingleton<ScriptAuditService>();
    }

    public async Task StartAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        // Start hot-reload watcher
        var hotReload = serviceProvider.GetRequiredService<ScriptHotReloadService>();
        await hotReload.StartAsync(ct);

        // Start activity calendar
        var calendar = serviceProvider.GetRequiredService<ActivityCalendarService>();
        await calendar.StartAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        // Cleanup handled by DI disposal
        await Task.CompletedTask;
    }
}
