using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class ScriptInstanceGrain(
    [PersistentState("scriptInstance", "GameStore")] IPersistentState<ScriptInstanceState> state,
    IEventBus eventBus,
    ILogger<ScriptInstanceGrain> logger) : Grain, IScriptInstanceGrain
{
    public Task<ScriptInstanceState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<bool> StartScriptAsync(string scriptId, int version)
    {
        if (state.State.ActiveScripts.ContainsKey(scriptId))
            return false; // already running

        // Fetch definition from ScriptManagerGrain
        var manager = GrainFactory.GetGrain<IScriptManagerGrain>("global");
        var definition = await manager.GetScriptAsync(scriptId);
        if (definition is null || !definition.IsActive)
            return false;

        var activeScript = new ActiveScript
        {
            ScriptId = scriptId,
            Version = version > 0 ? version : definition.Version,
            CurrentNodeId = definition.EntryNodeId,
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };
        activeScript.ExecutionLog.Add($"[{DateTime.UtcNow:O}] Started at node {definition.EntryNodeId}");

        state.State.ActiveScripts[scriptId] = activeScript;
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ScriptStartedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), scriptId, version));

        logger.LogInformation("Player {PlayerId} started script {ScriptId} v{Version}",
            this.GetPrimaryKey(), scriptId, activeScript.Version);

        // Auto-advance through non-blocking nodes
        await ExecuteCurrentNodeAsync(scriptId, definition);

        return true;
    }

    public async Task<bool> AdvanceAsync(string scriptId)
    {
        if (!state.State.ActiveScripts.TryGetValue(scriptId, out var active))
            return false;

        var manager = GrainFactory.GetGrain<IScriptManagerGrain>("global");
        var definition = await manager.GetScriptAsync(scriptId);
        if (definition is null)
            return false;

        return await ExecuteCurrentNodeAsync(scriptId, definition);
    }

    public async Task<DialogueSnapshot> GetDialogueAsync(string scriptId)
    {
        if (!state.State.ActiveScripts.TryGetValue(scriptId, out var active))
            return new DialogueSnapshot { ScriptId = scriptId, Text = "No active script." };

        var manager = GrainFactory.GetGrain<IScriptManagerGrain>("global");
        var definition = await manager.GetScriptAsync(scriptId);
        if (definition is null)
            return new DialogueSnapshot { ScriptId = scriptId, Text = "Script not found." };

        var currentNode = definition.Nodes.Find(n => n.NodeId == active.CurrentNodeId);
        if (currentNode is null || currentNode.Type != "Dialogue")
            return new DialogueSnapshot { ScriptId = scriptId, Text = "No dialogue at current node." };

        currentNode.Properties.TryGetValue("speaker", out var speaker);
        currentNode.Properties.TryGetValue("text", out var text);

        return new DialogueSnapshot
        {
            ScriptId = scriptId,
            NodeId = currentNode.NodeId,
            SpeakerName = speaker ?? "NPC",
            Text = text ?? string.Empty,
            Options = currentNode.DialogueOptions
                .Select(o => new DialogueOptionSnapshot
                {
                    Index = o.Index,
                    Text = o.Text,
                    IsAvailable = true // condition check in production
                })
                .ToList()
        };
    }

    public async Task<bool> ChooseDialogueOptionAsync(string scriptId, int optionIndex)
    {
        if (!state.State.ActiveScripts.TryGetValue(scriptId, out var active))
            return false;

        var manager = GrainFactory.GetGrain<IScriptManagerGrain>("global");
        var definition = await manager.GetScriptAsync(scriptId);
        if (definition is null)
            return false;

        var currentNode = definition.Nodes.Find(n => n.NodeId == active.CurrentNodeId);
        if (currentNode is null || currentNode.Type != "Dialogue")
            return false;

        var option = currentNode.DialogueOptions.Find(o => o.Index == optionIndex);
        if (option?.TargetNodeId is null)
            return false;

        active.CurrentNodeId = option.TargetNodeId;
        active.ExecutionLog.Add($"[{DateTime.UtcNow:O}] Chose option {optionIndex} → {option.TargetNodeId}");

        await state.WriteStateAsync();

        await eventBus.PublishAsync(new DialogueChoiceEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), scriptId, currentNode.NodeId, optionIndex));

        logger.LogInformation("Player {PlayerId} chose dialogue option {Option} in script {ScriptId}",
            this.GetPrimaryKey(), optionIndex, scriptId);

        // Continue executing after choice
        await ExecuteCurrentNodeAsync(scriptId, definition);

        return true;
    }

    public async Task<bool> AbortScriptAsync(string scriptId)
    {
        if (!state.State.ActiveScripts.TryGetValue(scriptId, out var active))
            return false;

        active.Status = "Aborted";
        active.ExecutionLog.Add($"[{DateTime.UtcNow:O}] Aborted");
        state.State.ActiveScripts.Remove(scriptId);
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ScriptAbortedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), scriptId));

        logger.LogInformation("Player {PlayerId} aborted script {ScriptId}",
            this.GetPrimaryKey(), scriptId);
        return true;
    }

    public Task<IReadOnlyList<string>> GetActiveScriptIdsAsync() =>
        Task.FromResult<IReadOnlyList<string>>(state.State.ActiveScripts.Keys.ToList());

    // ── Interpreter Engine ───────────────────────────────────────────

    private async Task<bool> ExecuteCurrentNodeAsync(string scriptId, ScriptDefinition definition)
    {
        if (!state.State.ActiveScripts.TryGetValue(scriptId, out var active))
            return false;

        var maxSteps = 50; // circuit breaker to prevent infinite loops
        var steps = 0;

        while (steps++ < maxSteps)
        {
            var node = definition.Nodes.Find(n => n.NodeId == active.CurrentNodeId);
            if (node is null)
            {
                await CompleteScriptAsync(scriptId, active);
                return true;
            }

            switch (node.Type)
            {
                case "Dialogue":
                    active.Status = "WaitingDialogue";
                    await state.WriteStateAsync();
                    return true; // pause: wait for player input

                case "Condition":
                    var passed = await EvaluateConditionAsync(node);
                    var transition = passed
                        ? node.Transitions.Find(t => t.IsDefault)
                        : node.Transitions.Find(t => !t.IsDefault);
                    transition ??= node.Transitions.FirstOrDefault();

                    if (transition is null)
                    {
                        await CompleteScriptAsync(scriptId, active);
                        return true;
                    }

                    active.CurrentNodeId = transition.TargetNodeId;
                    active.ExecutionLog.Add($"[{DateTime.UtcNow:O}] Condition {node.NodeId} → {(passed ? "pass" : "fail")} → {transition.TargetNodeId}");
                    break;

                case "Action":
                    await ExecuteActionNodeAsync(node, active);
                    var nextTransition = node.Transitions.FirstOrDefault();
                    if (nextTransition is null)
                    {
                        await CompleteScriptAsync(scriptId, active);
                        return true;
                    }
                    active.CurrentNodeId = nextTransition.TargetNodeId;
                    break;

                case "Branch":
                    var branchTarget = await ResolveBranchAsync(node);
                    if (branchTarget is null)
                    {
                        await CompleteScriptAsync(scriptId, active);
                        return true;
                    }
                    active.CurrentNodeId = branchTarget;
                    active.ExecutionLog.Add($"[{DateTime.UtcNow:O}] Branch {node.NodeId} → {branchTarget}");
                    break;

                case "Wait":
                    active.Status = "WaitingCondition";
                    if (node.Properties.TryGetValue("duration_sec", out var durStr) && int.TryParse(durStr, out var durSec))
                    {
                        RegisterTimer(
                            _ => AdvanceAsync(scriptId),
                            null,
                            TimeSpan.FromSeconds(durSec),
                            TimeSpan.FromMilliseconds(-1));
                    }
                    await state.WriteStateAsync();
                    return true;

                case "End":
                    await CompleteScriptAsync(scriptId, active);
                    return true;

                default:
                    // Unknown node type: skip to next
                    var fallback = node.Transitions.FirstOrDefault();
                    if (fallback is null)
                    {
                        await CompleteScriptAsync(scriptId, active);
                        return true;
                    }
                    active.CurrentNodeId = fallback.TargetNodeId;
                    break;
            }
        }

        logger.LogWarning("Script {ScriptId} hit step limit for player {PlayerId}",
            scriptId, this.GetPrimaryKey());
        await state.WriteStateAsync();
        return false;
    }

    private Task<bool> EvaluateConditionAsync(ScriptNode node)
    {
        // Evaluate transition conditions
        if (!node.Properties.TryGetValue("condition_type", out var condType))
            return Task.FromResult(true);

        node.Properties.TryGetValue("condition_param", out var param);
        node.Properties.TryGetValue("condition_value", out var value);

        return condType switch
        {
            "Always" => Task.FromResult(true),
            "Never" => Task.FromResult(false),
            "LevelCheck" => EvaluateLevelCheckAsync(param, value),
            "HasItem" => Task.FromResult(true), // simplified: would check inventory grain
            "QuestComplete" => Task.FromResult(true), // simplified: would check quest grain
            _ => Task.FromResult(true)
        };
    }

    private async Task<bool> EvaluateLevelCheckAsync(string? param, string? value)
    {
        if (!int.TryParse(value, out var requiredLevel))
            return true;

        var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(this.GetPrimaryKey());
        var level = await playerGrain.GetLevelAsync();
        return level >= requiredLevel;
    }

    private async Task ExecuteActionNodeAsync(ScriptNode node, ActiveScript active)
    {
        if (!node.Properties.TryGetValue("action_type", out var actionType))
            return;

        node.Properties.TryGetValue("action_param", out var param);
        node.Properties.TryGetValue("action_value", out var value);

        switch (actionType)
        {
            case "GiveExperience":
                if (long.TryParse(value, out var xp))
                {
                    var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(this.GetPrimaryKey());
                    await playerGrain.GainExperienceAsync(xp);
                }
                break;

            case "AcceptQuest":
                if (param is not null)
                {
                    var questGrain = GrainFactory.GetGrain<IQuestGrain>(this.GetPrimaryKey());
                    await questGrain.AcceptQuestAsync(param);
                }
                break;

            case "CompleteQuest":
                if (param is not null)
                {
                    var questGrain = GrainFactory.GetGrain<IQuestGrain>(this.GetPrimaryKey());
                    await questGrain.CompleteQuestAsync(param);
                }
                break;

            case "UnlockAchievement":
                if (param is not null)
                {
                    var achieveGrain = GrainFactory.GetGrain<IAchievementGrain>(this.GetPrimaryKey());
                    await achieveGrain.UnlockAsync(param, int.TryParse(value, out var pts) ? pts : 10);
                }
                break;

            case "SetVariable":
                if (param is not null && value is not null)
                    active.Variables[param] = value;
                break;

            case "LearnBlueprint":
                if (param is not null)
                {
                    var craftingGrain = GrainFactory.GetGrain<ICraftingGrain>(this.GetPrimaryKey());
                    await craftingGrain.LearnBlueprintAsync(param);
                }
                break;

            case "ChangeReputation":
                if (param is not null && int.TryParse(value, out var rep))
                {
                    var skillGrain = GrainFactory.GetGrain<ISkillGrain>(this.GetPrimaryKey());
                    await skillGrain.ChangeReputationAsync(param, rep);
                }
                break;
        }

        active.ExecutionLog.Add($"[{DateTime.UtcNow:O}] Action {actionType}({param}, {value})");

        await eventBus.PublishAsync(new ScriptActionExecutedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), active.ScriptId, node.NodeId, actionType));
    }

    private Task<string?> ResolveBranchAsync(ScriptNode node)
    {
        // Evaluate each transition condition; first passing wins
        foreach (var transition in node.Transitions)
        {
            if (transition.ConditionType is null || transition.IsDefault)
                continue;

            // Simplified: in production, evaluate condition against player state
            // For now, fall through to default
        }

        var defaultTransition = node.Transitions.Find(t => t.IsDefault);
        return Task.FromResult(defaultTransition?.TargetNodeId);
    }

    private async Task CompleteScriptAsync(string scriptId, ActiveScript active)
    {
        active.Status = "Completed";
        active.ExecutionLog.Add($"[{DateTime.UtcNow:O}] Completed");
        state.State.ActiveScripts.Remove(scriptId);
        state.State.CompletedScriptIds.Add(scriptId);
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new ScriptCompletedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), scriptId));

        logger.LogInformation("Player {PlayerId} completed script {ScriptId}",
            this.GetPrimaryKey(), scriptId);
    }
}
