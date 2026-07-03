using Friflo.Engine.ECS;

namespace Ark.Ecs.Tags;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                            小队系统标签                                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>小队队长（当前受玩家控制的角色）</summary>
public struct SquadLeader : ITag { }

/// <summary>小队跟随者（AI 控制，跟随队长）</summary>
public struct SquadFollower : ITag { }

/// <summary>小队成员（队长和跟随者都有此标签）</summary>
public struct InSquad : ITag { }

/// <summary>可切换控制（支持 F1-F5 切换）</summary>
public struct Controllable : ITag { }

/// <summary>正在跟随（激活跟随行为）</summary>
public struct Following : ITag { }

/// <summary>待机状态（停止跟随，原地待命）</summary>
public struct HoldPosition : ITag { }

/// <summary>战斗模式（攻击附近敌人）</summary>
public struct CombatMode : ITag { }

/// <summary>需要寻路到目标位置</summary>
public struct NeedsNavigation : ITag { }

/// <summary>已被瞬移到新位置（Godot 节点需在下一帧同步 GlobalPosition）</summary>
public struct Teleported : ITag { }
