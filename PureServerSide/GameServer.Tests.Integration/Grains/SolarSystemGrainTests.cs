using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class SolarSystemGrainTests : GrainTestBase
{
    [Fact]
    public async Task PlayerEntered_ShouldTrackPlayer()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-jita");
        var playerId = Guid.NewGuid();

        await grain.PlayerEnteredAsync(playerId);
        var players = await grain.GetPlayersInSystemAsync();

        players.Should().Contain(playerId);
    }

    [Fact]
    public async Task PlayerEntered_Duplicate_ShouldNotDuplicate()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-amarr");
        var playerId = Guid.NewGuid();

        await grain.PlayerEnteredAsync(playerId);
        await grain.PlayerEnteredAsync(playerId);
        var players = await grain.GetPlayersInSystemAsync();

        players.Should().HaveCount(1);
    }

    [Fact]
    public async Task PlayerLeft_ShouldRemovePlayer()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-dodixie");
        var playerId = Guid.NewGuid();
        await grain.PlayerEnteredAsync(playerId);

        await grain.PlayerLeftAsync(playerId);
        var players = await grain.GetPlayersInSystemAsync();

        players.Should().NotContain(playerId);
    }

    [Fact]
    public async Task PlayerLeft_NotPresent_ShouldNotFail()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-hek");

        await grain.PlayerLeftAsync(Guid.NewGuid());
        var players = await grain.GetPlayersInSystemAsync();

        players.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiplePlayers_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-rens");
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();

        await grain.PlayerEnteredAsync(p1);
        await grain.PlayerEnteredAsync(p2);
        await grain.PlayerEnteredAsync(p3);

        var players = await grain.GetPlayersInSystemAsync();
        players.Should().HaveCount(3);
    }

    [Fact]
    public async Task SetSovereignty_ShouldPersist()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-sov");
        var allianceId = Guid.NewGuid();

        await grain.SetSovereigntyAsync(allianceId);
        var state = await grain.GetStateAsync();

        state.SovereignAllianceId.Should().Be(allianceId);
    }

    [Fact]
    public async Task SetSovereignty_ToNull_ShouldClear()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-sov-clear");
        await grain.SetSovereigntyAsync(Guid.NewGuid());

        await grain.SetSovereigntyAsync(null);
        var state = await grain.GetStateAsync();

        state.SovereignAllianceId.Should().BeNull();
    }

    [Fact]
    public async Task GetState_ShouldReturnState()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISolarSystemGrain>("system-state-test");
        await grain.PlayerEnteredAsync(Guid.NewGuid());
        await grain.PlayerEnteredAsync(Guid.NewGuid());

        var state = await grain.GetStateAsync();

        state.PlayerIds.Should().HaveCount(2);
    }
}
