using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Abstractions;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;
using Ark.Gameplay.Combat.Systems;
using Ark.Gameplay.Vehicle;

namespace Ark.Gameplay.Combat;

/// <summary>
/// 战斗玩法模块 — Ark.Gameplay.Combat 的唯一入口门面（Facade）。
///
/// 架构层次：
/// ┌──────────────────────────────────────────────────────┐
/// │              CombatGameplayModule (门面)               │
/// │  • 初始化所有子系统                                    │
/// │  • 提供统一 API                                       │
/// │  • 驱动每帧更新                                       │
/// ├──────────────────────────────────────────────────────┤
/// │ DamageSystem │ HealthSystem │ StatusEffectSystem      │
/// │ WeaponSystem │ ProjectileSystem │ MeleeSystem         │
/// │ VehicleSystem                                         │
/// ├──────────────────────────────────────────────────────┤
/// │ WeaponDefs │ ProjectileDefs │ VehicleDefs (注册表)     │
/// └──────────────────────────────────────────────────────┘
/// </summary>
public sealed class CombatGameplayModule
{
    private readonly EntityStore _store;

    // ─── 定义注册表 ───
    private WeaponDefRegistry _weaponDefs = null!;
    private ProjectileDefRegistry _projectileDefs = null!;
    private VehicleDefRegistry _vehicleDefs = null!;

    // ─── 子系统 ───
    private DamageSystem _damageSystem = null!;
    private HealthSystem _healthSystem = null!;
    private StatusEffectSystem _statusEffectSystem = null!;
    private WeaponSystem _weaponSystem = null!;
    private ProjectileSystem _projectileSystem = null!;
    private MeleeSystem _meleeSystem = null!;
    private VehicleSystem _vehicleSystem = null!;

    private bool _initialized;

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开子系统（只读访问）
    // ═══════════════════════════════════════════════════════════════════════

    public DamageSystem Damage => _damageSystem;
    public HealthSystem Health => _healthSystem;
    public StatusEffectSystem StatusEffects => _statusEffectSystem;
    public WeaponSystem Weapons => _weaponSystem;
    public ProjectileSystem Projectiles => _projectileSystem;
    public MeleeSystem Melee => _meleeSystem;
    public VehicleSystem Vehicles => _vehicleSystem;
    public WeaponDefRegistry WeaponDefs => _weaponDefs;
    public ProjectileDefRegistry ProjectileDefs => _projectileDefs;
    public VehicleDefRegistry VehicleDefs => _vehicleDefs;

    /// <summary>将地形查询注入载具系统（生成时校正 Y、间距检测）。</summary>
    public void SetVehicleTerrainQuery(ITerrainQuery? terrain)
        => _vehicleSystem?.SetTerrainQuery(terrain);

