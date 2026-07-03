using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class SkillGrain(
    [PersistentState("skill", "GameStore")] IPersistentState<SkillGrainState> state,
    IEventBus eventBus,
    ILogger<SkillGrain> logger) : Grain, ISkillGrain
{
    public Task<SkillGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<bool> LearnSkillAsync(string skillId)
    {
        if (state.State.LearnedSkills.ContainsKey(skillId))
            return false;

        state.State.LearnedSkills[skillId] = 0;
        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} learned skill {SkillId}", this.GetPrimaryKey(), skillId);
        return true;
    }

    public async Task<bool> StartTrainingAsync(string skillId)
    {
        if (state.State.CurrentlyTrainingSkillId is not null)
            return false;

        if (!state.State.LearnedSkills.TryGetValue(skillId, out var currentLevel))
        {
            state.State.LearnedSkills[skillId] = 0;
            currentLevel = 0;
        }

        var targetLevel = currentLevel + 1;
        var trainingDurationSec = targetLevel * 3600; // 1h per level (EVE-style scaling)

        state.State.CurrentlyTrainingSkillId = skillId;
        state.State.TrainingCompletesAt = DateTime.UtcNow.AddSeconds(trainingDurationSec);
        state.State.TrainingQueue.Add(new SkillQueueEntry
        {
            SkillId = skillId,
            TargetLevel = targetLevel,
            EnqueuedAt = DateTime.UtcNow
        });

        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} started training {SkillId} to level {Level}",
            this.GetPrimaryKey(), skillId, targetLevel);

        // Register a timer for completion check
        RegisterTimer(
            _ => CheckTrainingCompletionAsync(),
            null,
            TimeSpan.FromSeconds(trainingDurationSec),
            TimeSpan.FromMilliseconds(-1));

        return true;
    }

    private async Task CheckTrainingCompletionAsync()
    {
        if (state.State.CurrentlyTrainingSkillId is null || state.State.TrainingCompletesAt > DateTime.UtcNow)
            return;

        await CompleteTrainingAsync();
    }

    public async Task<bool> CancelTrainingAsync()
    {
        if (state.State.CurrentlyTrainingSkillId is null)
            return false;

        var cancelledSkill = state.State.CurrentlyTrainingSkillId;
        state.State.CurrentlyTrainingSkillId = null;
        state.State.TrainingCompletesAt = null;
        state.State.TrainingQueue.RemoveAll(e => e.SkillId == cancelledSkill);

        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} cancelled training {SkillId}",
            this.GetPrimaryKey(), cancelledSkill);
        return true;
    }

    public async Task<bool> CompleteTrainingAsync()
    {
        if (state.State.CurrentlyTrainingSkillId is null)
            return false;

        var skillId = state.State.CurrentlyTrainingSkillId;
        state.State.LearnedSkills.TryGetValue(skillId, out var currentLevel);
        var newLevel = currentLevel + 1;

        state.State.LearnedSkills[skillId] = newLevel;
        state.State.TrainingQueue.RemoveAll(e => e.SkillId == skillId);
        state.State.CurrentlyTrainingSkillId = null;
        state.State.TrainingCompletesAt = null;
        state.State.UnspentTalentPoints++;

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new SkillTrainingCompletedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), skillId, newLevel));

        logger.LogInformation("Player {PlayerId} completed training {SkillId} to level {Level}",
            this.GetPrimaryKey(), skillId, newLevel);
        return true;
    }

    public async Task<bool> AllocateTalentPointAsync(string talentId)
    {
        if (state.State.UnspentTalentPoints <= 0)
            return false;

        state.State.AllocatedTalents[talentId] = true;
        state.State.UnspentTalentPoints--;
        await state.WriteStateAsync();

        logger.LogInformation("Player {PlayerId} allocated talent point to {TalentId}",
            this.GetPrimaryKey(), talentId);
        return true;
    }

    public async Task<bool> ResetTalentsAsync()
    {
        state.State.UnspentTalentPoints += state.State.AllocatedTalents.Count;
        state.State.AllocatedTalents.Clear();
        await state.WriteStateAsync();

        logger.LogInformation("Player {PlayerId} reset all talent points", this.GetPrimaryKey());
        return true;
    }

    public async Task ChangeReputationAsync(string factionId, int amount)
    {
        state.State.FactionReputation.TryGetValue(factionId, out var current);
        state.State.FactionReputation[factionId] = current + amount;
        await state.WriteStateAsync();

        logger.LogInformation("Player {PlayerId} reputation with {Faction} changed by {Amount} to {Total}",
            this.GetPrimaryKey(), factionId, amount, state.State.FactionReputation[factionId]);
    }
}
