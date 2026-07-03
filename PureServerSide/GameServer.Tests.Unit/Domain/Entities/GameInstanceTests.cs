using FluentAssertions;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Unit.Domain.Entities;

public class GameInstanceTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var leaderId = Guid.NewGuid();
        var instance = new GameInstance { TemplateId = "raid-dragon", LeaderId = leaderId, Difficulty = "Heroic" };

        instance.TemplateId.Should().Be("raid-dragon");
        instance.LeaderId.Should().Be(leaderId);
        instance.Difficulty.Should().Be("Heroic");
        instance.IsCompleted.Should().BeFalse();
        instance.PlayerIds.Should().BeEmpty();
    }

    [Fact]
    public void AddPlayer_ShouldAddToList()
    {
        var instance = new GameInstance { TemplateId = "dungeon-mines", LeaderId = Guid.NewGuid(), Difficulty = "Normal" };
        var playerId = Guid.NewGuid();

        instance.AddPlayer(playerId);

        instance.PlayerIds.Should().Contain(playerId);
    }

    [Fact]
    public void AddPlayer_Duplicate_ShouldNotDuplicate()
    {
        var instance = new GameInstance { TemplateId = "dungeon-mines", LeaderId = Guid.NewGuid(), Difficulty = "Normal" };
        var playerId = Guid.NewGuid();

        instance.AddPlayer(playerId);
        instance.AddPlayer(playerId);

        instance.PlayerIds.Should().HaveCount(1);
    }

    [Fact]
    public void AddPlayer_MultiplePlayers_ShouldTrackAll()
    {
        var instance = new GameInstance { TemplateId = "raid-boss", LeaderId = Guid.NewGuid(), Difficulty = "Mythic" };

        instance.AddPlayer(Guid.NewGuid());
        instance.AddPlayer(Guid.NewGuid());
        instance.AddPlayer(Guid.NewGuid());

        instance.PlayerIds.Should().HaveCount(3);
    }

    [Fact]
    public void Complete_ShouldSetIsCompleted()
    {
        var instance = new GameInstance { TemplateId = "dungeon-mines", LeaderId = Guid.NewGuid(), Difficulty = "Normal" };

        instance.Complete();

        instance.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Complete_ShouldUpdateTimestamp()
    {
        var instance = new GameInstance { TemplateId = "dungeon-mines", LeaderId = Guid.NewGuid(), Difficulty = "Normal" };
        var before = instance.UpdatedAt;

        instance.Complete();

        instance.UpdatedAt.Should().BeOnOrAfter(before);
    }
}

public class StationTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var ownerId = Guid.NewGuid();
        var station = new Station
        {
            Name = "Jita 4-4",
            SolarSystemId = "jita",
            OwnerId = ownerId,
            SecurityLevel = 0.9,
            HasMarket = true,
            HasRepairShop = true
        };

        station.Name.Should().Be("Jita 4-4");
        station.SolarSystemId.Should().Be("jita");
        station.OwnerId.Should().Be(ownerId);
        station.SecurityLevel.Should().Be(0.9);
        station.HasMarket.Should().BeTrue();
        station.HasRepairShop.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ShouldBeSet()
    {
        var station = new Station { Name = "Test", SolarSystemId = "sys-1" };

        station.SecurityLevel.Should().Be(1.0);
        station.HasMarket.Should().BeTrue();
        station.HasRepairShop.Should().BeTrue();
        station.OwnerId.Should().BeNull();
    }
}
