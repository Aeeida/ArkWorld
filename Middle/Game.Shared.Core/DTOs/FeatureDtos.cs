using MessagePack;

namespace Game.Shared.Core.DTOs;

// ── Skills ────────────────────────────────────────────────────────────

[MessagePackObject]
public sealed record SkillDto(
    [property: Key(0)] string SkillId,
    [property: Key(1)] string Name,
    [property: Key(2)] int CurrentLevel,
    [property: Key(3)] int TargetLevel,
    [property: Key(4)] DateTime? CompletesAt);

[MessagePackObject]
public sealed record SkillTreeNodeDto(
    [property: Key(0)] string SkillId,
    [property: Key(1)] string Name,
    [property: Key(2)] string Category,
    [property: Key(3)] int MaxLevel,
    [property: Key(4)] IReadOnlyList<string> Prerequisites);

[MessagePackObject]
public sealed record SkillTreeDto(
    [property: Key(0)] IReadOnlyList<SkillTreeNodeDto> Nodes,
    [property: Key(1)] IReadOnlyList<SkillDto> LearnedSkills);

[MessagePackObject]
public sealed record TrainSkillResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string SkillId,
    [property: Key(2)] DateTime? CompletesAt,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record CancelTrainingResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? CancelledSkillId,
    [property: Key(2)] string? ErrorMessage);

// ── Quests ────────────────────────────────────────────────────────────

[MessagePackObject]
public sealed record QuestDto(
    [property: Key(0)] string QuestId,
    [property: Key(1)] string Name,
    [property: Key(2)] string Status,
    [property: Key(3)] IReadOnlyList<QuestObjectiveDto> Objectives,
    [property: Key(4)] int? ChosenBranch);

[MessagePackObject]
public sealed record QuestObjectiveDto(
    [property: Key(0)] string ObjectiveId,
    [property: Key(1)] string Description,
    [property: Key(2)] int CurrentProgress,
    [property: Key(3)] int RequiredProgress);

[MessagePackObject]
public sealed record QuestRewardDto(
    [property: Key(0)] string QuestId,
    [property: Key(1)] long ExperienceReward,
    [property: Key(2)] decimal CurrencyReward,
    [property: Key(3)] IReadOnlyList<string> ItemRewards);

[MessagePackObject]
public sealed record AcceptQuestResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string QuestId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record SubmitQuestResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string QuestId,
    [property: Key(2)] QuestRewardDto? Rewards,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record QuestProgressDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] IReadOnlyList<QuestDto> ActiveQuests,
    [property: Key(2)] int CompletedCount,
    [property: Key(3)] int AbandonedCount);

// ── Crafting ──────────────────────────────────────────────────────────

[MessagePackObject]
public sealed record CraftingJobDto(
    [property: Key(0)] Guid JobId,
    [property: Key(1)] string BlueprintId,
    [property: Key(2)] int Quantity,
    [property: Key(3)] string Status,
    [property: Key(4)] DateTime CompletesAt);

[MessagePackObject]
public sealed record BlueprintDto(
    [property: Key(0)] string BlueprintId,
    [property: Key(1)] string Name,
    [property: Key(2)] string OutputItemId,
    [property: Key(3)] int OutputQuantity,
    [property: Key(4)] IReadOnlyList<BlueprintMaterialDto> Materials,
    [property: Key(5)] int CraftingTimeSeconds);

[MessagePackObject]
public sealed record BlueprintMaterialDto(
    [property: Key(0)] string ItemId,
    [property: Key(1)] int Quantity);

[MessagePackObject]
public sealed record StartCraftResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? JobId,
    [property: Key(2)] DateTime? CompletesAt,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record CraftingQueueDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] IReadOnlyList<CraftingJobDto> Jobs,
    [property: Key(2)] int MaxConcurrentJobs);

// ── Exploration ───────────────────────────────────────────────────────

[MessagePackObject]
public sealed record ScanResultDto(
    [property: Key(0)] string SolarSystemId,
    [property: Key(1)] int PlayerCount,
    [property: Key(2)] IReadOnlyList<ExplorationSiteDto> Sites,
    [property: Key(3)] IReadOnlyList<string> AnomalyIds);

[MessagePackObject]
public sealed record ExplorationSiteDto(
    [property: Key(0)] string SiteId,
    [property: Key(1)] string Type,
    [property: Key(2)] string Difficulty,
    [property: Key(3)] double X,
    [property: Key(4)] double Y,
    [property: Key(5)] double Z);

[MessagePackObject]
public sealed record HarvestResultDto(
    [property: Key(0)] string ResourceNodeId,
    [property: Key(1)] string ResourceType,
    [property: Key(2)] int QuantityHarvested,
    [property: Key(3)] bool NodeDepleted);

