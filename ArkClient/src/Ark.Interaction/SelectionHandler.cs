using Godot;
using System;
using Ark.Bridge.Features.BaseBuilding;
using Ark.UI;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.Interaction;

/// <summary>
/// 选中事件处理器 — 处理光标模式下的建筑/物体选中反馈。
///
/// 职责：
///   • 建筑选中 → 构建标题/详情文本 → 显示 HUD
///   • 坦克工厂选中且已完工 → 打开载具生成面板
///   • 火箭发射台选中且有权限 → 触发 OnLaunchPadActivated 进入太空
///   • 物体选中 → 显示名称和坐标
///   • 选中清除 → 隐藏 HUD
/// </summary>
public sealed class SelectionHandler
{
    private readonly SelectionHUD _hud;
    private readonly VehicleSpawnUI _vehicleUI;
    private readonly BuildingVisualManager _buildingVisuals;
    private readonly EntityStore _store;

    /// <summary>当前本地玩家 ID（用于权限检查）。</summary>
    public int LocalPlayerId { get; set; } = 1;

    /// <summary>火箭发射台被授权玩家激活 — 参数：(entityId, worldPosition)。</summary>
    public event Action<int, Vector3>? OnLaunchPadActivated;
    public event Action<int, string>? OnNpcSelected;

    public SelectionHandler(SelectionHUD hud, VehicleSpawnUI vehicleUI,
        BuildingVisualManager buildingVisuals, EntityStore store)
    {
        _hud             = hud;
        _vehicleUI       = vehicleUI;
        _buildingVisuals = buildingVisuals;
        _store           = store;
    }

    /// <summary>建筑被点选时回调。</summary>
    public void OnBuildingSelected(int entityId, int typeId, bool isComplete, Vector3 hitPos)
    {
        var def    = BuildingDef.Get(typeId);
        string name   = def?.Name ?? $"Building #{typeId}";
        string status = isComplete ? "\u2705 已完工" : "\U0001f6a7 建造中";
        string detail = $"{status}\nEntity: {entityId}";
        if (def != null) detail += $"\n{def.Value.Description}";

        // ── 权限信息 ──
        bool hasAccess = CheckAccess(entityId);
        if (!hasAccess)
            detail += "\n\u26d4 无操作权限";

        _hud.ShowSelection($"{def?.Icon} {name}", detail, 6f);

        // 坦克工厂（typeId == 6）且已完工 → 打开载具面板
        if (typeId == 6 && isComplete && hasAccess && !_vehicleUI.IsOpen)
        {
            var worldPos = _buildingVisuals.GetBuildingWorldPos(entityId);
            _vehicleUI.ShowForFactory(entityId, worldPos);
        }

        // 火箭发射台（typeId == 5）且已完工 + 有权限 → 进入太空配装
        if (typeId == 5 && isComplete && hasAccess)
        {
            var worldPos = _buildingVisuals.GetBuildingWorldPos(entityId);
            OnLaunchPadActivated?.Invoke(entityId, worldPos);
        }
    }

    /// <summary>非建筑物体被点选时回调。</summary>
    public void OnObjectSelected(GodotObject collider, Vector3 hitPos)
    {
        string name = collider is Node n ? n.Name : "Unknown";
        _hud.ShowSelection($"\U0001f4cc {name}", $"Pos: ({hitPos.X:F1}, {hitPos.Y:F1}, {hitPos.Z:F1})", 4f);
    }

    /// <summary>选中被清除时回调。</summary>
    public void OnSelectionCleared()
    {
        _hud.HideSelection();
    }

    /// <summary>检查本地玩家是否有权操作指定建筑。</summary>
    private bool CheckAccess(int entityId)
    {
        if (Ark.Services.GameServices.IsNetworkMode)
        {
            if (_buildingVisuals.TryGetBuildingOwner(entityId, out var ownerId))
                return ownerId == Ark.Services.GameServices.RemotePlayerId;

            return true;
        }

        if (!_store.TryGetEntityById(entityId, out var entity)) return false;
        if (!entity.TryGetComponent<BuildingAccess>(out var access)) return true; // 无权限组件 = 公开
        return access.HasAccess(LocalPlayerId);
    }
}
