using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.Gameplay.Combat.Systems;

/// <summary>
/// 小队战斗系统 — 当领队开火时，AI 控制的队友自动跟随射击。
///
/// 职责：
///   • 检测领队 WeaponState.IsFiring
///   • 遍历友方队员实体，读取各自的 CombatTarget 计算瞄准方向
///   • 各队员消耗自己的弹匣/弹药
///
/// 每个队员使用自己的 CombatTarget.AimPoint 作为射击方向，
/// 由 SquadFollowSystem.SyncMemberTargets 每帧从领队目标同步。
/// </summary>
public sealed class SquadCombatSystem
{
    private readonly EntityStore _store;
    private readonly CombatGameplayModule _combat;

    public SquadCombatSystem(EntityStore store, CombatGameplayModule combat)
    {
        _store  = store;
        _combat = combat;
    }

    /// <summary>
    /// 每帧调用。当 leaderEntity 正在射击时，让 memberEntities 中非被控队员也开火。
    /// 每个队员从自身 CombatTarget 组件读取瞄准点，计算从武器口到瞄准点的方向。
    /// </summary>
    /// <param name="leaderEntityId">领队实体 ID</param>
    /// <param name="leaderAimDirection">领队瞄准方向（后备，队员无目标时使用）</param>
    /// <param name="memberEntityIds">队员实体 ID 列表</param>
    /// <param name="memberPositions">队员世界坐标列表（对应 memberEntityIds）</param>
    /// <param name="controlledSlot">当前被玩家控制的槽位（跳过该队员）</param>
    public void Update(
        int leaderEntityId,
        Vector3 leaderAimDirection,
        ReadOnlySpan<int> memberEntityIds,
        ReadOnlySpan<Vector3> memberPositions,
        int controlledSlot)
    {
        var leaderEntity = _store.GetEntityById(leaderEntityId);
        if (leaderEntity.IsNull) return;
        if (!leaderEntity.TryGetComponent<WeaponState>(out var ws)) return;
        if (ws.IsFiring == 0) return;

        float gameTime = _combat.GameTime;

        for (int i = 0; i < memberEntityIds.Length; i++)
        {
            // 槽位从 1 开始，跳过被控制的
            if (i + 1 == controlledSlot) continue;

            int mid = memberEntityIds[i];
            if (mid <= 0) continue;
            var mEntity = _store.GetEntityById(mid);
            if (mEntity.IsNull) continue;

            var pos = memberPositions[i];
            var origin = new Vector3(pos.X, pos.Y + 1.2f, pos.Z);

            // 从队员自身 CombatTarget 读取瞄准点，计算武器口→瞄准点方向
            Vector3 fireDir;
            if (mEntity.TryGetComponent<CombatTarget>(out var ct) && ct.HasTarget != 0)
            {
                var aimPoint = new Vector3(ct.AimPointX, ct.AimPointY, ct.AimPointZ);
                var toTarget = aimPoint - origin;
                float lenSq = toTarget.LengthSquared();
                fireDir = lenSq > 1f ? Vector3.Normalize(toTarget) : leaderAimDirection;
            }
            else
            {
                fireDir = leaderAimDirection;
            }

            _combat.TryFire(mid, origin, fireDir, gameTime);
        }
    }
}
