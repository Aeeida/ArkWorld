using Friflo.Engine.ECS;

namespace Ark.Ecs.Tags;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                            战斗系统标签                                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ─── 战斗状态 ───

/// <summary>正在格斗/近战攻击</summary>
public struct MeleeAttacking : ITag { }

/// <summary>正在射击</summary>
public struct Firing : ITag { }

/// <summary>正在换弹</summary>
public struct Reloading : ITag { }

/// <summary>正在格挡/防御</summary>
public struct Blocking : ITag { }

/// <summary>正在瞄准（ADS）</summary>
public struct Aiming : ITag { }

/// <summary>已被击倒（等待复活/消失）</summary>
public struct Defeated : ITag { }

/// <summary>处于眩晕状态</summary>
public struct Stunned : ITag { }

/// <summary>处于护盾状态</summary>
public struct Shielded : ITag { }

/// <summary>无敌状态（复活保护等）</summary>
public struct Invulnerable : ITag { }

/// <summary>正在受伤（当帧有 DamageBuffer）</summary>
public struct TakingDamage : ITag { }

/// <summary>被击退中</summary>
public struct KnockedBack : ITag { }

// ─── 载具角色标签 ───

/// <summary>驾驶员</summary>
public struct IsDriver : ITag { }

/// <summary>炮手</summary>
public struct IsGunner : ITag { }

/// <summary>乘客</summary>
public struct IsPassenger : ITag { }

// ─── 投射物标签 ───

/// <summary>子弹（高速直线）</summary>
public struct BulletProjectile : ITag { }

/// <summary>炮弹（受重力影响的抛物线）</summary>
public struct ShellProjectile : ITag { }

/// <summary>火箭/导弹（可有制导）</summary>
public struct RocketProjectile : ITag { }

/// <summary>投掷物（手雷等）</summary>
public struct ThrownProjectile : ITag { }

// ─── 战斗分类 ───

/// <summary>友方单位</summary>
public struct Friendly : ITag { }

/// <summary>敌方单位</summary>
public struct Hostile : ITag { }

/// <summary>中立单位</summary>
public struct Neutral : ITag { }
