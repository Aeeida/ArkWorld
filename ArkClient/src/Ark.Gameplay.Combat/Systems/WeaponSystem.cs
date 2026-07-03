using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 武器系统 — 管理射击、换弹、武器切换。
/// </summary>
public sealed class WeaponSystem
{
    private readonly EntityStore _store;
    private readonly ProjectileSystem _projectileSystem;
    private readonly WeaponDefRegistry _weaponDefs;

    public event Action<int, int>? OnWeaponFired;
    public event Action<int>? OnReloadComplete;

    public WeaponSystem(EntityStore store, ProjectileSystem projectileSystem, WeaponDefRegistry weaponDefs)
    {
        _store = store;
        _projectileSystem = projectileSystem;
        _weaponDefs = weaponDefs;
    }

    public void Update(float deltaTime, float gameTime)
    {
        // ── 1. 处理换弹计时，收集完成列表（不能在 query loop 内做 structural change）──
        var reloadDone = new System.Collections.Generic.List<int>();

        var reloadQuery = _store.Query<WeaponState, AmmoState>()
            .AllTags(Tags.Get<Reloading>());

        foreach (var chunk in reloadQuery.Chunks)
        {
            var weapons  = chunk.Chunk1;
            var ammos    = chunk.Chunk2;
            var entities = chunk.Entities;

            for (int i = 0; i < entities.Length; i++)
            {
                ref var weapon = ref weapons.Span[i];
                ref var ammo   = ref ammos.Span[i];

                weapon.ReloadTimer -= deltaTime;

                if (weapon.ReloadTimer <= 0)
                {
                    int needed    = ammo.MagCapacity - ammo.CurrentMag;
                    int available = Math.Min(needed, ammo.ReserveAmmo);
                    ammo.CurrentMag  += available;
                    ammo.ReserveAmmo -= available;

                    weapon.IsReloading = 0;
                    weapon.BurstCount  = 0;

                    reloadDone.Add(entities[i]);
                }
            }
        }

        // ── 2. 在 query 外移除 Reloading 标记 ──
        foreach (var eid in reloadDone)
        {
            var e = _store.GetEntityById(eid);
            if (!e.IsNull) e.RemoveTag<Reloading>();
            OnReloadComplete?.Invoke(eid);
        }
    }

    public bool TryFire(int entityId, Vector3 aimOrigin, Vector3 aimDirection, float gameTime)
    {
        var entity = _store.GetEntityById(entityId);
        if (entity.IsNull) return false;
        if (!entity.TryGetComponent<WeaponState>(out var weapon)) return false;
        if (!entity.TryGetComponent<AmmoState>(out var ammo)) return false;

        if (weapon.IsReloading != 0) return false;
        if (entity.Tags.Has<Stunned>()) return false;
        if (entity.Tags.Has<Defeated>()) return false;

        var def = _weaponDefs.Get(weapon.WeaponDefId);
        if (def == null) return false;

        if (gameTime - weapon.LastFireTime < def.Value.FireInterval) return false;
        if (ammo.CurrentMag <= 0) { TryReload(entityId); return false; }

        ammo.CurrentMag--;
        weapon.LastFireTime = gameTime;
        weapon.IsFiring = 1;
        weapon.BurstCount++;

        entity.AddComponent(weapon);
        entity.AddComponent(ammo);
        entity.AddTag<Firing>();

        float spread = def.Value.Spread;
        if (entity.Tags.Has<Aiming>()) spread *= 0.3f;
        spread += Math.Min(weapon.BurstCount * 0.002f, 0.05f);
        var finalDir = ApplySpread(aimDirection, spread);

        float damageMul = 1f;
        if (entity.TryGetComponent<CombatStats>(out var stats))
            damageMul = stats.AttackPower / 10f;

        _projectileSystem.SpawnProjectile(new ProjectileSpawnRequest(
            def.Value.ProjectileDefId, entityId, aimOrigin, finalDir, damageMul));

        OnWeaponFired?.Invoke(entityId, weapon.WeaponDefId);
        return true;
    }

    public bool TryReload(int entityId)
    {
        var entity = _store.GetEntityById(entityId);
        if (entity.IsNull) return false;
        if (!entity.TryGetComponent<WeaponState>(out var weapon)) return false;
        if (!entity.TryGetComponent<AmmoState>(out var ammo)) return false;

        if (weapon.IsReloading != 0) return false;
        if (ammo.CurrentMag >= ammo.MagCapacity) return false;
        if (ammo.ReserveAmmo <= 0) return false;

        var def = _weaponDefs.Get(weapon.WeaponDefId);
        if (def == null) return false;

        weapon.IsReloading = 1;
        weapon.IsFiring = 0;
        weapon.ReloadTimer = def.Value.ReloadTime;
        weapon.BurstCount = 0;

        entity.AddComponent(weapon);
        entity.AddTag<Reloading>();
        entity.RemoveTag<Firing>();
        return true;
    }

    public void StopFiring(int entityId)
    {
        var entity = _store.GetEntityById(entityId);
        if (entity.IsNull) return;
        if (!entity.TryGetComponent<WeaponState>(out var weapon)) return;

        weapon.IsFiring = 0;
        weapon.BurstCount = 0;
        entity.AddComponent(weapon);
        entity.RemoveTag<Firing>();
    }

    public void EquipWeapon(int entityId, int weaponDefId, byte slotIndex = 0)
    {
        var entity = _store.GetEntityById(entityId);
        if (entity.IsNull) return;

        var def = _weaponDefs.Get(weaponDefId);
        if (def == null) return;

        entity.AddComponent(new WeaponState
        {
            WeaponDefId = weaponDefId,
            Category    = (byte)def.Value.Category,
            SlotIndex   = slotIndex,
        });
        entity.AddComponent(new AmmoState
        {
            CurrentMag  = def.Value.MagCapacity,
            MagCapacity = def.Value.MagCapacity,
            ReserveAmmo = def.Value.MaxReserve,
            MaxReserve  = def.Value.MaxReserve,
        });
    }

    private static Vector3 ApplySpread(Vector3 direction, float spreadAngle)
    {
        if (spreadAngle <= 0) return direction;
        var rng = Random.Shared;
        float angle  = (float)(rng.NextDouble() * Math.PI * 2);
        float radius = (float)(rng.NextDouble() * spreadAngle);

        var right = Vector3.Cross(direction, Vector3.UnitY);
        if (right.LengthSquared() < 0.001f)
            right = Vector3.Cross(direction, Vector3.UnitX);
        right = Vector3.Normalize(right);
        var up = Vector3.Cross(right, direction);

        var offset = right * MathF.Cos(angle) * radius + up * MathF.Sin(angle) * radius;
        return Vector3.Normalize(direction + offset);
    }
}
