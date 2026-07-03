using FluentAssertions;
using GameServer.Domain.Entities;
using GameServer.Domain.Events;

namespace GameServer.Tests.Unit.Domain.Entities;

public class PlayerTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var player = Player.Create(id, "TestPlayer", "Alliance", "Warrior");

        player.Id.Should().Be(id);
        player.Name.Should().Be("TestPlayer");
        player.Faction.Should().Be("Alliance");
        player.CharacterClass.Should().Be("Warrior");
        player.Level.Should().Be(1);
        player.Health.Should().Be(100);
        player.MaxHealth.Should().Be(100);
        player.Experience.Should().Be(0);
        player.WalletBalance.Should().Be(0);
        player.IsDead.Should().BeFalse();
        player.CurrentWorldId.Should().BeNull();
        player.GuildId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaisePlayerCreatedEvent()
    {
        var player = Player.Create(Guid.NewGuid(), "EventPlayer", "Empire", "Mage");

        player.DomainEvents.Should().HaveCount(1);
        player.DomainEvents[0].Should().BeOfType<PlayerCreatedEvent>();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrow()
    {
        var act = () => Player.Create(Guid.NewGuid(), "", "Alliance", "Warrior");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespaceName_ShouldThrow()
    {
        var act = () => Player.Create(Guid.NewGuid(), "   ", "Alliance", "Warrior");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GainExperience_Enough_ShouldLevelUp()
    {
        var player = Player.Create(Guid.NewGuid(), "LevelPlayer", "Alliance", "Warrior");

        var leveledUp = player.GainExperience(1000);

        leveledUp.Should().BeTrue();
        player.Level.Should().Be(2);
        player.MaxHealth.Should().Be(120);
        player.Health.Should().Be(120); // healed to max on level up
    }

    [Fact]
    public void GainExperience_ShouldRaiseLevelUpEvent()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Alliance", "Warrior");
        player.ClearDomainEvents();

        player.GainExperience(1000);

        player.DomainEvents.Should().HaveCount(1);
        player.DomainEvents[0].Should().BeOfType<PlayerLeveledUpEvent>();
    }

    [Fact]
    public void GainExperience_NotEnough_ShouldNotLevelUp()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Federation", "Rogue");

        var leveledUp = player.GainExperience(500);

        leveledUp.Should().BeFalse();
        player.Level.Should().Be(1);
    }

    [Fact]
    public void GainExperience_DoubleLevel_ShouldLevelUpTwice()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Alliance", "Warrior");

        // Level 1→2 needs 1000, level 2→3 needs 2000, total 3000
        var leveledUp = player.GainExperience(3000);

        leveledUp.Should().BeTrue();
        player.Level.Should().Be(3);
        player.MaxHealth.Should().Be(140);
    }

    [Fact]
    public void GainExperience_NoLevelUp_ShouldNotRaiseEvent()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Alliance", "Warrior");
        player.ClearDomainEvents();

        player.GainExperience(100);

        player.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TakeDamage_ShouldReduceHealth()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Empire", "Mage");

        player.TakeDamage(30);

        player.Health.Should().Be(70);
        player.IsDead.Should().BeFalse();
    }

    [Fact]
    public void TakeDamage_Lethal_ShouldDie()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Empire", "Mage");

        player.TakeDamage(200);

        player.Health.Should().Be(0);
        player.IsDead.Should().BeTrue();
    }

    [Fact]
    public void TakeDamage_Exact_ShouldDie()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Empire", "Mage");

        player.TakeDamage(100);

        player.Health.Should().Be(0);
        player.IsDead.Should().BeTrue();
    }

    [Fact]
    public void TakeDamage_Zero_ShouldNotChangeHealth()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Empire", "Mage");

        player.TakeDamage(0);

        player.Health.Should().Be(100);
    }

    [Fact]
    public void Heal_ShouldIncreaseHealth()
    {
        var player = Player.Create(Guid.NewGuid(), "HealPlayer", "Alliance", "Healer");
        player.TakeDamage(50);

        player.Heal(30);

        player.Health.Should().Be(80);
    }

    [Fact]
    public void Heal_ShouldNotExceedMaxHealth()
    {
        var player = Player.Create(Guid.NewGuid(), "HealPlayer", "Alliance", "Healer");
        player.TakeDamage(30);

        player.Heal(50);

        player.Health.Should().Be(100);
    }

    [Fact]
    public void Heal_AtFullHealth_ShouldStayAtMax()
    {
        var player = Player.Create(Guid.NewGuid(), "HealPlayer", "Alliance", "Healer");

        player.Heal(999);

        player.Health.Should().Be(100);
    }

    [Fact]
    public void Credit_ShouldIncreaseBalance()
    {
        var player = Player.Create(Guid.NewGuid(), "WalletPlayer", "Federation", "Trader");

        player.Credit(1000m);

        player.WalletBalance.Should().Be(1000m);
    }

    [Fact]
    public void Credit_ZeroOrNegative_ShouldThrow()
    {
        var player = Player.Create(Guid.NewGuid(), "WalletPlayer", "Federation", "Trader");

        var act0 = () => player.Credit(0);
        var actNeg = () => player.Credit(-100m);

        act0.Should().Throw<ArgumentException>();
        actNeg.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Debit_WithSufficientFunds_ShouldSucceed()
    {
        var player = Player.Create(Guid.NewGuid(), "WalletPlayer", "Federation", "Trader");
        player.Credit(1000m);

        var result = player.Debit(300m);

        result.Should().BeTrue();
        player.WalletBalance.Should().Be(700m);
    }

    [Fact]
    public void Debit_WithInsufficientFunds_ShouldFail()
    {
        var player = Player.Create(Guid.NewGuid(), "WalletPlayer", "Federation", "Trader");
        player.Credit(100m);

        var result = player.Debit(999m);

        result.Should().BeFalse();
        player.WalletBalance.Should().Be(100m);
    }

    [Fact]
    public void Debit_ExactBalance_ShouldSucceed()
    {
        var player = Player.Create(Guid.NewGuid(), "WalletPlayer", "Federation", "Trader");
        player.Credit(500m);

        var result = player.Debit(500m);

        result.Should().BeTrue();
        player.WalletBalance.Should().Be(0m);
    }

    [Fact]
    public void Debit_ZeroOrNegative_ShouldThrow()
    {
        var player = Player.Create(Guid.NewGuid(), "WalletPlayer", "Federation", "Trader");

        var act = () => player.Debit(0);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnterWorld_ShouldSetWorldId()
    {
        var player = Player.Create(Guid.NewGuid(), "WorldPlayer", "Alliance", "Warrior");

        player.EnterWorld("jita-system");

        player.CurrentWorldId.Should().Be("jita-system");
    }

    [Fact]
    public void LeaveWorld_ShouldClearWorldId()
    {
        var player = Player.Create(Guid.NewGuid(), "WorldPlayer", "Alliance", "Warrior");
        player.EnterWorld("jita-system");

        player.LeaveWorld();

        player.CurrentWorldId.Should().BeNull();
    }

    [Fact]
    public void AddBuff_ShouldAddToList()
    {
        var player = Player.Create(Guid.NewGuid(), "BuffPlayer", "Empire", "Mage");

        player.AddBuff("damage_boost");

        player.ActiveBuffIds.Should().Contain("damage_boost");
    }

    [Fact]
    public void AddBuff_Duplicate_ShouldNotDuplicate()
    {
        var player = Player.Create(Guid.NewGuid(), "BuffPlayer", "Empire", "Mage");

        player.AddBuff("shield");
        player.AddBuff("shield");

        player.ActiveBuffIds.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveBuff_ShouldRemoveFromList()
    {
        var player = Player.Create(Guid.NewGuid(), "BuffPlayer", "Empire", "Mage");
        player.AddBuff("damage_boost");
        player.AddBuff("shield");

        player.RemoveBuff("damage_boost");

        player.ActiveBuffIds.Should().HaveCount(1);
        player.ActiveBuffIds.Should().NotContain("damage_boost");
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAll()
    {
        var player = Player.Create(Guid.NewGuid(), "TestPlayer", "Alliance", "Warrior");
        player.DomainEvents.Should().NotBeEmpty();

        player.ClearDomainEvents();

        player.DomainEvents.Should().BeEmpty();
    }
}
