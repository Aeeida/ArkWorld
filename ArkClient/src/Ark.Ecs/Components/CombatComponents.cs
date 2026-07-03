using System.Runtime.InteropServices;
using Ark.Analyzers.Attributes;
using Friflo.Engine.ECS;

namespace Ark.Ecs.Components;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        战斗系统 ECS 组件                                       ║
// ║  分层设计：                                                                    ║
// ║    ① 属性层 — 静态/准静态数值（Health, Armor, CombatStats）                      ║
// ║    ② 状态层 — 每帧可变的运行时数据（WeaponState, AmmoState）                     ║
// ║    ③ 事件层 — 单帧消费的缓冲数据（DamageBuffer, HitEvent）                       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ═══════════════════════════════════════════════════════════════════════════════
//                          ① 属性层 — 生存属性
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 生命值组件 — 所有可受伤实体必备。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Health : IComponent
{
    /// <summary>当前生命值</summary>
    public float Current;

    /// <summary>最大生命值</summary>
    public float Max;

    /// <summary>生命恢复速度（每秒）</summary>
    public float RegenRate;

    /// <summary>受击后恢复延迟（秒）</summary>
    public float RegenDelay;

    /// <summary>上次受击时刻（游戏时间，秒）</summary>
    public float LastDamageTime;

    public float _pad0, _pad1, _pad2; // 对齐到 32 bytes

    public readonly float Ratio => Max > 0 ? Current / Max : 0;
    public readonly bool IsDead => Current <= 0;
}

/// <summary>
/// 护甲组件 — 减伤计算。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Armor : IComponent
{
    /// <summary>当前护甲值</summary>
    public float Current;

    /// <summary>最大护甲值</summary>
    public float Max;

    /// <summary>物理减伤百分比 (0~1)</summary>
    public float PhysicalReduction;

    /// <summary>能量减伤百分比 (0~1)</summary>
    public float EnergyReduction;
}

