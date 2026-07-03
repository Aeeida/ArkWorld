using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class InstanceGrainTests : GrainTestBase
{
    [Fact]
    public async Task Initialize_ShouldSetState()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        var leaderId = Guid.NewGuid();

        await grain.InitializeAsync("raid-dragon-lair", leaderId);
        var state = await grain.GetStateAsync();

        state.TemplateId.Should().Be("raid-dragon-lair");
        state.LeaderId.Should().Be(leaderId);
        state.PlayerIds.Should().Contain(leaderId);
        state.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task AddPlayer_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        await grain.InitializeAsync("dungeon", Guid.NewGuid());

        var player2 = Guid.NewGuid();
        var joined = await grain.AddPlayerAsync(player2);

        joined.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.PlayerIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddPlayer_Duplicate_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        var leaderId = Guid.NewGuid();
        await grain.InitializeAsync("dungeon", leaderId);

        var result = await grain.AddPlayerAsync(leaderId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddPlayer_AfterComplete_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        await grain.InitializeAsync("dungeon-mines", Guid.NewGuid());
        await grain.CompleteAsync();

        var joined = await grain.AddPlayerAsync(Guid.NewGuid());

        joined.Should().BeFalse();
    }

    [Fact]
    public async Task RemovePlayer_Existing_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        await grain.InitializeAsync("dungeon", Guid.NewGuid());
        var player2 = Guid.NewGuid();
        await grain.AddPlayerAsync(player2);

        var result = await grain.RemovePlayerAsync(player2);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemovePlayer_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        await grain.InitializeAsync("dungeon", Guid.NewGuid());

        var result = await grain.RemovePlayerAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Complete_ShouldMarkCompleted()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        await grain.InitializeAsync("dungeon", Guid.NewGuid());

        await grain.CompleteAsync();
        var state = await grain.GetStateAsync();

        state.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task MultiplePlayersJoin_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<IInstanceGrain>(Guid.NewGuid());
        await grain.InitializeAsync("raid", Guid.NewGuid());

        for (var i = 0; i < 10; i++)
            await grain.AddPlayerAsync(Guid.NewGuid());

        var state = await grain.GetStateAsync();
        state.PlayerIds.Should().HaveCount(11); // leader + 10
    }
}
