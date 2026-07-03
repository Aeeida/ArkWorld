using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class FleetGrain(
    [PersistentState("fleet", "GameStore")] IPersistentState<FleetGrainState> state,
    ILogger<FleetGrain> logger) : Grain, IFleetGrain
{
    public async Task InitializeAsync(Guid leaderId, string name)
    {
        state.State.LeaderId = leaderId;
        state.State.Name = name;
        state.State.MemberIds = [leaderId];
        await state.WriteStateAsync();
        logger.LogInformation("Fleet {Name} created by {LeaderId}", name, leaderId);
    }

    public async Task<bool> AddMemberAsync(Guid playerId)
    {
        if (state.State.IsDisbanded) return false;
        if (state.State.MemberIds.Contains(playerId)) return false;
        if (state.State.MemberIds.Count >= 256) return false;

        state.State.MemberIds.Add(playerId);
        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} joined fleet {FleetId}", playerId, this.GetPrimaryKey());
        return true;
    }

    public async Task<bool> RemoveMemberAsync(Guid playerId)
    {
        var removed = state.State.MemberIds.Remove(playerId);
        if (removed) await state.WriteStateAsync();
        return removed;
    }

    public Task<FleetGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task DisbandAsync()
    {
        state.State.IsDisbanded = true;
        state.State.MemberIds.Clear();
        await state.WriteStateAsync();
    }
}
