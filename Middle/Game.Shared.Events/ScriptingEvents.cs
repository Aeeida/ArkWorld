namespace Game.Shared.Events;

// ── Script Lifecycle Events ───────────────────────────────────────────

public sealed record ScriptStartedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string ScriptId, int Version) : IGameEvent;

public sealed record ScriptCompletedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string ScriptId) : IGameEvent;

public sealed record ScriptAbortedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string ScriptId) : IGameEvent;

public sealed record ScriptActionExecutedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string ScriptId,
    string NodeId, string ActionType) : IGameEvent;

// ── Dialogue Events ──────────────────────────────────────────────────

public sealed record DialogueChoiceEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string ScriptId,
    string NodeId, int OptionIndex) : IGameEvent;

// ── Script Management Events ─────────────────────────────────────────

public sealed record ScriptRegisteredEvent(
    Guid EventId, DateTime OccurredAt,
    string ScriptId, int Version, string Author) : IGameEvent;

public sealed record ScriptUpdatedEvent(
    Guid EventId, DateTime OccurredAt,
    string ScriptId, int Version, string Author) : IGameEvent;

public sealed record ScriptRolledBackEvent(
    Guid EventId, DateTime OccurredAt,
    string ScriptId, int TargetVersion) : IGameEvent;

// ── Activity Events ──────────────────────────────────────────────────

public sealed record ActivityScheduledEvent(
    Guid EventId, DateTime OccurredAt,
    Guid ActivityId, string ScriptId,
    DateTime StartsAt, DateTime EndsAt,
    string? TargetZone) : IGameEvent;

public sealed record ActivityStartedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid ActivityId, string ScriptId) : IGameEvent;

public sealed record ActivityEndedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid ActivityId, string ScriptId) : IGameEvent;