    /// <summary>最近一次 Update 传入的游戏时间（秒）。</summary>
    public float GameTime { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    //                          事件（聚合各子系统）
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<DamageResult>? OnDamageApplied;
    public event Action<KillEvent>? OnKill;
    public event Action<int, int>? OnWeaponFired;
    public event Action<VehicleEnterExitEvent>? OnVehicleEntered;
    public event Action<VehicleEnterExitEvent>? OnVehicleExited;
    public event Action<int>? OnStructureCollapsed;

    // ═══════════════════════════════════════════════════════════════════════
    //                          构造 & 初始化
    // ═══════════════════════════════════════════════════════════════════════

    public CombatGameplayModule(EntityStore store)
    {
        _store = store;
    }

    /// <summary>初始化所有战斗子系统。</summary>
    public void Initialize()
    {
        if (_initialized) return;

        // 1. 定义注册表
        _weaponDefs = new WeaponDefRegistry();
        _weaponDefs.RegisterDefaults();

        _projectileDefs = new ProjectileDefRegistry();
        _projectileDefs.RegisterDefaults();

        _vehicleDefs = new VehicleDefRegistry();
        _vehicleDefs.RegisterDefaults();

        // 2. 核心系统（有依赖顺序）
        _damageSystem       = new DamageSystem(_store);
        _healthSystem       = new HealthSystem(_store);
        _statusEffectSystem = new StatusEffectSystem(_store, _damageSystem);
        _projectileSystem   = new ProjectileSystem(_store, _projectileDefs);
        _weaponSystem       = new WeaponSystem(_store, _projectileSystem, _weaponDefs);
        _meleeSystem        = new MeleeSystem(_store, _damageSystem);
        _vehicleSystem      = new VehicleSystem(_store, _vehicleDefs,
            (entityId, weaponDefId, slotIndex) => _weaponSystem.EquipWeapon(entityId, weaponDefId, (byte)slotIndex));

        // 3. 绑定事件转发
        _damageSystem.OnDamageApplied += r => OnDamageApplied?.Invoke(r);
        _damageSystem.OnKill          += k => OnKill?.Invoke(k);
        _weaponSystem.OnWeaponFired   += (e, w) => OnWeaponFired?.Invoke(e, w);
        _vehicleSystem.OnVehicleEntered += ev => OnVehicleEntered?.Invoke(ev);
        _vehicleSystem.OnVehicleExited  += ev => OnVehicleExited?.Invoke(ev);

        _initialized = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          每帧更新
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧更新（由 GameBootstrap._Process 调用）。
    /// 顺序：StatusEffect → Weapon → Projectile → Damage → Health → Vehicle
    /// </summary>
    public void Update(float deltaTime, float gameTime)
    {
        if (!_initialized) return;

        GameTime = gameTime;

        _statusEffectSystem.Update(deltaTime, gameTime);
        _weaponSystem.Update(deltaTime, gameTime);
        _projectileSystem.Update(deltaTime);
        _damageSystem.Update(gameTime);
        _healthSystem.Update(deltaTime, gameTime);
        _vehicleSystem.Update(deltaTime);
        UpdateStructuralCollapse();
    }

    /// <summary>
    /// 检查结构完整性超限的建筑/载具 — 标记为摧毁。
    /// </summary>
    private void UpdateStructuralCollapse()
    {
        var toDestroy = new System.Collections.Generic.List<int>();
        var query = _store.Query<StructuralIntegrity>();

        foreach (var chunk in query.Chunks)
        {
            var integrities = chunk.Chunk1;
            var entities    = chunk.Entities;
            for (int i = 0; i < entities.Length; i++)
            {
                ref readonly var si = ref integrities.Span[i];
                if (si.AccumulatedDamage >= si.MaxIntegrity)
                    toDestroy.Add(entities[i]);
            }
        }

        foreach (var eid in toDestroy)
        {
            var entity = _store.GetEntityById(eid);
            if (entity.IsNull) continue;
            entity.AddTag<Defeated>();
            entity.AddTag<Dead>();
            OnStructureCollapsed?.Invoke(eid);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          便捷 API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>给实体添加战斗属性（使其可参与战斗）。</summary>
    public void MakeCombatant(int entityId,
        float maxHealth = 100f, float healthRegen = 0f, float regenDelay = 5f,
        float armorPhysical = 0f, float armorEnergy = 0f,
        float attackPower = 10f, float critChance = 0.05f)
    {
        var entity = _store.GetEntityById(entityId);
        if (entity.IsNull) return;

        entity.AddComponent(new Ark.Ecs.Components.Health
        {
            Current = maxHealth, Max = maxHealth,
            RegenRate = healthRegen, RegenDelay = regenDelay,
        });

        if (armorPhysical > 0 || armorEnergy > 0)
        {
            entity.AddComponent(new Armor
            {
                Current = 100f, Max = 100f,
                PhysicalReduction = armorPhysical, EnergyReduction = armorEnergy,
            });
        }

        entity.AddComponent(new CombatStats
        {
            AttackPower = attackPower, CritChance = critChance,
            CritMultiplier = 1.5f, AttackSpeedMul = 1f,
            MoveSpeedMul = 1f, DamageReductionMul = 1f,
        });
    }

    // ─── 武器 ───
    public void EquipWeapon(int entityId, int weaponDefId, byte slot = 0)
        => _weaponSystem.EquipWeapon(entityId, weaponDefId, slot);

    /// <summary>获取实体当前装备的武器定义 ID（无武器返回 0）。</summary>
    public int GetCurrentWeaponDefId(int entityId)
    {
        var entity = _store.GetEntityById(entityId);
        if (entity.IsNull) return 0;
        return entity.TryGetComponent<Ark.Ecs.Components.WeaponState>(out var ws) ? ws.WeaponDefId : 0;
    }

    public bool TryFire(int entityId, Vector3 aimOrigin, Vector3 aimDirection, float gameTime)
        => _weaponSystem.TryFire(entityId, aimOrigin, aimDirection, gameTime);

    public void StopFiring(int entityId) => _weaponSystem.StopFiring(entityId);

    public bool TryReload(int entityId) => _weaponSystem.TryReload(entityId);

    // ─── 近战 ───
    public void PerformMeleeAttack(int attackerEntityId, Vector3 position, Vector3 forward, float range = 2.5f)
        => _meleeSystem.PerformMeleeAttack(attackerEntityId, position, forward, range);

    public void StartBlocking(int entityId) => _meleeSystem.StartBlocking(entityId);
    public void StopBlocking(int entityId) => _meleeSystem.StopBlocking(entityId);

    // ─── 载具 ───
    public Entity SpawnVehicle(int vehicleDefId, Vector3 position, Quaternion rotation)
        => _vehicleSystem.SpawnVehicle(vehicleDefId, position, rotation);

    public bool EnterVehicle(int characterId, int vehicleId, int preferredSeat = 0)
        => _vehicleSystem.TryEnterVehicle(characterId, vehicleId, preferredSeat);

    public bool ExitVehicle(int characterId) => _vehicleSystem.TryExitVehicle(characterId);

    public bool SwitchSeat(int characterId, int seatIndex)
        => _vehicleSystem.TrySwitchSeat(characterId, seatIndex);

    public bool CycleToNextSeat(int characterId)
        => _vehicleSystem.TryCycleToNextSeat(characterId);

    public void ProcessDriveInput(int vehicleId, float throttle, float steering, float brake)
        => _vehicleSystem.ProcessDriveInput(vehicleId, throttle, steering, brake);

    public void ProcessTurretInput(int vehicleId, float yawDelta, float pitchDelta)
        => _vehicleSystem.ProcessTurretInput(vehicleId, yawDelta, pitchDelta);

    // ─── 伤害 ───
    public void ApplyDamage(int targetId, int sourceId, float rawDamage, DamageType type = DamageType.Physical)
        => _damageSystem.ApplyDamage(targetId, sourceId, rawDamage, type);

    // ─── 状态效果 ───
    public void ApplyStatusEffect(int targetId, int sourceId, StatusEffectType type,
        float duration, float tickValue, float intensity = 1f)
        => _statusEffectSystem.ApplyEffect(targetId, sourceId, type, duration, tickValue, intensity);
}
