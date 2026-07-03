using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IAchievementGrain : IGrainWithGuidKey
{
    Task<AchievementGrainState> GetStateAsync();
    Task<bool> UnlockAsync(string achievementId, int points);
    Task<bool> SetActiveTitleAsync(string titleId);
    Task<bool> UnlockTitleAsync(string titleId, string name);
    Task<bool> UnlockAppearanceAsync(string appearanceId, string category);
    Task IncrementProgressAsync(string achievementId, int amount);
}

[GenerateSerializer]
public sealed class AchievementGrainState
{
    [Id(0)] public Dictionary<string, UnlockedAchievement> Achievements { get; set; } = [];
    [Id(1)] public Dictionary<string, AchievementProgress> InProgress { get; set; } = [];
    [Id(2)] public Dictionary<string, TitleState> Titles { get; set; } = [];
    [Id(3)] public string? ActiveTitleId { get; set; }
    [Id(4)] public Dictionary<string, AppearanceState> Appearances { get; set; } = [];
    [Id(5)] public int TotalPoints { get; set; }
}

[GenerateSerializer]
public sealed class UnlockedAchievement
{
    [Id(0)] public string AchievementId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public DateTime UnlockedAt { get; set; }
    [Id(3)] public int Points { get; set; }
}

[GenerateSerializer]
public sealed class AchievementProgress
{
    [Id(0)] public string AchievementId { get; set; } = string.Empty;
    [Id(1)] public int Current { get; set; }
    [Id(2)] public int Required { get; set; }
}

[GenerateSerializer]
public sealed class TitleState
{
    [Id(0)] public string TitleId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public DateTime UnlockedAt { get; set; }
}

[GenerateSerializer]
public sealed class AppearanceState
{
    [Id(0)] public string AppearanceId { get; set; } = string.Empty;
    [Id(1)] public string Category { get; set; } = string.Empty;
    [Id(2)] public DateTime UnlockedAt { get; set; }
}
