using System.Collections.Generic;
using Ark.Bridge.Features.Combat;
using Ark.Gameplay.Combat;

namespace Ark.World;

/// <summary>
/// 世界加载器 — 编排世界的加载流程（敌人武器装备 + 视觉节点生成）。
/// 运行时仅支持 Network 模式；Demo 世界由服务端快照驱动。
/// </summary>
public static class WorldLoader
{
    /// <summary>默认敌方武器 ID 列表。</summary>
    private static readonly int[] DefaultEnemyWeapons = [21, 50, 20]; // AK-47, RPG-7, M4

    /// <summary>
    /// 装备敌方武器并生成视觉节点（由服务端快照中的敌方实体 ID 驱动）。
    /// </summary>
    public static void EquipAndVisualize(
        IReadOnlyList<int> enemyEntityIds,
        CombatGameplayModule combat,
        EnemyVisualManager? enemyVisuals)
    {
        EquipEnemies(enemyEntityIds, combat, DefaultEnemyWeapons);
        enemyVisuals?.SpawnEnemyNodes(enemyEntityIds, DefaultEnemyWeapons);
    }

    private static void EquipEnemies(
        IReadOnlyList<int> enemyIds,
        CombatGameplayModule combat,
        int[] weaponIds)
    {
        for (int i = 0; i < enemyIds.Count; i++)
        {
            combat.EquipWeapon(enemyIds[i], weaponIds[i % weaponIds.Length]);
        }
    }
}
