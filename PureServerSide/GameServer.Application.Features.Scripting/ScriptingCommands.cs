using Game.Shared.Core.DTOs;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Scripting;

// ── Commands ──────────────────────────────────────────────────────────

public sealed record StartScriptCommand(Guid PlayerId, string ScriptId, int Version = 0) : ICommand<ScriptResultDto>;

public sealed record AdvanceScriptCommand(Guid PlayerId, string ScriptId) : ICommand<ScriptResultDto>;

public sealed record ChooseDialogueOptionCommand(Guid PlayerId, string ScriptId, int OptionIndex) : ICommand<ScriptResultDto>;

public sealed record AbortScriptCommand(Guid PlayerId, string ScriptId) : ICommand<ScriptResultDto>;

public sealed record RegisterScriptCommand(
    string ScriptId, string Name, string Description, string Category,
    string EntryNodeId, string Author, List<ScriptNodeDto> Nodes) : ICommand<ScriptResultDto>;

public sealed record UpdateScriptCommand(
    string ScriptId, string Name, string Description,
    string EntryNodeId, string Author, List<ScriptNodeDto> Nodes) : ICommand<ScriptResultDto>;

public sealed record RollbackScriptCommand(string ScriptId, int TargetVersion) : ICommand<ScriptResultDto>;

public sealed record ScheduleActivityCommand(
    string ScriptId, DateTime StartsAt, DateTime EndsAt, string? TargetZone) : ICommand<Guid>;

public sealed record CancelActivityCommand(Guid ActivityId) : ICommand<bool>;

// ── Queries ───────────────────────────────────────────────────────────

public sealed record GetDialogueQuery(Guid PlayerId, string ScriptId) : IQuery<DialogueDto>;

public sealed record GetActiveScriptsQuery(Guid PlayerId) : IQuery<IReadOnlyList<ScriptStatusDto>>, ICacheableQuery
{
    public string CacheKey => $"scripts:active:{PlayerId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);
}

public sealed record GetScriptDefinitionsQuery() : IQuery<IReadOnlyList<ScriptDefinitionDto>>, ICacheableQuery
{
    public string CacheKey => "scripts:definitions";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);
}

public sealed record GetActiveActivitiesQuery() : IQuery<IReadOnlyList<ActivityDto>>, ICacheableQuery
{
    public string CacheKey => "scripts:activities";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);
}

public sealed record StartScriptedNarrativeCommand(Guid PlayerId, string ScriptId) : ICommand<StartScriptResultDto>;

public sealed record ChooseDialogueWrappedCommand(Guid PlayerId, string ScriptId, int OptionIndex) : ICommand<DialogueChoiceResultDto>;

public sealed record TriggerWorldActivityCommand(string ScriptId, DateTime StartsAt, DateTime EndsAt, string? TargetZone) : ICommand<TriggerActivityResultDto>;

public sealed record GetActiveScriptStatusQuery(Guid PlayerId, string ScriptId) : IQuery<ScriptStatusDto>;

// ── Script Node DTO (for registration) ────────────────────────────────

public sealed record ScriptNodeDto(
    string NodeId,
    string Type,
    Dictionary<string, string> Properties,
    List<ScriptTransitionDto> Transitions,
    List<ScriptDialogueOptionDto> DialogueOptions);

public sealed record ScriptTransitionDto(
    string TargetNodeId,
    string? ConditionType,
    string? ConditionParam,
    string? ConditionValue,
    bool IsDefault);

public sealed record ScriptDialogueOptionDto(
    int Index,
    string Text,
    string? TargetNodeId,
    string? RequiredConditionType,
    string? RequiredConditionParam);

// ── Handlers ──────────────────────────────────────────────────────────

public sealed class StartScriptHandler(
    IGrainFactory grainFactory,
    ILogger<StartScriptHandler> logger)
    : ICommandHandler<StartScriptCommand, ScriptResultDto>
{
    public async Task<ScriptResultDto> Handle(StartScriptCommand request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var result = await scriptGrain.StartScriptAsync(request.ScriptId, request.Version);

        logger.LogInformation("Player {PlayerId} start script {ScriptId}: {Result}",
            request.PlayerId, request.ScriptId, result);

        return new ScriptResultDto(result, request.ScriptId, result ? null : "Failed to start script");
    }
}

