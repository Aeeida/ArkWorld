using System.Runtime.InteropServices;
using Ark.Analyzers.Attributes;
using Friflo.Engine.ECS;

namespace Ark.Ecs.Components;

/// <summary>
/// 本地玩家运动预测状态（Phase 4）。
/// 由 <c>InputIntentCollectSystem</c> 和 <c>LocalMovementPredictionSystem</c> 写入；
/// 由角色控制器读取并执行碰撞步进（MoveAndSlide）后回写更新后的速度。
/// 权威位置仍走服务端校正。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LocalMovementPredict : IComponent
{
    public float VelocityX;
    public float VelocityY;
    public float VelocityZ;
    public float DesiredVelocityX;
    public float DesiredVelocityZ;
    public int JumpsRemaining;
    public byte IsOnFloor;
    public byte JumpRequested;
    public byte _pad0;
    public byte _pad1;

    // 调参（可由 ECS 初始化或控制器在 Init 时写入）
    public float WalkSpeed;
    public float SprintSpeed;
    public float Acceleration;
    public float Deceleration;
    public float AirControl;
    public float Gravity;
    public float JumpVelocity;
    public int MaxJumps;
}

/// <summary>
/// 相机轨道（绕角色第三人称）状态（Phase 4）。
/// 由 <c>InputIntentCollectSystem</c> 累积鼠标增量；由 <c>CameraOrbitSystem</c> 平滑到目标值；
/// 由相机表现层读取最终 yaw/pitch/zoom 应用到 Camera3D。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CameraOrbitState : IComponent
{
    public float Yaw;
    public float Pitch;
    public float Zoom;
    public float TargetYaw;
    public float TargetPitch;
    public float TargetZoom;
    public float MinPitch;
    public float MaxPitch;
    public float MinZoom;
    public float MaxZoom;
    public float YawSensitivity;
    public float PitchSensitivity;
    public float ZoomSensitivity;
    public float SmoothFactor;
    public byte InvertPitch;
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}

/// <summary>
/// 玩家输入意图（Phase 4）— 抽象自原始输入设备。
/// 由 <c>InputIntentCollectSystem</c> 在 _Process 阶段写入；下游 System 消费此组件而非直接读 Godot Input。
/// 数值已归一化到 [-1, 1]（移动/瞄准）；按钮按帧脉冲（Just 系列）+ 持续位（Pressed 系列）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct InputIntent : IComponent
{
    public float MoveX;          // strafe (left/right) in local frame
    public float MoveZ;          // forward/back in local frame
    public float AimDeltaX;      // mouse delta X (raw, this frame)
    public float AimDeltaY;      // mouse delta Y (raw, this frame)
    public float ZoomDelta;      // wheel delta (raw, this frame)

    public byte JumpJustPressed;
    public byte SprintHeld;
    public byte FireHeld;
    public byte FireJustPressed;
    public byte InteractJustPressed;
    public byte ReloadJustPressed;
    public byte BuildToggleJustPressed;
    public byte AimingHeld;

    public byte HasIntent; // 0 if input is suppressed (e.g. UI focused, build camera mode)
    public byte _pad0;
    public byte _pad1;
    public byte _pad2;
}
