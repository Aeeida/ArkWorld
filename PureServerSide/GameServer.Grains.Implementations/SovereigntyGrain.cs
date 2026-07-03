using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class SovereigntyGrain(
    [PersistentState("sovereignty", "GameStore")] IPersistentState<SovereigntyGrainState> state,
    IEventBus eventBus,
    ILogger<SovereigntyGrain> logger) : Grain, ISovereigntyGrain
{
    public override Task OnActivateAsync(CancellationToken ct)
    {
        state.State.SolarSystemId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(ct);
    }

    public Task<SovereigntyGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<bool> ClaimAsync(Guid allianceId)
    {
        if (state.State.OwnerAllianceId is not null)
            return false; // already claimed

        if (state.State.ActiveContests.Count > 0)
            return false; // contested

        var oldOwner = state.State.OwnerAllianceId;
        state.State.OwnerAllianceId = allianceId;
        state.State.ClaimedAt = DateTime.UtcNow;
        state.State.Status = "Claimed";
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new SovereigntyChangedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            state.State.SolarSystemId, oldOwner, allianceId));

        logger.LogInformation("Alliance {AllianceId} claimed sovereignty over {SystemId}",
            allianceId, state.State.SolarSystemId);
        return true;
    }

    public async Task<Guid> ContestAsync(Guid attackingAllianceId)
    {
        var contest = new SovereigntyContest
        {
            ContestId = Guid.NewGuid(),
            AttackingAllianceId = attackingAllianceId,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddHours(2)
        };

        state.State.ActiveContests.Add(contest);
        state.State.Status = "Contested";
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new SovereigntyContestedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            state.State.SolarSystemId, attackingAllianceId, contest.ContestId));

        logger.LogInformation("Alliance {AllianceId} contesting {SystemId}, contest {ContestId}",
            attackingAllianceId, state.State.SolarSystemId, contest.ContestId);

        // Register timer for contest resolution
        RegisterTimer(
            _ => AutoResolveContestAsync(contest.ContestId),
            null,
            TimeSpan.FromHours(2),
            TimeSpan.FromMilliseconds(-1));

        return contest.ContestId;
    }

    private async Task AutoResolveContestAsync(Guid contestId)
    {
        var contest = state.State.ActiveContests.Find(c => c.ContestId == contestId);
        if (contest is null || contest.Status != "InProgress")
            return;

        // Default: defender wins if not explicitly resolved
        if (state.State.OwnerAllianceId.HasValue)
            await ResolveContestAsync(contestId, state.State.OwnerAllianceId.Value);
    }

    public async Task<bool> ResolveContestAsync(Guid contestId, Guid winnerId)
    {
        var contest = state.State.ActiveContests.Find(c => c.ContestId == contestId);
        if (contest is null)
            return false;

        contest.Status = "Resolved";
        state.State.ActiveContests.Remove(contest);

        // If attacker wins, transfer sovereignty
        if (winnerId == contest.AttackingAllianceId)
        {
            var oldOwner = state.State.OwnerAllianceId;
            state.State.OwnerAllianceId = winnerId;
            state.State.ClaimedAt = DateTime.UtcNow;

            await eventBus.PublishAsync(new SovereigntyChangedEvent(
                Guid.NewGuid(), DateTime.UtcNow,
                state.State.SolarSystemId, oldOwner, winnerId));
        }

        state.State.Status = state.State.ActiveContests.Count > 0 ? "Contested" : "Claimed";
        await state.WriteStateAsync();

        logger.LogInformation("Contest {ContestId} resolved in {SystemId}, winner: {WinnerId}",
            contestId, state.State.SolarSystemId, winnerId);
        return true;
    }

    public async Task<Guid> PlaceStructureAsync(Guid allianceId, string structureType)
    {
        var structure = new StructureState
        {
            StructureId = Guid.NewGuid(),
            StructureType = structureType,
            OwnerAllianceId = allianceId,
            HealthPercent = 100.0,
            Status = "Online",
            BuiltAt = DateTime.UtcNow
        };

        state.State.Structures.Add(structure);
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new StructurePlacedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            state.State.SolarSystemId, structure.StructureId, structureType, allianceId));

        logger.LogInformation("Alliance {AllianceId} placed {StructureType} in {SystemId}: {StructureId}",
            allianceId, structureType, state.State.SolarSystemId, structure.StructureId);
        return structure.StructureId;
    }

    public async Task<bool> DestroyStructureAsync(Guid structureId)
    {
        var structure = state.State.Structures.Find(s => s.StructureId == structureId);
        if (structure is null)
            return false;

        state.State.Structures.Remove(structure);
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new StructureDestroyedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            state.State.SolarSystemId, structureId, structure.StructureType));

        logger.LogInformation("Structure {StructureId} destroyed in {SystemId}",
            structureId, state.State.SolarSystemId);
        return true;
    }

    public async Task<bool> SetTaxRateAsync(Guid allianceId, decimal taxRate)
    {
        if (state.State.OwnerAllianceId != allianceId)
            return false;

        state.State.TaxRate = Math.Clamp(taxRate, 0m, 0.5m);
        await state.WriteStateAsync();

        logger.LogInformation("Tax rate set to {TaxRate} in {SystemId} by alliance {AllianceId}",
            taxRate, state.State.SolarSystemId, allianceId);
        return true;
    }
}
