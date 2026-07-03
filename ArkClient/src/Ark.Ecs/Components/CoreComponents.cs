using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;

namespace Ark.Ecs.Components;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        核心 Transform 组件（GPU 兼容布局）                       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>世界坐标位置（与 GPU std430 vec4 对齐）</summary>
[StructLayout(LayoutKind.Sequential)]
public struct WorldPosition : IComponent
{
    public float X, Y, Z;
    public float _pad; // 对齐到 16 bytes (vec4)

    public Vector3 ToVector3() => new(X, Y, Z);
    public static WorldPosition From(Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };
}

/// <summary>速度向量（与 GPU std430 vec4 对齐）</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Velocity : IComponent
{
    public float X, Y, Z;
    public float Speed; // 第4分量存标量速度

    public Vector3 ToVector3() => new(X, Y, Z);
}

/// <summary>旋转（四元数，GPU 友好）</summary>
[StructLayout(LayoutKind.Sequential)]
public struct WorldRotation : IComponent
{
    public float X, Y, Z, W;

    public Quaternion ToQuaternion() => new(X, Y, Z, W);
    public static WorldRotation Identity => new() { W = 1f };
    public static WorldRotation From(Quaternion q) => new() { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
}

/// <summary>缩放（均匀缩放 + 非均匀缩放备用）</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Scale : IComponent
{
    public float X, Y, Z;
    public float Uniform; // 常用的均匀缩放

    public static Scale One => new() { X = 1, Y = 1, Z = 1, Uniform = 1 };
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              渲染相关组件                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>MultiMesh 批渲染槽位</summary>
public struct MultiMeshSlot : IComponent
{
    public int   MeshGroupId;  // 属于哪个 MultiMesh 组（怪物类型、建筑类型等）
    public int   InstanceIndex;// 在该组中的实例索引
    public float DistanceToCamera; // LOD 用
}

/// <summary>RenderingServer 直驱实例（非 MultiMesh 的单独渲染）</summary>
public struct RenderInstance : IComponent
{
    public ulong InstanceRid; // Rid.Id
    public ulong MeshRid;
    public int   MaterialId;
}

/// <summary>LOD 等级</summary>
public struct LodLevel : IComponent
{
    public byte CurrentLevel;  // 0 = 最高精度, 255 = 最低/隐藏
    public byte TargetLevel;   // 目标等级（平滑过渡）
    public float Distance;     // 到相机距离
}

/// <summary>可见性状态（GPU 剔除结果）</summary>
public struct Visibility : IComponent
{
    public byte IsVisible;     // 0 = 不可见, 1 = 可见
    public byte WasVisible;    // 上一帧是否可见（用于淡入淡出）
    public ushort OccluderMask;// 被哪些遮挡体遮挡
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              物理相关组件                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>物理 Body 引用（Jolt 物理）</summary>
public struct PhysicsBody : IComponent
{
    public ulong BodyRid;         // PhysicsServer3D body Rid
    public byte  BodyType;        // 0 = Static, 1 = Kinematic, 2 = Dynamic
    public byte  CollisionLayer;
    public byte  CollisionMask;
    public byte  _pad;
}

/// <summary>碰撞包围盒（GPU 剔除 + 空间分区用）</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BoundingBox : IComponent
{
    public float MinX, MinY, MinZ, _pad0;
    public float MaxX, MaxY, MaxZ, _pad1;
}

/// <summary>移动输入（玩家/AI 产生）</summary>
[StructLayout(LayoutKind.Sequential)]
public struct MoveInput : IComponent
{
    public float DirX, DirY, DirZ; // 移动方向（归一化）
    public float TargetSpeed;      // 目标速度
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              网络同步组件                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>网络实体标识</summary>
public struct NetworkId : IComponent
{
    public ulong ServerId;   // 服务端分配的唯一 ID
    public uint  OwnerId;    // 所属玩家 ID（0 = 服务端控制）
    public uint  LastSyncTick; // 上次同步的服务端 Tick
}

/// <summary>网络插值状态</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetworkInterpolation : IComponent
{
    // 起点
    public float FromX, FromY, FromZ, FromTime;
    // 终点
    public float ToX, ToY, ToZ, ToTime;
    // 插值进度
    public float Alpha;
    public float _pad0, _pad1, _pad2;
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                              AI 相关组件                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>AI 状态机状态</summary>
public struct AiState : IComponent
{
    public byte  CurrentState;   // 状态枚举
    public byte  PreviousState;
    public ushort StateFrames;   // 当前状态持续帧数
    public int   TargetEntityId; // 目标实体 ID（-1 = 无）
}

/// <summary>寻路请求/结果</summary>
public struct PathfindingData : IComponent
{
    public float TargetX, TargetY, TargetZ;
    public float NextWaypointX, NextWaypointY, NextWaypointZ;
    public byte  PathStatus;     // 0 = None, 1 = Pending, 2 = Found, 3 = Failed
    public byte  WaypointIndex;
    public ushort TotalWaypoints;
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                           游戏逻辑组件（按功能模块）                             ║
// ║  注意：Health, CombatStats 已迁移到 CombatComponents.cs                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>装备槽引用</summary>
public struct Equipment : IComponent
{
    public int WeaponId;
    public int ArmorId;
    public int AccessoryId;
    public int VehicleId;
}

/// <summary>背包容量</summary>
public struct Inventory : IComponent
{
    public int  OwnerId;       // 所属 Entity ID
    public byte SlotCount;
    public byte UsedSlots;
    public ushort WeightCapacity;
}

/// <summary>建筑状态（基地建造）</summary>
public struct Building : IComponent
{
    public ushort BuildingTypeId;
    public byte   Level;
    public byte   ConstructionProgress; // 0-100
    public int    OwnerId;
}

/// <summary>
/// 建筑权限控制 — 所有者 + 白名单（最多 8 名额外授权玩家）。
/// 适用于所有建筑类型：发射台、工厂、仓库等。
/// 只有所有者或白名单内的玩家可以操作该建筑。
/// </summary>
public struct BuildingAccess : IComponent
{
    /// <summary>建造者/拥有者的玩家 ID</summary>
    public int OwnerId;

    /// <summary>白名单槽位（0 = 空）</summary>
    public int Slot0, Slot1, Slot2, Slot3;
    public int Slot4, Slot5, Slot6, Slot7;

    /// <summary>权限等级：0=仅查看, 1=可操作, 2=可管理(含添加白名单)</summary>
    public byte AccessLevel;

    /// <summary>检查指定玩家是否有权限操作此建筑。</summary>
    public readonly bool HasAccess(int playerId)
    {
        if (playerId <= 0) return false;
        if (playerId == OwnerId) return true;
        if (playerId == Slot0 || playerId == Slot1 || playerId == Slot2 || playerId == Slot3) return true;
        if (playerId == Slot4 || playerId == Slot5 || playerId == Slot6 || playerId == Slot7) return true;
        return false;
    }

    /// <summary>检查指定玩家是否为所有者。</summary>
    public readonly bool IsOwner(int playerId) => playerId > 0 && playerId == OwnerId;

    /// <summary>获取白名单中已使用的槽位数量。</summary>
    public readonly int WhitelistCount
    {
        get
        {
            int c = 0;
            if (Slot0 > 0) c++; if (Slot1 > 0) c++; if (Slot2 > 0) c++; if (Slot3 > 0) c++;
            if (Slot4 > 0) c++; if (Slot5 > 0) c++; if (Slot6 > 0) c++; if (Slot7 > 0) c++;
            return c;
        }
    }

    /// <summary>将玩家添加到白名单。返回 true 如果成功。</summary>
    public bool GrantAccess(int playerId)
    {
        if (playerId <= 0 || HasAccess(playerId)) return false;
        if (Slot0 == 0) { Slot0 = playerId; return true; }
        if (Slot1 == 0) { Slot1 = playerId; return true; }
        if (Slot2 == 0) { Slot2 = playerId; return true; }
        if (Slot3 == 0) { Slot3 = playerId; return true; }
        if (Slot4 == 0) { Slot4 = playerId; return true; }
        if (Slot5 == 0) { Slot5 = playerId; return true; }
        if (Slot6 == 0) { Slot6 = playerId; return true; }
        if (Slot7 == 0) { Slot7 = playerId; return true; }
        return false; // 白名单已满
    }

    /// <summary>从白名单中移除玩家。</summary>
    public bool RevokeAccess(int playerId)
    {
        if (playerId <= 0) return false;
        if (Slot0 == playerId) { Slot0 = 0; return true; }
        if (Slot1 == playerId) { Slot1 = 0; return true; }
        if (Slot2 == playerId) { Slot2 = 0; return true; }
        if (Slot3 == playerId) { Slot3 = 0; return true; }
        if (Slot4 == playerId) { Slot4 = 0; return true; }
        if (Slot5 == playerId) { Slot5 = 0; return true; }
        if (Slot6 == playerId) { Slot6 = 0; return true; }
        if (Slot7 == playerId) { Slot7 = 0; return true; }
        return false;
    }

    /// <summary>转移所有权给新玩家。原所有者自动加入白名单。</summary>
    public bool TransferOwnership(int currentOwnerId, int newOwnerId)
    {
        if (currentOwnerId != OwnerId || newOwnerId <= 0) return false;
        RevokeAccess(newOwnerId); // 如果新所有者在白名单中，先移除
        GrantAccess(currentOwnerId); // 旧所有者加入白名单
        OwnerId = newOwnerId;
        return true;
    }

    /// <summary>创建仅含所有者的权限。</summary>
    public static BuildingAccess OwnerOnly(int ownerId) => new() { OwnerId = ownerId, AccessLevel = 2 };
}

/// <summary>
/// 结构完整性 — 建筑/载具的抗伤害阈值与累积损坏。
/// 低于 DamageThreshold 的单次伤害被忽略；超过阈值的部分累积到 AccumulatedDamage。
/// AccumulatedDamage >= MaxIntegrity 时物体崩塌/摧毁。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct StructuralIntegrity : IComponent
{
    /// <summary>单次伤害低于此值时无效（子弹打不动坦克）</summary>
    public float DamageThreshold;

    /// <summary>累积已受到的有效伤害</summary>
    public float AccumulatedDamage;

    /// <summary>最大结构完整性 — 崩塌临界值</summary>
    public float MaxIntegrity;
}

/// <summary>载具状态</summary>
public struct Vehicle : IComponent
{
    public ushort VehicleTypeId;
    public byte   SeatCount;
    public byte   OccupiedSeats;
    public float  Fuel;
    public float  MaxFuel;
}

/// <summary>宇宙飞船状态</summary>
public struct Spacecraft : IComponent
{
    public float Altitude;       // 高度（米）
    public float OrbitalVelocity;// 轨道速度
    public byte  FlightPhase;    // 0 = Ground, 1 = Launch, 2 = Orbit, 3 = Reentry
    public byte  _pad0;
    public ushort FuelPercent;
}

/// <summary>NPC 对话/任务状态</summary>
public struct NpcInteraction : IComponent
{
    public ushort NpcTypeId;
    public byte   InteractionRadius;
    public byte   QuestGiver;    // bool: 是否有任务
    public int    CurrentDialogId;
}