// ── Sovereignty ───────────────────────────────────────────────────────

[MessagePackObject]
public sealed record SovereigntyDto(
    [property: Key(0)] string SolarSystemId,
    [property: Key(1)] Guid OwnerAllianceId,
    [property: Key(2)] DateTime ClaimedAt,
    [property: Key(3)] string Status);

[MessagePackObject]
public sealed record SovereigntyContestDto(
    [property: Key(0)] Guid ContestId,
    [property: Key(1)] string SolarSystemId,
    [property: Key(2)] Guid AttackingAllianceId,
    [property: Key(3)] string Status,
    [property: Key(4)] DateTime EndsAt);

[MessagePackObject]
public sealed record StructureDto(
    [property: Key(0)] Guid StructureId,
    [property: Key(1)] string StructureType,
    [property: Key(2)] string SolarSystemId,
    [property: Key(3)] Guid OwnerAllianceId,
    [property: Key(4)] double HealthPercent,
    [property: Key(5)] string Status);

[MessagePackObject]
public sealed record ClaimSovereigntyResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string SolarSystemId,
    [property: Key(2)] Guid AllianceId,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record BuildStructureResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? StructureId,
    [property: Key(2)] string? StructureType,
    [property: Key(3)] string? ErrorMessage);

// ── Achievements ──────────────────────────────────────────────────────

[MessagePackObject]
public sealed record AchievementDto(
    [property: Key(0)] string AchievementId,
    [property: Key(1)] string Name,
    [property: Key(2)] string Status,
    [property: Key(3)] DateTime? UnlockedAt,
    [property: Key(4)] int Points);

[MessagePackObject]
public sealed record TitleDto(
    [property: Key(0)] string TitleId,
    [property: Key(1)] string Name,
    [property: Key(2)] bool IsActive);

[MessagePackObject]
public sealed record CollectionProgressDto(
    [property: Key(0)] int TotalAchievements,
    [property: Key(1)] int UnlockedAchievements,
    [property: Key(2)] int TotalTitles,
    [property: Key(3)] int UnlockedTitles);

[MessagePackObject]
public sealed record AchievementProgressDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] IReadOnlyList<AchievementDto> Achievements,
    [property: Key(2)] int TotalPoints,
    [property: Key(3)] int UnlockedCount);

[MessagePackObject]
public sealed record UnlockCosmeticResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? CosmeticId,
    [property: Key(2)] string? ErrorMessage);

// ── Gameplay Mode & Abilities (Server-Authoritative) ─────────────────

/// <summary>技能/功能定义 — 由服务端下发给客户端渲染动作栏。</summary>
[MessagePackObject]
public sealed record AbilityDefinitionDto(
    [property: Key(0)]  string AbilityId,
    [property: Key(1)]  string Name,
    [property: Key(2)]  string Icon,
    [property: Key(3)]  string Description,
    [property: Key(4)]  byte   Mode,              // AbilityModeFlags
    [property: Key(5)]  byte   TargetType,         // TargetType enum
    [property: Key(6)]  byte   EffectCategory,     // SkillEffectCategory
    [property: Key(7)]  byte   EffectShape,        // SkillEffectShape
    [property: Key(8)]  float  Range,
    [property: Key(9)]  float  AoERadius,
    [property: Key(10)] float  CooldownSeconds,
    [property: Key(11)] float  CastTimeSeconds,
    [property: Key(12)] float  EffectValue,        // damage/heal amount or buff %
    [property: Key(13)] float  DurationSeconds,    // buff/debuff/aura duration
    [property: Key(14)] int    ResourceCost,       // mana/energy/fuel/ammo
    [property: Key(15)] byte   FactionFilter,      // FactionRelation: FriendlyOnly/EnemyOnly/NeutralAllowed
    [property: Key(16)] byte   ListFilterType,     // ListFilterType
    [property: Key(17)] IReadOnlyList<string>? ListFilterIds); // Whitelist/Blacklist entity IDs

/// <summary>单个动作栏槽位。</summary>
[MessagePackObject]
public sealed record ActionBarSlotDto(
    [property: Key(0)] int    SlotIndex,
    [property: Key(1)] string AbilityId,
    [property: Key(2)] string Hotkey);

/// <summary>某个模式的完整动作栏配置。</summary>
[MessagePackObject]
public sealed record ModeActionBarDto(
    [property: Key(0)] byte Mode,  // GameplayMode
    [property: Key(1)] IReadOnlyList<ActionBarSlotDto> PrimaryBar,
    [property: Key(2)] IReadOnlyList<ActionBarSlotDto>? SecondaryBar,
    [property: Key(3)] IReadOnlyList<ActionBarSlotDto>? VehicleBar);

