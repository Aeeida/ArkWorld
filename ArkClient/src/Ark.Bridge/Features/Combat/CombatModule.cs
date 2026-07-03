using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;
using Ark.Shared.Data;

namespace Ark.Bridge.Features.Combat;

/// <summary>
/// 战斗模块 — 射击、伤害计算、技能释放。
/// 实现 ICombatService 接口供上层调用。
/// TODO: 实现完整战斗系统
/// </summary>
public sealed class CombatModule : ICombatService
{
    private readonly EntityStore _store;

    public WeaponInfo? CurrentWeapon => null;
    public int AimTargetId => -1;

    public event Action<DamageEvent>? OnDamageDealt;
    public event Action<DamageEvent>? OnDamageReceived;
    public event Action<int>? OnKill;

    public CombatModule(EntityStore store)
    {
        _store = store;
    }

    public void Attack(Vector3 aimDirection)
    {
        // TODO: 实现射击/近战攻击逻辑
    }

    public void Reload()
    {
        // TODO: 实现装弹
    }

    public void SwitchWeapon(int weaponSlot)
    {
        // TODO: 切换武器
    }

    public void UseSkill(int skillId, Vector3? targetPosition = null, int? targetEntityId = null)
    {
        // TODO: 使用技能
    }

    public void SetAimTarget(int entityId)
    {
        // TODO: 设置瞄准目标
    }
}
