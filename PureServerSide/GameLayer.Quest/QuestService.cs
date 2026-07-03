namespace GameLayer.Quest;

/// <summary>
/// Server-authoritative quest management.
/// Mirrors Ark's IQuestService — accept, abandon, submit, progress tracking, NPC dialogue.
/// </summary>
public sealed class QuestManager
{
    private readonly Dictionary<Guid, PlayerQuestState> _playerQuests = [];
    private readonly QuestDefinitionRegistry _definitions;

    public event Action<Guid, string>? OnQuestAccepted;
    public event Action<Guid, string>? OnQuestCompleted;
    public event Action<Guid, string, string, int>? OnProgressUpdated; // playerId, questId, objectiveId, current

    public QuestManager(QuestDefinitionRegistry definitions)
    {
        _definitions = definitions;
    }

    public PlayerQuestState GetOrCreate(Guid playerId)
    {
        if (!_playerQuests.TryGetValue(playerId, out var state))
        {
            state = new PlayerQuestState();
            _playerQuests[playerId] = state;
        }
        return state;
    }

    public bool AcceptQuest(Guid playerId, string questId)
    {
        var def = _definitions.Get(questId);
        if (def is null) return false;

        var state = GetOrCreate(playerId);
        if (state.ActiveQuests.ContainsKey(questId)) return false;
        if (state.CompletedQuestIds.Contains(questId) && !def.IsRepeatable) return false;

        var quest = new ActiveQuest
        {
            QuestId = questId,
            AcceptedAt = DateTime.UtcNow,
            Objectives = def.Objectives.ToDictionary(o => o.Id, _ => 0)
        };
        state.ActiveQuests[questId] = quest;

        OnQuestAccepted?.Invoke(playerId, questId);
        return true;
    }

    public bool AbandonQuest(Guid playerId, string questId)
    {
        var state = GetOrCreate(playerId);
        return state.ActiveQuests.Remove(questId);
    }

    public bool SubmitQuest(Guid playerId, string questId)
    {
        var def = _definitions.Get(questId);
        if (def is null) return false;

        var state = GetOrCreate(playerId);
        if (!state.ActiveQuests.TryGetValue(questId, out var quest)) return false;

        // Check all objectives are complete
        foreach (var obj in def.Objectives)
        {
            if (!quest.Objectives.TryGetValue(obj.Id, out var progress) || progress < obj.Required)
                return false;
        }

        state.ActiveQuests.Remove(questId);
        state.CompletedQuestIds.Add(questId);

        OnQuestCompleted?.Invoke(playerId, questId);
        return true;
    }

    public bool UpdateProgress(Guid playerId, string questId, string objectiveId, int amount = 1)
    {
        var state = GetOrCreate(playerId);
        if (!state.ActiveQuests.TryGetValue(questId, out var quest)) return false;
        if (!quest.Objectives.ContainsKey(objectiveId)) return false;

        quest.Objectives[objectiveId] += amount;
        OnProgressUpdated?.Invoke(playerId, questId, objectiveId, quest.Objectives[objectiveId]);
        return true;
    }

    public IReadOnlyList<QuestInfo> GetActiveQuests(Guid playerId)
    {
        var state = GetOrCreate(playerId);
        var result = new List<QuestInfo>();
        foreach (var (questId, quest) in state.ActiveQuests)
        {
            var def = _definitions.Get(questId);
            if (def is null) continue;

            var objectives = def.Objectives.Select(o => new QuestObjectiveInfo(
                o.Id, o.Description,
                quest.Objectives.GetValueOrDefault(o.Id, 0),
                o.Required)).ToArray();

            result.Add(new QuestInfo(questId, def.Title, def.Description, objectives,
                objectives.All(o => o.Current >= o.Required)));
        }
        return result.AsReadOnly();
    }

    public IReadOnlyList<QuestInfo> GetAvailableQuests(Guid playerId)
    {
        var state = GetOrCreate(playerId);
        var result = new List<QuestInfo>();
        foreach (var def in _definitions.GetAll())
        {
            if (state.ActiveQuests.ContainsKey(def.Id)) continue;
            if (state.CompletedQuestIds.Contains(def.Id) && !def.IsRepeatable) continue;

            var objectives = def.Objectives.Select(o =>
                new QuestObjectiveInfo(o.Id, o.Description, 0, o.Required)).ToArray();
            result.Add(new QuestInfo(def.Id, def.Title, def.Description, objectives, false));
        }
        return result.AsReadOnly();
    }

    public DialogResult? TalkToNpc(Guid playerId, int npcTypeId)
    {
        // Simple dialogue system — returns first available quest dialogue for this NPC
        var available = GetAvailableQuests(playerId);
        if (available.Count == 0)
            return new DialogResult(0, $"NPC-{npcTypeId}", "I have nothing for you right now.", []);

        var quest = available[0];
        return new DialogResult(
            npcTypeId,
            $"NPC-{npcTypeId}",
            $"Will you help me? Quest: {quest.Title}\n{quest.Description}",
            [
                new DialogOption(1, "Accept", -1),
                new DialogOption(2, "Decline", -1)
            ]);
    }
}

public sealed class PlayerQuestState
{
    public Dictionary<string, ActiveQuest> ActiveQuests { get; } = [];
    public HashSet<string> CompletedQuestIds { get; } = [];
}

public sealed class ActiveQuest
{
    public string QuestId { get; init; } = string.Empty;
    public DateTime AcceptedAt { get; init; }
    public Dictionary<string, int> Objectives { get; init; } = [];
}

// ── Data types ──

public record struct QuestInfo(string QuestId, string Title, string Description, QuestObjectiveInfo[] Objectives, bool IsComplete);
public record struct QuestObjectiveInfo(string Id, string Description, int Current, int Required);
public record struct DialogResult(int DialogId, string NpcName, string Text, DialogOption[] Options);
public record struct DialogOption(int OptionId, string Text, int NextDialogId);

// ── Definition Registry ──

public sealed class QuestDefinitionRegistry
{
    private readonly Dictionary<string, QuestDefinition> _quests = [];

    public void Register(QuestDefinition quest) => _quests[quest.Id] = quest;
    public QuestDefinition? Get(string questId) => _quests.GetValueOrDefault(questId);
    public IReadOnlyCollection<QuestDefinition> GetAll() => _quests.Values.ToList().AsReadOnly();

    public void SeedDefaults()
    {
        Register(new QuestDefinition("q_patrol", "Perimeter Patrol",
            "Patrol the perimeter and eliminate 5 hostile entities.",
            [new("obj_kill", "Eliminate hostiles", 5)], false));
        Register(new QuestDefinition("q_gather", "Resource Gathering",
            "Collect 10 iron ore from the mining zone.",
            [new("obj_ore", "Collect iron ore", 10)], true));
        Register(new QuestDefinition("q_scout", "Scout Report",
            "Visit 3 waypoints and report back.",
            [new("obj_wp1", "Visit Waypoint Alpha", 1),
             new("obj_wp2", "Visit Waypoint Beta", 1),
             new("obj_wp3", "Visit Waypoint Gamma", 1)], false));
    }
}

public record QuestDefinition(
    string Id, string Title, string Description,
    QuestObjectiveDef[] Objectives, bool IsRepeatable);

public record QuestObjectiveDef(string Id, string Description, int Required);