/// <summary>服务端下发的全模式技能快捷栏总配置。</summary>
[MessagePackObject]
public sealed record AbilityBarSyncDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] IReadOnlyList<AbilityDefinitionDto> Abilities,
    [property: Key(2)] IReadOnlyList<ModeActionBarDto> ActionBars);

/// <summary>模式切换请求结果。</summary>
[MessagePackObject]
public sealed record ModeChangeResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] byte NewMode,  // GameplayMode
    [property: Key(2)] ModeActionBarDto? ActionBar,
    [property: Key(3)] string? ErrorMessage);

/// <summary>技能/功能使用结果。</summary>
[MessagePackObject]
public sealed record AbilityUseResultDto(
    [property: Key(0)] bool   Success,
    [property: Key(1)] string AbilityId,
    [property: Key(2)] float  EffectValue,
    [property: Key(3)] float  CooldownRemaining,
    [property: Key(4)] IReadOnlyList<AbilityTargetHitDto>? Hits,
    [property: Key(5)] string? ErrorMessage);

/// <summary>技能命中单个目标结果。</summary>
[MessagePackObject]
public sealed record AbilityTargetHitDto(
    [property: Key(0)] Guid   TargetId,
    [property: Key(1)] float  EffectApplied,
    [property: Key(2)] bool   IsKill,
    [property: Key(3)] string TargetName);

/// <summary>目标信息帧 — 服务端推送给客户端显示目标 UI。</summary>
[MessagePackObject]
public sealed record TargetFrameDto(
    [property: Key(0)] Guid   EntityId,
    [property: Key(1)] string Name,
    [property: Key(2)] float  HealthCurrent,
    [property: Key(3)] float  HealthMax,
    [property: Key(4)] float  ShieldCurrent,
    [property: Key(5)] float  ShieldMax,
    [property: Key(6)] byte   FactionRelation,  // FactionRelation
    [property: Key(7)] float  Distance);

/// <summary>队伍/团队成员状态帧。</summary>
[MessagePackObject]
public sealed record PartyMemberFrameDto(
    [property: Key(0)] Guid   PlayerId,
    [property: Key(1)] string Name,
    [property: Key(2)] float  HealthCurrent,
    [property: Key(3)] float  HealthMax,
    [property: Key(4)] float  ShieldCurrent,
    [property: Key(5)] float  ShieldMax,
    [property: Key(6)] IReadOnlyList<string>? ActiveBuffs,
    [property: Key(7)] IReadOnlyList<string>? ActiveDebuffs,
    [property: Key(8)] bool   IsOnline);

/// <summary>载具覆盖 HUD 数据帧。</summary>
[MessagePackObject]
public sealed record VehicleHudFrameDto(
    [property: Key(0)] Guid   VehicleId,
    [property: Key(1)] string VehicleName,
    [property: Key(2)] float  Speed,
    [property: Key(3)] float  MaxSpeed,
    [property: Key(4)] float  HealthCurrent,
    [property: Key(5)] float  HealthMax,
    [property: Key(6)] float  FuelCurrent,
    [property: Key(7)] float  FuelMax,
    [property: Key(8)] int    AmmoCount,
    [property: Key(9)] int    AmmoMax);

/// <summary>太空飞船 HUD 数据帧。</summary>
[MessagePackObject]
public sealed record SpaceshipHudFrameDto(
    [property: Key(0)] Guid   ShipId,
    [property: Key(1)] string ShipName,
    [property: Key(2)] float  HullCurrent,
    [property: Key(3)] float  HullMax,
    [property: Key(4)] float  ShieldFront,
    [property: Key(5)] float  ShieldRear,
    [property: Key(6)] float  ShieldLeft,
    [property: Key(7)] float  ShieldRight,
    [property: Key(8)] float  ShieldMax,
    [property: Key(9)] float  CapacitorCurrent,
    [property: Key(10)] float CapacitorMax,
    [property: Key(11)] float Speed,
    [property: Key(12)] float MaxSpeed);

/// <summary>舰队指挥覆盖帧。</summary>
[MessagePackObject]
public sealed record FleetCommandFrameDto(
    [property: Key(0)] Guid   FleetId,
    [property: Key(1)] string FleetName,
    [property: Key(2)] int    MemberCount,
    [property: Key(3)] IReadOnlyList<FleetMemberStatusDto> Members);

/// <summary>舰队成员状态。</summary>
[MessagePackObject]
public sealed record FleetMemberStatusDto(
    [property: Key(0)] Guid   PlayerId,
    [property: Key(1)] string Name,
    [property: Key(2)] string ShipType,
    [property: Key(3)] float  HealthPercent,
    [property: Key(4)] float  ShieldPercent);
