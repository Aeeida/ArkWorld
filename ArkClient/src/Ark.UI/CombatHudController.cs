using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.UI;

/// <summary>
/// 战斗 HUD 控制器 — 每帧从 ECS 读取活动角色的武器/弹药数据，驱动 SelectionHUD 显示。
///
/// 设计原则：
///   • 不依赖 Ark.Gameplay.Combat — 武器名称由外部查询后传入
///   • 仅读取 ECS 组件（WeaponState、AmmoState）
///   • 调用 SelectionHUD API 刷新 UI
/// </summary>
public sealed class CombatHudController
{
    /// <summary>外部注入的武器名称查询委托。参数：weaponDefId → 返回名称。</summary>
    public System.Func<int, string>? WeaponNameResolver { get; set; }

    private readonly EntityStore _store;
    private readonly SelectionHUD _hud;
    private readonly SeatWeaponPanel? _seatWeaponPanel;

    public CombatHudController(EntityStore store, SelectionHUD hud, SeatWeaponPanel? seatWeaponPanel = null)
    {
        _store = store;
        _hud   = hud;
        _seatWeaponPanel = seatWeaponPanel;
    }

    /// <summary>
    /// 每帧调用。activeEntityId = 当前被控角色实体 ID，charName = 显示名称。
    /// </summary>
    public void Update(int activeEntityId, string charName)
    {
        if (activeEntityId <= 0) return;

        var entity = _store.GetEntityById(activeEntityId);
        if (entity.IsNull) return;

        string weaponName = "—";
        int currentMag = 0, magCap = 0, reserve = 0;
        string debugInfo = string.Empty;

        if (entity.TryGetComponent<WeaponState>(out var ws))
        {
            weaponName = WeaponNameResolver?.Invoke(ws.WeaponDefId) ?? $"Weapon #{ws.WeaponDefId}";
            if (ws.IsReloading != 0) weaponName += " [装填中]";
        }

        if (entity.TryGetComponent<AmmoState>(out var ammo))
        {
            currentMag = ammo.CurrentMag;
            magCap     = ammo.MagCapacity;
            reserve    = ammo.ReserveAmmo;
        }

        if (entity.TryGetComponent<MountedWeaponRuntimeState>(out var mountedRuntime))
        {
            debugInfo = $"HEAT {mountedRuntime.Heat:P0}  |  CYCLE {mountedRuntime.FireCycleRemaining:F2}s";
            if (mountedRuntime.IsReloading != 0)
                debugInfo += $"  |  RELOAD {mountedRuntime.ReloadRemaining:F2}s";
            if (mountedRuntime.IsMaintaining != 0)
                debugInfo += $"  |  MAINT {mountedRuntime.MaintenanceRemaining:F2}s";
            if (mountedRuntime.RepairStepCount > 0)
                debugInfo += $"  |  STEP {mountedRuntime.RepairStep}/{mountedRuntime.RepairStepCount} {mountedRuntime.OperationProgress:P0} MAT {mountedRuntime.MaterialUnits} SK {mountedRuntime.SkillScalar:F2}";
            else if (mountedRuntime.IsOverheated != 0)
                debugInfo += "  |  OVERHEATED";
            else if (mountedRuntime.FaultCode == 1)
                debugInfo += "  |  JAMMED";
            else if (mountedRuntime.FaultCode == 3)
                debugInfo += "  |  FEED";
            else if (mountedRuntime.FaultCode == 4)
                debugInfo += "  |  ALIGN";

            _seatWeaponPanel?.UpdatePanel(
                weaponName,
                mountedRuntime.Heat,
                mountedRuntime.ReloadNormalized,
                mountedRuntime.FireCycleRemaining,
                mountedRuntime.ReloadRemaining,
                mountedRuntime.MaintenanceLevel,
                mountedRuntime.MaintenanceRemaining,
                mountedRuntime.OperationProgress,
                mountedRuntime.SkillScalar,
                mountedRuntime.RepairStep,
                mountedRuntime.RepairStepCount,
                mountedRuntime.MaterialUnits,
                mountedRuntime.FaultCode);
        }
        else
        {
            _seatWeaponPanel?.HidePanel();
        }

        _hud.UpdateCharacterInfo(charName, weaponName, currentMag, magCap, reserve, debugInfo);
    }
}
