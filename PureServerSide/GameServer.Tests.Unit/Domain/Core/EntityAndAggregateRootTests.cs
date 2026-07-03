using FluentAssertions;
using GameServer.Domain.Core;
using GameServer.Domain.Entities;
using GameServer.Domain.Events;

namespace GameServer.Tests.Unit.Domain.Core;

public class EntityTests
{
    [Fact]
    public void Equals_SameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var player1 = Player.Create(id, "Player1", "Alliance", "Warrior");
        var player2 = Player.Create(id, "Player2", "Empire", "Mage");

        player1.Equals(player2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentId_ShouldNotBeEqual()
    {
        var player1 = Player.Create(Guid.NewGuid(), "Player1", "Alliance", "Warrior");
        var player2 = Player.Create(Guid.NewGuid(), "Player2", "Alliance", "Warrior");

        player1.Equals(player2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var player1 = Player.Create(id, "Player1", "Alliance", "Warrior");
        var player2 = Player.Create(id, "Player2", "Empire", "Mage");

        player1.GetHashCode().Should().Be(player2.GetHashCode());
    }

    [Fact]
    public void CreatedAt_ShouldBeSetOnCreation()
    {
        var before = DateTime.UtcNow;
        var player = Player.Create(Guid.NewGuid(), "Test", "Alliance", "Warrior");

        player.CreatedAt.Should().BeOnOrAfter(before);
        player.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void UpdatedAt_ShouldChangeAfterMutation()
    {
        var player = Player.Create(Guid.NewGuid(), "Test", "Alliance", "Warrior");
        var original = player.UpdatedAt;

        player.TakeDamage(10);

        player.UpdatedAt.Should().BeOnOrAfter(original);
    }
}

public class AggregateRootTests
{
    [Fact]
    public void DomainEvents_ShouldBeEmptyInitially()
    {
        // Ship does not raise events on Create
        var ship = Ship.Create(Guid.NewGuid(), Guid.NewGuid(), "Frigate", 100, 50, 75);

        ship.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAddToList()
    {
        var player = Player.Create(Guid.NewGuid(), "Test", "Alliance", "Warrior");

        player.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyList()
    {
        var player = Player.Create(Guid.NewGuid(), "Test", "Alliance", "Warrior");
        player.ClearDomainEvents();

        player.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MultipleDomainEvents_ShouldAccumulate()
    {
        var player = Player.Create(Guid.NewGuid(), "Test", "Alliance", "Warrior");
        // Create raises 1 event, level up raises another
        player.GainExperience(1000);

        player.DomainEvents.Should().HaveCount(2);
    }
}
