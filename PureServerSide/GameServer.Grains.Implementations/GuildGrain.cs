using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class GuildGrain(
    [PersistentState("guild", "GameStore")] IPersistentState<GuildGrainState> state,
    ILogger<GuildGrain> logger) : Grain, IGuildGrain
{
    public async Task InitializeAsync(Guid founderId, string name)
    {
        state.State.FounderId = founderId;
        state.State.Name = name;
        state.State.MemberIds = [founderId];
        await state.WriteStateAsync();
        logger.LogInformation("Guild {Name} founded by {FounderId}", name, founderId);
    }

    public async Task<bool> AddMemberAsync(Guid playerId)
    {
        if (state.State.MemberIds.Contains(playerId)) return false;
        if (state.State.MemberIds.Count >= 500) return false;

        state.State.MemberIds.Add(playerId);
        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} joined guild {GuildId}", playerId, this.GetPrimaryKey());
        return true;
    }

    public async Task<bool> RemoveMemberAsync(Guid playerId)
    {
        if (playerId == state.State.FounderId) return false;
        var removed = state.State.MemberIds.Remove(playerId);
        if (removed) await state.WriteStateAsync();
        return removed;
    }

    public Task<GuildGrainState> GetStateAsync() => Task.FromResult(state.State);
}
