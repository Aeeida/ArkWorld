using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class QuestGrain(
    [PersistentState("quest", "GameStore")] IPersistentState<QuestGrainState> state,
    IEventBus eventBus,
    ILogger<QuestGrain> logger) : Grain, IQuestGrain
{
    public Task<QuestGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<bool> AcceptQuestAsync(string questId)
    {
        if (state.State.ActiveQuests.ContainsKey(questId))
            return false;

        if (state.State.CompletedQuestIds.Contains(questId))
            return false; // already completed (non-repeatable)

        state.State.ActiveQuests[questId] = new ActiveQuestState
        {
            QuestId = questId,
            Status = "InProgress",
            AcceptedAt = DateTime.UtcNow
        };

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new QuestAcceptedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), questId));

        logger.LogInformation("Player {PlayerId} accepted quest {QuestId}",
            this.GetPrimaryKey(), questId);
        return true;
    }

    public async Task<bool> CompleteQuestAsync(string questId)
    {
        if (!state.State.ActiveQuests.TryGetValue(questId, out var quest))
            return false;

        state.State.ActiveQuests.Remove(questId);
        state.State.CompletedQuestIds.Add(questId);

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new QuestCompletedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), questId));

        logger.LogInformation("Player {PlayerId} completed quest {QuestId}",
            this.GetPrimaryKey(), questId);
        return true;
    }

    public async Task<bool> AbandonQuestAsync(string questId)
    {
        if (!state.State.ActiveQuests.Remove(questId))
            return false;

        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} abandoned quest {QuestId}",
            this.GetPrimaryKey(), questId);
        return true;
    }

    public async Task<bool> UpdateProgressAsync(string questId, string objectiveId, int amount)
    {
        if (!state.State.ActiveQuests.TryGetValue(questId, out var quest))
            return false;

        quest.ObjectiveProgress.TryGetValue(objectiveId, out var current);
        quest.ObjectiveProgress[objectiveId] = current + amount;

        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} quest {QuestId} objective {Objective} progress +{Amount}",
            this.GetPrimaryKey(), questId, objectiveId, amount);
        return true;
    }

    public async Task<bool> ChooseBranchAsync(string questId, int branchIndex)
    {
        if (!state.State.ActiveQuests.TryGetValue(questId, out var quest))
            return false;

        if (quest.ChosenBranch is not null)
            return false; // already chosen

        quest.ChosenBranch = branchIndex;
        await state.WriteStateAsync();

        logger.LogInformation("Player {PlayerId} chose branch {Branch} for quest {QuestId}",
            this.GetPrimaryKey(), branchIndex, questId);
        return true;
    }

    public async Task ResetDailyQuestsAsync()
    {
        state.State.DailyCompletions.Clear();
        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} daily quests reset", this.GetPrimaryKey());
    }

    public async Task ResetWeeklyQuestsAsync()
    {
        state.State.WeeklyCompletions.Clear();
        await state.WriteStateAsync();
        logger.LogInformation("Player {PlayerId} weekly quests reset", this.GetPrimaryKey());
    }
}
