using Game.Shared.Core.Enums;
using MessagePack;

namespace Game.Shared.Core.DTOs;

// ── Shared Helpers ───────────────────────────────────────────────────

[MessagePackObject]
public sealed record Vector3Dto(
    [property: Key(0)] double X,
    [property: Key(1)] double Y,
    [property: Key(2)] double Z);

[MessagePackObject]
public sealed record ItemDto(
    [property: Key(0)] Guid ItemId,
    [property: Key(1)] int Quantity,
    [property: Key(2)] string? Name = null);

[MessagePackObject]
public record BaseCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 1. Auth & Session
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record LoginCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] string AccountId,
    [property: Key(4)] string PasswordHash,
    [property: Key(5)] string DeviceId,
    [property: Key(6)] string ClientVersion,
    [property: Key(7)] string? AuthToken = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record JoinWorldCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid? SolarSystemId = null,
    [property: Key(4)] Vector3Dto? SpawnPoint = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record LogoutCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] LogoutReason Reason = LogoutReason.Normal)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 2. Character & Progression
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record StartTrainingCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid SkillId,
    [property: Key(4)] int TargetLevel,
    [property: Key(5)] int? QueuePosition = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record CancelTrainingCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid QueueEntryId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record LevelUpCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] AttributeType AttributeToIncrease)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 3. Inventory & Items
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record ItemMoveCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid SourceInventoryId,
    [property: Key(4)] Guid TargetInventoryId,
    [property: Key(5)] Guid ItemInstanceId,
    [property: Key(6)] int Quantity,
    [property: Key(7)] int? SlotIndex = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record EquipItemCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid ItemInstanceId,
    [property: Key(4)] EquipSlot EquipSlot)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record DropItemCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid ItemInstanceId,
    [property: Key(4)] int Quantity,
    [property: Key(5)] Vector3Dto? DropLocation = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 4. Combat & AI
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record AttackCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid TargetId,
    [property: Key(4)] Guid? SkillId = null,
    [property: Key(5)] Vector3Dto? Position = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record UseSkillCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid SkillId,
    [property: Key(4)] List<Guid> Targets,
    [property: Key(5)] Dictionary<string, object>? Payload = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record CommandFleetAttackCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid FleetId,
    [property: Key(4)] Guid TargetId,
    [property: Key(5)] FleetFormation? Formation = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 5. World & Exploration
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record NavigationCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid TargetSolarSystemId,
    [property: Key(4)] RouteType? RouteType = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record ScanCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] ScanType ScanType,
    [property: Key(4)] float AreaRadius,
    [property: Key(5)] Vector3Dto Coordinates)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record CollectResourceCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid ResourceNodeId,
    [property: Key(4)] Guid? ToolId = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 6. Economy & Manufacturing
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record CreateMarketOrderCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid ItemId,
    [property: Key(4)] int Quantity,
    [property: Key(5)] decimal PricePerUnit,
    [property: Key(6)] OrderType OrderType,
    [property: Key(7)] TimeSpan? Duration = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record BuyOrderCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid OrderId,
    [property: Key(4)] int Quantity)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record StartCraftCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid BlueprintId,
    [property: Key(4)] int Quantity,
    [property: Key(5)] Dictionary<Guid, int>? Materials = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 7. Fleet & Sovereignty
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record FormFleetCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] List<Guid> ShipIds,
    [property: Key(4)] Guid LeaderId,
    [property: Key(5)] string? FleetName = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record CommandFleetCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid FleetId,
    [property: Key(4)] FleetCommandType CommandType,
    [property: Key(5)] Vector3Dto TargetCoordinates)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record ClaimSovereigntyCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid SolarSystemId,
    [property: Key(4)] Guid? AllianceId = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record BuildStructureCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] StructureType StructureType,
    [property: Key(4)] Guid SolarSystemId,
    [property: Key(5)] Vector3Dto Coordinates)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 8. Guild & Social
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record CreateGuildCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] string GuildName,
    [property: Key(4)] string Tag,
    [property: Key(5)] string? Description = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record SendChatCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] ChatChannel Channel,
    [property: Key(4)] string Message,
    [property: Key(5)] Guid? TargetId = null,
    [property: Key(6)] List<ItemDto>? Attachments = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 9. Instance & Dungeon
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record EnterInstanceCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid InstanceTemplateId,
    [property: Key(4)] DifficultyLevel? Difficulty = null,
    [property: Key(5)] List<Guid>? PartyIds = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 10. Quests & Narrative (Scripting Core)
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record AcceptQuestCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid QuestId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record SubmitQuestCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid QuestId,
    [property: Key(4)] Dictionary<string, object>? ProofData = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record StartScriptCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid ScriptId,
    [property: Key(4)] int? Version = null,
    [property: Key(5)] Dictionary<string, object>? TriggerContext = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record ChooseDialogueCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid DialogueNodeId,
    [property: Key(4)] int ChoiceIndex,
    [property: Key(5)] Guid ScriptInstanceId)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record TriggerActivityCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid ActivityId,
    [property: Key(4)] Dictionary<string, object>? Parameters = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 11. Achievements / Cosmetics
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record UnlockCosmeticCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid CosmeticId,
    [property: Key(4)] PaymentType? PaymentType = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 12. Misc Support
// ══════════════════════════════════════════════════════════════════════

[MessagePackObject]
public sealed record SendMailCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] Guid RecipientId,
    [property: Key(4)] string Title,
    [property: Key(5)] string Content,
    [property: Key(6)] List<ItemDto>? Attachments = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

[MessagePackObject]
public sealed record RespawnCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] RespawnType RespawnType)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

// ══════════════════════════════════════════════════════════════════════
// 13. Gameplay Mode & Ability System (Server-Authoritative)
// ══════════════════════════════════════════════════════════════════════

/// <summary>请求切换玩法模式。</summary>
[MessagePackObject]
public sealed record ChangeModeCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] GameplayMode TargetMode)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>请求使用技能/功能。</summary>
[MessagePackObject]
public sealed record UseAbilityCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] string AbilityId,
    [property: Key(4)] List<Guid>? TargetIds = null,
    [property: Key(5)] Vector3Dto? GroundTarget = null)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>请求获取指定模式的动作栏配置。</summary>
[MessagePackObject]
public sealed record GetAbilityBarCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp,
    [property: Key(3)] GameplayMode Mode)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);

/// <summary>请求获取全模式技能列表。</summary>
[MessagePackObject]
public sealed record GetAllAbilitiesCommandDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] Guid RequestId,
    [property: Key(2)] DateTime Timestamp)
    : BaseCommandDto(PlayerId, RequestId, Timestamp);
