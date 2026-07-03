namespace Game.Shared.Core.Enums;

public enum Faction
{
    None = 0,
    Alliance,
    Empire,
    Federation,
    Republic
}

public enum CharacterClass
{
    None = 0,
    Warrior,
    Mage,
    Rogue,
    Healer,
    Engineer,
    Pilot
}

public enum ItemRarity
{
    Common = 0,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum CombatState
{
    Idle = 0,
    InCombat,
    Dead,
    Respawning
}

public enum OrderType
{
    Buy = 0,
    Sell
}

public enum ShipClass
{
    Frigate = 0,
    Destroyer,
    Cruiser,
    Battlecruiser,
    Battleship,
    Capital,
    Titan
}

public enum InstanceDifficulty
{
    Normal = 0,
    Heroic,
    Mythic
}

public enum LogoutReason
{
    Normal = 0,
    Timeout,
    Kicked
}

public enum AttributeType
{
    Strength = 0,
    Agility,
    Intelligence,
    Stamina,
    Luck
}

public enum EquipSlot
{
    Head = 0,
    Chest,
    Legs,
    Feet,
    Hands,
    Weapon,
    Shield,
    Accessory
}

public enum FleetFormation
{
    Line = 0,
    Wedge,
    Sphere,
    Wall,
    Claw
}

public enum RouteType
{
    Shortest = 0,
    Safest,
    Fastest
}

public enum ScanType
{
    Probe = 0,
    Radar,
    Directional
}

public enum FleetCommandType
{
    Move = 0,
    Attack,
    Defend,
    Patrol,
    Dock
}

public enum StructureType
{
    Station = 0,
    Citadel,
    Refinery,
    JumpGate,
    Observatory
}

public enum ChatChannel
{
    World = 0,
    Guild,
    Fleet,
    Whisper
}

public enum DifficultyLevel
{
    Easy = 0,
    Normal,
    Hard,
    Nightmare
}

public enum PaymentType
{
    Achievement = 0,
    Currency,
    Premium
}

public enum RespawnType
{
    Nearest = 0,
    Home,
    Insurance
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                 服务端权威 — 玩法模式 & 技能/功能系统                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>玩法模式（服务端权威，客户端仅渲染）。</summary>
public enum GameplayMode : byte
{
    /// <summary>平民模式 — 和平探索、采集、贸易、社会互动</summary>
    Civilian = 0,
    /// <summary>战斗模式 — 人员/载具战斗</summary>
    Combat = 1,
    /// <summary>太空模式 — 火箭发射/飞船控制/军团指挥</summary>
    Space = 2,
}

/// <summary>技能/功能目标类型 — 服务端用于验证技能是否可对目标施放。</summary>
public enum TargetType : byte
{
    /// <summary>自身</summary>
    Self = 0,
    /// <summary>友方单位</summary>
    Friendly = 1,
    /// <summary>敌方单位</summary>
    Enemy = 2,
    /// <summary>中立单位</summary>
    Neutral = 3,
    /// <summary>所有目标</summary>
    All = 4,
    /// <summary>资源节点/采集点</summary>
    ResourceNode = 5,
    /// <summary>地面位置（AOE 落点）</summary>
    Ground = 6,
}

/// <summary>阵营关系 — 服务端派系检查。</summary>
public enum FactionRelation : byte
{
    /// <summary>友好 — 同派系/军团/舰队</summary>
    Friendly = 0,
    /// <summary>敌对 — 对立派系/PvP 标记</summary>
    Hostile = 1,
    /// <summary>中立 — 野生 NPC/无标记玩家</summary>
    Neutral = 2,
}

/// <summary>技能效果区域形状。</summary>
public enum SkillEffectShape : byte
{
    /// <summary>单体</summary>
    Single = 0,
    /// <summary>球形 AoE</summary>
    Sphere = 1,
    /// <summary>锥形射线</summary>
    Cone = 2,
    /// <summary>前方直线</summary>
    Line = 3,
    /// <summary>光环（持续围绕施法者）</summary>
    Aura = 4,
}

/// <summary>技能效果类别。</summary>
public enum SkillEffectCategory : byte
{
    /// <summary>伤害</summary>
    Damage = 0,
    /// <summary>治疗</summary>
    Heal = 1,
    /// <summary>增益 Buff</summary>
    Buff = 2,
    /// <summary>减益 Debuff</summary>
    Debuff = 3,
    /// <summary>控制（眩晕/减速/沉默）</summary>
    CrowdControl = 4,
    /// <summary>移动/传送</summary>
    Movement = 5,
    /// <summary>采集/资源</summary>
    Gather = 6,
    /// <summary>制作/经济</summary>
    Craft = 7,
    /// <summary>社交/辅助</summary>
    Social = 8,
    /// <summary>舰队指挥</summary>
    FleetCommand = 9,
    /// <summary>载具操控</summary>
    VehicleControl = 10,
    /// <summary>护盾/防御</summary>
    Shield = 11,
}

/// <summary>技能归属的玩法模式过滤。</summary>
[Flags]
public enum AbilityModeFlags : byte
{
    None     = 0,
    Civilian = 1 << 0,
    Combat   = 1 << 1,
    Space    = 1 << 2,
    All      = Civilian | Combat | Space,
}

/// <summary>白/黑名单过滤类型。</summary>
public enum ListFilterType : byte
{
    /// <summary>无过滤</summary>
    None = 0,
    /// <summary>白名单 — 仅名单内目标生效</summary>
    Whitelist = 1,
    /// <summary>黑名单 — 名单内目标排除</summary>
    Blacklist = 2,
}
