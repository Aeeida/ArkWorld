using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class FleetGrainTests : GrainTestBase
{
    [Fact]
    public async Task Initialize_ShouldSetLeaderAndName()
    {
        var grain = Cluster.GrainFactory.GetGrain<IFleetGrain>(Guid.NewGuid());
        var leaderId = Guid.NewGuid();

        await grain.InitializeAsync(leaderId, "Alpha Fleet");
        var state = await grain.GetStateAsync();

        state.Name.Should().Be("Alpha Fleet");
        state.LeaderId.Should().Be(leaderId);
        state.MemberIds.Should().Contain(leaderId);
    }

    [Fact]
    public async Task AddMember_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IFleetGrain>(Guid.NewGuid());
        var leaderId = Guid.NewGuid();
        await grain.InitializeAsync(leaderId, "Fleet");

        var player2 = Guid.NewGuid();
        var joined = await grain.AddMemberAsync(player2);

        joined.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.MemberIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddMember_Duplicate_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IFleetGrain>(Guid.NewGuid());
        var leaderId = Guid.NewGuid();
        await grain.InitializeAsync(leaderId, "Fleet");

        var result = await grain.AddMemberAsync(leaderId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMember_AfterDisband_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IFleetGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Fleet");
        await grain.DisbandAsync();

        var result = await grain.AddMemberAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMember_Existing_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IFleetGrain>(Guid.NewGuid());
        var leaderId = Guid.NewGuid();
        await grain.InitializeAsync(leaderId, "Fleet");
        var member = Guid.NewGuid();
        await grain.AddMemberAsync(member);

        var result = await grain.RemoveMemberAsync(member);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.MemberIds.Should().NotContain(member);
    }

    [Fact]
    public async Task RemoveMember_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IFleetGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Fleet");

        var result = await grain.RemoveMemberAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Disband_ShouldClearMembers()
    {
        var grain = Cluster.GrainFactory.GetGrain<IFleetGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Fleet");
        await grain.AddMemberAsync(Guid.NewGuid());

        await grain.DisbandAsync();
        var state = await grain.GetStateAsync();

        state.IsDisbanded.Should().BeTrue();
        state.MemberIds.Should().BeEmpty();
    }
}
