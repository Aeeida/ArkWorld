using Game.Shared.Core.DTOs;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Achievements;

// ── Commands ──────────────────────────────────────────────────────────

public sealed record UnlockAchievementCommand(
    Guid PlayerId, string AchievementId) : ICommand<AchievementDto>;

public sealed record GetAchievementsQuery(Guid PlayerId)
    : IQuery<IReadOnlyList<AchievementDto>>, ICacheableQuery
{
    public string CacheKey => $"achievements:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public sealed record GetTitlesQuery(Guid PlayerId) : IQuery<IReadOnlyList<TitleDto>>;

public sealed record SetActiveTitleCommand(Guid PlayerId, string TitleId) : ICommand<bool>;

public sealed record UnlockAppearanceCommand(
    Guid PlayerId, string AppearanceId, string Category) : ICommand<bool>;

public sealed record GetCollectionProgressQuery(Guid PlayerId)
    : IQuery<CollectionProgressDto>, ICacheableQuery
{
    public string CacheKey => $"collections:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(2);
}

public sealed record GetAchievementProgressQuery(Guid PlayerId)
    : IQuery<AchievementProgressDto>, ICacheableQuery
{
    public string CacheKey => $"achievements:progress:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(2);
}

public sealed record UnlockCosmeticCommand(Guid PlayerId, string CosmeticId) : ICommand<UnlockCosmeticResultDto>;

// ── Handlers ──────────────────────────────────────────────────────────

public sealed class UnlockAchievementHandler(
    IGrainFactory grainFactory,
    ILogger<UnlockAchievementHandler> logger)
    : ICommandHandler<UnlockAchievementCommand, AchievementDto>
{
    public async Task<AchievementDto> Handle(UnlockAchievementCommand request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var unlocked = await achievementGrain.UnlockAsync(request.AchievementId, 10);

        var grainState = await achievementGrain.GetStateAsync();

        if (unlocked && grainState.Achievements.TryGetValue(request.AchievementId, out var achievement))
        {
            logger.LogInformation("Player {PlayerId} unlocked achievement {AchievementId}",
                request.PlayerId, request.AchievementId);
            return new AchievementDto(achievement.AchievementId, achievement.Name, "Unlocked", achievement.UnlockedAt, achievement.Points);
        }

        return new AchievementDto(request.AchievementId, request.AchievementId, "AlreadyUnlocked", null, 0);
    }
}

public sealed class GetAchievementsHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetAchievementsQuery, IReadOnlyList<AchievementDto>>
{
    public async Task<IReadOnlyList<AchievementDto>> Handle(
        GetAchievementsQuery request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var grainState = await achievementGrain.GetStateAsync();

        return grainState.Achievements.Values
            .Select(a => new AchievementDto(a.AchievementId, a.Name, "Unlocked", a.UnlockedAt, a.Points))
            .ToList();
    }
}

public sealed class GetTitlesHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetTitlesQuery, IReadOnlyList<TitleDto>>
{
    public async Task<IReadOnlyList<TitleDto>> Handle(GetTitlesQuery request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var grainState = await achievementGrain.GetStateAsync();

        return grainState.Titles.Values
            .Select(t => new TitleDto(t.TitleId, t.Name, t.TitleId == grainState.ActiveTitleId))
            .ToList();
    }
}

public sealed class SetActiveTitleHandler(
    IGrainFactory grainFactory,
    ILogger<SetActiveTitleHandler> logger)
    : ICommandHandler<SetActiveTitleCommand, bool>
{
    public async Task<bool> Handle(SetActiveTitleCommand request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var result = await achievementGrain.SetActiveTitleAsync(request.TitleId);

        logger.LogInformation("Player {PlayerId} set active title to {TitleId}: {Result}",
            request.PlayerId, request.TitleId, result);
        return result;
    }
}

public sealed class UnlockAppearanceHandler(
    IGrainFactory grainFactory,
    ILogger<UnlockAppearanceHandler> logger)
    : ICommandHandler<UnlockAppearanceCommand, bool>
{
    public async Task<bool> Handle(UnlockAppearanceCommand request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var result = await achievementGrain.UnlockAppearanceAsync(request.AppearanceId, request.Category);

        logger.LogInformation("Player {PlayerId} unlocked appearance {AppearanceId} in {Category}: {Result}",
            request.PlayerId, request.AppearanceId, request.Category, result);
        return result;
    }
}

public sealed class GetCollectionProgressHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetCollectionProgressQuery, CollectionProgressDto>
{
    public async Task<CollectionProgressDto> Handle(
        GetCollectionProgressQuery request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var grainState = await achievementGrain.GetStateAsync();

        return new CollectionProgressDto(
            grainState.Achievements.Count + grainState.InProgress.Count,
            grainState.Achievements.Count,
            grainState.Titles.Count,
            grainState.Titles.Count);
    }
}

public sealed class GetAchievementProgressHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetAchievementProgressQuery, AchievementProgressDto>
{
    public async Task<AchievementProgressDto> Handle(
        GetAchievementProgressQuery request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var grainState = await achievementGrain.GetStateAsync();

        var achievements = grainState.Achievements.Values
            .Select(a => new AchievementDto(a.AchievementId, a.Name, "Unlocked", a.UnlockedAt, a.Points))
            .ToList();

        var totalPoints = achievements.Sum(a => a.Points);

        return new AchievementProgressDto(
            request.PlayerId, achievements, totalPoints, achievements.Count);
    }
}

public sealed class UnlockCosmeticHandler(
    IGrainFactory grainFactory,
    ILogger<UnlockCosmeticHandler> logger)
    : ICommandHandler<UnlockCosmeticCommand, UnlockCosmeticResultDto>
{
    public async Task<UnlockCosmeticResultDto> Handle(UnlockCosmeticCommand request, CancellationToken ct)
    {
        var achievementGrain = grainFactory.GetGrain<IAchievementGrain>(request.PlayerId);
        var result = await achievementGrain.UnlockAppearanceAsync(request.CosmeticId, "Cosmetic");

        logger.LogInformation("Player {PlayerId} unlock cosmetic {CosmeticId}: {Result}",
            request.PlayerId, request.CosmeticId, result);

        return new UnlockCosmeticResultDto(result, request.CosmeticId,
            result ? null : "Failed to unlock cosmetic");
    }
}
