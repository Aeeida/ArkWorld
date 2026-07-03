using Godot;
using System;
using System.Collections.Generic;
using Ark.Events;
using Ark.Shared.Data;

namespace Ark.UI;

/// <summary>
/// 模式 HUD 管理器 — 根据服务端确认的玩法模式切换显示对应 HUD 层。
///
/// 职责：
///   • 持有 CivilianHud / CombatModeHud / SpaceModeHud / GameplayModeSwitchPanel
///   • 监听服务端模式切换确认，自动淡入/切换对应 HUD
///   • 管理动作栏槽位与客户端技能注册表绑定
///   • 转发槽位激活事件供网络层发送至服务端
///
/// 设计原则：服务端始终优先 — 所有技能属性由服务端定义，客户端仅渲染。
/// </summary>
public partial class ModeHudManager : Node
{
    // ─── 子 HUD ───
    private CivilianHud?          _civilianHud;
    private CombatModeHud?        _combatHud;
    private SpaceModeHud?         _spaceHud;
    private GameplayModeSwitchPanel? _switchPanel;
    private CrosshairWidget?      _crosshair;
    private SelectionHUD?         _selectionHud;

    // ─── 依赖 ───
    private EventBus? _eventBus;

    // ─── 状态 ───
    private ClientGameplayMode _currentMode = ClientGameplayMode.Combat;
    private bool _hudVisible = true;
    private readonly ClientAbilityDefRegistry _abilityRegistry = new();
    private readonly Dictionary<ClientGameplayMode, List<ClientActionBarSlot>> _actionBars = new()
    {
        [ClientGameplayMode.Civilian] = [],
        [ClientGameplayMode.Combat]   = [],
        [ClientGameplayMode.Space]    = [],
    };

    /// <summary>当用户请求使用技能时触发（由网络层订阅发送到服务端）。</summary>
    public event Action<string>? OnAbilityUseRequested;

    /// <summary>当用户请求切换模式时触发（由网络层订阅发送到服务端）。</summary>
    public event Action<ClientGameplayMode>? OnModeChangeRequested;

    public ClientGameplayMode CurrentMode => _currentMode;

    public override void _Ready()
    {
        // CivilianHud / CombatModeHud / SpaceModeHud / CrosshairWidget 由本管理器创建
        // GameplayModeSwitchPanel 和 SelectionHUD 由 GameBootstrap 创建并通过 Bind 方法注入

        _civilianHud = new CivilianHud();
        AddChild(_civilianHud);
        _civilianHud.OnSlotActivated += OnSlotActivated;

        _combatHud = new CombatModeHud();
        AddChild(_combatHud);
        _combatHud.OnPrimarySlotActivated += OnSlotActivated;
        _combatHud.OnSecondarySlotActivated += OnSlotActivated;
        _combatHud.OnVehicleSlotActivated += OnSlotActivated;

        _spaceHud = new SpaceModeHud();
        AddChild(_spaceHud);
        _spaceHud.OnModuleActivated += OnSlotActivated;
        _spaceHud.OnCommandActivated += OnSlotActivated;

        _crosshair = new CrosshairWidget();
        AddChild(_crosshair);

        // 默认显示战斗模式
        ApplyModeVisibility();
    }

    /// <summary>
    /// 注入 GameBootstrap 管理的 GameplayModeSwitchPanel（避免重复创建）。
    /// </summary>
    public void BindSwitchPanel(GameplayModeSwitchPanel panel)
    {
        _switchPanel = panel;
        _switchPanel.OnModeSelected += OnSwitchPanelModeSelected;
    }

