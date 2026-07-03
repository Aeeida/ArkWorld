using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class CraftingGrain(
    [PersistentState("crafting", "GameStore")] IPersistentState<CraftingGrainState> state,
    IEventBus eventBus,
    ILogger<CraftingGrain> logger) : Grain, ICraftingGrain
{
    public Task<CraftingGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<CraftingJob> StartCraftingAsync(string blueprintId, int quantity)
    {
        if (!state.State.LearnedBlueprints.Contains(blueprintId))
        {
            // Auto-learn for now; in production, this would be validated
            state.State.LearnedBlueprints.Add(blueprintId);
        }

        var durationSec = 300 * quantity; // 5 min per item
        var job = new CraftingJob
        {
            JobId = Guid.NewGuid(),
            BlueprintId = blueprintId,
            Quantity = quantity,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            CompletesAt = DateTime.UtcNow.AddSeconds(durationSec)
        };

        state.State.ActiveJobs.Add(job);
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new CraftingStartedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), blueprintId, quantity));

        logger.LogInformation("Player {PlayerId} started crafting {BlueprintId} x{Qty}, job {JobId}",
            this.GetPrimaryKey(), blueprintId, quantity, job.JobId);

        // Register timer for completion
        RegisterTimer(
            _ => CheckCraftingCompletionAsync(job.JobId),
            null,
            TimeSpan.FromSeconds(durationSec),
            TimeSpan.FromMilliseconds(-1));

        return job;
    }

    private async Task CheckCraftingCompletionAsync(Guid jobId)
    {
        var job = state.State.ActiveJobs.Find(j => j.JobId == jobId);
        if (job is null || job.CompletesAt > DateTime.UtcNow)
            return;

        await CompleteCraftingAsync(jobId);
    }

    public async Task<bool> CancelCraftingAsync(Guid jobId)
    {
        var job = state.State.ActiveJobs.Find(j => j.JobId == jobId);
        if (job is null)
            return false;

        state.State.ActiveJobs.Remove(job);
        await state.WriteStateAsync();

        logger.LogInformation("Player {PlayerId} cancelled crafting job {JobId}",
            this.GetPrimaryKey(), jobId);
        return true;
    }

    public async Task<bool> CompleteCraftingAsync(Guid jobId)
    {
        var job = state.State.ActiveJobs.Find(j => j.JobId == jobId);
        if (job is null)
            return false;

        job.Status = "Completed";
        state.State.ActiveJobs.Remove(job);
        state.State.CompletedJobs.Add(job);
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new CraftingCompletedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), job.BlueprintId, job.Quantity));

        logger.LogInformation("Player {PlayerId} completed crafting job {JobId}: {BlueprintId} x{Qty}",
            this.GetPrimaryKey(), jobId, job.BlueprintId, job.Quantity);
        return true;
    }

    public async Task<bool> LearnBlueprintAsync(string blueprintId)
    {
        if (!state.State.LearnedBlueprints.Add(blueprintId))
            return false;

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new BlueprintLearnedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), blueprintId));

        logger.LogInformation("Player {PlayerId} learned blueprint {BlueprintId}",
            this.GetPrimaryKey(), blueprintId);
        return true;
    }
}
