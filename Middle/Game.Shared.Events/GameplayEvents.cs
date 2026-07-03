namespace Game.Shared.Events;

public sealed record FleetCreatedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid FleetId, string FleetName, Guid LeaderId) : IGameEvent;

public sealed record FleetMemberJoinedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid FleetId, Guid PlayerId) : IGameEvent;

public sealed record FleetBattleStartedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid FleetId, string SolarSystemId, int AttackerCount, int DefenderCount) : IGameEvent;

public sealed record FleetBattleEndedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid WinningFleetId, Guid LosingFleetId, string SolarSystemId) : IGameEvent;

public sealed record GuildCreatedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid GuildId, string GuildName, Guid FounderId) : IGameEvent;

public sealed record GuildMemberJoinedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid GuildId, Guid PlayerId) : IGameEvent;

public sealed record InstanceCreatedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid InstanceId, string TemplateId, string Difficulty) : IGameEvent;

public sealed record InstanceCompletedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid InstanceId, TimeSpan Duration, int PlayerCount) : IGameEvent;

public sealed record PlayerDiedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, Guid? KillerId, string ZoneId) : IGameEvent;

public sealed record ShipDockedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid ShipId, Guid StationId) : IGameEvent;

public sealed record SkillTrainingCompletedEvent(
    Guid EventId, DateTime OccurredAt,
    Guid PlayerId, string SkillId, int NewLevel) : IGameEvent;
