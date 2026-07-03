using FluentAssertions;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Unit.Domain.Entities;

public class FleetTests
{
    [Fact]
    public void AddMember_ShouldAddToMemberList()
    {
        var fleet = new Fleet { Name = "Alpha Fleet", LeaderId = Guid.NewGuid() };
        var memberId = Guid.NewGuid();

        fleet.AddMember(memberId);

        fleet.MemberIds.Should().Contain(memberId);
    }

    [Fact]
    public void AddMember_Duplicate_ShouldNotDuplicate()
    {
        var fleet = new Fleet { Name = "Alpha Fleet", LeaderId = Guid.NewGuid() };
        var memberId = Guid.NewGuid();

        fleet.AddMember(memberId);
        fleet.AddMember(memberId);

        fleet.MemberIds.Should().HaveCount(1);
    }

    [Fact]
    public void AddMember_MultipleMembers_ShouldTrackAll()
    {
        var fleet = new Fleet { Name = "Alpha Fleet", LeaderId = Guid.NewGuid() };
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var m3 = Guid.NewGuid();

        fleet.AddMember(m1);
        fleet.AddMember(m2);
        fleet.AddMember(m3);

        fleet.MemberIds.Should().HaveCount(3);
    }

    [Fact]
    public void RemoveMember_Existing_ShouldReturnTrue()
    {
        var fleet = new Fleet { Name = "Alpha Fleet", LeaderId = Guid.NewGuid() };
        var memberId = Guid.NewGuid();
        fleet.AddMember(memberId);

        var removed = fleet.RemoveMember(memberId);

        removed.Should().BeTrue();
        fleet.MemberIds.Should().NotContain(memberId);
    }

    [Fact]
    public void RemoveMember_NonExisting_ShouldReturnFalse()
    {
        var fleet = new Fleet { Name = "Alpha Fleet", LeaderId = Guid.NewGuid() };

        var removed = fleet.RemoveMember(Guid.NewGuid());

        removed.Should().BeFalse();
    }

    [Fact]
    public void LeaderAndName_ShouldBeSet()
    {
        var leaderId = Guid.NewGuid();
        var fleet = new Fleet { Name = "Bravo Fleet", LeaderId = leaderId };

        fleet.Name.Should().Be("Bravo Fleet");
        fleet.LeaderId.Should().Be(leaderId);
    }
}
