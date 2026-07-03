using System.Collections.Generic;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                     定义注册表 — 静态配置数据仓库                                ║
// ║  每种定义有独立的 Registry，支持运行时注册和查询                                  ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 武器定义注册表。
/// </summary>
public sealed class WeaponDefRegistry
{
    private readonly Dictionary<int, WeaponDef> _defs = [];

    public void Register(WeaponDef def) => _defs[def.Id] = def;
    public WeaponDef? Get(int id) => _defs.TryGetValue(id, out var d) ? d : null;
    public IEnumerable<WeaponDef> GetAll() => _defs.Values;

    /// <summary>
    /// 注册默认武器定义。
    /// </summary>
    public void RegisterDefaults()
    {
        // ─── 近战 ───
        Register(new WeaponDef(
            Id: 1, Name: "拳头", Category: WeaponCategory.Fist,
            FireMode: FireMode.Semi, BaseDamage: 8f, FireRate: 2f,
            ReloadTime: 0, MagCapacity: int.MaxValue, MaxReserve: 0,
            Range: 2f, Spread: 0, RecoilVertical: 0, RecoilHorizontal: 0,
            ProjectileDefId: 0, HeadshotMul: 1.5f
        ));

        Register(new WeaponDef(
            Id: 2, Name: "战术刀", Category: WeaponCategory.Melee,
            FireMode: FireMode.Semi, BaseDamage: 25f, FireRate: 1.5f,
            ReloadTime: 0, MagCapacity: int.MaxValue, MaxReserve: 0,
            Range: 2.5f, Spread: 0, RecoilVertical: 0, RecoilHorizontal: 0,
            ProjectileDefId: 0, HeadshotMul: 2f
        ));

        // ─── 手枪 ───
        Register(new WeaponDef(
            Id: 10, Name: "M9 手枪", Category: WeaponCategory.Pistol,
            FireMode: FireMode.Semi, BaseDamage: 18f, FireRate: 6f,
            ReloadTime: 1.5f, MagCapacity: 15, MaxReserve: 90,
            Range: 50f, Spread: 0.015f, RecoilVertical: 0.02f, RecoilHorizontal: 0.005f,
            ProjectileDefId: 100, HeadshotMul: 2f
        ));

        // ─── 步枪 ───
        Register(new WeaponDef(
            Id: 20, Name: "M4 突击步枪", Category: WeaponCategory.Rifle,
            FireMode: FireMode.Auto, BaseDamage: 22f, FireRate: 12f,
            ReloadTime: 2.2f, MagCapacity: 30, MaxReserve: 180,
            Range: 200f, Spread: 0.01f, RecoilVertical: 0.015f, RecoilHorizontal: 0.008f,
            ProjectileDefId: 101, HeadshotMul: 2.5f
        ));

        Register(new WeaponDef(
            Id: 21, Name: "AK-47", Category: WeaponCategory.Rifle,
            FireMode: FireMode.Auto, BaseDamage: 28f, FireRate: 10f,
            ReloadTime: 2.5f, MagCapacity: 30, MaxReserve: 150,
            Range: 180f, Spread: 0.018f, RecoilVertical: 0.025f, RecoilHorizontal: 0.012f,
            ProjectileDefId: 101, HeadshotMul: 2.5f
        ));

        // ─── 霰弹枪 ───
        Register(new WeaponDef(
            Id: 30, Name: "M870 霰弹枪", Category: WeaponCategory.Shotgun,
            FireMode: FireMode.Semi, BaseDamage: 12f, FireRate: 1.2f,
            ReloadTime: 4f, MagCapacity: 8, MaxReserve: 32,
            Range: 30f, Spread: 0.08f, RecoilVertical: 0.05f, RecoilHorizontal: 0.02f,
            ProjectileDefId: 102, HeadshotMul: 1.5f
        ));

        // ─── 狙击枪 ───
        Register(new WeaponDef(
            Id: 40, Name: "AWP 狙击步枪", Category: WeaponCategory.Sniper,
            FireMode: FireMode.Semi, BaseDamage: 85f, FireRate: 0.8f,
            ReloadTime: 3.5f, MagCapacity: 5, MaxReserve: 30,
            Range: 500f, Spread: 0.001f, RecoilVertical: 0.06f, RecoilHorizontal: 0.01f,
            ProjectileDefId: 103, HeadshotMul: 4f
        ));

        // ─── 火箭筒 ───
        Register(new WeaponDef(
            Id: 50, Name: "RPG-7", Category: WeaponCategory.Launcher,
            FireMode: FireMode.Semi, BaseDamage: 150f, FireRate: 0.3f,
            ReloadTime: 5f, MagCapacity: 1, MaxReserve: 5,
            Range: 300f, Spread: 0.005f, RecoilVertical: 0.1f, RecoilHorizontal: 0.02f,
            ProjectileDefId: 200, HeadshotMul: 1f
        ));

        // ─── 载具武器 ───
        Register(new WeaponDef(
            Id: 60, Name: "坦克主炮", Category: WeaponCategory.Launcher,
            FireMode: FireMode.Semi, BaseDamage: 300f, FireRate: 0.15f,
            ReloadTime: 8f, MagCapacity: 1, MaxReserve: 40,
            Range: 800f, Spread: 0.002f, RecoilVertical: 0.15f, RecoilHorizontal: 0.01f,
            ProjectileDefId: 201, HeadshotMul: 1f
        ));

        Register(new WeaponDef(
            Id: 61, Name: "防空机枪", Category: WeaponCategory.Rifle,
            FireMode: FireMode.Auto, BaseDamage: 15f, FireRate: 20f,
            ReloadTime: 6f, MagCapacity: 200, MaxReserve: 1000,
            Range: 400f, Spread: 0.02f, RecoilVertical: 0.01f, RecoilHorizontal: 0.01f,
            ProjectileDefId: 104, HeadshotMul: 2f
        ));

        Register(new WeaponDef(
            Id: 62, Name: "舰炮", Category: WeaponCategory.Launcher,
            FireMode: FireMode.Semi, BaseDamage: 500f, FireRate: 0.1f,
            ReloadTime: 10f, MagCapacity: 1, MaxReserve: 60,
            Range: 1500f, Spread: 0.008f, RecoilVertical: 0.2f, RecoilHorizontal: 0.05f,
            ProjectileDefId: 202, HeadshotMul: 1f
        ));
    }
}

