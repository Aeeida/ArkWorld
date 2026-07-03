using System.Numerics;
using GameLayer.Core;

namespace GameLayer.Combat;

/// <summary>
/// Server-authoritative damage calculation.
/// Mirrors Ark's DamageResult/CombatStats logic with armor, crits, and hit zones.
/// </summary>
public static class DamageCalculator
{
    public static DamageResult Calculate(
        ServerEntity attacker,
        ServerEntity target,
        WeaponDef weapon,
        HitZone hitZone = HitZone.Body,
        float distanceToTarget = 0f)
    {
        var baseDamage = weapon.BaseDamage;

        // Headshot multiplier
        var zoneMul = hitZone switch
        {
            HitZone.Head => weapon.HeadshotMul,
            HitZone.Limb => 0.75f,
            _ => 1f
        };
        baseDamage *= zoneMul;

        // Crit roll (5% base chance)
        var random = Random.Shared;
        var isCrit = random.NextSingle() < 0.05f;
        if (isCrit) baseDamage *= 1.5f;

        // Range falloff (beyond 80% of max range)
        if (weapon.Range > 0 && distanceToTarget > weapon.Range * 0.8f)
        {
            var falloff = 1f - (distanceToTarget - weapon.Range * 0.8f) / (weapon.Range * 0.2f);
            baseDamage *= Math.Clamp(falloff, 0.2f, 1f);
        }

        // Armor reduction (simple percentage)
        var armorReduction = 0f; // Could be looked up from target's equipment
        var absorbed = baseDamage * armorReduction;
        var finalDamage = baseDamage - absorbed;
        finalDamage = MathF.Max(1f, finalDamage); // Minimum 1 damage

        var isKill = target.Health - finalDamage <= 0;

        return new DamageResult(
            attacker.Id, target.Id,
            weapon.BaseDamage, finalDamage, absorbed,
            weapon.DamageType, hitZone,
            isCrit, isKill, target.Position);
    }

    /// <summary>
    /// Applies calculated damage to the target entity.
    /// Returns true if the target died.
    /// </summary>
    public static bool ApplyDamage(ServerEntity target, float damage)
    {
        var wasDead = !target.IsAlive;
        target.Health = MathF.Max(0, target.Health - damage);
        return !wasDead && !target.IsAlive;
    }
}

// ── Data types matching Ark's CombatDataTypes ──

public enum DamageType : byte
{
    Physical = 0, Energy = 1, Explosive = 2,
    Fire = 3, Poison = 4, Fall = 5,
}

public enum HitZone : byte { Body = 0, Head = 1, Limb = 2 }

public enum WeaponCategory : byte
{
    Fist = 0, Pistol = 1, Rifle = 2, Shotgun = 3,
    Sniper = 4, Launcher = 5, Melee = 6,
}

public enum FireMode : byte { Semi = 0, Auto = 1, Burst = 2, Charge = 3 }

public record struct WeaponDef(
    int Id, string Name, WeaponCategory Category, FireMode FireMode,
    float BaseDamage, float FireRate, float ReloadTime,
    int MagCapacity, int MaxReserve, float Range, float Spread,
    float RecoilVertical, float RecoilHorizontal,
    DamageType DamageType, float HeadshotMul)
{
    public readonly float FireInterval => FireRate > 0 ? 1f / FireRate : float.MaxValue;
}

public readonly record struct DamageResult(
    int SourceEntityId, int TargetEntityId,
    float RawDamage, float FinalDamage, float ArmorAbsorbed,
    DamageType Type, HitZone Zone,
    bool IsCrit, bool IsKill, Vector3 HitPosition);

/// <summary>
/// Manages weapon definitions — server-side weapon registry.
/// </summary>
public sealed class WeaponRegistry
{
    private readonly Dictionary<int, WeaponDef> _weapons = [];

    public void Register(WeaponDef weapon) => _weapons[weapon.Id] = weapon;
    public WeaponDef? Get(int weaponId) => _weapons.GetValueOrDefault(weaponId);
    public IReadOnlyCollection<WeaponDef> GetAll() => _weapons.Values.ToList().AsReadOnly();

    public bool TryApplyLoadout(ServerEntity entity, int weaponId, int? currentMag = null, int? reserveAmmo = null, bool setPersonal = true)
    {
        if (!_weapons.TryGetValue(weaponId, out var weapon))
            return false;

        int appliedCurrentMag = currentMag ?? weapon.MagCapacity;
        int appliedReserveAmmo = reserveAmmo ?? weapon.MaxReserve;

        entity.WeaponDefId = weapon.Id;
        entity.WeaponCategory = (byte)weapon.Category;
        entity.CurrentMag = appliedCurrentMag;
        entity.MagCapacity = weapon.MagCapacity;
        entity.ReserveAmmo = appliedReserveAmmo;
        entity.MaxReserve = weapon.MaxReserve;
        entity.IsReloading = false;

        if (setPersonal)
        {
            entity.PersonalWeaponDefId = weapon.Id;
            entity.PersonalWeaponCategory = (byte)weapon.Category;
            entity.PersonalCurrentMag = appliedCurrentMag;
            entity.PersonalMagCapacity = weapon.MagCapacity;
            entity.PersonalReserveAmmo = appliedReserveAmmo;
            entity.PersonalMaxReserve = weapon.MaxReserve;
        }

        return true;
    }

    public void RestorePersonalLoadout(ServerEntity entity)
    {
        entity.WeaponDefId = entity.PersonalWeaponDefId;
        entity.WeaponCategory = entity.PersonalWeaponCategory;
        entity.CurrentMag = entity.PersonalCurrentMag;
        entity.MagCapacity = entity.PersonalMagCapacity;
        entity.ReserveAmmo = entity.PersonalReserveAmmo;
        entity.MaxReserve = entity.PersonalMaxReserve;
        entity.IsReloading = false;
    }