public sealed class AdvanceScriptHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<AdvanceScriptCommand, ScriptResultDto>
{
    public async Task<ScriptResultDto> Handle(AdvanceScriptCommand request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var result = await scriptGrain.AdvanceAsync(request.ScriptId);
        return new ScriptResultDto(result, request.ScriptId, result ? null : "Cannot advance script");
    }
}

public sealed class ChooseDialogueOptionHandler(
    IGrainFactory grainFactory,
    ILogger<ChooseDialogueOptionHandler> logger)
    : ICommandHandler<ChooseDialogueOptionCommand, ScriptResultDto>
{
    public async Task<ScriptResultDto> Handle(ChooseDialogueOptionCommand request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var result = await scriptGrain.ChooseDialogueOptionAsync(request.ScriptId, request.OptionIndex);

        logger.LogInformation("Player {PlayerId} chose option {Option} in script {ScriptId}: {Result}",
            request.PlayerId, request.OptionIndex, request.ScriptId, result);

        return new ScriptResultDto(result, request.ScriptId, result ? null : "Invalid dialogue choice");
    }
}

public sealed class AbortScriptHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<AbortScriptCommand, ScriptResultDto>
{
    public async Task<ScriptResultDto> Handle(AbortScriptCommand request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var result = await scriptGrain.AbortScriptAsync(request.ScriptId);
        return new ScriptResultDto(result, request.ScriptId, result ? null : "Script not found");
    }
}

public sealed class RegisterScriptHandler(
    IGrainFactory grainFactory,
    ILogger<RegisterScriptHandler> logger)
    : ICommandHandler<RegisterScriptCommand, ScriptResultDto>
{
    public async Task<ScriptResultDto> Handle(RegisterScriptCommand request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        var definition = new ScriptDefinition
        {
            ScriptId = request.ScriptId,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            EntryNodeId = request.EntryNodeId,
            Author = request.Author,
            Nodes = request.Nodes.Select(MapNode).ToList()
        };

        var result = await manager.RegisterScriptAsync(definition);

        logger.LogInformation("Script {ScriptId} registered: {Result}", request.ScriptId, result);
        return new ScriptResultDto(result, request.ScriptId, result ? null : "Script already exists");
    }

    private static ScriptNode MapNode(ScriptNodeDto dto) => new()
    {
        NodeId = dto.NodeId,
        Type = dto.Type,
        Properties = new Dictionary<string, string>(dto.Properties),
        Transitions = dto.Transitions.Select(t => new ScriptTransition
        {
            TargetNodeId = t.TargetNodeId,
            ConditionType = t.ConditionType,
            ConditionParam = t.ConditionParam,
            ConditionValue = t.ConditionValue,
            IsDefault = t.IsDefault
        }).ToList(),
        DialogueOptions = dto.DialogueOptions.Select(o => new DialogueOption
        {
            Index = o.Index,
            Text = o.Text,
            TargetNodeId = o.TargetNodeId,
            RequiredConditionType = o.RequiredConditionType,
            RequiredConditionParam = o.RequiredConditionParam
        }).ToList()
    };
}

