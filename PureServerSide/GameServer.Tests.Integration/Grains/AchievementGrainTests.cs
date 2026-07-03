using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class AchievementGrainTests : GrainTestBase
{
    [Fact]
    public async Task Unlock_NewAchievement_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        var result = await grain.UnlockAsync("first-kill", 10);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.Achievements.Should().ContainKey("first-kill");
        state.Achievements["first-kill"].Points.Should().Be(10);
        state.TotalPoints.Should().Be(10);
    }

    [Fact]
    public async Task Unlock_AlreadyUnlocked_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());
        await grain.UnlockAsync("first-kill", 10);

        var result = await grain.UnlockAsync("first-kill", 10);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Unlock_MultipleAchievements_ShouldAccumulatePoints()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        await grain.UnlockAsync("first-kill", 10);
        await grain.UnlockAsync("first-trade", 20);
        await grain.UnlockAsync("explorer", 50);

        var state = await grain.GetStateAsync();
        state.Achievements.Should().HaveCount(3);
        state.TotalPoints.Should().Be(80);
    }

    [Fact]
    public async Task UnlockTitle_NewTitle_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        var result = await grain.UnlockTitleAsync("pirate-lord", "Pirate Lord");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.Titles.Should().ContainKey("pirate-lord");
        state.Titles["pirate-lord"].Name.Should().Be("Pirate Lord");
    }

    [Fact]
    public async Task UnlockTitle_AlreadyUnlocked_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());
        await grain.UnlockTitleAsync("pirate-lord", "Pirate Lord");

        var result = await grain.UnlockTitleAsync("pirate-lord", "Pirate Lord");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveTitle_UnlockedTitle_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());
        await grain.UnlockTitleAsync("pirate-lord", "Pirate Lord");

        var result = await grain.SetActiveTitleAsync("pirate-lord");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveTitleId.Should().Be("pirate-lord");
    }

    [Fact]
    public async Task SetActiveTitle_NotUnlocked_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        var result = await grain.SetActiveTitleAsync("not-unlocked");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveTitle_ChangeTitle_ShouldUpdate()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());
        await grain.UnlockTitleAsync("pirate-lord", "Pirate Lord");
        await grain.UnlockTitleAsync("admiral", "Admiral");
        await grain.SetActiveTitleAsync("pirate-lord");

        await grain.SetActiveTitleAsync("admiral");

        var state = await grain.GetStateAsync();
        state.ActiveTitleId.Should().Be("admiral");
    }

    [Fact]
    public async Task UnlockAppearance_NewAppearance_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        var result = await grain.UnlockAppearanceAsync("golden-ship", "ship-skin");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.Appearances.Should().ContainKey("golden-ship");
        state.Appearances["golden-ship"].Category.Should().Be("ship-skin");
    }

    [Fact]
    public async Task UnlockAppearance_AlreadyUnlocked_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());
        await grain.UnlockAppearanceAsync("golden-ship", "ship-skin");

        var result = await grain.UnlockAppearanceAsync("golden-ship", "ship-skin");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementProgress_ShouldCreateAndTrack()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        await grain.IncrementProgressAsync("kill-100", 25);

        var state = await grain.GetStateAsync();
        state.InProgress.Should().ContainKey("kill-100");
        state.InProgress["kill-100"].Current.Should().Be(25);
    }

    [Fact]
    public async Task IncrementProgress_MultipleIncrements_ShouldAccumulate()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        await grain.IncrementProgressAsync("kill-100", 25);
        await grain.IncrementProgressAsync("kill-100", 30);

        var state = await grain.GetStateAsync();
        state.InProgress["kill-100"].Current.Should().Be(55);
    }

    [Fact]
    public async Task IncrementProgress_ReachRequired_ShouldAutoUnlock()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());

        // Default required is 100
        await grain.IncrementProgressAsync("kill-100", 100);

        var state = await grain.GetStateAsync();
        state.Achievements.Should().ContainKey("kill-100");
        state.InProgress.Should().NotContainKey("kill-100");
    }

    [Fact]
    public async Task IncrementProgress_AlreadyUnlocked_ShouldBeIgnored()
    {
        var grain = Cluster.GrainFactory.GetGrain<IAchievementGrain>(Guid.NewGuid());
        await grain.UnlockAsync("first-kill", 10);

        // Should not throw or change anything
        await grain.IncrementProgressAsync("first-kill", 50);

        var state = await grain.GetStateAsync();
        state.TotalPoints.Should().Be(10); // Unchanged
    }
}
