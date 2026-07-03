using Godot;

namespace Ark.Bridge.Utilities;

/// <summary>
/// StringName 缓存 — 避免热路径重复创建字符串。
/// </summary>
public static class CachedNames
{
    // ═══ Transform ═══
    public static readonly StringName Position        = "position";
    public static readonly StringName GlobalPosition  = "global_position";
    public static readonly StringName Transform3D     = "transform";
    public static readonly StringName GlobalTransform = "global_transform";
    public static readonly StringName Rotation        = "rotation";
    public static readonly StringName Scale           = "scale";

    // ═══ Visibility ═══
    public static readonly StringName Visible = "visible";

    // ═══ Animation ═══
    public static readonly StringName Parameters    = "parameters";
    public static readonly StringName BlendPosition = "blend_position";
    public static readonly StringName TimeScale     = "time_scale";
    public static readonly StringName Active        = "active";

    // ═══ Physics ═══
    public static readonly StringName LinearVelocity  = "linear_velocity";
    public static readonly StringName AngularVelocity = "angular_velocity";

    // ═══ MultiMesh ═══
    public static readonly StringName Buffer               = "buffer";
    public static readonly StringName VisibleInstanceCount = "visible_instance_count";
    public static readonly StringName InstanceCount        = "instance_count";

    // ═══ Signals ═══
    public static readonly StringName TreeExited  = "tree_exited";
    public static readonly StringName BodyEntered = "body_entered";
    public static readonly StringName BodyExited  = "body_exited";

    // ═══ Methods ═══
    public static readonly StringName QueueFree = "queue_free";
    public static readonly StringName AddChild  = "add_child";
}

/// <summary>
/// 简易计时器 — 用于性能监控。
/// </summary>
public struct ScopedTimer : System.IDisposable
{
    private readonly string _name;
    private readonly ulong  _startTicks;
    private readonly bool   _enabled;

    public ScopedTimer(string name, bool enabled = true)
    {
        _name       = name;
        _enabled    = enabled;
        _startTicks = enabled ? Time.GetTicksUsec() : 0;
    }

    public void Dispose()
    {
        if (!_enabled) return;
        ulong elapsed = Time.GetTicksUsec() - _startTicks;
        if (elapsed > 1000) // 只打印 >1ms 的
        {
            GD.Print($"[PERF] {_name}: {elapsed / 1000f:F2}ms");
        }
    }
}