/// <summary>
/// 战斗属性 — 攻击/防御综合数值。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CombatStats : IComponent
{
    /// <summary>基础攻击力</summary>
    public float AttackPower;

    /// <summary>暴击率 (0~1)</summary>
    public float CritChance;

    /// <summary>暴击倍率</summary>
    public float CritMultiplier;

    /// <summary>攻击速度倍率（1.0 = 正常）</summary>
    public float AttackSpeedMul;

    /// <summary>移动速度倍率</summary>
    public float MoveSpeedMul;

    /// <summary>受伤减免倍率</summary>
    public float DamageReductionMul;

    public float _pad0, _pad1;

    public static CombatStats Default => new()
    {
        AttackPower = 10f, CritChance = 0.05f, CritMultiplier = 1.5f,
        AttackSpeedMul = 1f, MoveSpeedMul = 1f, DamageReductionMul = 1f
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
//                          ② 状态层 — 武器/弹药
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 武器状态 — 当前装备的武器运行时数据。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct WeaponState : IComponent
{
    /// <summary>武器定义 ID（索引 WeaponDefs 表）</summary>
    public int WeaponDefId;

    /// <summary>武器类别 (0=拳头, 1=手枪, 2=步枪, 3=霰弹枪, 4=狙击, 5=火箭筒, 6=近战)</summary>
    public byte Category;

    /// <summary>当前武器槽位 (0~3)</summary>
    public byte SlotIndex;

    /// <summary>是否正在开火</summary>
    public byte IsFiring;

    /// <summary>是否正在换弹</summary>
    public byte IsReloading;

    /// <summary>上次开火时刻（游戏时间，秒）</summary>
    public float LastFireTime;

    /// <summary>换弹剩余时间（秒）</summary>
    public float ReloadTimer;

    /// <summary>连射计数（用于后坐力递增）</summary>
    public int BurstCount;
}

/// <summary>
/// 弹药状态 — 弹药管理。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AmmoState : IComponent
{
    /// <summary>弹匣内剩余</summary>
    public int CurrentMag;

    /// <summary>弹匣容量</summary>
    public int MagCapacity;

    /// <summary>储备弹药</summary>
    public int ReserveAmmo;

    /// <summary>储备弹药上限</summary>
    public int MaxReserve;
}

/// <summary>
/// 个人武器备份 — 进入载具前保存角色自身的武器 / 弹药，退出时恢复。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PersonalWeapon : IComponent
{
    public int  WeaponDefId;
    public byte Category;
    public byte SlotIndex;
    public int  CurrentMag;
    public int  MagCapacity;
    public int  ReserveAmmo;
    public int  MaxReserve;
}

// ═══════════════════════════════════════════════════════════════════════════════
//                          ② 状态层 — 载具
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 载具状态 — 运行时载具数据。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VehicleState : IComponent
{
    /// <summary>载具定义 ID（索引 VehicleDefs 表）</summary>
    public int VehicleDefId;

    /// <summary>载具类型 (0=汽车, 1=坦克, 2=防空炮, 3=飞机, 4=船, 5=火箭)</summary>
    public byte VehicleType;

    /// <summary>当前速度 (m/s)</summary>
    public float CurrentSpeed;

    /// <summary>最大速度 (m/s)</summary>
    public float MaxSpeed;

    /// <summary>当前生命值</summary>
    public float HealthCurrent;

    /// <summary>最大生命值</summary>
    public float HealthMax;

    /// <summary>当前燃油</summary>
    public float FuelCurrent;

    /// <summary>最大燃油</summary>
    public float FuelMax;

    /// <summary>是否可运行</summary>
    public byte IsOperational;

    public byte _pad0, _pad1, _pad2;
}

/// <summary>
/// 载具座位 — 标识谁占据哪个座位。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[ControlAuthoritySource]
public struct VehicleSeat : IComponent
{
    /// <summary>载具实体 ID</summary>
    public int VehicleEntityId;

    /// <summary>座位索引 (0=驾驶员, 1=炮手, 2+=乘客)</summary>
    public byte SeatIndex;

    /// <summary>座位类型 (0=驾驶, 1=炮手, 2=乘客)</summary>
    public byte SeatType;

    public byte _pad0, _pad1;

    /// <summary>座位在载具本地空间的偏移</summary>
    public float OffsetX, OffsetY, OffsetZ;
}

/// <summary>
/// 炮塔状态 — 载具/固定炮塔。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TurretState : IComponent
{
    /// <summary>炮塔当前 Yaw (弧度)</summary>
    public float Yaw;

    /// <summary>炮塔当前 Pitch (弧度)</summary>
    public float Pitch;

    /// <summary>Yaw 旋转速度 (弧度/秒)</summary>
    public float YawSpeed;

    /// <summary>Pitch 旋转速度 (弧度/秒)</summary>
    public float PitchSpeed;

    /// <summary>Pitch 最小值 (弧度)</summary>
    public float MinPitch;

    /// <summary>Pitch 最大值 (弧度)</summary>
    public float MaxPitch;

    /// <summary>关联武器定义 ID</summary>
    public int WeaponDefId;

    public float _pad0;
}

// ═══════════════════════════════════════════════════════════════════════════════
//                          ② 状态层 — 投射物
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 投射物数据 — 子弹/炮弹/火箭运行时状态。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ProjectileData : IComponent
{
    /// <summary>投射物定义 ID</summary>
    public int ProjectileDefId;

    /// <summary>发射者实体 ID</summary>
    public int OwnerEntityId;

    /// <summary>伤害值</summary>
    public float Damage;

    /// <summary>当前速度 (m/s)</summary>
    public float Speed;

    /// <summary>最大飞行距离</summary>
    public float MaxRange;

    /// <summary>已飞行距离</summary>
    public float TraveledDistance;

    /// <summary>爆炸半径（0 = 无 AOE）</summary>
    public float ExplosionRadius;

    /// <summary>伤害类型</summary>
    public byte DamageType;

    /// <summary>是否受重力影响</summary>
    public byte AffectedByGravity;

    public byte _pad0, _pad1;
}

// ═══════════════════════════════════════════════════════════════════════════════
//                          ③ 事件层 — 伤害/命中
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 伤害缓冲 — 本帧待处理的伤害数据（DamageSystem 消费后清除）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DamageBuffer : IComponent
{
    /// <summary>伤害来源实体 ID</summary>
    public int SourceEntityId;

    /// <summary>伤害值（未经减免）</summary>
    public float RawDamage;

    /// <summary>伤害类型 (0=物理, 1=能量, 2=爆炸, 3=火焰, 4=毒素)</summary>
    public byte DamageType;

    /// <summary>是否暴击</summary>
    public byte IsCrit;

    /// <summary>命中部位 (0=身体, 1=头部, 2=四肢)</summary>
    public byte HitZone;

    public byte _pad0;

    /// <summary>命中世界坐标</summary>
    public float HitX, HitY, HitZ;

    public float _pad1;
}

/// <summary>
/// 状态效果 — 持续性增益/减益。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct StatusEffect : IComponent
{
    /// <summary>效果 ID</summary>
    public int EffectId;

    /// <summary>效果类型 (0=灼烧, 1=中毒, 2=减速, 3=加速, 4=治疗, 5=护盾)</summary>
    public byte EffectType;

    /// <summary>剩余持续时间（秒）</summary>
    public float Duration;

    /// <summary>每秒效果值（伤害/治疗）</summary>
    public float TickValue;

    /// <summary>效果强度倍率</summary>
    public float Intensity;

    /// <summary>来源实体 ID</summary>
    public int SourceEntityId;

    public byte _pad0, _pad1, _pad2;
}

// ═══════════════════════════════════════════════════════════════════════════════
//                          ② 状态层 — 瞄准目标
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 战斗目标组件 — 所有活物（玩家、队友、敌人、载具）通用的瞄准/锁定目标。
/// <para>
/// 用途：
/// • 领队：准心射线命中的远方碰撞体即为当前目标
/// • 队友/敌人：由 AI 或领队命令指定锁定目标
/// • 载具：自身也可拥有目标（驾驶员操控时目标权归准心）
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CombatTarget : IComponent
{
    /// <summary>锁定的目标实体 ID（-1 = 无目标）</summary>
    public int TargetEntityId;

    /// <summary>瞄准点世界坐标 X</summary>
    public float AimPointX;

    /// <summary>瞄准点世界坐标 Y</summary>
    public float AimPointY;

    /// <summary>瞄准点世界坐标 Z</summary>
    public float AimPointZ;

    /// <summary>是否有有效目标</summary>
    public byte HasTarget;

    /// <summary>目标是否由领队命令指定（优先级高于 AI 自选）</summary>
    public byte IsCommandTarget;

    public byte _pad0, _pad1;

    /// <summary>无目标状态。</summary>
    public static CombatTarget None => new() { TargetEntityId = -1 };
}
