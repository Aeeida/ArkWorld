using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class InstanceGrain(
    [PersistentState("instance", "GameStore")] IPersistentState<InstanceGrainState> state,
    ILogger<InstanceGrain> logger) : Grain, IInstanceGrain
{
    public async Task InitializeAsync(string templateId, Guid leaderId)
    {
        state.State.TemplateId = templateId;
        state.State.LeaderId = leaderId;
        state.State.PlayerIds = [leaderId];
        state.State.CreatedAt = DateTime.UtcNow;
        await state.WriteStateAsync();
        logger.LogInformation("Instance {TemplateId} created by {LeaderId}", templateId, leaderId);
    }

    public async Task<bool> AddPlayerAsync(Guid playerId)
    {
        if (state.State.IsCompleted) return false;
        if (state.State.PlayerIds.Contains(playerId)) return false;
        if (state.State.PlayerIds.Count >= 40) return false; // Raid cap

        state.State.PlayerIds.Add(playerId);
        await state.WriteStateAsync();
        return true;
    }

    public async Task<bool> RemovePlayerAsync(Guid playerId)
    {
        var removed = state.State.PlayerIds.Remove(playerId);
        if (removed) await state.WriteStateAsync();
        return removed;
    }

    public async Task CompleteAsync()
    {
        state.State.IsCompleted = true;
        await state.WriteStateAsync();
        logger.LogInformation("Instance {InstanceId} completed", this.GetPrimaryKey());
    }

    public Task<InstanceGrainState> GetStateAsync() => Task.FromResult(state.State);
}
