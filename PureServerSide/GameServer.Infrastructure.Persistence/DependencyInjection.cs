using GameServer.Domain.Core;
using GameServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GameDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GameDbContext>());
        services.AddScoped<GameServer.Application.Core.Behaviors.IUnitOfWork>(sp => sp.GetRequiredService<GameDbContext>());
        services.AddScoped<GameServer.Application.Core.IAccountCharacterRegistry, PersistentAccountCharacterRegistry>();
        services.AddScoped<IRepository<Player, Guid>, GenericRepository<Player, Guid>>();
        services.AddScoped<IRepository<Ship, Guid>, GenericRepository<Ship, Guid>>();
        services.AddScoped<IRepository<MarketOrder, Guid>, GenericRepository<MarketOrder, Guid>>();
        services.AddScoped<IRepository<Station, Guid>, GenericRepository<Station, Guid>>();
        services.AddScoped<IRepository<Guild, Guid>, GenericRepository<Guild, Guid>>();
        services.AddScoped<IRepository<Fleet, Guid>, GenericRepository<Fleet, Guid>>();
        services.AddScoped<IRepository<InventoryItem, Guid>, GenericRepository<InventoryItem, Guid>>();
        services.AddScoped<IRepository<GameInstance, Guid>, GenericRepository<GameInstance, Guid>>();
        services.AddScoped<IRepository<LocationNode, long>, GenericRepository<LocationNode, long>>();
        services.AddScoped<IRepository<TerrainModification, long>, GenericRepository<TerrainModification, long>>();
        services.AddScoped<IRepository<WorldEnvironmentState, long>, GenericRepository<WorldEnvironmentState, long>>();
        services.AddScoped<IRepository<WorldSpawnEntry, long>, GenericRepository<WorldSpawnEntry, long>>();

        return services;
    }
}
