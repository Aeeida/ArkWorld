using System.Numerics;

namespace GameLayer.Building;

public readonly record struct BuildingDamagePersistenceDelta(
    int EntityId,
    string WorldId,
    string ChunkKey,
    Vector3 WorldPosition,
    byte LayerState,
    ulong Sequence,
    string PayloadJson);

public interface IBuildingDamagePersistenceSink
{
    Task PersistAsync(IReadOnlyList<BuildingDamagePersistenceDelta> deltas, CancellationToken cancellationToken);
}
