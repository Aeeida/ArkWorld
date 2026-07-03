using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class ScriptManagerGrain(
    [PersistentState("scriptManager", "GameStore")] IPersistentState<ScriptManagerState> state,
    IEventBus eventBus,
    ILogger<ScriptManagerGrain> logger) : Grain, IScriptManagerGrain
{
    public Task<ScriptManagerState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<bool> RegisterScriptAsync(ScriptDefinition definition)
    {
        if (state.State.Scripts.ContainsKey(definition.ScriptId))
            return false;

        definition.CreatedAt = DateTime.UtcNow;
        definition.UpdatedAt = DateTime.UtcNow;
        definition.Version = 1;

        state.State.Scripts[definition.ScriptId] = definition;
        state.State.VersionHistory[definition.ScriptId] = [Clone(definition)];
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ScriptRegisteredEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            definition.ScriptId, definition.Version, definition.Author));

        logger.LogInformation("Script {ScriptId} v{Version} registered by {Author}",
            definition.ScriptId, definition.Version, definition.Author);
        return true;
    }

    public async Task<bool> UpdateScriptAsync(ScriptDefinition definition)
    {
        if (!state.State.Scripts.TryGetValue(definition.ScriptId, out var existing))
            return false;

        definition.Version = existing.Version + 1;
        definition.CreatedAt = existing.CreatedAt;
        definition.UpdatedAt = DateTime.UtcNow;

        state.State.Scripts[definition.ScriptId] = definition;

        if (!state.State.VersionHistory.TryGetValue(definition.ScriptId, out var history))
        {
            history = [];
            state.State.VersionHistory[definition.ScriptId] = history;
        }
        history.Add(Clone(definition));

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ScriptUpdatedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            definition.ScriptId, definition.Version, definition.Author));

        logger.LogInformation("Script {ScriptId} updated to v{Version} by {Author}",
            definition.ScriptId, definition.Version, definition.Author);
        return true;
    }

    public Task<ScriptDefinition?> GetScriptAsync(string scriptId)
    {
        state.State.Scripts.TryGetValue(scriptId, out var definition);
        return Task.FromResult(definition);
    }

    public Task<IReadOnlyList<ScriptDefinition>> GetAllScriptsAsync() =>
        Task.FromResult<IReadOnlyList<ScriptDefinition>>(state.State.Scripts.Values.ToList());

    public async Task<Guid> ScheduleActivityAsync(string scriptId, DateTime startsAt, DateTime endsAt, string? targetZone)
    {
        if (!state.State.Scripts.ContainsKey(scriptId))
            return Guid.Empty;

        var activity = new ScheduledActivity
        {
            ActivityId = Guid.NewGuid(),
            ScriptId = scriptId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            TargetZone = targetZone,
            Status = startsAt <= DateTime.UtcNow ? "Active" : "Scheduled"
        };

        state.State.ScheduledActivities.Add(activity);
        await state.WriteStateAsync();

        // Schedule activation timer
        if (startsAt > DateTime.UtcNow)
        {
            RegisterTimer(
                _ => ActivateActivityAsync(activity.ActivityId),
                null,
                startsAt - DateTime.UtcNow,
                TimeSpan.FromMilliseconds(-1));
        }

        // Schedule expiration timer
        RegisterTimer(
            _ => ExpireActivityAsync(activity.ActivityId),
            null,
            endsAt - DateTime.UtcNow,
            TimeSpan.FromMilliseconds(-1));

        await eventBus.PublishAsync(new ActivityScheduledEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            activity.ActivityId, scriptId, startsAt, endsAt, targetZone));

        logger.LogInformation("Activity {ActivityId} scheduled for script {ScriptId}: {Start} - {End}",
            activity.ActivityId, scriptId, startsAt, endsAt);
        return activity.ActivityId;
    }

    private async Task ActivateActivityAsync(Guid activityId)
    {
        var activity = state.State.ScheduledActivities.Find(a => a.ActivityId == activityId);
        if (activity is null || activity.Status != "Scheduled")
            return;

        activity.Status = "Active";
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ActivityStartedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            activityId, activity.ScriptId));

        logger.LogInformation("Activity {ActivityId} activated for script {ScriptId}",
            activityId, activity.ScriptId);
    }

    private async Task ExpireActivityAsync(Guid activityId)
    {
        var activity = state.State.ScheduledActivities.Find(a => a.ActivityId == activityId);
        if (activity is null || activity.Status is "Completed" or "Cancelled")
            return;

        activity.Status = "Completed";
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ActivityEndedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            activityId, activity.ScriptId));

        logger.LogInformation("Activity {ActivityId} expired for script {ScriptId}",
            activityId, activity.ScriptId);
    }

    public async Task<bool> CancelActivityAsync(Guid activityId)
    {
        var activity = state.State.ScheduledActivities.Find(a => a.ActivityId == activityId);
        if (activity is null)
            return false;

        activity.Status = "Cancelled";
        await state.WriteStateAsync();

        logger.LogInformation("Activity {ActivityId} cancelled", activityId);
        return true;
    }

    public Task<IReadOnlyList<ScheduledActivity>> GetActiveActivitiesAsync() =>
        Task.FromResult<IReadOnlyList<ScheduledActivity>>(
            state.State.ScheduledActivities
                .Where(a => a.Status is "Scheduled" or "Active")
                .ToList());

    public async Task<bool> RollbackScriptAsync(string scriptId, int targetVersion)
    {
        if (!state.State.VersionHistory.TryGetValue(scriptId, out var history))
            return false;

        var target = history.Find(h => h.Version == targetVersion);
        if (target is null)
            return false;

        state.State.Scripts[scriptId] = Clone(target);
        state.State.Scripts[scriptId].UpdatedAt = DateTime.UtcNow;
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ScriptRolledBackEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            scriptId, targetVersion));

        logger.LogInformation("Script {ScriptId} rolled back to v{Version}",
            scriptId, targetVersion);
        return true;
    }

    private static ScriptDefinition Clone(ScriptDefinition src) => new()
    {
        ScriptId = src.ScriptId,
        Version = src.Version,
        Name = src.Name,
        Description = src.Description,
        Category = src.Category,
        Nodes = src.Nodes.ToList(),
        EntryNodeId = src.EntryNodeId,
        CreatedAt = src.CreatedAt,
        UpdatedAt = src.UpdatedAt,
        Author = src.Author,
        IsActive = src.IsActive,
        Tags = src.Tags.ToList()
    };
}
