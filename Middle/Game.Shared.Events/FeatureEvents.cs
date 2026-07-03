namespace Game.Shared.Events;

// ── Quest Events ──────────────────────────────────────────────────────

public sealed record QuestAcceptedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string QuestId) : IGameEvent;

public sealed record QuestCompletedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string QuestId) : IGameEvent;

public sealed record QuestAbandonedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string QuestId) : IGameEvent;

// ── Crafting Events ───────────────────────────────────────────────────

public sealed record CraftingStartedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string BlueprintId, int Quantity) : IGameEvent;

public sealed record CraftingCompletedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string BlueprintId, int Quantity) : IGameEvent;

public sealed record BlueprintLearnedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string BlueprintId) : IGameEvent;

// ── Exploration Events ────────────────────────────────────────────────

public sealed record ResourceHarvestedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string SolarSystemId,
    string ResourceType, int Quantity) : IGameEvent;

public sealed record DynamicWorldEventTriggeredEvent(
    Guid EventId, DateTime OccurredAt,
    string SolarSystemId, string EventType,
    Guid WorldEventId) : IGameEvent;

public sealed record ResourceDiscoveredEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string SolarSystemId,
    string ResourceType) : IGameEvent;

// ── Sovereignty Events ────────────────────────────────────────────────

public sealed record SovereigntyContestedEvent(
    Guid EventId, DateTime OccurredAt,
    string SolarSystemId, Guid AttackingAllianceId,
    Guid ContestId) : IGameEvent;

public sealed record StructurePlacedEvent(
    Guid EventId, DateTime OccurredAt,
    string SolarSystemId, Guid StructureId,
    string StructureType, Guid AllianceId) : IGameEvent;

public sealed record StructureDestroyedEvent(
    Guid EventId, DateTime OccurredAt,
    string SolarSystemId, Guid StructureId,
    string StructureType) : IGameEvent;

// ── Achievement Events ────────────────────────────────────────────────

public sealed record AchievementUnlockedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string AchievementId,
    int Points) : IGameEvent;

public sealed record TitleUnlockedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string TitleId,
    string TitleName) : IGameEvent;
