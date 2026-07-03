namespace Ark.Debug;

/// <summary>
/// 调试工具注册表 — 运行时 Gizmo、性能面板、ECS 实体检查器。
/// TODO: 实现运行时调试 UI 面板、实体树可视化、系统耗时追踪。
/// </summary>
public static class DebugTools
{
    private static bool _enabled;

    /// <summary>启用/禁用调试工具。</summary>
    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>在指定世界位置绘制调试球体（仅在启用时）。</summary>
    public static void DrawSphere(System.Numerics.Vector3 center, float radius, uint color = 0xFF00FF00) { }

    /// <summary>在指定位置之间绘制调试线段。</summary>
    public static void DrawLine(System.Numerics.Vector3 from, System.Numerics.Vector3 to, uint color = 0xFFFFFFFF) { }

    /// <summary>在屏幕上显示文本叠加。</summary>
    public static void DrawText(string text, System.Numerics.Vector2 screenPos) { }
}
