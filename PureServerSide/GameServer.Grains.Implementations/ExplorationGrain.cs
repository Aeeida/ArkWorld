using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class ExplorationGrain(
    [PersistentState("exploration", "GameStore")] IPersistentState<ExplorationGrainState> state,
    IEventBus eventBus,
    ILogger<ExplorationGrain> logger) : Grain, IExplorationGrain
{
    public override Task OnActivateAsync(CancellationToken ct)
    {
        state.State.SolarSystemId = this.GetPrimaryKeyString();

        if (state.State.ResourceNodes.Count == 0)
            SeedResourceNodes();

        // Periodic resource refresh every 30 minutes
        RegisterTimer(
            _ => RefreshResourceNodesAsync(),
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(30));

        return base.OnActivateAsync(ct);
    }

    private void SeedResourceNodes()
    {
        var rng = Random.Shared;
        var resourceTypes = new[] { "Veldspar", "Scordite", "Pyroxeres", "Plagioclase", "Omber" };
        var nodeCount = rng.Next(3, 8);

        for (var i = 0; i < nodeCount; i++)
        {
            state.State.ResourceNodes.Add(new ResourceNode
            {
                NodeId = $"{state.State.SolarSystemId}-node-{Guid.NewGuid():N}",
                ResourceType = resourceTypes[rng.Next(resourceTypes.Length)],
                RemainingQuantity = rng.Next(100, 5000),
                X = rng.NextDouble() * 1000 - 500,
                Y = rng.NextDouble() * 1000 - 500,
                Z = rng.NextDouble() * 1000 - 500
            });
        }
    }

    public Task<ExplorationGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<ScanResult> ScanAsync(Guid playerId)
    {
        var solarSystem = GrainFactory.GetGrain<ISolarSystemGrain>(state.State.SolarSystemId);
        var players = await solarSystem.GetPlayersInSystemAsync();

        var result = new ScanResult
        {
            PlayerCount = players.Count,
            ResourceNodes = state.State.ResourceNodes,
            AnomalyIds = state.State.ActiveEvents
                .Select(e => e.EventId.ToString())
                .ToList()
        };

        logger.LogInformation("Player {PlayerId} scanned system {SystemId}: {NodeCount} nodes, {EventCount} anomalies",
            playerId, state.State.SolarSystemId, result.ResourceNodes.Count, result.AnomalyIds.Count);

        return result;
    }

    public async Task<HarvestResult> HarvestResourceAsync(Guid playerId, string resourceNodeId)
    {
        var node = state.State.ResourceNodes.Find(n => n.NodeId == resourceNodeId);
        if (node is null || node.RemainingQuantity <= 0)
        {
            return new HarvestResult
            {
                ResourceNodeId = resourceNodeId,
                ResourceType = "None",
                QuantityHarvested = 0,
                NodeDepleted = true
            };
        }

        var harvestQty = Math.Min(node.RemainingQuantity, Random.Shared.Next(5, 50));
        node.RemainingQuantity -= harvestQty;
        var depleted = node.RemainingQuantity <= 0;

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ResourceHarvestedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            playerId, state.State.SolarSystemId, node.ResourceType, harvestQty));

        logger.LogInformation("Player {PlayerId} harvested {Qty} {Resource} from {NodeId}",
            playerId, harvestQty, node.ResourceType, resourceNodeId);

        return new HarvestResult
        {
            ResourceNodeId = resourceNodeId,
            ResourceType = node.ResourceType,
            QuantityHarvested = harvestQty,
            NodeDepleted = depleted
        };
    }

    public async Task<Guid> TriggerDynamicEventAsync(string eventType)
    {
        var worldEvent = new ActiveWorldEvent
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            StartsAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        };

        state.State.ActiveEvents.Add(worldEvent);
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new DynamicWorldEventTriggeredEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            state.State.SolarSystemId, eventType, worldEvent.EventId));

        logger.LogInformation("Dynamic event {EventType} triggered in {SystemId}: {EventId}",
            eventType, state.State.SolarSystemId, worldEvent.EventId);

        // Register timer to expire the event
        RegisterTimer(
            _ => ExpireWorldEventAsync(worldEvent.EventId),
            null,
            TimeSpan.FromHours(2),
            TimeSpan.FromMilliseconds(-1));

        return worldEvent.EventId;
    }

    private async Task ExpireWorldEventAsync(Guid eventId)
    {
        state.State.ActiveEvents.RemoveAll(e => e.EventId == eventId);
        await state.WriteStateAsync();
        logger.LogInformation("World event {EventId} expired in {SystemId}", eventId, state.State.SolarSystemId);
    }

    public async Task RefreshResourceNodesAsync()
    {
        // Replenish depleted nodes
        var rng = Random.Shared;
        foreach (var node in state.State.ResourceNodes.Where(n => n.RemainingQuantity <= 0))
        {
            node.RemainingQuantity = rng.Next(50, 3000);
        }

        await state.WriteStateAsync();
        logger.LogInformation("Resource nodes refreshed in {SystemId}", state.State.SolarSystemId);
    }
}