    /// <summary>
    /// Seeds default weapon definitions.
    /// </summary>
    public void SeedDefaults()
    {
        Register(new WeaponDef(10, "M9 Pistol", WeaponCategory.Pistol, FireMode.Semi,
            18f, 6f, 1.5f, 15, 90, 50f, 0.2f, 0.02f, 0.01f, DamageType.Physical, 2f));
        Register(new WeaponDef(20, "M4 Rifle", WeaponCategory.Rifle, FireMode.Auto,
            22f, 12f, 2.2f, 30, 180, 200f, 0.1f, 0.015f, 0.008f, DamageType.Physical, 2.5f));
        Register(new WeaponDef(21, "AK-47", WeaponCategory.Rifle, FireMode.Auto,
            28f, 10f, 2.5f, 30, 150, 180f, 0.18f, 0.025f, 0.012f, DamageType.Physical, 2.5f));
        Register(new WeaponDef(30, "M870 Shotgun", WeaponCategory.Shotgun, FireMode.Semi,
            12f, 1.2f, 4f, 8, 32, 30f, 0.8f, 0.05f, 0.02f, DamageType.Physical, 1.5f));
        Register(new WeaponDef(40, "AWP", WeaponCategory.Sniper, FireMode.Semi,
            85f, 0.8f, 3.5f, 5, 30, 500f, 0.01f, 0.06f, 0.01f, DamageType.Physical, 4f));
        Register(new WeaponDef(50, "RPG-7", WeaponCategory.Launcher, FireMode.Semi,
            150f, 0.3f, 5f, 1, 5, 300f, 0.05f, 0.1f, 0.02f, DamageType.Explosive, 1f));
        Register(new WeaponDef(60, "Tank Cannon", WeaponCategory.Launcher, FireMode.Semi,
            300f, 0.15f, 8f, 1, 40, 800f, 0.02f, 0.15f, 0.01f, DamageType.Explosive, 1f));
        Register(new WeaponDef(61, "Anti-Air MG", WeaponCategory.Rifle, FireMode.Auto,
            15f, 20f, 6f, 200, 1000, 400f, 0.2f, 0.01f, 0.01f, DamageType.Physical, 2f));
        Register(new WeaponDef(62, "Naval Cannon", WeaponCategory.Launcher, FireMode.Semi,
            500f, 0.1f, 10f, 1, 60, 1500f, 0.08f, 0.2f, 0.05f, DamageType.Explosive, 1f));

        Register(new WeaponDef(1, "Pistol", WeaponCategory.Pistol, FireMode.Semi,
            15f, 3f, 1.5f, 12, 60, 50f, 2f, 3f, 1f, DamageType.Physical, 2f));
        Register(new WeaponDef(2, "Assault Rifle", WeaponCategory.Rifle, FireMode.Auto,
            20f, 8f, 2.5f, 30, 120, 100f, 3f, 4f, 2f, DamageType.Physical, 2f));
        Register(new WeaponDef(3, "Shotgun", WeaponCategory.Shotgun, FireMode.Semi,
            80f, 1f, 2f, 6, 30, 20f, 12f, 8f, 4f, DamageType.Physical, 1.5f));
        Register(new WeaponDef(4, "Sniper", WeaponCategory.Sniper, FireMode.Semi,
            90f, 0.5f, 3f, 5, 25, 300f, 0.5f, 6f, 1f, DamageType.Physical, 3f));
        Register(new WeaponDef(5, "Laser Rifle", WeaponCategory.Rifle, FireMode.Auto,
            18f, 10f, 2f, 40, 160, 120f, 1f, 2f, 1f, DamageType.Energy, 2f));
        Register(new WeaponDef(6, "Rocket Launcher", WeaponCategory.Launcher, FireMode.Semi,
            200f, 0.3f, 4f, 1, 6, 200f, 5f, 10f, 5f, DamageType.Explosive, 1f));
        Register(new WeaponDef(7, "Combat Knife", WeaponCategory.Melee, FireMode.Semi,
            30f, 2f, 0f, 0, 0, 3f, 0f, 0f, 0f, DamageType.Physical, 2.5f));
    }
}

/// <summary>
/// Tracks active combat sessions — who is fighting whom, cooldowns, etc.
/// </summary>
public sealed class CombatSessionManager
{
    private readonly Dictionary<Guid, CombatSession> _sessions = [];

    public CombatSession? GetSession(Guid playerId) => _sessions.GetValueOrDefault(playerId);

    public CombatSession StartSession(Guid attackerId, Guid defenderId)
    {
        var session = new CombatSession
        {
            AttackerId = attackerId,
            DefenderId = defenderId,
            StartedAt = DateTime.UtcNow
        };
        _sessions[attackerId] = session;
        return session;
    }

    public void EndSession(Guid playerId) => _sessions.Remove(playerId);

    /// <summary>
    /// Checks whether the player can fire their weapon (cooldown).
    /// </summary>
    public bool CanFire(Guid playerId, float fireInterval)
    {
        if (!_sessions.TryGetValue(playerId, out var session))
            return true;

        var elapsed = (float)(DateTime.UtcNow - session.LastAttackAt).TotalSeconds;
        return elapsed >= fireInterval;
    }

    public void RecordAttack(Guid playerId)
    {
        if (_sessions.TryGetValue(playerId, out var session))
            session.LastAttackAt = DateTime.UtcNow;
    }
}

public sealed class CombatSession
{
    public Guid AttackerId { get; init; }
    public Guid DefenderId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime LastAttackAt { get; set; }
}
