using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class SkillGrainTests : GrainTestBase
{
    [Fact]
    public async Task LearnSkill_NewSkill_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        var result = await grain.LearnSkillAsync("gunnery");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.LearnedSkills.Should().ContainKey("gunnery");
    }

    [Fact]
    public async Task LearnSkill_AlreadyLearned_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        await grain.LearnSkillAsync("gunnery");

        var result = await grain.LearnSkillAsync("gunnery");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LearnSkill_MultipleSkills_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        await grain.LearnSkillAsync("gunnery");
        await grain.LearnSkillAsync("shields");
        await grain.LearnSkillAsync("navigation");

        var state = await grain.GetStateAsync();
        state.LearnedSkills.Should().HaveCount(3);
    }

    [Fact]
    public async Task StartTraining_NoCurrentTraining_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        await grain.LearnSkillAsync("gunnery");

        var result = await grain.StartTrainingAsync("gunnery");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.CurrentlyTrainingSkillId.Should().Be("gunnery");
        state.TrainingCompletesAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartTraining_AlreadyTraining_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        await grain.LearnSkillAsync("gunnery");
        await grain.StartTrainingAsync("gunnery");

        var result = await grain.StartTrainingAsync("shields");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task StartTraining_UnknownSkill_ShouldAutoLearnAndTrain()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        var result = await grain.StartTrainingAsync("new-skill");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.LearnedSkills.Should().ContainKey("new-skill");
        state.CurrentlyTrainingSkillId.Should().Be("new-skill");
    }

    [Fact]
    public async Task CancelTraining_WithActive_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        await grain.LearnSkillAsync("gunnery");
        await grain.StartTrainingAsync("gunnery");

        var result = await grain.CancelTrainingAsync();

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.CurrentlyTrainingSkillId.Should().BeNull();
        state.TrainingCompletesAt.Should().BeNull();
    }

    [Fact]
    public async Task CancelTraining_NoActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        var result = await grain.CancelTrainingAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteTraining_ShouldIncrementLevel()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        await grain.LearnSkillAsync("gunnery");
        await grain.StartTrainingAsync("gunnery");

        var result = await grain.CompleteTrainingAsync();

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.LearnedSkills["gunnery"].Should().Be(1);
        state.CurrentlyTrainingSkillId.Should().BeNull();
        state.TrainingCompletesAt.Should().BeNull();
    }

    [Fact]
    public async Task CompleteTraining_ShouldGrantTalentPoint()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        await grain.LearnSkillAsync("gunnery");
        await grain.StartTrainingAsync("gunnery");

        await grain.CompleteTrainingAsync();

        var state = await grain.GetStateAsync();
        state.UnspentTalentPoints.Should().Be(1);
    }

    [Fact]
    public async Task CompleteTraining_NoActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        var result = await grain.CompleteTrainingAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AllocateTalentPoint_WithPoints_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        await grain.LearnSkillAsync("gunnery");
        await grain.StartTrainingAsync("gunnery");
        await grain.CompleteTrainingAsync();

        var result = await grain.AllocateTalentPointAsync("rapid-fire");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.AllocatedTalents.Should().ContainKey("rapid-fire");
        state.UnspentTalentPoints.Should().Be(0);
    }

    [Fact]
    public async Task AllocateTalentPoint_NoPoints_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        var result = await grain.AllocateTalentPointAsync("rapid-fire");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResetTalents_ShouldRefundAllPoints()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());
        // Train twice to get 2 points
        await grain.LearnSkillAsync("gunnery");
        await grain.StartTrainingAsync("gunnery");
        await grain.CompleteTrainingAsync();
        await grain.StartTrainingAsync("gunnery");
        await grain.CompleteTrainingAsync();

        await grain.AllocateTalentPointAsync("rapid-fire");
        await grain.AllocateTalentPointAsync("sharpshooter");

        var result = await grain.ResetTalentsAsync();

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.AllocatedTalents.Should().BeEmpty();
        state.UnspentTalentPoints.Should().Be(2);
    }

    [Fact]
    public async Task ChangeReputation_ShouldIncrement()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        await grain.ChangeReputationAsync("caldari", 100);

        var state = await grain.GetStateAsync();
        state.FactionReputation["caldari"].Should().Be(100);
    }

    [Fact]
    public async Task ChangeReputation_MultipleFactions_ShouldTrackSeparately()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISkillGrain>(Guid.NewGuid());

        await grain.ChangeReputationAsync("caldari", 100);
        await grain.ChangeReputationAsync("gallente", -50);
        await grain.ChangeReputationAsync("caldari", 50);

        var state = await grain.GetStateAsync();
        state.FactionReputation["caldari"].Should().Be(150);
        state.FactionReputation["gallente"].Should().Be(-50);
    }
}