    /// <summary>
    /// 注入 GameBootstrap 管理的 SelectionHUD（避免重复创建）。
    /// </summary>
    public void BindSelectionHud(SelectionHUD hud)
    {
        _selectionHud = hud;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                     服务端确认后的模式切换
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 服务端确认模式切换后调用 — 切换 HUD 显示。
    /// </summary>
    public void OnServerModeChanged(ClientGameplayMode newMode)
    {
        if (_currentMode == newMode) return;

        var prev = _currentMode;
        _currentMode = newMode;

        // 同步 GameplayModeSwitchPanel
        _switchPanel?.SetMode(newMode switch
        {
            ClientGameplayMode.Civilian => GameplayMode.Life,
            ClientGameplayMode.Combat   => GameplayMode.Combat,
            ClientGameplayMode.Space    => GameplayMode.Space,
            _ => GameplayMode.Combat,
        });

        ApplyModeVisibility();

        // 发布 ECS 事件
        _eventBus?.Publish(new GameplayModeChangedEvent(
            ToArkMode(prev), ToArkMode(newMode)));
    }

    /// <summary>
    /// 服务端同步动作栏时调用。
    /// </summary>
    public void OnServerAbilityBarSync(ClientGameplayMode mode,
        IReadOnlyList<ClientActionBarSlot> slots,
        IReadOnlyList<ClientActionBarSlot>? secondarySlots = null,
        IReadOnlyList<ClientActionBarSlot>? vehicleSlots = null)
    {
        _actionBars[mode] = new List<ClientActionBarSlot>(slots);
        RefreshBarForMode(mode, slots, secondarySlots, vehicleSlots);
    }

    /// <summary>
    /// 注册/更新客户端技能定义缓存（从服务端同步）。
    /// </summary>
    public void RegisterAbility(ClientAbilityDef def) => _abilityRegistry.Register(def);

    /// <summary>批量注册技能定义。</summary>
    public void RegisterAbilities(IEnumerable<ClientAbilityDef> defs)
    {
        foreach (var d in defs) _abilityRegistry.Register(d);
    }

    /// <summary>绑定事件总线（用于发布模式切换事件）。</summary>
    public void BindEventBus(EventBus eventBus) => _eventBus = eventBus;

    /// <summary>设置整组模式 HUD 的显示状态。</summary>
    public void SetHudVisible(bool visible)
    {
        _hudVisible = visible;
        ApplyModeVisibility();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                     HUD 可见性管理
    // ═══════════════════════════════════════════════════════════════════════

    private void ApplyModeVisibility()
    {
        bool civilian = _hudVisible && _currentMode == ClientGameplayMode.Civilian;
        bool combat   = _hudVisible && _currentMode == ClientGameplayMode.Combat;
        bool space    = _hudVisible && _currentMode == ClientGameplayMode.Space;

        if (_civilianHud is not null) _civilianHud.Visible = civilian;
        if (_combatHud is not null)   _combatHud.Visible = combat;
        if (_spaceHud is not null)    _spaceHud.Visible = space;

        // 准心仅在战斗模式显示
        if (_crosshair is not null)   _crosshair.Visible = combat;

        // SelectionHUD 在战斗和太空模式显示
        if (_selectionHud is not null) _selectionHud.Visible = _hudVisible && (combat || space);
    }

    private void RefreshBarForMode(ClientGameplayMode mode,
        IReadOnlyList<ClientActionBarSlot> primary,
        IReadOnlyList<ClientActionBarSlot>? secondary,
        IReadOnlyList<ClientActionBarSlot>? vehicle)
    {
        switch (mode)
        {
            case ClientGameplayMode.Civilian:
                _civilianHud?.SetActionBarSlots(primary, _abilityRegistry);
                break;

            case ClientGameplayMode.Combat:
                _combatHud?.SetPrimaryBar(primary, _abilityRegistry);
                if (secondary is not null) _combatHud?.SetSecondaryBar(secondary, _abilityRegistry);
                if (vehicle is not null) _combatHud?.SetVehicleBar(vehicle, _abilityRegistry);
                break;

            case ClientGameplayMode.Space:
                _spaceHud?.SetModuleBar(primary, _abilityRegistry);
                if (secondary is not null) _spaceHud?.SetCommandBar(secondary, _abilityRegistry);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                     事件处理
    // ═══════════════════════════════════════════════════════════════════════

    private void OnSwitchPanelModeSelected(GameplayMode mode)
    {
        var clientMode = mode switch
        {
            GameplayMode.Life   => ClientGameplayMode.Civilian,
            GameplayMode.Combat => ClientGameplayMode.Combat,
            GameplayMode.Space  => ClientGameplayMode.Space,
            _ => ClientGameplayMode.Combat,
        };
        // 向服务端请求模式切换（而非直接切换）
        OnModeChangeRequested?.Invoke(clientMode);
    }

    private void OnSlotActivated(int slotIndex, string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return;
        OnAbilityUseRequested?.Invoke(abilityId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                     HUD 数据更新代理
    // ═══════════════════════════════════════════════════════════════════════

    // ── 平民模式 ──

    public void UpdateCivilianCharInfo(string name, int level, float hp, float maxHp, float energy, float maxEnergy)
        => _civilianHud?.UpdateCharacterInfo(name, level, hp, maxHp, energy, maxEnergy);

    public void UpdateQuestTracker(string questName, string objective)
        => _civilianHud?.UpdateQuestTracker(questName, objective);

    public void UpdateLocation(string location)
        => _civilianHud?.UpdateLocation(location);

    // ── 战斗模式 ──

    public void UpdateCombatPlayerFrame(string name, float hp, float maxHp, float shield, float maxShield, float energy, float maxEnergy)
        => _combatHud?.UpdatePlayerFrame(name, hp, maxHp, shield, maxShield, energy, maxEnergy);

    public void UpdateCombatTarget(string name, float hp, float maxHp, float distance, string faction)
        => _combatHud?.UpdateTargetFrame(name, hp, maxHp, distance, faction);

    public void ClearCombatTarget() => _combatHud?.ClearTarget();

    public void UpdateCombatPartyMember(int index, string name, float hp, float maxHp, bool online)
        => _combatHud?.UpdatePartyMember(index, name, hp, maxHp, online);

    public void SetCombatVehicleMode(bool active) => _combatHud?.SetVehicleMode(active);

    public void UpdateCombatVehicle(float speed, float hp, float maxHp, float fuel, float maxFuel, int ammo, int maxAmmo)
        => _combatHud?.UpdateVehicleOverlay(speed, hp, maxHp, fuel, maxFuel, ammo, maxAmmo);

    // ── 太空模式 ──

    public void UpdateSpaceShip(string shipName, float hull, float maxHull,
        float shieldF, float shieldR, float shieldL, float shieldRi, float shieldMax,
        float cap, float maxCap, float speed, float maxSpeed)
        => _spaceHud?.UpdateShipStatus(shipName, hull, maxHull, shieldF, shieldR, shieldL, shieldRi, shieldMax, cap, maxCap, speed, maxSpeed);

    public void UpdateSpaceNavigation(string status, bool autopilot)
        => _spaceHud?.UpdateNavigation(status, autopilot);

    public void AddSpaceLockedTarget(string name, float distance, float hpPercent)
        => _spaceHud?.AddLockedTarget(name, distance, hpPercent);

    public void ClearSpaceLockedTargets() => _spaceHud?.ClearLockedTargets();

    public void SetFleetCommandMode(bool active) => _spaceHud?.SetFleetCommandMode(active);

    public void UpdateFleetInfo(string fleetName, int memberCount)
        => _spaceHud?.UpdateFleetInfo(fleetName, memberCount);

    // ── 通用 ──

    public void SetCrosshairEnemyHover(bool isEnemy) => _crosshair?.SetEnemyHover(isEnemy);

    public SelectionHUD? SelectionHud => _selectionHud;

    // ═══════════════════════════════════════════════════════════════════════

    private static GameplayMode ToArkMode(ClientGameplayMode m) => m switch
    {
        ClientGameplayMode.Civilian => GameplayMode.Life,
        ClientGameplayMode.Combat   => GameplayMode.Combat,
        ClientGameplayMode.Space    => GameplayMode.Space,
        _ => GameplayMode.Combat,
    };
}
