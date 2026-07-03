using Friflo.Engine.ECS;

namespace Ark.Ecs.Tags;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              实体类型标签                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>本地玩家控制的角色</summary>
public struct LocalPlayer : ITag { }

/// <summary>远程玩家（其他玩家）</summary>
public struct RemotePlayer : ITag { }

/// <summary>NPC（非敌对）</summary>
public struct Npc : ITag { }

/// <summary>敌对怪物</summary>
public struct Monster : ITag { }

/// <summary>可破坏的环境物体</summary>
public struct Destructible : ITag { }

/// <summary>建筑物</summary>
public struct BuildingTag : ITag { }

/// <summary>载具</summary>
public struct VehicleTag : ITag { }

/// <summary>宇宙飞船</summary>
public struct SpacecraftTag : ITag { }

/// <summary>弹丸/投射物</summary>
public struct Projectile : ITag { }

/// <summary>粒子发射器</summary>
public struct ParticleEmitter : ITag { }

/// <summary>环境动态物体（树、草、岩石等）</summary>
public struct EnvironmentDynamic : ITag { }

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              状态标签                                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>刚创建，需要初始化渲染资源</summary>
public struct PendingSpawn : ITag { }

/// <summary>标记为待销毁（下一帧处理）</summary>
public struct PendingDestroy : ITag { }

/// <summary>已死亡但尚未移除</summary>
public struct Dead : ITag { }

/// <summary>正在被玩家交互</summary>
public struct Interacting : ITag { }

/// <summary>处于战斗状态</summary>
public struct InCombat : ITag { }

/// <summary>正在移动</summary>
public struct Moving : ITag { }

/// <summary>正在跳跃/空中</summary>
public struct Airborne : ITag { }

/// <summary>正在载具内</summary>
public struct InVehicle : ITag { }

/// <summary>处于太空（不受地面物理影响）</summary>
public struct InSpace : ITag { }

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              渲染标签                                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>使用 MultiMesh 批渲染</summary>
public struct UsesMultiMesh : ITag { }

/// <summary>使用 RenderingServer 直驱（非 Node）</summary>
public struct UsesDirectRender : ITag { }

/// <summary>有关联的 Node3D（近景/可交互）</summary>
public struct HasNode : ITag { }

/// <summary>需要 GPU 计算移动（群体模拟）</summary>
public struct GpuSimulated : ITag { }

/// <summary>需要 LOD 切换</summary>
public struct LodEnabled : ITag { }

/// <summary>始终渲染（不剔除）</summary>
public struct AlwaysVisible : ITag { }

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              网络标签                                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>本地权威（客户端预测）</summary>
public struct LocalAuthority : ITag { }

/// <summary>服务端权威（需要插值）</summary>
public struct ServerAuthority : ITag { }

/// <summary>需要同步到服务端</summary>
public struct DirtySync : ITag { }

/// <summary>刚收到服务端更新</summary>
public struct ReceivedUpdate : ITag { }

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              AI 标签                                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>需要 AI 更新</summary>
public struct AiControlled : ITag { }

/// <summary>需要寻路</summary>
public struct NeedsPathfinding : ITag { }

/// <summary>正在追击目标</summary>
public struct Chasing : ITag { }

/// <summary>正在巡逻</summary>
public struct Patrolling : ITag { }

/// <summary>正在逃跑</summary>
public struct Fleeing : ITag { }

/// <summary>正在警戒</summary>
public struct Alert : ITag { }

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              空间分区标签                                       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>在玩家 AOI（兴趣区域）内 — 需要完整更新</summary>
public struct InAoi : ITag { }

/// <summary>在扩展 AOI 内 — 低频更新</summary>
public struct InExtendedAoi : ITag { }

/// <summary>在 AOI 外 — 休眠状态</summary>
public struct OutOfAoi : ITag { }
