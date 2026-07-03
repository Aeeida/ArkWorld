using Ark.Analyzers.Attributes;
using MessagePack;

namespace Game.Shared.Core.DTOs;

[MessagePackObject]
public sealed record CombatResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid AttackerId,
    [property: Key(2)] Guid DefenderId,
    [property: Key(3)] double DamageDealt,
    [property: Key(4)] string? ErrorMessage);

[MessagePackObject]
[MapToEcsComponent("Ark.Ecs.Components.Health")]
public sealed record ShipDto(
    [property: Key(0), EcsIgnore] Guid ShipId,
    [property: Key(1), EcsIgnore] Guid OwnerId,
    [property: Key(2), EcsIgnore] string ShipType,
    [property: Key(3), EcsFieldMap("Current")] double HullPoints,
    [property: Key(4), EcsIgnore] double ShieldPoints,
    [property: Key(5), EcsIgnore] double ArmorPoints,
    [property: Key(6), EcsFieldMap("Max")] double MaxHull,
    [property: Key(7), EcsIgnore] double MaxShield,
    [property: Key(8), EcsIgnore] double MaxArmor,
    [property: Key(9), EcsIgnore] string? CurrentSolarSystemId,
    [property: Key(10), EcsIgnore] bool IsDestroyed);

[MessagePackObject]
public sealed record MarketOrderDto(
    [property: Key(0)] Guid OrderId,
    [property: Key(1)] Guid SellerId,
    [property: Key(2)] string ItemId,
    [property: Key(3)] int Quantity,
    [property: Key(4)] decimal PricePerUnit,
    [property: Key(5)] string StationId,
    [property: Key(6)] bool IsBuyOrder,
    [property: Key(7)] DateTime ExpiresAt);

[MessagePackObject]
public sealed record InventoryItemDto(
    [property: Key(0)] Guid Id,
    [property: Key(1)] string ItemId,
    [property: Key(2)] int Quantity,
    [property: Key(3)] string Rarity);

[MessagePackObject]
public sealed record FleetDto(
    [property: Key(0)] Guid FleetId,
    [property: Key(1)] Guid LeaderId,
    [property: Key(2)] string FleetName,
    [property: Key(3)] int MemberCount);

[MessagePackObject]
public sealed record GuildDto(
    [property: Key(0)] Guid GuildId,
    [property: Key(1)] string Name,
    [property: Key(2)] Guid FounderId,
    [property: Key(3)] int MemberCount);

[MessagePackObject]
public sealed record InstanceDto(
    [property: Key(0)] Guid InstanceId,
    [property: Key(1)] string TemplateId,
    [property: Key(2)] string Difficulty,
    [property: Key(3)] int PlayerCount);

[MessagePackObject]
public sealed record SolarSystemDto(
    [property: Key(0)] string SystemId,
    [property: Key(1)] string Name,
    [property: Key(2)] double SecurityLevel,
    [property: Key(3)] int OnlinePlayerCount,
    [property: Key(4)] string? SovereignAllianceId);

[MessagePackObject]
public sealed record StationDto(
    [property: Key(0)] Guid StationId,
    [property: Key(1)] string Name,
    [property: Key(2)] string SolarSystemId,
    [property: Key(3)] bool HasMarket,
    [property: Key(4)] bool HasRepairShop);

// ── Combat Result DTOs ───────────────────────────────────────────────

[MessagePackObject]
public sealed record AttackResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid AttackerId,
    [property: Key(2)] Guid TargetId,
    [property: Key(3)] double DamageDealt,
    [property: Key(4)] bool TargetDestroyed,
    [property: Key(5)] string? ErrorMessage);

[MessagePackObject]
public sealed record UseSkillResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid CasterId,
    [property: Key(2)] string SkillId,
    [property: Key(3)] double EffectValue,
    [property: Key(4)] double CooldownSeconds,
    [property: Key(5)] string? ErrorMessage);

[MessagePackObject]
public sealed record BattleStatusDto(
    [property: Key(0)] Guid BattleId,
    [property: Key(1)] string Status,
    [property: Key(2)] IReadOnlyList<BattleParticipantDto> Participants,
    [property: Key(3)] int RoundNumber,
    [property: Key(4)] DateTime StartedAt);

