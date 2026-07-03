using Game.Shared.Core.DTOs;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Quests;

// ── Commands ──────────────────────────────────────────────────────────

public sealed record AcceptQuestCommand(Guid PlayerId, string QuestId) : ICommand<QuestDto>;

public sealed record CompleteQuestCommand(Guid PlayerId, string QuestId) : ICommand<QuestRewardDto>;

public sealed record AbandonQuestCommand(Guid PlayerId, string QuestId) : ICommand<bool>;

public sealed record ChooseQuestBranchCommand(Guid PlayerId, string QuestId, int BranchIndex) : ICommand<QuestDto>;

public sealed record GetActiveQuestsQuery(Guid PlayerId) : IQuery<IReadOnlyList<QuestDto>>, ICacheableQuery
{
    public string CacheKey => $"quests:active:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(15);
}

public sealed record GetAvailableQuestsQuery(Guid PlayerId, string ZoneId) : IQuery<IReadOnlyList<QuestDto>>;

public sealed record AcceptQuestWrappedCommand(Guid PlayerId, string QuestId) : ICommand<AcceptQuestResultDto>;

public sealed record SubmitQuestCommand(Guid PlayerId, string QuestId) : ICommand<SubmitQuestResultDto>;

public sealed record GetQuestProgressQuery(Guid PlayerId) : IQuery<QuestProgressDto>, ICacheableQuery
{
    public string CacheKey => $"quests:progress:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(15);
}

// ── Handlers ──────────────────────────────────────────────────────────

public sealed class AcceptQuestHandler(
    IGrainFactory grainFactory,
    ILogger<AcceptQuestHandler> logger)
    : ICommandHandler<AcceptQuestCommand, QuestDto>
{
    public async Task<QuestDto> Handle(AcceptQuestCommand request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        var accepted = await questGrain.AcceptQuestAsync(request.QuestId);

        if (!accepted)
        {
            logger.LogWarning("Player {PlayerId} failed to accept quest {QuestId}",
                request.PlayerId, request.QuestId);
            return new QuestDto(request.QuestId, request.QuestId, "Failed", [], null);
        }

        var grainState = await questGrain.GetStateAsync();
        var quest = grainState.ActiveQuests[request.QuestId];

        return new QuestDto(
            quest.QuestId,
            quest.QuestId,
            quest.Status,
            quest.ObjectiveProgress
                .Select(kv => new QuestObjectiveDto(kv.Key, kv.Key, kv.Value, 1))
                .ToList(),
            quest.ChosenBranch);
    }
}

public sealed class CompleteQuestHandler(
    IGrainFactory grainFactory,
    ILogger<CompleteQuestHandler> logger)
    : ICommandHandler<CompleteQuestCommand, QuestRewardDto>
{
    public async Task<QuestRewardDto> Handle(CompleteQuestCommand request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        var completed = await questGrain.CompleteQuestAsync(request.QuestId);

        if (!completed)
        {
            logger.LogWarning("Player {PlayerId} failed to complete quest {QuestId}",
                request.PlayerId, request.QuestId);
            return new QuestRewardDto(request.QuestId, 0, 0, []);
        }

        // Grant rewards via PlayerGrain
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        await playerGrain.GainExperienceAsync(500);

        // Trigger achievement progress
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        await achievementGrain.IncrementProgressAsync("quest_master", 1);

        logger.LogInformation("Player {PlayerId} completed quest {QuestId}",
            request.PlayerId, request.QuestId);

        return new QuestRewardDto(request.QuestId, 500, 100m, []);
    }
}

public sealed class AbandonQuestHandler(
    IGrainFactory grainFactory,
    ILogger<AbandonQuestHandler> logger)
    : ICommandHandler<AbandonQuestCommand, bool>
{
    public async Task<bool> Handle(AbandonQuestCommand request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        var result = await questGrain.AbandonQuestAsync(request.QuestId);

        logger.LogInformation("Player {PlayerId} abandon quest {QuestId}: {Result}",
            request.PlayerId, request.QuestId, result);
        return result;
    }
}

