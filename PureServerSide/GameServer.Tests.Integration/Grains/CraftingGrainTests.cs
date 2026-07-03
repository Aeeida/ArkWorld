using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class CraftingGrainTests : GrainTestBase
{
    [Fact]
    public async Task StartCrafting_ShouldCreateJob()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        var job = await grain.StartCraftingAsync("blueprint-laser", 2);

        job.Should().NotBeNull();
        job.BlueprintId.Should().Be("blueprint-laser");
        job.Quantity.Should().Be(2);
        job.Status.Should().Be("InProgress");
        job.JobId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartCrafting_ShouldAutoLearnBlueprint()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        await grain.StartCraftingAsync("unknown-blueprint", 1);

        var state = await grain.GetStateAsync();
        state.LearnedBlueprints.Should().Contain("unknown-blueprint");
    }

    [Fact]
    public async Task StartCrafting_ShouldAddToActiveJobs()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        await grain.StartCraftingAsync("blueprint-laser", 1);

        var state = await grain.GetStateAsync();
        state.ActiveJobs.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartCrafting_MultipleJobs_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        await grain.StartCraftingAsync("blueprint-laser", 1);
        await grain.StartCraftingAsync("blueprint-shield", 2);
        await grain.StartCraftingAsync("blueprint-armor", 3);

        var state = await grain.GetStateAsync();
        state.ActiveJobs.Should().HaveCount(3);
    }

    [Fact]
    public async Task CancelCrafting_ExistingJob_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());
        var job = await grain.StartCraftingAsync("blueprint-laser", 1);

        var result = await grain.CancelCraftingAsync(job.JobId);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelCrafting_NonExistentJob_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        var result = await grain.CancelCraftingAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteCrafting_ExistingJob_ShouldMoveToCompleted()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());
        var job = await grain.StartCraftingAsync("blueprint-laser", 1);

        var result = await grain.CompleteCraftingAsync(job.JobId);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveJobs.Should().BeEmpty();
        state.CompletedJobs.Should().HaveCount(1);
        state.CompletedJobs[0].Status.Should().Be("Completed");
    }

    [Fact]
    public async Task CompleteCrafting_NonExistentJob_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        var result = await grain.CompleteCraftingAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LearnBlueprint_NewBlueprint_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        var result = await grain.LearnBlueprintAsync("blueprint-laser");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.LearnedBlueprints.Should().Contain("blueprint-laser");
    }

    [Fact]
    public async Task LearnBlueprint_AlreadyLearned_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());
        await grain.LearnBlueprintAsync("blueprint-laser");

        var result = await grain.LearnBlueprintAsync("blueprint-laser");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LearnBlueprint_MultipleBlueprints_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());

        await grain.LearnBlueprintAsync("blueprint-laser");
        await grain.LearnBlueprintAsync("blueprint-shield");
        await grain.LearnBlueprintAsync("blueprint-armor");

        var state = await grain.GetStateAsync();
        state.LearnedBlueprints.Should().HaveCount(3);
    }

    [Fact]
    public async Task CompleteCrafting_ShouldPreserveJobData()
    {
        var grain = Cluster.GrainFactory.GetGrain<ICraftingGrain>(Guid.NewGuid());
        var job = await grain.StartCraftingAsync("blueprint-laser", 5);

        await grain.CompleteCraftingAsync(job.JobId);

        var state = await grain.GetStateAsync();
        var completed = state.CompletedJobs[0];
        completed.BlueprintId.Should().Be("blueprint-laser");
        completed.Quantity.Should().Be(5);
    }
}
