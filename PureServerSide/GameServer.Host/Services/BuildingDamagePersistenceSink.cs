using System.Linq;
using GameLayer.Building;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Host.Services;

public sealed class BuildingDamagePersistenceSink(IServiceScopeFactory scopeFactory) : IBuildingDamagePersistenceSink
{
    public async Task PersistAsync(IReadOnlyList<BuildingDamagePersistenceDelta> deltas, CancellationToken cancellationToken)
    {
        if (deltas.Count == 0)
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var worldIds = deltas.Select(d => d.WorldId).Distinct().ToArray();
        var locations = await db.Locations
            .Where(l => worldIds.Contains(l.Code) || worldIds.Contains(l.Name))
            .ToListAsync(cancellationToken);

        foreach (var delta in deltas)
        {
            var location = locations.FirstOrDefault(l => l.Code == delta.WorldId || l.Name == delta.WorldId);
            if (location is null)
                continue;

            db.TerrainModifications.Add(new TerrainModification
            {
                LocationId = location.Id,
                CenterX = delta.WorldPosition.X,
                CenterZ = delta.WorldPosition.Z,
                RadiusX = 0f,
                RadiusZ = 0f,
                TargetHeight = delta.LayerState,
                ModificationType = "building_damage_layer_v1",
                ChunkKey = delta.ChunkKey,
                SequenceTick = (long)delta.Sequence,
                MetadataJson = delta.PayloadJson,
                ModifiedAtUtc = DateTime.UtcNow,
                ModifiedBy = null,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