public sealed class UpdateScriptHandler(
    IGrainFactory grainFactory,
    ILogger<UpdateScriptHandler> logger)
    : ICommandHandler<UpdateScriptCommand, ScriptResultDto>
{
    public async Task<ScriptResultDto> Handle(UpdateScriptCommand request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        var existing = await manager.GetScriptAsync(request.ScriptId);
        if (existing is null)
            return new ScriptResultDto(false, request.ScriptId, "Script not found");

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.EntryNodeId = request.EntryNodeId;
        existing.Author = request.Author;
        existing.Nodes = request.Nodes.Select(dto => new ScriptNode
        {
            NodeId = dto.NodeId,
            Type = dto.Type,
            Properties = new Dictionary<string, string>(dto.Properties),
            Transitions = dto.Transitions.Select(t => new ScriptTransition
            {
                TargetNodeId = t.TargetNodeId,
                ConditionType = t.ConditionType,
                ConditionParam = t.ConditionParam,
                ConditionValue = t.ConditionValue,
                IsDefault = t.IsDefault
            }).ToList(),
            DialogueOptions = dto.DialogueOptions.Select(o => new DialogueOption
            {
                Index = o.Index,
                Text = o.Text,
                TargetNodeId = o.TargetNodeId,
                RequiredConditionType = o.RequiredConditionType,
                RequiredConditionParam = o.RequiredConditionParam
            }).ToList()
        }).ToList();

        var result = await manager.UpdateScriptAsync(existing);

        logger.LogInformation("Script {ScriptId} updated: {Result}", request.ScriptId, result);
        return new ScriptResultDto(result, request.ScriptId, null);
    }
}

public sealed class RollbackScriptHandler(
    IGrainFactory grainFactory,
    ILogger<RollbackScriptHandler> logger)
    : ICommandHandler<RollbackScriptCommand, ScriptResultDto>
{
    public async Task<ScriptResultDto> Handle(RollbackScriptCommand request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        var result = await manager.RollbackScriptAsync(request.ScriptId, request.TargetVersion);

        logger.LogInformation("Script {ScriptId} rollback to v{Version}: {Result}",
            request.ScriptId, request.TargetVersion, result);

        return new ScriptResultDto(result, request.ScriptId, result ? null : "Version not found");
    }
}

public sealed class ScheduleActivityHandler(
    IGrainFactory grainFactory,
    ILogger<ScheduleActivityHandler> logger)
    : ICommandHandler<ScheduleActivityCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleActivityCommand request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        var activityId = await manager.ScheduleActivityAsync(
            request.ScriptId, request.StartsAt, request.EndsAt, request.TargetZone);

        logger.LogInformation("Activity scheduled for script {ScriptId}: {ActivityId}",
            request.ScriptId, activityId);
        return activityId;
    }
}

public sealed class CancelActivityHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<CancelActivityCommand, bool>
{
    public async Task<bool> Handle(CancelActivityCommand request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        return await manager.CancelActivityAsync(request.ActivityId);
    }
}

// ── Query Handlers ────────────────────────────────────────────────────

public sealed class GetDialogueHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetDialogueQuery, DialogueDto>
{
    public async Task<DialogueDto> Handle(GetDialogueQuery request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var snapshot = await scriptGrain.GetDialogueAsync(request.ScriptId);

        return new DialogueDto(
            snapshot.ScriptId,
            snapshot.NodeId,
            snapshot.SpeakerName,
            snapshot.Text,
            snapshot.Options
                .Select(o => new DialogueOptionDto(o.Index, o.Text, o.IsAvailable))
                .ToList());
    }
}

public sealed class GetActiveScriptsHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetActiveScriptsQuery, IReadOnlyList<ScriptStatusDto>>
{
    public async Task<IReadOnlyList<ScriptStatusDto>> Handle(GetActiveScriptsQuery request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var grainState = await scriptGrain.GetStateAsync();

        return grainState.ActiveScripts.Values
            .Select(a => new ScriptStatusDto(a.ScriptId, a.Version, a.Status, a.CurrentNodeId, a.StartedAt))
            .ToList();
    }
}

public sealed class GetScriptDefinitionsHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetScriptDefinitionsQuery, IReadOnlyList<ScriptDefinitionDto>>
{
    public async Task<IReadOnlyList<ScriptDefinitionDto>> Handle(GetScriptDefinitionsQuery request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        var scripts = await manager.GetAllScriptsAsync();

        return scripts
            .Select(s => new ScriptDefinitionDto(s.ScriptId, s.Version, s.Name, s.Description, s.Category, s.IsActive, s.UpdatedAt))
            .ToList();
    }
}

