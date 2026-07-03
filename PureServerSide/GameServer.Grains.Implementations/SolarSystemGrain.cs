using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class SolarSystemGrain(
    [PersistentState("solarSystem", "GameStore")] IPersistentState<SolarSystemGrainState> state,
    ILogger<SolarSystemGrain> logger) : Grain, ISolarSystemGrain
{
    public Task<IReadOnlyList<Guid>> GetPlayersInSystemAsync() =>
        Task.FromResult<IReadOnlyList<Guid>>(state.State.PlayerIds.AsReadOnly());

    public async Task PlayerEnteredAsync(Guid playerId)
    {
        if (!state.State.PlayerIds.Contains(playerId))
        {
            state.State.PlayerIds.Add(playerId);
            await state.WriteStateAsync();
            logger.LogInformation("Player {PlayerId} entered system {SystemId}",
                playerId, this.GetPrimaryKeyString());
        }
    }

    public async Task PlayerLeftAsync(Guid playerId)
    {
        if (state.State.PlayerIds.Remove(playerId))
        {
            await state.WriteStateAsync();
            logger.LogInformation("Player {PlayerId} left system {SystemId}",
                playerId, this.GetPrimaryKeyString());
        }
    }

    public Task<SolarSystemGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task SetSovereigntyAsync(Guid? allianceId)
    {
        state.State.SovereignAllianceId = allianceId;
        await state.WriteStateAsync();
    }
}