public sealed class ChooseQuestBranchHandler(
    IGrainFactory grainFactory,
    ILogger<ChooseQuestBranchHandler> logger)
    : ICommandHandler<ChooseQuestBranchCommand, QuestDto>
{
    public async Task<QuestDto> Handle(ChooseQuestBranchCommand request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        await questGrain.ChooseBranchAsync(request.QuestId, request.BranchIndex);

        var grainState = await questGrain.GetStateAsync();
        if (!grainState.ActiveQuests.TryGetValue(request.QuestId, out var quest))
            return new QuestDto(request.QuestId, request.QuestId, "NotFound", [], null);

        logger.LogInformation("Player {PlayerId} chose branch {Branch} for quest {QuestId}",
            request.PlayerId, request.BranchIndex, request.QuestId);

        return new QuestDto(
            quest.QuestId, quest.QuestId, quest.Status,
            quest.ObjectiveProgress
                .Select(kv => new QuestObjectiveDto(kv.Key, kv.Key, kv.Value, 1))
                .ToList(),
            quest.ChosenBranch);
    }
}

public sealed class GetActiveQuestsHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetActiveQuestsQuery, IReadOnlyList<QuestDto>>
{
    public async Task<IReadOnlyList<QuestDto>> Handle(GetActiveQuestsQuery request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        var grainState = await questGrain.GetStateAsync();

        return grainState.ActiveQuests.Values
            .Select(q => new QuestDto(
                q.QuestId, q.QuestId, q.Status,
                q.ObjectiveProgress
                    .Select(kv => new QuestObjectiveDto(kv.Key, kv.Key, kv.Value, 1))
                    .ToList(),
                q.ChosenBranch))
            .ToList();
    }
}

public sealed class GetAvailableQuestsHandler
    : IQueryHandler<GetAvailableQuestsQuery, IReadOnlyList<QuestDto>>
{
    public Task<IReadOnlyList<QuestDto>> Handle(GetAvailableQuestsQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<QuestDto>>([]);
}

public sealed class AcceptQuestWrappedHandler(
    IGrainFactory grainFactory,
    ILogger<AcceptQuestWrappedHandler> logger)
    : ICommandHandler<AcceptQuestWrappedCommand, AcceptQuestResultDto>
{
    public async Task<AcceptQuestResultDto> Handle(AcceptQuestWrappedCommand request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        var accepted = await questGrain.AcceptQuestAsync(request.QuestId);

        logger.LogInformation("Player {PlayerId} accept quest {QuestId}: {Result}",
            request.PlayerId, request.QuestId, accepted);

        return new AcceptQuestResultDto(accepted, request.QuestId,
            accepted ? null : "Failed to accept quest");
    }
}

public sealed class SubmitQuestHandler(
    IGrainFactory grainFactory,
    ILogger<SubmitQuestHandler> logger)
    : ICommandHandler<SubmitQuestCommand, SubmitQuestResultDto>
{
    public async Task<SubmitQuestResultDto> Handle(SubmitQuestCommand request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        var completed = await questGrain.CompleteQuestAsync(request.QuestId);

        if (!completed)
            return new SubmitQuestResultDto(false, request.QuestId, null, "Quest not completable");

        var rewards = new QuestRewardDto(request.QuestId, 500, 100m, []);

        // Grant rewards
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        await playerGrain.GainExperienceAsync(rewards.ExperienceReward);

        logger.LogInformation("Player {PlayerId} submitted quest {QuestId}",
            request.PlayerId, request.QuestId);

        return new SubmitQuestResultDto(true, request.QuestId, rewards, null);
    }
}

public sealed class GetQuestProgressHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetQuestProgressQuery, QuestProgressDto>
{
    public async Task<QuestProgressDto> Handle(GetQuestProgressQuery request, CancellationToken ct)
    {
        var questGrain = grainFactory.GetGrain<IQuestGrain>(request.PlayerId);
        var grainState = await questGrain.GetStateAsync();

        var activeQuests = grainState.ActiveQuests.Values
            .Select(q => new QuestDto(
                q.QuestId, q.QuestId, q.Status,
                q.ObjectiveProgress
                    .Select(kv => new QuestObjectiveDto(kv.Key, kv.Key, kv.Value, 1))
                    .ToList(),
                q.ChosenBranch))
            .ToList();

        return new QuestProgressDto(
            request.PlayerId,
            activeQuests,
            grainState.CompletedQuestIds.Count,
            0);
    }
}
