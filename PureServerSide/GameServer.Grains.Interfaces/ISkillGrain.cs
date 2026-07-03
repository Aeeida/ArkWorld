using Orleans;

namespace GameServer.Grains.Interfaces;

public interface ISkillGrain : IGrainWithGuidKey
{
    Task<SkillGrainState> GetStateAsync();
    Task<bool> LearnSkillAsync(string skillId);
    Task<bool> StartTrainingAsync(string skillId);
    Task<bool> CancelTrainingAsync();
    Task<bool> CompleteTrainingAsync();
    Task<bool> AllocateTalentPointAsync(string talentId);
    Task<bool> ResetTalentsAsync();
    Task ChangeReputationAsync(string factionId, int amount);
}

[GenerateSerializer]
public sealed class SkillGrainState
{
    [Id(0)] public Dictionary<string, int> LearnedSkills { get; set; } = [];
    [Id(1)] public List<SkillQueueEntry> TrainingQueue { get; set; } = [];
    [Id(2)] public string? CurrentlyTrainingSkillId { get; set; }
    [Id(3)] public DateTime? TrainingCompletesAt { get; set; }
    [Id(4)] public Dictionary<string, bool> AllocatedTalents { get; set; } = [];
    [Id(5)] public int UnspentTalentPoints { get; set; }
    [Id(6)] public Dictionary<string, int> FactionReputation { get; set; } = [];
}

[GenerateSerializer]
public sealed class SkillQueueEntry
{
    [Id(0)] public string SkillId { get; set; } = string.Empty;
    [Id(1)] public int TargetLevel { get; set; }
    [Id(2)] public DateTime EnqueuedAt { get; set; }
}
