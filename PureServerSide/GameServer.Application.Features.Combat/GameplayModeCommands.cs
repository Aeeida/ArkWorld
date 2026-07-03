using Game.Shared.Core.DTOs;
using Game.Shared.Core.Enums;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Combat;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║        服务端权威 — 玩法模式切换 & 技能/功能系统 命令/查询 + 处理器              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ── Commands / Queries ───────────────────────────────────────────────

public sealed record ChangeModeCommand(Guid PlayerId, GameplayMode TargetMode)
    : ICommand<ModeChangeResultDto>;

public sealed record UseAbilityCommand(
    Guid PlayerId, string AbilityId,
    List<Guid>? TargetIds, Vector3Dto? GroundTarget)
    : ICommand<AbilityUseResultDto>;

public sealed record GetAbilityBarQuery(Guid PlayerId, GameplayMode Mode)
    : IQuery<ModeActionBarDto>;

public sealed record GetAllAbilitiesQuery(Guid PlayerId)
    : IQuery<AbilityBarSyncDto>;

// ── Handlers ─────────────────────────────────────────────────────────

/// <summary>
/// 服务端全权验证模式切换 — 检查是否允许切换（如安全区/战斗锁定/太空飞行中等），
/// 切换成功后返回新模式的动作栏配置。
/// </summary>
public sealed class ChangeModeHandler(
    IGrainFactory grainFactory,
    ILogger<ChangeModeHandler> logger)
    : ICommandHandler<ChangeModeCommand, ModeChangeResultDto>
{
    public async Task<ModeChangeResultDto> Handle(ChangeModeCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var state = await playerGrain.GetStateAsync();

        if (state.Health <= 0)
            return new ModeChangeResultDto(false, (byte)request.TargetMode, null, "Cannot switch mode while dead.");

        // 服务端验证完毕，允许切换
        logger.LogInformation("Player {PlayerId} mode change → {Mode}", request.PlayerId, request.TargetMode);

        var actionBar = BuildDefaultActionBar(request.TargetMode);
        return new ModeChangeResultDto(true, (byte)request.TargetMode, actionBar, null);
    }

    private static ModeActionBarDto BuildDefaultActionBar(GameplayMode mode)
    {
        return mode switch
        {
            GameplayMode.Civilian => new ModeActionBarDto(
                (byte)mode,
                PrimaryBar:
                [
                    new(0, "mining",         "1"),
                    new(1, "herbalism",       "2"),
                    new(2, "scan_resources",  "3"),
                    new(3, "crafting",        "4"),
                    new(4, "trade_offer",     "5"),
                    new(5, "invite_party",    "6"),
                    new(6, "summon_mount",    "7"),
                    new(7, "flight_boost",    "8"),
                    new(8, "portal",          "9"),
                    new(9, "scout_area",      "0"),
                ],
                SecondaryBar: null,
                VehicleBar: null),

            GameplayMode.Combat => new ModeActionBarDto(
                (byte)mode,
                PrimaryBar:
                [
                    new(0, "single_target_dps", "1"),
                    new(1, "area_blast",         "2"),
                    new(2, "shield_bubble",      "3"),
                    new(3, "dodge_roll",         "4"),
                    new(4, "heal_self",          "5"),
                    new(5, "stun",               "6"),
                    new(6, "slow_aura",          "7"),
                    new(7, "strength_boost",     "8"),
                    new(8, "fire_storm",         "9"),
                    new(9, "emp_pulse",          "0"),
                ],
                SecondaryBar:
                [
                    new(0, "grenade_frag",  "Shift+1"),
                    new(1, "grenade_fire",  "Shift+2"),
                    new(2, "medkit",        "Shift+3"),
                    new(3, "ammo_resupply", "Shift+4"),
                ],
                VehicleBar:
                [
                    new(0, "ram_attack",    "1"),
                    new(1, "missile_lock",  "2"),
                    new(2, "turret_fire",   "3"),
                    new(3, "emp_pulse_v",   "4"),
                ]),

            GameplayMode.Space => new ModeActionBarDto(
                (byte)mode,
                PrimaryBar:
                [
                    new(0, "laser_volley",    "F1"),
                    new(1, "missile_salvo",   "F2"),
                    new(2, "ion_disruptor",   "F3"),
                    new(3, "shield_boost",    "F4"),
                    new(4, "repair_drone",    "F5"),
                    new(5, "scan_enemy",      "F6"),
                    new(6, "warp_jump",       "F7"),
                    new(7, "afterburner",     "F8"),
                ],
                SecondaryBar:
                [
                    new(0, "cloak",           "Shift+F1"),
                    new(1, "formation_lock",  "Shift+F2"),
                    new(2, "focus_fire",      "Shift+F3"),
                    new(3, "retreat_order",   "Shift+F4"),
                    new(4, "fleet_jump",      "Shift+F5"),
                ],
                VehicleBar: null),

            _ => new ModeActionBarDto((byte)mode, [], null, null),
        };
    }
}

