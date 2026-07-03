using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IQuestGrain : IGrainWithGuidKey
{
    Task<QuestGrainState> GetStateAsync();
    Task<bool> AcceptQuestAsync(string questId);
    Task<bool> CompleteQuestAsync(string questId);
    Task<bool> AbandonQuestAsync(string questId);
    Task<bool> UpdateProgressAsync(string questId, string objectiveId, int amount);
    Task<bool> ChooseBranchAsync(string questId, int branchIndex);
    Task ResetDailyQuestsAsync();
    Task ResetWeeklyQuestsAsync();
}

[GenerateSerializer]
public sealed class QuestGrainState
{
    [Id(0)] public Dictionary<string, ActiveQuestState> ActiveQuests { get; set; } = [];
    [Id(1)] public HashSet<string> CompletedQuestIds { get; set; } = [];
    [Id(2)] public Dictionary<string, DateTime> DailyCompletions { get; set; } = [];
    [Id(3)] public Dictionary<string, DateTime> WeeklyCompletions { get; set; } = [];
}

[GenerateSerializer]
public sealed class ActiveQuestState
{
    [Id(0)] public string QuestId { get; set; } = string.Empty;
    [Id(1)] public string Status { get; set; } = "InProgress";
    [Id(2)] public Dictionary<string, int> ObjectiveProgress { get; set; } = [];
    [Id(3)] public int? ChosenBranch { get; set; }
    [Id(4)] public DateTime AcceptedAt { get; set; }
}
