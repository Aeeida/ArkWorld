using FluentAssertions;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Unit.Domain.Entities;

public class GuildTests
{
    [Fact]
    public void AddMember_ShouldAddToMemberList()
    {
        var guild = new Guild { Name = "TestGuild", FounderId = Guid.NewGuid() };
        var memberId = Guid.NewGuid();

        guild.AddMember(memberId);

        guild.MemberIds.Should().Contain(memberId);
    }

    [Fact]
    public void AddMember_Duplicate_ShouldNotDuplicate()
    {
        var guild = new Guild { Name = "TestGuild", FounderId = Guid.NewGuid() };
        var memberId = Guid.NewGuid();

        guild.AddMember(memberId);
        guild.AddMember(memberId);

        guild.MemberIds.Should().HaveCount(1);
    }

    [Fact]
    public void AddMember_MultipleMembers_ShouldTrackAll()
    {
        var guild = new Guild { Name = "TestGuild", FounderId = Guid.NewGuid() };
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var member3 = Guid.NewGuid();

        guild.AddMember(member1);
        guild.AddMember(member2);
        guild.AddMember(member3);

        guild.MemberIds.Should().HaveCount(3);
    }

    [Fact]
    public void RemoveMember_Existing_ShouldReturnTrue()
    {
        var guild = new Guild { Name = "TestGuild", FounderId = Guid.NewGuid() };
        var memberId = Guid.NewGuid();
        guild.AddMember(memberId);

        var removed = guild.RemoveMember(memberId);

        removed.Should().BeTrue();
        guild.MemberIds.Should().NotContain(memberId);
    }

    [Fact]
    public void RemoveMember_NonExisting_ShouldReturnFalse()
    {
        var guild = new Guild { Name = "TestGuild", FounderId = Guid.NewGuid() };

        var removed = guild.RemoveMember(Guid.NewGuid());

        removed.Should().BeFalse();
    }

    [Fact]
    public void FounderProperties_ShouldBeSet()
    {
        var founderId = Guid.NewGuid();
        var guild = new Guild { Name = "Goonswarm", FounderId = founderId };

        guild.Name.Should().Be("Goonswarm");
        guild.FounderId.Should().Be(founderId);
    }
}
