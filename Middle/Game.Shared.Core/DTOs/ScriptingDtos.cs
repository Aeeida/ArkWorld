using MessagePack;

namespace Game.Shared.Core.DTOs;

// ── Scripting DTOs ────────────────────────────────────────────────────

[MessagePackObject]
public sealed record ScriptStatusDto(
    [property: Key(0)] string ScriptId,
    [property: Key(1)] int Version,
    [property: Key(2)] string Status,
    [property: Key(3)] string CurrentNodeId,
    [property: Key(4)] DateTime StartedAt);

[MessagePackObject]
public sealed record DialogueDto(
    [property: Key(0)] string ScriptId,
    [property: Key(1)] string NodeId,
    [property: Key(2)] string SpeakerName,
    [property: Key(3)] string Text,
    [property: Key(4)] IReadOnlyList<DialogueOptionDto> Options);

[MessagePackObject]
public sealed record DialogueOptionDto(
    [property: Key(0)] int Index,
    [property: Key(1)] string Text,
    [property: Key(2)] bool IsAvailable);

[MessagePackObject]
public sealed record ScriptDefinitionDto(
    [property: Key(0)] string ScriptId,
    [property: Key(1)] int Version,
    [property: Key(2)] string Name,
    [property: Key(3)] string Description,
    [property: Key(4)] string Category,
    [property: Key(5)] bool IsActive,
    [property: Key(6)] DateTime UpdatedAt);

[MessagePackObject]
public sealed record ActivityDto(
    [property: Key(0)] Guid ActivityId,
    [property: Key(1)] string ScriptId,
    [property: Key(2)] DateTime StartsAt,
    [property: Key(3)] DateTime EndsAt,
    [property: Key(4)] string? TargetZone,
    [property: Key(5)] string Status);

[MessagePackObject]
public sealed record ScriptResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string ScriptId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record StartScriptResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string ScriptId,
    [property: Key(2)] string? CurrentNodeId,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record DialogueChoiceResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string ScriptId,
    [property: Key(2)] int ChosenOptionIndex,
    [property: Key(3)] string? NextNodeId,
    [property: Key(4)] string? ErrorMessage);

[MessagePackObject]
public sealed record TriggerActivityResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? ActivityId,
    [property: Key(2)] string ScriptId,
    [property: Key(3)] string? ErrorMessage);
