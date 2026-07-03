using GameServer.Domain.Entities;

namespace GameServer.Application.Core;

/// <summary>
/// 出生点分配接口 — 为新角色分配初始位置。
/// 由 WorldModule 实现，在角色创建时自动注入。
/// </summary>
public interface ISpawnPointAssigner
{
    /// <summary>
    /// 根据阵营为新角色分配初始出生位置。
    /// </summary>
    Task AssignSpawnPositionAsync(Player player, CancellationToken ct = default);
}
