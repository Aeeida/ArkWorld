using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class QuestGrainTests : GrainTestBase
{
    [Fact]
    public async Task AcceptQuest_NewQuest_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        var result = await grain.AcceptQuestAsync("quest-001");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveQuests.Should().ContainKey("quest-001");
        state.ActiveQuests["quest-001"].Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task AcceptQuest_AlreadyActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");

        var result = await grain.AcceptQuestAsync("quest-001");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptQuest_AlreadyCompleted_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");
        await grain.CompleteQuestAsync("quest-001");

        var result = await grain.AcceptQuestAsync("quest-001");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptQuest_MultipleQuests_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        await grain.AcceptQuestAsync("quest-001");
        await grain.AcceptQuestAsync("quest-002");
        await grain.AcceptQuestAsync("quest-003");

        var state = await grain.GetStateAsync();
        state.ActiveQuests.Should().HaveCount(3);
    }

    [Fact]
    public async Task CompleteQuest_ActiveQuest_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");

        var result = await grain.CompleteQuestAsync("quest-001");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveQuests.Should().NotContainKey("quest-001");
        state.CompletedQuestIds.Should().Contain("quest-001");
    }

    [Fact]
    public async Task CompleteQuest_NotActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        var result = await grain.CompleteQuestAsync("quest-not-exist");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AbandonQuest_ActiveQuest_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");

        var result = await grain.AbandonQuestAsync("quest-001");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveQuests.Should().NotContainKey("quest-001");
        state.CompletedQuestIds.Should().NotContain("quest-001");
    }

    [Fact]
    public async Task AbandonQuest_NotActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        var result = await grain.AbandonQuestAsync("quest-not-exist");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProgress_ActiveQuest_ShouldIncrement()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");

        var result = await grain.UpdateProgressAsync("quest-001", "kill-dragons", 5);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveQuests["quest-001"].ObjectiveProgress["kill-dragons"].Should().Be(5);
    }

    [Fact]
    public async Task UpdateProgress_MultipleIncrements_ShouldAccumulate()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");

        await grain.UpdateProgressAsync("quest-001", "kill-dragons", 3);
        await grain.UpdateProgressAsync("quest-001", "kill-dragons", 7);

        var state = await grain.GetStateAsync();
        state.ActiveQuests["quest-001"].ObjectiveProgress["kill-dragons"].Should().Be(10);
    }

    [Fact]
    public async Task UpdateProgress_NotActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        var result = await grain.UpdateProgressAsync("quest-not-exist", "kill-dragons", 5);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ChooseBranch_FirstChoice_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");

        var result = await grain.ChooseBranchAsync("quest-001", 2);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveQuests["quest-001"].ChosenBranch.Should().Be(2);
    }

    [Fact]
    public async Task ChooseBranch_AlreadyChosen_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());
        await grain.AcceptQuestAsync("quest-001");
        await grain.ChooseBranchAsync("quest-001", 1);

        var result = await grain.ChooseBranchAsync("quest-001", 2);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ChooseBranch_NotActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        var result = await grain.ChooseBranchAsync("quest-not-exist", 1);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResetDailyQuests_ShouldClearCompletions()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        await grain.ResetDailyQuestsAsync();

        var state = await grain.GetStateAsync();
        state.DailyCompletions.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetWeeklyQuests_ShouldClearCompletions()
    {
        var grain = Cluster.GrainFactory.GetGrain<IQuestGrain>(Guid.NewGuid());

        await grain.ResetWeeklyQuestsAsync();

        var state = await grain.GetStateAsync();
        state.WeeklyCompletions.Should().BeEmpty();
    }
}
