using Orleans;

namespace GameServer.Grains.Interfaces;

/// <summary>
/// Per-player script instance grain. Key = PlayerId.
/// Manages all active scripted narratives, dialogues, and activities for a player.
/// </summary>
public interface IScriptInstanceGrain : IGrainWithGuidKey
{
    Task<ScriptInstanceState> GetStateAsync();
    Task<bool> StartScriptAsync(string scriptId, int version);
    Task<bool> AdvanceAsync(string scriptId);
    Task<DialogueSnapshot> GetDialogueAsync(string scriptId);
    Task<bool> ChooseDialogueOptionAsync(string scriptId, int optionIndex);
    Task<bool> AbortScriptAsync(string scriptId);
    Task<IReadOnlyList<string>> GetActiveScriptIdsAsync();
}

/// <summary>
/// Global script manager grain (singleton). Key = "global".
/// Manages script definitions, versions, and world activities.
/// </summary>
public interface IScriptManagerGrain : IGrainWithStringKey
{
    Task<ScriptManagerState> GetStateAsync();
    Task<bool> RegisterScriptAsync(ScriptDefinition definition);
    Task<bool> UpdateScriptAsync(ScriptDefinition definition);
    Task<ScriptDefinition?> GetScriptAsync(string scriptId);
    Task<IReadOnlyList<ScriptDefinition>> GetAllScriptsAsync();
    Task<Guid> ScheduleActivityAsync(string scriptId, DateTime startsAt, DateTime endsAt, string? targetZone);
    Task<bool> CancelActivityAsync(Guid activityId);
    Task<IReadOnlyList<ScheduledActivity>> GetActiveActivitiesAsync();
    Task<bool> RollbackScriptAsync(string scriptId, int targetVersion);
}

// ── Script Definition Model (DSL nodes) ──────────────────────────────

[GenerateSerializer]
public sealed class ScriptDefinition
{
    [Id(0)] public string ScriptId { get; set; } = string.Empty;
    [Id(1)] public int Version { get; set; } = 1;
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string Description { get; set; } = string.Empty;
    [Id(4)] public string Category { get; set; } = "Quest"; // Quest, Activity, Narrative, GM
    [Id(5)] public List<ScriptNode> Nodes { get; set; } = [];
    [Id(6)] public string EntryNodeId { get; set; } = string.Empty;
    [Id(7)] public DateTime CreatedAt { get; set; }
    [Id(8)] public DateTime UpdatedAt { get; set; }
    [Id(9)] public string Author { get; set; } = string.Empty;
    [Id(10)] public bool IsActive { get; set; } = true;
    [Id(11)] public List<string> Tags { get; set; } = [];
}

[GenerateSerializer]
public sealed class ScriptNode
{
    [Id(0)] public string NodeId { get; set; } = string.Empty;
    [Id(1)] public string Type { get; set; } = string.Empty; // Dialogue, Condition, Action, Branch, Wait, Parallel
    [Id(2)] public Dictionary<string, string> Properties { get; set; } = [];
    [Id(3)] public List<ScriptTransition> Transitions { get; set; } = [];
    [Id(4)] public List<DialogueOption> DialogueOptions { get; set; } = [];
}

[GenerateSerializer]
public sealed class ScriptTransition
{
    [Id(0)] public string TargetNodeId { get; set; } = string.Empty;
    [Id(1)] public string? ConditionType { get; set; } // LevelCheck, QuestComplete, ItemHas, FactionRep, etc.
    [Id(2)] public string? ConditionParam { get; set; }
    [Id(3)] public string? ConditionValue { get; set; }
    [Id(4)] public bool IsDefault { get; set; }
}

[GenerateSerializer]
public sealed class DialogueOption
{
    [Id(0)] public int Index { get; set; }
    [Id(1)] public string Text { get; set; } = string.Empty;
    [Id(2)] public string? TargetNodeId { get; set; }
    [Id(3)] public string? RequiredConditionType { get; set; }
    [Id(4)] public string? RequiredConditionParam { get; set; }
}

// ── Runtime State ────────────────────────────────────────────────────

[GenerateSerializer]
public sealed class ScriptInstanceState
{
    [Id(0)] public Dictionary<string, ActiveScript> ActiveScripts { get; set; } = [];
    [Id(1)] public HashSet<string> CompletedScriptIds { get; set; } = [];
}

[GenerateSerializer]
public sealed class ActiveScript
{
    [Id(0)] public string ScriptId { get; set; } = string.Empty;
    [Id(1)] public int Version { get; set; }
    [Id(2)] public string CurrentNodeId { get; set; } = string.Empty;
    [Id(3)] public string Status { get; set; } = "Running"; // Running, WaitingDialogue, WaitingCondition, Completed, Aborted
    [Id(4)] public DateTime StartedAt { get; set; }
    [Id(5)] public Dictionary<string, string> Variables { get; set; } = [];
    [Id(6)] public List<string> ExecutionLog { get; set; } = [];
}

[GenerateSerializer]
public sealed class DialogueSnapshot
{
    [Id(0)] public string ScriptId { get; set; } = string.Empty;
    [Id(1)] public string NodeId { get; set; } = string.Empty;
    [Id(2)] public string SpeakerName { get; set; } = string.Empty;
    [Id(3)] public string Text { get; set; } = string.Empty;
    [Id(4)] public List<DialogueOptionSnapshot> Options { get; set; } = [];
}

[GenerateSerializer]
public sealed class DialogueOptionSnapshot
{
    [Id(0)] public int Index { get; set; }
    [Id(1)] public string Text { get; set; } = string.Empty;
    [Id(2)] public bool IsAvailable { get; set; } = true;
}

// ── Script Manager State ─────────────────────────────────────────────

[GenerateSerializer]
public sealed class ScriptManagerState
{
    [Id(0)] public Dictionary<string, ScriptDefinition> Scripts { get; set; } = [];
    [Id(1)] public Dictionary<string, List<ScriptDefinition>> VersionHistory { get; set; } = [];
    [Id(2)] public List<ScheduledActivity> ScheduledActivities { get; set; } = [];
}

[GenerateSerializer]
public sealed class ScheduledActivity
{
    [Id(0)] public Guid ActivityId { get; set; }
    [Id(1)] public string ScriptId { get; set; } = string.Empty;
    [Id(2)] public DateTime StartsAt { get; set; }
    [Id(3)] public DateTime EndsAt { get; set; }
    [Id(4)] public string? TargetZone { get; set; }
    [Id(5)] public string Status { get; set; } = "Scheduled"; // Scheduled, Active, Completed, Cancelled
}
