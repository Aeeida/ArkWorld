using Game.Shared.Core.DTOs;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Skills;

// ── Commands ──────────────────────────────────────────────────────────

public sealed record TrainSkillCommand(Guid PlayerId, string SkillId) : ICommand<SkillDto>;

public sealed record CancelSkillTrainingCommand(Guid PlayerId) : ICommand<bool>;

public sealed record GetSkillQueueQuery(Guid PlayerId) : IQuery<IReadOnlyList<SkillDto>>, ICacheableQuery
{
    public string CacheKey => $"skills:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);
}

public sealed record GetSkillTreeQuery(string Faction) : IQuery<IReadOnlyList<SkillTreeNodeDto>>, ICacheableQuery
{
    public string CacheKey => $"skilltree:{Faction}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public sealed record AllocateTalentPointCommand(Guid PlayerId, string TalentId) : ICommand<bool>;

public sealed record ChangeReputationCommand(Guid PlayerId, string FactionId, int Amount) : ICommand<bool>;

public sealed record GetFullSkillTreeQuery(Guid PlayerId) : IQuery<SkillTreeDto>, ICacheableQuery
{
    public string CacheKey => $"skilltree:full:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public sealed record StartSkillTrainingCommand(Guid PlayerId, string SkillId) : ICommand<TrainSkillResultDto>;

public sealed record CancelTrainingCommand(Guid PlayerId) : ICommand<CancelTrainingResultDto>;

public sealed record LevelUpCommand(Guid PlayerId) : ICommand<LevelUpResultDto>;

public sealed record GetAttributesQuery(Guid PlayerId) : IQuery<AttributeSetDto>, ICacheableQuery
{
    public string CacheKey => $"attributes:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);
}

// ── Handlers ──────────────────────────────────────────────────────────

public sealed class TrainSkillHandler(
    IGrainFactory grainFactory,
    ILogger<TrainSkillHandler> logger)
    : ICommandHandler<TrainSkillCommand, SkillDto>
{
    public async Task<SkillDto> Handle(TrainSkillCommand request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var started = await skillGrain.StartTrainingAsync(request.SkillId);

        if (!started)
        {
            logger.LogWarning("Player {PlayerId} failed to start training {SkillId} (already training or invalid)",
                request.PlayerId, request.SkillId);
        }

        var grainState = await skillGrain.GetStateAsync();
        grainState.LearnedSkills.TryGetValue(request.SkillId, out var currentLevel);

        return new SkillDto(
            request.SkillId,
            request.SkillId,
            currentLevel,
            currentLevel + 1,
            grainState.TrainingCompletesAt);
    }
}

public sealed class CancelSkillTrainingHandler(
    IGrainFactory grainFactory,
    ILogger<CancelSkillTrainingHandler> logger)
    : ICommandHandler<CancelSkillTrainingCommand, bool>
{
    public async Task<bool> Handle(CancelSkillTrainingCommand request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var result = await skillGrain.CancelTrainingAsync();

        logger.LogInformation("Player {PlayerId} cancel training result: {Result}",
            request.PlayerId, result);
        return result;
    }
}

public sealed class GetSkillQueueHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetSkillQueueQuery, IReadOnlyList<SkillDto>>
{
    public async Task<IReadOnlyList<SkillDto>> Handle(GetSkillQueueQuery request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var grainState = await skillGrain.GetStateAsync();

        return grainState.TrainingQueue
            .Select(e =>
            {
                grainState.LearnedSkills.TryGetValue(e.SkillId, out var currentLevel);
                return new SkillDto(e.SkillId, e.SkillId, currentLevel, e.TargetLevel, grainState.TrainingCompletesAt);
            })
            .ToList();
    }
}

public sealed class GetSkillTreeHandler
    : IQueryHandler<GetSkillTreeQuery, IReadOnlyList<SkillTreeNodeDto>>
{
    public Task<IReadOnlyList<SkillTreeNodeDto>> Handle(GetSkillTreeQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SkillTreeNodeDto>>([]);
}

public sealed class AllocateTalentPointHandler(
    IGrainFactory grainFactory,
    ILogger<AllocateTalentPointHandler> logger)
    : ICommandHandler<AllocateTalentPointCommand, bool>
{
    public async Task<bool> Handle(AllocateTalentPointCommand request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var result = await skillGrain.AllocateTalentPointAsync(request.TalentId);

        logger.LogInformation("Player {PlayerId} talent allocation {TalentId}: {Result}",
            request.PlayerId, request.TalentId, result);
        return result;
    }
}

public sealed class ChangeReputationHandler(
    IGrainFactory grainFactory,
    ILogger<ChangeReputationHandler> logger)
    : ICommandHandler<ChangeReputationCommand, bool>
{
    public async Task<bool> Handle(ChangeReputationCommand request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        await skillGrain.ChangeReputationAsync(request.FactionId, request.Amount);

        logger.LogInformation("Player {PlayerId} reputation with {FactionId} changed by {Amount}",
            request.PlayerId, request.FactionId, request.Amount);
        return true;
    }
}

public sealed class GetFullSkillTreeHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetFullSkillTreeQuery, SkillTreeDto>
{
    public async Task<SkillTreeDto> Handle(GetFullSkillTreeQuery request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var grainState = await skillGrain.GetStateAsync();

        var learned = grainState.LearnedSkills
            .Select(kv => new SkillDto(kv.Key, kv.Key, kv.Value, kv.Value, null))
            .ToList();

        return new SkillTreeDto([], learned);
    }
}

public sealed class StartSkillTrainingHandler(
    IGrainFactory grainFactory,
    ILogger<StartSkillTrainingHandler> logger)
    : ICommandHandler<StartSkillTrainingCommand, TrainSkillResultDto>
{
    public async Task<TrainSkillResultDto> Handle(StartSkillTrainingCommand request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var started = await skillGrain.StartTrainingAsync(request.SkillId);
        var grainState = await skillGrain.GetStateAsync();

        return new TrainSkillResultDto(
            started,
            request.SkillId,
            grainState.TrainingCompletesAt,
            started ? null : "Failed to start training");
    }
}

public sealed class CancelTrainingHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<CancelTrainingCommand, CancelTrainingResultDto>
{
    public async Task<CancelTrainingResultDto> Handle(CancelTrainingCommand request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var grainState = await skillGrain.GetStateAsync();
        var cancelledSkill = grainState.CurrentlyTrainingSkillId;
        var result = await skillGrain.CancelTrainingAsync();

        return new CancelTrainingResultDto(result, cancelledSkill, result ? null : "No active training");
    }
}

public sealed class LevelUpHandler(
    IGrainFactory grainFactory,
    ILogger<LevelUpHandler> logger)
    : ICommandHandler<LevelUpCommand, LevelUpResultDto>
{
    public async Task<LevelUpResultDto> Handle(LevelUpCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var oldState = await playerGrain.GetStateAsync();
        var oldLevel = oldState.Level;

        var leveled = await playerGrain.TryLevelUpAsync();
        if (!leveled)
            return new LevelUpResultDto(false, oldLevel, 0, "Not enough experience to level up");

        var newState = await playerGrain.GetStateAsync();
        logger.LogInformation("Player {PlayerId} leveled up from {Old} to {New}",
            request.PlayerId, oldLevel, newState.Level);

        return new LevelUpResultDto(true, newState.Level, 5, null);
    }
}

public sealed class GetAttributesHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetAttributesQuery, AttributeSetDto>
{
    public async Task<AttributeSetDto> Handle(GetAttributesQuery request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var state = await playerGrain.GetStateAsync();

        return new AttributeSetDto(
            request.PlayerId,
            state.Level * 5,   // Strength (placeholder)
            state.Level * 3,   // Agility
            state.Level * 4,   // Intelligence
            state.Level * 6,   // Stamina
            state.Level * 2,   // Luck
            0);                // UnspentPoints
    }
}
