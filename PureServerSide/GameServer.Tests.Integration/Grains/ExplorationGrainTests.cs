using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class ExplorationGrainTests : GrainTestBase
{
    [Fact]
    public async Task GetState_ShouldSeedResourceNodes()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("explore-system-1");

        var state = await grain.GetStateAsync();

        state.SolarSystemId.Should().Be("explore-system-1");
        state.ResourceNodes.Should().HaveCountGreaterThanOrEqualTo(3);
        state.ResourceNodes.Should().HaveCountLessThanOrEqualTo(7);
    }

    [Fact]
    public async Task GetState_ResourceNodes_ShouldHaveValidTypes()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("explore-system-2");

        var state = await grain.GetStateAsync();

        var validTypes = new[] { "Veldspar", "Scordite", "Pyroxeres", "Plagioclase", "Omber" };
        state.ResourceNodes.Should().OnlyContain(n => validTypes.Contains(n.ResourceType));
    }

    [Fact]
    public async Task GetState_ResourceNodes_ShouldHavePositiveQuantity()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("explore-system-3");

        var state = await grain.GetStateAsync();

        state.ResourceNodes.Should().OnlyContain(n => n.RemainingQuantity > 0);
    }

    [Fact]
    public async Task Scan_ShouldReturnResourceNodes()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("scan-system-1");

        var result = await grain.ScanAsync(Guid.NewGuid());

        result.ResourceNodes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Scan_ShouldReturnPlayerCount()
    {
        var systemId = "scan-system-2";
        var solarSystem = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>(systemId);
        await solarSystem.PlayerEnteredAsync(Guid.NewGuid());
        await solarSystem.PlayerEnteredAsync(Guid.NewGuid());

        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>(systemId);

        var result = await grain.ScanAsync(Guid.NewGuid());

        result.PlayerCount.Should().Be(2);
    }

    [Fact]
    public async Task HarvestResource_ValidNode_ShouldReturnHarvestedQuantity()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("harvest-system-1");
        var state = await grain.GetStateAsync();
        var firstNode = state.ResourceNodes[0];

        var result = await grain.HarvestResourceAsync(Guid.NewGuid(), firstNode.NodeId);

        result.ResourceNodeId.Should().Be(firstNode.NodeId);
        result.ResourceType.Should().Be(firstNode.ResourceType);
        result.QuantityHarvested.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HarvestResource_NonExistentNode_ShouldReturnEmpty()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("harvest-system-2");

        var result = await grain.HarvestResourceAsync(Guid.NewGuid(), "non-existent-node");

        result.ResourceType.Should().Be("None");
        result.QuantityHarvested.Should().Be(0);
        result.NodeDepleted.Should().BeTrue();
    }

    [Fact]
    public async Task HarvestResource_ShouldReduceRemainingQuantity()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("harvest-system-3");
        var state = await grain.GetStateAsync();
        var firstNode = state.ResourceNodes[0];
        var originalQty = firstNode.RemainingQuantity;

        var result = await grain.HarvestResourceAsync(Guid.NewGuid(), firstNode.NodeId);

        var updatedState = await grain.GetStateAsync();
        var updatedNode = updatedState.ResourceNodes.Find(n => n.NodeId == firstNode.NodeId)!;
        updatedNode.RemainingQuantity.Should().Be(originalQty - result.QuantityHarvested);
    }

    [Fact]
    public async Task TriggerDynamicEvent_ShouldReturnEventId()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("event-system-1");

        var eventId = await grain.TriggerDynamicEventAsync("PirateRaid");

        eventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task TriggerDynamicEvent_ShouldAddToActiveEvents()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("event-system-2");

        await grain.TriggerDynamicEventAsync("PirateRaid");

        var state = await grain.GetStateAsync();
        state.ActiveEvents.Should().HaveCount(1);
        state.ActiveEvents[0].EventType.Should().Be("PirateRaid");
    }

    [Fact]
    public async Task TriggerDynamicEvent_MultipleEvents_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("event-system-3");

        await grain.TriggerDynamicEventAsync("PirateRaid");
        await grain.TriggerDynamicEventAsync("AsteroidStorm");
        await grain.TriggerDynamicEventAsync("WormholeSpawn");

        var state = await grain.GetStateAsync();
        state.ActiveEvents.Should().HaveCount(3);
    }

    [Fact]
    public async Task Scan_ShouldIncludeActiveEventsAsAnomalies()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("event-scan-system");
        await grain.TriggerDynamicEventAsync("PirateRaid");
        await grain.TriggerDynamicEventAsync("AsteroidStorm");

        var result = await grain.ScanAsync(Guid.NewGuid());

        result.AnomalyIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task RefreshResourceNodes_ShouldReplenishDepletedNodes()
    {
        var grain = Cluster.GrainFactory.GetGrain<IExplorationGrain>("refresh-system-1");
        var state = await grain.GetStateAsync();

        // Deplete all nodes by harvesting repeatedly
        foreach (var node in state.ResourceNodes)
        {
            while (node.RemainingQuantity > 0)
            {
                await grain.HarvestResourceAsync(Guid.NewGuid(), node.NodeId);
                state = await grain.GetStateAsync();
                var currentNode = state.ResourceNodes.Find(n => n.NodeId == node.NodeId);
                if (currentNode is null || currentNode.RemainingQuantity <= 0) break;
            }
        }

        await grain.RefreshResourceNodesAsync();

        var refreshedState = await grain.GetStateAsync();
        // Any previously depleted nodes should now have quantity > 0
        refreshedState.ResourceNodes.Should().OnlyContain(n => n.RemainingQuantity > 0);
    }
}
