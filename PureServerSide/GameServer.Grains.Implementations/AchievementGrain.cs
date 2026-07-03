using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class AchievementGrain(
    [PersistentState("achievement", "GameStore")] IPersistentState<AchievementGrainState> state,
    IEventBus eventBus,
    ILogger<AchievementGrain> logger) : Grain, IAchievementGrain
{
    public Task<AchievementGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<bool> UnlockAsync(string achievementId, int points)
    {
        if (state.State.Achievements.ContainsKey(achievementId))
            return false; // already unlocked

        state.State.Achievements[achievementId] = new UnlockedAchievement
        {
            AchievementId = achievementId,
            Name = achievementId, // name lookup from config in production
            UnlockedAt = DateTime.UtcNow,
            Points = points
        };
        state.State.TotalPoints += points;

        // Remove from in-progress if it was being tracked
        state.State.InProgress.Remove(achievementId);

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new AchievementUnlockedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), achievementId, points));

        logger.LogInformation("Player {PlayerId} unlocked achievement {AchievementId} ({Points} pts)",
            this.GetPrimaryKey(), achievementId, points);
        return true;
    }

    public async Task<bool> SetActiveTitleAsync(string titleId)
    {
        if (!state.State.Titles.ContainsKey(titleId))
            return false;

        state.State.ActiveTitleId = titleId;
        await state.WriteStateAsync();

        logger.LogInformation("Player {PlayerId} set active title to {TitleId}",
            this.GetPrimaryKey(), titleId);
        return true;
    }

    public async Task<bool> UnlockTitleAsync(string titleId, string name)
    {
        if (state.State.Titles.ContainsKey(titleId))
            return false;

        state.State.Titles[titleId] = new TitleState
        {
            TitleId = titleId,
            Name = name,
            UnlockedAt = DateTime.UtcNow
        };

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new TitleUnlockedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), titleId, name));

        logger.LogInformation("Player {PlayerId} unlocked title {TitleId}",
            this.GetPrimaryKey(), titleId);
        return true;
    }

    public async Task<bool> UnlockAppearanceAsync(string appearanceId, string category)
    {
        if (state.State.Appearances.ContainsKey(appearanceId))
            return false;

        state.State.Appearances[appearanceId] = new AppearanceState
        {
            AppearanceId = appearanceId,
            Category = category,
            UnlockedAt = DateTime.UtcNow
        };

        await state.WriteStateAsync();

        logger.LogInformation("Player {PlayerId} unlocked appearance {AppearanceId} in {Category}",
            this.GetPrimaryKey(), appearanceId, category);
        return true;
    }

    public async Task IncrementProgressAsync(string achievementId, int amount)
    {
        // Already unlocked, ignore
        if (state.State.Achievements.ContainsKey(achievementId))
            return;

        if (!state.State.InProgress.TryGetValue(achievementId, out var progress))
        {
            progress = new AchievementProgress
            {
                AchievementId = achievementId,
                Current = 0,
                Required = 100 // default; would come from config
            };
            state.State.InProgress[achievementId] = progress;
        }

        progress.Current += amount;

        if (progress.Current >= progress.Required)
        {
            // Auto-unlock when complete
            await UnlockAsync(achievementId, 10);
            return;
        }

        await state.WriteStateAsync();
    }
}