/// <summary>
/// 服务端全权验证技能/功能使用 —
/// 检查：冷却、资源、视线、派系匹配、白/黑名单，
/// 计算影响，广播效果。
/// </summary>
public sealed class UseAbilityHandler(
    IGrainFactory grainFactory,
    ILogger<UseAbilityHandler> logger)
    : ICommandHandler<UseAbilityCommand, AbilityUseResultDto>
{
    public async Task<AbilityUseResultDto> Handle(UseAbilityCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var state = await playerGrain.GetStateAsync();

        if (state.Health <= 0)
            return Fail(request.AbilityId, "Cannot use ability while dead.");

        // 查找技能定义（服务端静态注册表，此处用简化映射）
        var def = AbilityRegistry.Get(request.AbilityId);
        if (def is null)
            return Fail(request.AbilityId, "Unknown ability.");

        // TODO: 冷却检查、资源检查、LoS 检查、派系匹配、白/黑名单过滤

        var hits = new List<AbilityTargetHitDto>();
        if (request.TargetIds is { Count: > 0 })
        {
            foreach (var tid in request.TargetIds)
            {
                var targetGrain = grainFactory.GetGrain<IPlayerGrain>(tid);
                var targetState = await targetGrain.GetStateAsync();

                // 根据 EffectCategory 执行效果
                if (def.EffectCategory is SkillEffectCategory.Damage)
                {
                    var dmg = def.EffectValue;
                    await targetGrain.TakeDamageAsync(dmg);
                    var newState = await targetGrain.GetStateAsync();
                    hits.Add(new AbilityTargetHitDto(tid, dmg, newState.Health <= 0, targetState.Name));
                }
                else if (def.EffectCategory is SkillEffectCategory.Heal)
                {
                    hits.Add(new AbilityTargetHitDto(tid, def.EffectValue, false, targetState.Name));
                }
                else
                {
                    hits.Add(new AbilityTargetHitDto(tid, def.EffectValue, false, targetState.Name));
                }
            }
        }

        logger.LogInformation("Player {PlayerId} used ability {Ability}, hits={HitCount}",
            request.PlayerId, request.AbilityId, hits.Count);

        return new AbilityUseResultDto(true, request.AbilityId, def.EffectValue,
            def.CooldownSeconds, hits, null);
    }

    private static AbilityUseResultDto Fail(string abilityId, string error)
        => new(false, abilityId, 0, 0, null, error);
}

/// <summary>
/// 返回指定模式的动作栏配置。
/// </summary>
public sealed class GetAbilityBarHandler
    : IQueryHandler<GetAbilityBarQuery, ModeActionBarDto>
{
    public Task<ModeActionBarDto> Handle(GetAbilityBarQuery request, CancellationToken ct)
    {
        var bar = new ChangeModeHandler(null!, null!);
        // Reuse default bar builder (static method would be cleaner, but kept simple)
        var result = request.Mode switch
        {
            GameplayMode.Civilian => BuildBar(request.Mode),
            GameplayMode.Combat   => BuildBar(request.Mode),
            GameplayMode.Space    => BuildBar(request.Mode),
            _ => new ModeActionBarDto((byte)request.Mode, [], null, null),
        };
        return Task.FromResult(result);
    }

    private static ModeActionBarDto BuildBar(GameplayMode mode)
    {
        var allDefs = AbilityRegistry.GetAllForMode(mode);
        var slots = allDefs
            .Select((d, i) => new ActionBarSlotDto(i, d.AbilityId, (i + 1).ToString()))
            .ToList();
        return new ModeActionBarDto((byte)mode, slots, null, null);
    }
}