[MessagePackObject]
public sealed record BattleParticipantDto(
    [property: Key(0)] Guid EntityId,
    [property: Key(1)] string Name,
    [property: Key(2)] double Health,
    [property: Key(3)] double MaxHealth,
    [property: Key(4)] string Team);

[MessagePackObject]
public sealed record FleetBattleCommandResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid FleetId,
    [property: Key(2)] Guid TargetFleetId,
    [property: Key(3)] string? ErrorMessage);

// ── Navigation DTOs ──────────────────────────────────────────────────

[MessagePackObject]
public sealed record NavigationResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string TargetSolarSystemId,
    [property: Key(2)] double EstimatedTravelTimeSeconds,
    [property: Key(3)] int JumpCount,
    [property: Key(4)] string? ErrorMessage);

[MessagePackObject]
public sealed record CollectResourceResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string ResourceType,
    [property: Key(2)] int QuantityCollected,
    [property: Key(3)] bool NodeDepleted,
    [property: Key(4)] string? ErrorMessage);

// ── Inventory DTOs ───────────────────────────────────────────────────

[MessagePackObject]
public sealed record InventoryDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] IReadOnlyList<InventoryItemDto> Items,
    [property: Key(2)] int MaxSlots,
    [property: Key(3)] int UsedSlots);

[MessagePackObject]
public sealed record ItemMoveResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? ErrorMessage);

[MessagePackObject]
public sealed record EquipResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? ItemId,
    [property: Key(2)] string? Slot,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record DropItemResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? ItemId,
    [property: Key(2)] int QuantityDropped,
    [property: Key(3)] string? ErrorMessage);

// ── Market DTOs ──────────────────────────────────────────────────────

[MessagePackObject]
public sealed record MarketOrdersDto(
    [property: Key(0)] string StationId,
    [property: Key(1)] IReadOnlyList<MarketOrderDto> Orders,
    [property: Key(2)] int TotalCount);

[MessagePackObject]
public sealed record CreateOrderResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? OrderId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record BuyOrderResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid OrderId,
    [property: Key(2)] int QuantityFilled,
    [property: Key(3)] decimal TotalCost,
    [property: Key(4)] string? ErrorMessage);

// ── Fleet DTOs ───────────────────────────────────────────────────────

[MessagePackObject]
public sealed record FormFleetResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? FleetId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record CommandFleetResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid FleetId,
    [property: Key(2)] string CommandType,
    [property: Key(3)] string? ErrorMessage);

// ── Guild DTOs ───────────────────────────────────────────────────────

[MessagePackObject]
public sealed record CreateGuildResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? GuildId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record GuildInfoDto(
    [property: Key(0)] Guid GuildId,
    [property: Key(1)] string Name,
    [property: Key(2)] Guid FounderId,
    [property: Key(3)] int MemberCount,
    [property: Key(4)] DateTime CreatedAt,
    [property: Key(5)] string? Motd);

[MessagePackObject]
public sealed record SendGuildChatResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? ErrorMessage);

// ── Instance DTOs ────────────────────────────────────────────────────

[MessagePackObject]
public sealed record EnterInstanceResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? InstanceId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record InstanceStatusDto(
    [property: Key(0)] Guid InstanceId,
    [property: Key(1)] string TemplateId,
    [property: Key(2)] string Status,
    [property: Key(3)] int PlayerCount,
    [property: Key(4)] int MaxPlayers,
    [property: Key(5)] TimeSpan Elapsed);

// ── Mail DTOs ────────────────────────────────────────────────────────

[MessagePackObject]
public sealed record SendMailResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? MailId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record GetMailDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] IReadOnlyList<MailDto> Mails,
    [property: Key(2)] int UnreadCount);

[MessagePackObject]
public sealed record MailDto(
    [property: Key(0)] Guid MailId,
    [property: Key(1)] Guid SenderId,
    [property: Key(2)] string SenderName,
    [property: Key(3)] string Subject,
    [property: Key(4)] string Body,
    [property: Key(5)] bool IsRead,
    [property: Key(6)] DateTime SentAt);
