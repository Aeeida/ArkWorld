using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class GuildGrainTests : GrainTestBase
{
    [Fact]
    public async Task Initialize_ShouldSetFounderAndName()
    {
        var grain = Cluster.GrainFactory.GetGrain<IGuildGrain>(Guid.NewGuid());
        var founderId = Guid.NewGuid();

        await grain.InitializeAsync(founderId, "Goonswarm");
        var state = await grain.GetStateAsync();

        state.Name.Should().Be("Goonswarm");
        state.FounderId.Should().Be(founderId);
        state.MemberIds.Should().Contain(founderId);
    }

    [Fact]
    public async Task AddMember_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IGuildGrain>(Guid.NewGuid());
        var founderId = Guid.NewGuid();
        await grain.InitializeAsync(founderId, "TestGuild");

        var memberId = Guid.NewGuid();
        var joined = await grain.AddMemberAsync(memberId);

        joined.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.MemberIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddMember_Duplicate_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IGuildGrain>(Guid.NewGuid());
        var founderId = Guid.NewGuid();
        await grain.InitializeAsync(founderId, "TestGuild");

        var result = await grain.AddMemberAsync(founderId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMember_Regular_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IGuildGrain>(Guid.NewGuid());
        var founderId = Guid.NewGuid();
        await grain.InitializeAsync(founderId, "TestGuild");
        var memberId = Guid.NewGuid();
        await grain.AddMemberAsync(memberId);

        var result = await grain.RemoveMemberAsync(memberId);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.MemberIds.Should().NotContain(memberId);
    }

    [Fact]
    public async Task RemoveMember_Founder_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IGuildGrain>(Guid.NewGuid());
        var founderId = Guid.NewGuid();
        await grain.InitializeAsync(founderId, "TestGuild");

        var removed = await grain.RemoveMemberAsync(founderId);

        removed.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMember_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IGuildGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "TestGuild");

        var result = await grain.RemoveMemberAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMember_MultipleMembers_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<IGuildGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "BigGuild");

        for (var i = 0; i < 5; i++)
            await grain.AddMemberAsync(Guid.NewGuid());

        var state = await grain.GetStateAsync();
        state.MemberIds.Should().HaveCount(6); // founder + 5
    }
}