public sealed class GetActiveActivitiesHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetActiveActivitiesQuery, IReadOnlyList<ActivityDto>>
{
    public async Task<IReadOnlyList<ActivityDto>> Handle(GetActiveActivitiesQuery request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        var activities = await manager.GetActiveActivitiesAsync();

        return activities
            .Select(a => new ActivityDto(a.ActivityId, a.ScriptId, a.StartsAt, a.EndsAt, a.TargetZone, a.Status))
            .ToList();
    }
}

public sealed class StartScriptedNarrativeHandler(
    IGrainFactory grainFactory,
    ILogger<StartScriptedNarrativeHandler> logger)
    : ICommandHandler<StartScriptedNarrativeCommand, StartScriptResultDto>
{
    public async Task<StartScriptResultDto> Handle(StartScriptedNarrativeCommand request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var result = await scriptGrain.StartScriptAsync(request.ScriptId, 0);

        if (!result)
            return new StartScriptResultDto(false, request.ScriptId, null, "Failed to start narrative");

        var grainState = await scriptGrain.GetStateAsync();
        grainState.ActiveScripts.TryGetValue(request.ScriptId, out var active);

        logger.LogInformation("Player {PlayerId} started narrative {ScriptId}",
            request.PlayerId, request.ScriptId);

        return new StartScriptResultDto(true, request.ScriptId, active?.CurrentNodeId, null);
    }
}

public sealed class ChooseDialogueWrappedHandler(
    IGrainFactory grainFactory,
    ILogger<ChooseDialogueWrappedHandler> logger)
    : ICommandHandler<ChooseDialogueWrappedCommand, DialogueChoiceResultDto>
{
    public async Task<DialogueChoiceResultDto> Handle(ChooseDialogueWrappedCommand request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var result = await scriptGrain.ChooseDialogueOptionAsync(request.ScriptId, request.OptionIndex);

        if (!result)
            return new DialogueChoiceResultDto(false, request.ScriptId, request.OptionIndex, null, "Invalid choice");

        var grainState = await scriptGrain.GetStateAsync();
        grainState.ActiveScripts.TryGetValue(request.ScriptId, out var active);

        logger.LogInformation("Player {PlayerId} chose option {Option} in {ScriptId}",
            request.PlayerId, request.OptionIndex, request.ScriptId);

        return new DialogueChoiceResultDto(true, request.ScriptId, request.OptionIndex, active?.CurrentNodeId, null);
    }
}

public sealed class TriggerWorldActivityHandler(
    IGrainFactory grainFactory,
    ILogger<TriggerWorldActivityHandler> logger)
    : ICommandHandler<TriggerWorldActivityCommand, TriggerActivityResultDto>
{
    public async Task<TriggerActivityResultDto> Handle(TriggerWorldActivityCommand request, CancellationToken ct)
    {
        var manager = grainFactory.GetGrain<IScriptManagerGrain>("global");
        var activityId = await manager.ScheduleActivityAsync(
            request.ScriptId, request.StartsAt, request.EndsAt, request.TargetZone);

        logger.LogInformation("World activity triggered for {ScriptId}: {ActivityId}",
            request.ScriptId, activityId);

        return new TriggerActivityResultDto(true, activityId, request.ScriptId, null);
    }
}

public sealed class GetActiveScriptStatusHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetActiveScriptStatusQuery, ScriptStatusDto>
{
    public async Task<ScriptStatusDto> Handle(GetActiveScriptStatusQuery request, CancellationToken ct)
    {
        var scriptGrain = grainFactory.GetGrain<IScriptInstanceGrain>(request.PlayerId);
        var grainState = await scriptGrain.GetStateAsync();

        if (grainState.ActiveScripts.TryGetValue(request.ScriptId, out var active))
            return new ScriptStatusDto(active.ScriptId, active.Version, active.Status, active.CurrentNodeId, active.StartedAt);

        return new ScriptStatusDto(request.ScriptId, 0, "NotFound", string.Empty, DateTime.MinValue);
    }
}