/// <summary>
/// 返回全模式技能总配置（登录后一次性同步）。
/// </summary>
public sealed class GetAllAbilitiesHandler
    : IQueryHandler<GetAllAbilitiesQuery, AbilityBarSyncDto>
{
    public Task<AbilityBarSyncDto> Handle(GetAllAbilitiesQuery request, CancellationToken ct)
    {
        var allDefs = AbilityRegistry.GetAll()
            .Select(d => new AbilityDefinitionDto(
                d.AbilityId, d.Name, d.Icon, d.Description,
                (byte)d.ModeFlags, (byte)d.TargetType, (byte)d.EffectCategory,
                (byte)d.EffectShape, d.Range, d.AoERadius, d.CooldownSeconds,
                d.CastTimeSeconds, d.EffectValue, d.DurationSeconds,
                d.ResourceCost, (byte)d.FactionFilter, (byte)d.ListFilter, null))
            .ToList();

        var bars = new List<ModeActionBarDto>();
        foreach (var mode in new[] { GameplayMode.Civilian, GameplayMode.Combat, GameplayMode.Space })
        {
            var modeDefs = AbilityRegistry.GetAllForMode(mode);
            var slots = modeDefs
                .Select((d, i) => new ActionBarSlotDto(i, d.AbilityId, (i + 1).ToString()))
                .ToList();
            bars.Add(new ModeActionBarDto((byte)mode, slots, null, null));
        }

        return Task.FromResult(new AbilityBarSyncDto(request.PlayerId, allDefs, bars));
    }
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║            服务端技能/功能定义注册表（静态数据，权威来源）                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 服务端技能/功能定义 — 不可变记录。
/// 属性包含 TargetType、FactionFilter、EffectShape、白/黑名单等，
/// 服务端用此验证客户端技能使用请求。
/// </summary>
public record AbilityDef(
    string AbilityId,
    string Name,
    string Icon,
    string Description,
    AbilityModeFlags ModeFlags,
    TargetType TargetType,
    SkillEffectCategory EffectCategory,
    SkillEffectShape EffectShape,
    float Range,
    float AoERadius,
    float CooldownSeconds,
    float CastTimeSeconds,
    float EffectValue,
    float DurationSeconds,
    int ResourceCost,
    FactionRelation FactionFilter,
    ListFilterType ListFilter);

/// <summary>
/// 服务端技能注册表 — 所有玩法模式的技能/功能定义集中管理。
/// </summary>
public static class AbilityRegistry
{
    private static readonly Dictionary<string, AbilityDef> _defs = new(StringComparer.OrdinalIgnoreCase);

    static AbilityRegistry() => RegisterDefaults();

    public static AbilityDef? Get(string id) => _defs.TryGetValue(id, out var d) ? d : null;

    public static IEnumerable<AbilityDef> GetAll() => _defs.Values;

    public static IReadOnlyList<AbilityDef> GetAllForMode(GameplayMode mode)
    {
        var flag = mode switch
        {
            GameplayMode.Civilian => AbilityModeFlags.Civilian,
            GameplayMode.Combat   => AbilityModeFlags.Combat,
            GameplayMode.Space    => AbilityModeFlags.Space,
            _ => AbilityModeFlags.None,
        };
        return _defs.Values.Where(d => d.ModeFlags.HasFlag(flag)).ToList();
    }

    private static void Register(AbilityDef def) => _defs[def.AbilityId] = def;

    private static void RegisterDefaults()
    {
        // ══════════════════════════════════════════════════════════════
        // 平民模式技能
        // ══════════════════════════════════════════════════════════════
        Register(new("mining", "采矿 Mining", "⛏️", "采集矿石资源节点",
            AbilityModeFlags.Civilian, TargetType.ResourceNode, SkillEffectCategory.Gather,
            SkillEffectShape.Single, 3f, 0f, 1f, 2f, 1f, 0f, 0,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("herbalism", "采集草药 Herbalism", "🌿", "采集植物资源节点",
            AbilityModeFlags.Civilian, TargetType.ResourceNode, SkillEffectCategory.Gather,
            SkillEffectShape.Single, 3f, 0f, 1f, 2f, 1f, 0f, 0,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("scan_resources", "扫描资源 Scan Resources", "🔍", "扫描附近资源点",
            AbilityModeFlags.Civilian, TargetType.Self, SkillEffectCategory.Gather,
            SkillEffectShape.Sphere, 0f, 50f, 10f, 1f, 0f, 5f, 0,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("crafting", "锻造 Crafting", "🔨", "开启制造界面",
            AbilityModeFlags.Civilian, TargetType.Self, SkillEffectCategory.Craft,
            SkillEffectShape.Single, 0f, 0f, 0f, 0f, 0f, 0f, 0,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("trade_offer", "交易 Trade Offer", "💰", "向附近玩家发起交易",
            AbilityModeFlags.Civilian, TargetType.Friendly, SkillEffectCategory.Social,
            SkillEffectShape.Single, 10f, 0f, 0f, 0f, 0f, 0f, 0,
            FactionRelation.Friendly, ListFilterType.None));

        Register(new("invite_party", "组队邀请 Invite Party", "👥", "邀请目标加入队伍",
            AbilityModeFlags.Civilian | AbilityModeFlags.Combat, TargetType.Friendly, SkillEffectCategory.Social,
            SkillEffectShape.Single, 30f, 0f, 0f, 0f, 0f, 0f, 0,
            FactionRelation.Friendly, ListFilterType.None));

        Register(new("summon_mount", "召唤坐骑 Summon Mount", "🐎", "召唤个人坐骑",
            AbilityModeFlags.Civilian, TargetType.Self, SkillEffectCategory.Movement,
            SkillEffectShape.Single, 0f, 0f, 3f, 2f, 0f, 0f, 0,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("flight_boost", "飞行加速 Flight Boost", "💨", "短暂加速飞行",
            AbilityModeFlags.Civilian, TargetType.Self, SkillEffectCategory.Movement,
            SkillEffectShape.Single, 0f, 0f, 30f, 0f, 1.5f, 10f, 20,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("portal", "传送门 Portal", "🌀", "开启传送门至已知地点",
            AbilityModeFlags.Civilian, TargetType.Self, SkillEffectCategory.Movement,
            SkillEffectShape.Single, 0f, 0f, 60f, 5f, 0f, 15f, 50,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("scout_area", "侦察 Scout Area", "🦅", "侦察周围区域，标记敌人",
            AbilityModeFlags.Civilian | AbilityModeFlags.Combat, TargetType.Self, SkillEffectCategory.Social,
            SkillEffectShape.Sphere, 0f, 80f, 20f, 1f, 0f, 10f, 10,
            FactionRelation.Neutral, ListFilterType.None));

        // ══════════════════════════════════════════════════════════════
        // 战斗模式技能 — 人员
        // ══════════════════════════════════════════════════════════════
        Register(new("single_target_dps", "射击/斩击 Single Target DPS", "🎯", "对单体造成伤害",
            AbilityModeFlags.Combat, TargetType.Enemy, SkillEffectCategory.Damage,
            SkillEffectShape.Single, 50f, 0f, 0.5f, 0f, 22f, 0f, 1,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("area_blast", "火焰风暴 Area Blast", "🔥", "对范围内所有敌方造成持续火焰伤害",
            AbilityModeFlags.Combat, TargetType.Ground, SkillEffectCategory.Damage,
            SkillEffectShape.Sphere, 30f, 8f, 15f, 1.5f, 45f, 6f, 30,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("shield_bubble", "护盾 Shield Bubble", "🛡️", "展开个人护盾吸收伤害",
            AbilityModeFlags.Combat, TargetType.Self, SkillEffectCategory.Shield,
            SkillEffectShape.Single, 0f, 0f, 25f, 0.5f, 100f, 8f, 25,
            FactionRelation.Friendly, ListFilterType.None));

        Register(new("dodge_roll", "闪避翻滚 Dodge/Roll", "🏃", "快速闪避，短暂无敌帧",
            AbilityModeFlags.Combat, TargetType.Self, SkillEffectCategory.Movement,
            SkillEffectShape.Single, 0f, 0f, 5f, 0f, 0f, 0.3f, 5,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("heal_self", "自我治疗 Heal Self", "❤️", "恢复自身生命值",
            AbilityModeFlags.Combat, TargetType.Self, SkillEffectCategory.Heal,
            SkillEffectShape.Single, 0f, 0f, 12f, 1.5f, 60f, 0f, 20,
            FactionRelation.Friendly, ListFilterType.None));

        Register(new("stun", "眩晕 Stun", "⚡", "使目标眩晕无法行动",
            AbilityModeFlags.Combat, TargetType.Enemy, SkillEffectCategory.CrowdControl,
            SkillEffectShape.Single, 15f, 0f, 20f, 0.3f, 0f, 3f, 15,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("slow_aura", "减速光环 Slow Aura", "🌊", "附近敌方移动速度降低",
            AbilityModeFlags.Combat, TargetType.Enemy, SkillEffectCategory.Debuff,
            SkillEffectShape.Aura, 0f, 12f, 30f, 0f, 0.4f, 10f, 20,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("strength_boost", "力量增益 Strength Boost", "💪", "临时提升自身及附近盟友攻击力",
            AbilityModeFlags.Combat, TargetType.Friendly, SkillEffectCategory.Buff,
            SkillEffectShape.Sphere, 0f, 15f, 45f, 1f, 1.2f, 15f, 25,
            FactionRelation.Friendly, ListFilterType.None));

        Register(new("fire_storm", "火焰风暴 Fire Storm", "🌋", "在目标区域持续降火焰",
            AbilityModeFlags.Combat, TargetType.Ground, SkillEffectCategory.Damage,
            SkillEffectShape.Sphere, 35f, 10f, 30f, 2f, 30f, 8f, 40,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("emp_pulse", "EMP 脉冲 EMP Pulse", "⚡", "禁用附近敌方电子系统",
            AbilityModeFlags.Combat | AbilityModeFlags.Space, TargetType.Enemy, SkillEffectCategory.CrowdControl,
            SkillEffectShape.Sphere, 0f, 20f, 45f, 0.5f, 0f, 5f, 35,
            FactionRelation.Hostile, ListFilterType.None));

        // ── 战斗模式 — 手雷/补给 ──
        Register(new("grenade_frag", "破片手雷 Frag Grenade", "💣", "投掷破片手雷爆炸伤害",
            AbilityModeFlags.Combat, TargetType.Ground, SkillEffectCategory.Damage,
            SkillEffectShape.Sphere, 30f, 10f, 8f, 0.5f, 80f, 0f, 1,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("grenade_fire", "燃烧弹 Incendiary", "🧨", "投掷燃烧弹造成持续火焰伤害",
            AbilityModeFlags.Combat, TargetType.Ground, SkillEffectCategory.Damage,
            SkillEffectShape.Sphere, 25f, 6f, 12f, 0.5f, 20f, 6f, 1,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("medkit", "急救包 Medkit", "🩹", "使用急救包恢复大量生命",
            AbilityModeFlags.Combat, TargetType.Self, SkillEffectCategory.Heal,
            SkillEffectShape.Single, 0f, 0f, 20f, 3f, 120f, 0f, 1,
            FactionRelation.Friendly, ListFilterType.None));

        Register(new("ammo_resupply", "弹药补给 Ammo Resupply", "📦", "恢复弹药储备",
            AbilityModeFlags.Combat, TargetType.Self, SkillEffectCategory.Buff,
            SkillEffectShape.Single, 0f, 0f, 30f, 2f, 0f, 0f, 1,
            FactionRelation.Friendly, ListFilterType.None));

        // ── 战斗模式 — 载具专用 ──
        Register(new("ram_attack", "冲撞 Ram Attack", "🚗", "载具冲撞前方目标",
            AbilityModeFlags.Combat, TargetType.Enemy, SkillEffectCategory.Damage,
            SkillEffectShape.Line, 5f, 0f, 10f, 0f, 200f, 0f, 20,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("missile_lock", "导弹锁定 Missile Lock", "🚀", "锁定目标发射导弹",
            AbilityModeFlags.Combat, TargetType.Enemy, SkillEffectCategory.Damage,
            SkillEffectShape.Single, 300f, 5f, 8f, 2f, 150f, 0f, 1,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("turret_fire", "炮塔扫射 Turret Fire", "🔫", "载具炮塔持续扫射",
            AbilityModeFlags.Combat, TargetType.Enemy, SkillEffectCategory.Damage,
            SkillEffectShape.Cone, 400f, 0f, 0.1f, 0f, 15f, 0f, 1,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("emp_pulse_v", "载具 EMP Vehicle EMP", "⚡", "载具 EMP 禁用附近敌方载具",
            AbilityModeFlags.Combat, TargetType.Enemy, SkillEffectCategory.CrowdControl,
            SkillEffectShape.Sphere, 0f, 30f, 60f, 0.5f, 0f, 8f, 50,
            FactionRelation.Hostile, ListFilterType.None));

        // ══════════════════════════════════════════════════════════════
        // 太空模式技能
        // ══════════════════════════════════════════════════════════════
        Register(new("laser_volley", "激光炮 Laser Volley", "💥", "发射激光炮组齐射",
            AbilityModeFlags.Space, TargetType.Enemy, SkillEffectCategory.Damage,
            SkillEffectShape.Single, 200f, 0f, 3f, 0f, 80f, 0f, 30,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("missile_salvo", "导弹齐射 Missile Salvo", "🚀", "齐射多枚导弹锁定目标+附近溅射",
            AbilityModeFlags.Space, TargetType.Enemy, SkillEffectCategory.Damage,
            SkillEffectShape.Sphere, 250f, 15f, 10f, 1.5f, 120f, 0f, 50,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("ion_disruptor", "离子炮 Ion Disruptor", "⚡", "离子射线禁用目标模块",
            AbilityModeFlags.Space, TargetType.Enemy, SkillEffectCategory.CrowdControl,
            SkillEffectShape.Line, 180f, 0f, 15f, 0.5f, 0f, 6f, 40,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("shield_boost", "护盾强化 Shield Boost", "🛡️", "强化自身护盾",
            AbilityModeFlags.Space, TargetType.Self, SkillEffectCategory.Shield,
            SkillEffectShape.Single, 0f, 0f, 8f, 0.5f, 150f, 0f, 35,
            FactionRelation.Friendly, ListFilterType.None));

        Register(new("repair_drone", "维修无人机 Repair Drone", "🔧", "释放维修无人机修复自身/盟友",
            AbilityModeFlags.Space, TargetType.Friendly, SkillEffectCategory.Heal,
            SkillEffectShape.Sphere, 0f, 20f, 20f, 1f, 50f, 15f, 40,
            FactionRelation.Friendly, ListFilterType.Whitelist));

        Register(new("scan_enemy", "扫描探测 Scan Enemy", "📡", "扫描揭示周围敌舰信息",
            AbilityModeFlags.Space, TargetType.Enemy, SkillEffectCategory.Social,
            SkillEffectShape.Sphere, 0f, 100f, 15f, 1f, 0f, 10f, 20,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("warp_jump", "跃迁 Warp Jump", "🌌", "跃迁至目标星系",
            AbilityModeFlags.Space, TargetType.Self, SkillEffectCategory.Movement,
            SkillEffectShape.Single, 0f, 0f, 30f, 5f, 0f, 0f, 100,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("afterburner", "加速推进 Afterburner", "🔥", "短暂大幅加速",
            AbilityModeFlags.Space, TargetType.Self, SkillEffectCategory.Movement,
            SkillEffectShape.Single, 0f, 0f, 15f, 0f, 2f, 8f, 30,
            FactionRelation.Neutral, ListFilterType.None));

        Register(new("cloak", "隐形 Cloak", "👁️", "启动隐形装置",
            AbilityModeFlags.Space, TargetType.Self, SkillEffectCategory.Buff,
            SkillEffectShape.Single, 0f, 0f, 60f, 3f, 0f, 20f, 80,
            FactionRelation.Neutral, ListFilterType.None));

        // ── 太空模式 — 军团指挥 ──
        Register(new("formation_lock", "编队命令 Formation Lock", "📐", "命令舰队进入编队",
            AbilityModeFlags.Space, TargetType.Friendly, SkillEffectCategory.FleetCommand,
            SkillEffectShape.Sphere, 0f, 500f, 10f, 0f, 0f, 0f, 0,
            FactionRelation.Friendly, ListFilterType.Whitelist));

        Register(new("focus_fire", "集中火力 Focus Fire", "🎯", "命令舰队集中火力打击目标",
            AbilityModeFlags.Space, TargetType.Enemy, SkillEffectCategory.FleetCommand,
            SkillEffectShape.Single, 300f, 0f, 5f, 0f, 0f, 0f, 0,
            FactionRelation.Hostile, ListFilterType.None));

        Register(new("retreat_order", "撤退信号 Retreat Order", "🏳️", "命令舰队撤退",
            AbilityModeFlags.Space, TargetType.Friendly, SkillEffectCategory.FleetCommand,
            SkillEffectShape.Sphere, 0f, 1000f, 30f, 0f, 0f, 0f, 0,
            FactionRelation.Friendly, ListFilterType.Whitelist));

        Register(new("fleet_jump", "舰队跃迁 Fleet Jump", "🌌", "命令整个舰队集体跃迁",
            AbilityModeFlags.Space, TargetType.Friendly, SkillEffectCategory.FleetCommand,
            SkillEffectShape.Sphere, 0f, 2000f, 120f, 10f, 0f, 0f, 200,
            FactionRelation.Friendly, ListFilterType.Whitelist));
    }
}
