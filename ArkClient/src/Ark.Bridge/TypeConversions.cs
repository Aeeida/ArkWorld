using System.Runtime.CompilerServices;
using SysVec3 = System.Numerics.Vector3;
using SysQuat = System.Numerics.Quaternion;
using GodotVec3 = Godot.Vector3;
using GodotQuat = Godot.Quaternion;
using GodotColor = Godot.Color;

namespace Ark.Bridge;

/// <summary>
/// Godot ↔ System.Numerics 类型转换扩展方法。
/// 
/// 用于类库（使用 System.Numerics）与主项目（使用 Godot 类型）之间的桥接。
/// 所有转换方法都是内联的，零运行时开销。
/// </summary>
public static class TypeConversions
{
    // ═══════════════════════════════════════════════════════════════════════════
    //                           Vector3 转换
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>System.Numerics.Vector3 → Godot.Vector3</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GodotVec3 ToGodot(this SysVec3 v) => new(v.X, v.Y, v.Z);

    /// <summary>Godot.Vector3 → System.Numerics.Vector3</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SysVec3 ToSystem(this GodotVec3 v) => new(v.X, v.Y, v.Z);

    // ═══════════════════════════════════════════════════════════════════════════
    //                           Quaternion 转换
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>System.Numerics.Quaternion → Godot.Quaternion</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GodotQuat ToGodot(this SysQuat q) => new(q.X, q.Y, q.Z, q.W);

    /// <summary>Godot.Quaternion → System.Numerics.Quaternion</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SysQuat ToSystem(this GodotQuat q) => new(q.X, q.Y, q.Z, q.W);

    // ═══════════════════════════════════════════════════════════════════════════
    //                           Color 转换（用于 BuildingDef 等）
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>RGBA float tuple → Godot.Color</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GodotColor ToGodotColor(float r, float g, float b, float a = 1f)
        => new(r, g, b, a);

    /// <summary>Godot.Color → RGBA tuple</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (float R, float G, float B, float A) ToRgba(this GodotColor c)
        => (c.R, c.G, c.B, c.A);

    // ═══════════════════════════════════════════════════════════════════════════
    //                           ECS 组件辅助方法
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>从 ECS WorldPosition 组件创建 Godot.Vector3</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GodotVec3 ToGodotPosition(this Ark.Ecs.Components.WorldPosition wp)
        => new(wp.X, wp.Y, wp.Z);

    /// <summary>从 ECS WorldRotation 组件创建 Godot.Quaternion</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GodotQuat ToGodotRotation(this Ark.Ecs.Components.WorldRotation wr)
        => new(wr.X, wr.Y, wr.Z, wr.W);

    /// <summary>从 Godot.Vector3 创建 ECS WorldPosition 组件</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ark.Ecs.Components.WorldPosition ToWorldPosition(this GodotVec3 v)
        => new() { X = v.X, Y = v.Y, Z = v.Z };

    /// <summary>从 Godot.Quaternion 创建 ECS WorldRotation 组件</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ark.Ecs.Components.WorldRotation ToWorldRotation(this GodotQuat q)
        => new() { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
}
