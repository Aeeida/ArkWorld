using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class PlayerGrainTests : GrainTestBase
{
    [Fact]
    public async Task SetAndGetName_ShouldPersist()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        await grain.SetNameAsync("TestPlayer");
        var name = await grain.GetNameAsync();

        name.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task SetName_ShouldOverwrite()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        await grain.SetNameAsync("OldName");

        await grain.SetNameAsync("NewName");
        var name = await grain.GetNameAsync();

        name.Should().Be("NewName");
    }

    [Fact]
    public async Task GainExperience_ShouldLevelUp()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        await grain.SetNameAsync("LevelTester");

        var leveledUp = await grain.GainExperienceAsync(1000);

        leveledUp.Should().BeTrue();
        var level = await grain.GetLevelAsync();
        level.Should().Be(2);
    }

    [Fact]
    public async Task GainExperience_NotEnough_ShouldNotLevelUp()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        var leveledUp = await grain.GainExperienceAsync(500);

        leveledUp.Should().BeFalse();
        var level = await grain.GetLevelAsync();
        level.Should().Be(1);
    }

    [Fact]
    public async Task GainExperience_MultiLevel_ShouldLevelMultipleTimes()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        // Level 1→2 needs 1000, level 2→3 needs 2000
        await grain.GainExperienceAsync(3000);

        var level = await grain.GetLevelAsync();
        level.Should().Be(3);
    }

    [Fact]
    public async Task TakeDamage_ShouldReduceHealth()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        await grain.TakeDamageAsync(30);
        var health = await grain.GetHealthAsync();

        health.Should().Be(70);
    }

    [Fact]
    public async Task TakeDamage_Lethal_ShouldReduceToZero()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        await grain.TakeDamageAsync(200);
        var health = await grain.GetHealthAsync();

        health.Should().Be(0);
    }

    [Fact]
    public async Task Heal_ShouldRestoreHealth()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        await grain.TakeDamageAsync(50);

        await grain.HealAsync(30);
        var health = await grain.GetHealthAsync();

        health.Should().Be(80);
    }

    [Fact]
    public async Task Heal_ShouldNotExceedMaxHealth()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        await grain.TakeDamageAsync(20);

        await grain.HealAsync(999);
        var health = await grain.GetHealthAsync();

        health.Should().Be(100);
    }

    [Fact]
    public async Task WorldTracking_SetAndGet()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        await grain.SetWorldAsync("jita");
        var world = await grain.GetWorldAsync();
        world.Should().Be("jita");

        await grain.SetWorldAsync(null);
        world = await grain.GetWorldAsync();
        world.Should().BeNull();
    }

    [Fact]
    public async Task Respawn_WhenDead_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        await grain.TakeDamageAsync(200);

        var result = await grain.RespawnAsync();

        result.Should().BeTrue();
        var health = await grain.GetHealthAsync();
        health.Should().Be(100);
    }

    [Fact]
    public async Task Respawn_WhenAlive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        var result = await grain.RespawnAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryLevelUp_InsufficientXp_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        // GainExperienceAsync auto-levels when threshold is reached,
        // so with 999 XP (threshold 1000), TryLevelUp should fail
        await grain.GainExperienceAsync(999);

        var result = await grain.TryLevelUpAsync();

        result.Should().BeFalse();
        var level = await grain.GetLevelAsync();
        level.Should().Be(1);
    }

    [Fact]
    public async Task TryLevelUp_ExactThreshold_ShouldSucceedViaGainExperience()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        // GainExperienceAsync(1000) auto-levels inside the while loop
        var leveledUp = await grain.GainExperienceAsync(1000);

        leveledUp.Should().BeTrue();
        var level = await grain.GetLevelAsync();
        level.Should().Be(2);
    }

    [Fact]
    public async Task GetState_ShouldReturnFullState()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());
        await grain.SetNameAsync("StateTest");
        await grain.SetWorldAsync("amarr");
        await grain.GainExperienceAsync(1000);

        var state = await grain.GetStateAsync();

        state.Name.Should().Be("StateTest");
        state.CurrentWorldId.Should().Be("amarr");
        state.Level.Should().Be(2);
        state.MaxHealth.Should().Be(120);
    }

    [Fact]
    public async Task LevelUp_ShouldUpdateMaxHealth()
    {
        var grain = Cluster.GrainFactory.GetGrain<IPlayerGrain>(Guid.NewGuid());

        await grain.GainExperienceAsync(1000);

        var state = await grain.GetStateAsync();
        state.MaxHealth.Should().Be(120);
        state.Health.Should().Be(120);
    }
}
