using Ark.Ecs.Components;
using Friflo.Engine.ECS;
using Godot;

namespace Ark;

public partial class GameBootstrap
{
    private readonly System.Collections.Generic.List<int> _authorityResultEntityDeletes = new();
    private System.Guid? _pendingRocketControlNetworkId;

    private void ProcessAuthorityResultEvents()
    {
        _authorityResultEntityDeletes.Clear();
        _pendingRocketControlNetworkId = null;

        DrainBuildingPlacementResults();
        DrainVehicleSpawnResults();
        DrainRocketAssemblyResults();
        DrainRocketLaunchResults();

        FlushPendingRocketControlState();
        FlushAuthorityResultEntityDeletes();
    }

    private void DrainBuildingPlacementResults()
    {
        var query = _store.Query<RemoteBuildingPlacementResultEvent>();
        foreach (var chunk in query.Chunks)
        {
            var results = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var result = ref results.Span[i];
                if (result.Success != 0)
                    GD.Print($"[DispatcherEcs] Building confirmed: entity={result.EntityId} network={result.NetworkId}");

                _authorityResultEntityDeletes.Add(chunk.Entities[i]);
            }
        }
    }

    private void DrainVehicleSpawnResults()
    {
        var query = _store.Query<RemoteVehicleSpawnResultEvent>();
        foreach (var chunk in query.Chunks)
        {
            var results = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var result = ref results.Span[i];
                if (result.Success != 0)
                    GD.Print($"[DispatcherEcs] Vehicle spawned: id={result.VehicleEntityId} def={result.VehicleDefId}");

                _authorityResultEntityDeletes.Add(chunk.Entities[i]);
            }
        }
    }

    private void DrainRocketAssemblyResults()
    {
        var query = _store.Query<RemoteRocketAssemblyResultEvent>();
        foreach (var chunk in query.Chunks)
        {
            var results = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var result = ref results.Span[i];
                if (result.Success != 0)
                {
                    _activeRocketNetworkId = result.RocketEntityId;
                    _pendingRocketControlNetworkId = result.RocketEntityId;
                    GD.Print($"[DispatcherEcs] Rocket assembled: id={result.RocketEntityId}");
                }

                _authorityResultEntityDeletes.Add(chunk.Entities[i]);
            }
        }
    }

    private void DrainRocketLaunchResults()
    {
        var query = _store.Query<RemoteRocketLaunchResultEvent>();
        foreach (var chunk in query.Chunks)
        {
            var results = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var result = ref results.Span[i];
                if (result.Success != 0)
                    GD.Print($"[DispatcherEcs] Rocket launched: phase={(Ark.Shared.Data.SpaceFlightPhase)result.FlightPhase}");

                _authorityResultEntityDeletes.Add(chunk.Entities[i]);
            }
        }
    }

    private void FlushPendingRocketControlState()
    {
        if (_pendingRocketControlNetworkId is not { } rocketNetworkId)
            return;

        SetActiveRocketControlState(rocketNetworkId);
    }

    private void FlushAuthorityResultEntityDeletes()
    {
        foreach (var entityId in _authorityResultEntityDeletes)
        {
            _ecsAuth.DeleteById(entityId);
        }
    }
}
