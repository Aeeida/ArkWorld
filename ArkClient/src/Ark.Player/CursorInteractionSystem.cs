using Godot;
using System;
using Ark.Bridge.Features.BaseBuilding;
using Ark.Abstractions;

namespace Ark.Player;

/// <summary>
/// 光标交互系统 — 鼠标解锁（光标模式）下的所有点击交互统一入口。
///
/// 设计原则：
///   • 仅在鼠标未锁定时激活，锁定时完全休眠
///   • 射线检测在 _PhysicsProcess 中执行（Godot 要求 DirectSpaceState 仅在物理帧可用）
///   • _UnhandledInput 只记录"待处理的点击请求"，不做任何物理查询
///   • 通过事件向外通知命中结果，不直接操作 UI
///
/// 使用方式：
///   1. GameBootstrap 创建并 AddChild
///   2. 调用 Initialize(player, buildingVisuals) 注入依赖
///   3. 订阅 OnBuildingSelected / OnEntitySelected 等事件
/// </summary>
public partial class CursorInteractionSystem : Node
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          依赖（由 Initialize 注入）
    // ═══════════════════════════════════════════════════════════════════════

    private Func<IControllable?>? _activeResolver;
    private BuildingVisualManager? _buildingVisuals;

    // ═══════════════════════════════════════════════════════════════════════
    //                          待处理点击请求
    // ═══════════════════════════════════════════════════════════════════════

    private bool _pendingClick;
    private Vector2 _pendingScreenPos;

    // ═══════════════════════════════════════════════════════════════════════
    //                          事件
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 点击命中建筑：(entityId, typeId, isComplete, worldPosition)
    /// </summary>
    public event Action<int, int, bool, Vector3>? OnBuildingSelected;

    /// <summary>
    /// 点击命中其他 3D 物体（非建筑碰撞体）：(collider, hitPosition)
    /// 未来可扩展为人物/矿物/NPC 选择。
    /// </summary>
    public event Action<GodotObject, Vector3>? OnObjectSelected;

    /// <summary>
    /// 点击未命中任何物体（点空白区域）。
    /// </summary>
    public event Action? OnSelectionCleared;

    // ═══════════════════════════════════════════════════════════════════════
    //                          初始化
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化 — 注入活动控制器解析器和建筑视觉管理器。
    /// activeResolver 每次调用返回当前被玩家控制的 IControllable（角色、载具等）。
    /// </summary>
    public void Initialize(Func<IControllable?> activeResolver, BuildingVisualManager? buildingVisuals)
    {
        _activeResolver  = activeResolver;
        _buildingVisuals = buildingVisuals;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          输入捕获（仅记录请求）
    // ═══════════════════════════════════════════════════════════════════════

    public override void _UnhandledInput(InputEvent @event)
    {
        // 仅在光标模式（鼠标未锁定）且非建造模式下响应
        var active = _activeResolver?.Invoke();
        if (active == null || active.IsMouseCaptured) return;
        if (active.IsBuildModeActive) return;

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            _pendingClick = true;
            _pendingScreenPos = mb.Position;
            GetViewport().SetInputAsHandled();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          物理帧执行射线检测
    // ═══════════════════════════════════════════════════════════════════════

    public override void _PhysicsProcess(double delta)
    {
        if (!_pendingClick) return;
        _pendingClick = false;

        var camera = _activeResolver?.Invoke()?.Camera;
        if (camera == null || !camera.IsInsideTree()) return;

        var from = camera.ProjectRayOrigin(_pendingScreenPos);
        var dir  = camera.ProjectRayNormal(_pendingScreenPos);

        var spaceState = camera.GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return;

        var rayParams = PhysicsRayQueryParameters3D.Create(from, from + dir * 200f);
        // 检测所有碰撞层（建筑 Layer1 + 未来可扩展的角色/矿物层）
        rayParams.CollisionMask = 0xFFFFFFFF;

        var result = spaceState.IntersectRay(rayParams);
        if (result == null || result.Count == 0)
        {
            OnSelectionCleared?.Invoke();
            return;
        }

        var collider = result["collider"].AsGodotObject();
        var hitPos   = (Vector3)result["position"];

        // 优先检测建筑
        if (_buildingVisuals != null &&
            _buildingVisuals.TryGetEntityIdByCollider(collider, out int entityId, out int typeId, out bool isComplete))
        {
            OnBuildingSelected?.Invoke(entityId, typeId, isComplete, hitPos);
            return;
        }

        // 非建筑碰撞体 — 通用物体选择
        OnObjectSelected?.Invoke(collider, hitPos);
    }
}
