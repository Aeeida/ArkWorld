using System.Runtime.InteropServices;
using Friflo.Engine.ECS;

namespace Ark.Ecs.Components;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                           小队系统 ECS 组件                                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 小队成员组件 — 标识实体属于哪个小队，以及在队伍中的位置。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SquadMember : IComponent
{
    /// <summary>小队 ID（0 = 默认小队）</summary>
    public int   SquadId;

    /// <summary>在小队中的索引（0 = 队长，1-5 = 队员）</summary>
    public byte  SlotIndex;

    /// <summary>是否为当前控制的角色</summary>
    public byte  IsControlled;

    /// <summary>队员颜色标识（用于视觉区分）</summary>
    public byte  ColorIndex;

    public byte  _pad;
}

/// <summary>
/// 跟随目标组件 — 指定实体应该跟随谁。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FollowTarget : IComponent
{
    /// <summary>跟随的目标实体 ID（-1 = 无目标）</summary>
    public int   TargetEntityId;

    /// <summary>期望与目标的距离</summary>
    public float FollowDistance;

    /// <summary>允许的最小距离（小于此距离停止移动）</summary>
    public float StopDistance;

    /// <summary>跟随的角度偏移（弧度，用于分散站位）</summary>
    public float AngleOffset;
}

/// <summary>
/// 小队阵型偏移 — 定义队员相对于队长的站位。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FormationOffset : IComponent
{
    /// <summary>相对于队长的本地偏移（X = 左右，Z = 前后）</summary>
    public float OffsetX;
    public float OffsetZ;

    /// <summary>阵型类型（0 = V形，1 = 横排，2 = 纵列，3 = 菱形）</summary>
    public byte  FormationType;

    public byte  _pad0, _pad1, _pad2;
}

/// <summary>
/// AI 移动状态 — 用于小队成员的移动控制。
/// <para>
/// 使用静态工厂方法创建（避免手动分散赋值）：
/// <code>
/// movement = AiMovement.MoveTo(pos, speed, yaw);   // 正常移动
/// movement = AiMovement.Sprint(pos, yaw);           // 远距冲刺
/// movement = AiMovement.Teleport(pos, yaw);         // 超远瞬移
/// movement = AiMovement.Idle(yaw);                  // 停止 + 面朝方向
/// movement = AiMovement.Arrived(prev);              // 标记到达（保留目标）
/// </code>
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AiMovement : IComponent
{
    /// <summary>目标位置</summary>
    public float TargetX, TargetY, TargetZ;

    /// <summary>移动速度</summary>
    public float MoveSpeed;

    /// <summary>停止时面朝方向（弧度，与领队一致）</summary>
    public float FacingYaw;

    /// <summary>是否正在移动</summary>
    public byte  IsMoving;

    /// <summary>是否已到达目标</summary>
    public byte  HasArrived;

    public byte  _pad0, _pad1;

    // ═══════════════════════════════════════════════════════════════════
    //                     工厂方法（统一状态构建）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>向指定位置移动。</summary>
    public static AiMovement MoveTo(float x, float y, float z, float speed, float facingYaw) => new()
    {
        TargetX = x, TargetY = y, TargetZ = z,
        MoveSpeed = speed, FacingYaw = facingYaw,
        IsMoving = 1, HasArrived = 0,
    };

    /// <summary>远距离冲刺（高速追赶）。</summary>
    public static AiMovement Sprint(float x, float y, float z, float facingYaw) => new()
    {
        TargetX = x, TargetY = y, TargetZ = z,
        MoveSpeed = 500f, FacingYaw = facingYaw,
        IsMoving = 1, HasArrived = 0,
    };

    /// <summary>停止移动，面朝指定方向。</summary>
    public static AiMovement Idle(float facingYaw) => new()
    {
        FacingYaw = facingYaw,
        IsMoving = 0, HasArrived = 1,
    };

    /// <summary>标记已到达（保留当前目标和朝向）。</summary>
    public static AiMovement Arrived(in AiMovement prev) => new()
    {
        TargetX = prev.TargetX, TargetY = prev.TargetY, TargetZ = prev.TargetZ,
        MoveSpeed = prev.MoveSpeed, FacingYaw = prev.FacingYaw,
        IsMoving = 0, HasArrived = 1,
    };

    /// <summary>就地更新目标位置和朝向（保持移动状态不变）。</summary>
    public void UpdateTarget(float x, float y, float z, float facingYaw)
    {
        TargetX = x; TargetY = y; TargetZ = z;
        FacingYaw = facingYaw;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              阵型预设
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 阵型预设 — 定义常用阵型的队员站位。
/// </summary>
public static class SquadFormations
{
    /// <summary>V 形阵（默认）</summary>
    public static readonly (float X, float Z)[] VShape =
    [
        (0f,   0f),    // 0: 队长
        (-2f, -2f),    // 1: 左后
        ( 2f, -2f),    // 2: 右后
        (-4f, -4f),    // 3: 左后后
        ( 4f, -4f),    // 4: 右后后
        ( 0f, -3f),    // 5: 正后
    ];

    /// <summary>横排阵</summary>
    public static readonly (float X, float Z)[] Line =
    [
        ( 0f, 0f),     // 0: 队长
        (-2f, 0f),     // 1
        ( 2f, 0f),     // 2
        (-4f, 0f),     // 3
        ( 4f, 0f),     // 4
        (-6f, 0f),     // 5
    ];

    /// <summary>纵列阵</summary>
    public static readonly (float X, float Z)[] Column =
    [
        (0f,  0f),     // 0: 队长
        (0f, -2f),     // 1
        (0f, -4f),     // 2
        (0f, -6f),     // 3
        (0f, -8f),     // 4
        (0f, -10f),    // 5
    ];

    /// <summary>菱形阵</summary>
    public static readonly (float X, float Z)[] Diamond =
    [
        ( 0f,  0f),    // 0: 队长
        ( 0f, -2f),    // 1: 后
        (-2f, -1f),    // 2: 左
        ( 2f, -1f),    // 3: 右
        ( 0f, -4f),    // 4: 尾
        ( 0f,  2f),    // 5: 前（侦察）
    ];

    /// <summary>根据阵型类型获取站位数组</summary>
    public static (float X, float Z)[] Get(byte formationType) => formationType switch
    {
        0 => VShape,
        1 => Line,
        2 => Column,
        3 => Diamond,
        _ => VShape
    };
}
