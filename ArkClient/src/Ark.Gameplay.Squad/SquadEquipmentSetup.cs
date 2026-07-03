using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Gameplay.Combat;
using Ark.Abstractions;

namespace Ark.Gameplay.Squad;

/// <summary>
/// 小队装备初始化 — 为领队和所有队员装备武器并设置战斗属性。
///
/// 纯 ECS 层，不依赖 Godot。
/// 视觉武器挂载由外部回调处理。
/// </summary>
public static class SquadEquipmentSetup
{
    /// <summary>
    /// 为领队装备武器并设置战斗属性。
    /// </summary>
    public static void EquipLeader(CombatGameplayModule combat, Entity leaderEntity, int weaponDefId = 20)
    {
        combat.EquipWeapon(leaderEntity.Id, weaponDefId);
        combat.MakeCombatant(leaderEntity.Id, 100f, 5f, 3f, 0.2f, 0.1f, 10f, 0.05f);
        if (!leaderEntity.Tags.Has<Friendly>()) leaderEntity.AddTag<Friendly>();
    }

    /// <summary>
    /// 为所有队员装备武器并设置战斗属性。
    /// </summary>
    /// <param name="getMember">按槽位获取队员：(slotIndex) → controller or null</param>
    /// <param name="memberCount">队员数量（不含队长）</param>
    /// <param name="weaponDefId">武器定义 ID</param>
    /// <param name="onMemberEquipped">队员装备完成后回调（entityId, slotIndex）— 供视觉层挂载武器模型</param>
    public static void EquipMembers(
        CombatGameplayModule combat,
        System.Func<int, ISquadMemberController?> getMember,
        int memberCount,
        int weaponDefId = 20,
        System.Action<int, int>? onMemberEquipped = null)
    {
        for (int i = 0; i < memberCount; i++)
        {
            var member = getMember(i + 1);
            if (member == null) continue;

            combat.EquipWeapon(member.Entity.Id, weaponDefId);
            combat.MakeCombatant(member.Entity.Id, 80f, 3f, 3f, 0.1f, 0f, 8f, 0.05f);

            member.Entity.AddComponent(new BoundingBox
            {
                MinX = -0.35f, MinY = 0, MinZ = -0.35f,
                MaxX =  0.35f, MaxY = 1.6f, MaxZ = 0.35f,
            });
            if (!member.Entity.Tags.Has<Friendly>()) member.Entity.AddTag<Friendly>();

            onMemberEquipped?.Invoke(member.Entity.Id, i + 1);
        }
    }
}