/// <summary>
/// 投射物定义注册表。
/// </summary>
public sealed class ProjectileDefRegistry
{
    private readonly Dictionary<int, ProjectileDef> _defs = [];

    public void Register(ProjectileDef def) => _defs[def.Id] = def;
    public ProjectileDef? Get(int id) => _defs.TryGetValue(id, out var d) ? d : null;
    public IEnumerable<ProjectileDef> GetAll() => _defs.Values;

    /// <summary>
    /// 注册默认投射物定义。
    /// </summary>
    public void RegisterDefaults()
    {
        // ─── 子弹 ───
        Register(new ProjectileDef(100, "9mm 子弹", ProjectileType.Bullet,
            Speed: 400f, MaxRange: 100f, ExplosionRadius: 0, Gravity: 0,
            DamageType: DamageType.Physical, LifeTime: 2f));

        Register(new ProjectileDef(101, "5.56mm 步枪弹", ProjectileType.Bullet,
            Speed: 900f, MaxRange: 300f, ExplosionRadius: 0, Gravity: 0,
            DamageType: DamageType.Physical, LifeTime: 2f));

        Register(new ProjectileDef(102, "12号霰弹", ProjectileType.Bullet,
            Speed: 350f, MaxRange: 40f, ExplosionRadius: 0, Gravity: 0,
            DamageType: DamageType.Physical, LifeTime: 1f));

        Register(new ProjectileDef(103, ".338 狙击弹", ProjectileType.Bullet,
            Speed: 1200f, MaxRange: 600f, ExplosionRadius: 0, Gravity: 0,
            DamageType: DamageType.Physical, LifeTime: 3f));

        Register(new ProjectileDef(104, "12.7mm 重机枪弹", ProjectileType.Bullet,
            Speed: 800f, MaxRange: 500f, ExplosionRadius: 0, Gravity: 0,
            DamageType: DamageType.Physical, LifeTime: 3f));

        // ─── 火箭/炮弹 ───
        Register(new ProjectileDef(200, "RPG 火箭弹", ProjectileType.Rocket,
            Speed: 120f, MaxRange: 400f, ExplosionRadius: 8f, Gravity: 0,
            DamageType: DamageType.Explosive, LifeTime: 5f));

        Register(new ProjectileDef(201, "坦克炮弹", ProjectileType.Shell,
            Speed: 1500f, MaxRange: 1000f, ExplosionRadius: 5f, Gravity: 0.5f,
            DamageType: DamageType.Explosive, LifeTime: 4f));

        Register(new ProjectileDef(202, "舰炮弹", ProjectileType.Shell,
            Speed: 800f, MaxRange: 2000f, ExplosionRadius: 15f, Gravity: 1f,
            DamageType: DamageType.Explosive, LifeTime: 8f));

        // ─── 手雷 ───
        Register(new ProjectileDef(300, "破片手雷", ProjectileType.Grenade,
            Speed: 20f, MaxRange: 30f, ExplosionRadius: 10f, Gravity: 9.81f,
            DamageType: DamageType.Explosive, LifeTime: 3f));

        Register(new ProjectileDef(301, "燃烧弹", ProjectileType.Grenade,
            Speed: 18f, MaxRange: 25f, ExplosionRadius: 6f, Gravity: 9.81f,
            DamageType: DamageType.Fire, LifeTime: 3f));
    }
}

/// <summary>
/// 载具定义注册表 — 已迁移到 Ark.Gameplay.Vehicle.VehicleDefRegistry。
/// 此处保留向后兼容别名。
/// </summary>
// MOVED → Ark.Gameplay.Vehicle.VehicleDefRegistry
