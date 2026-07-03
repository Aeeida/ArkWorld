// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  MOVED → Ark.GameInput.InputActions (project: Ark.Input)               ║
// ║  此文件保留为向后兼容转发。新代码请直接 using Ark.GameInput;              ║
// ╚══════════════════════════════════════════════════════════════════════════╝

namespace Ark.Player;

/// <summary>
/// 向后兼容转发 — 实际实现已迁移到 Ark.GameInput.InputActions。
/// </summary>
public static class InputActions
{
    public static void RegisterAll() => Ark.GameInput.InputActions.RegisterAll();
}
