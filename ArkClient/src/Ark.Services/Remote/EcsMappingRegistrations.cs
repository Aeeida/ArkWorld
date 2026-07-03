// 程序集级 DTO→ECS 组件映射注册：
// 目标 DTO 来自共享协议库（Game.Shared.Core.DTOs），均为 sealed record，无法直接修改。
// 通过 [ExternalDtoMapping] 在客户端侧声明它们到 ECS 组件的字段映射，由 DtoToEcsMapperGenerator 生成
// 强类型 ApplyToEcs/To<TComponent> 扩展，避免在 Flush* 方法中手写大量字段赋值。
//
// 字段重命名约定："DtoField=ComponentField"；右侧留空（"DtoField="）等价于忽略。
// 未列出的字段按大小写不敏感同名匹配。

using Ark.Analyzers.Attributes;
using Ark.Ecs.Components;
using Game.Shared.Core.DTOs;

// ── 库存 ──────────────────────────────────────────────────────────────
// MaxSlots → SlotCount, UsedSlots → OccupiedSlotCount；
// Items / PlayerId 由调用方手动派生 / 忽略。
[assembly: ExternalDtoMapping(
    typeof(InventoryDto),
    typeof(RemoteInventoryState),
    "MaxSlots=SlotCount",
    "UsedSlots=OccupiedSlotCount",
    "Items=",
    "PlayerId=")]

// ── 天气 ──────────────────────────────────────────────────────────────
// WeatherId 同名直接匹配；Intensity → WeatherIntensity；其余字段 ECS 暂不持有。
[assembly: ExternalDtoMapping(
    typeof(WeatherDto),
    typeof(RemoteWorldServiceState),
    "Intensity=WeatherIntensity",
    "WindX=", "WindY=", "WindZ=",
    "FogDensity=", "Temperature=")]

// ── 实体归属 ──────────────────────────────────────────────────────────
// OwnerId / FactionRelation / AccessLevel / ColorPacked 同名匹配；
// GuildId 可空 + HasGuildId 派生，仍由调用方处理；白名单字段 ECS 不存。
[assembly: ExternalDtoMapping(
    typeof(EntityOwnershipDto),
    typeof(RemoteOwnershipState),
    "EntityNetworkId=",
    "GuildId=",
    "WhitelistPlayerIds=",
    "WhitelistGuildIds=")]

// ── 太空飞行 ──────────────────────────────────────────────────────────
// Altitude / OrbitalVelocity / RemainingDeltaV 同名匹配；
// FuelPercent 需 /100 转换、Phase 是 string→enum，由调用方手动应用。
[assembly: ExternalDtoMapping(
    typeof(SpaceFlightStateDto),
    typeof(RemoteSpacecraftState),
    "SpacecraftId=",
    "FuelPercent=",
    "Phase=")]
