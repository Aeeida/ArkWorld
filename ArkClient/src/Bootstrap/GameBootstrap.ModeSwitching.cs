using Godot;
using Ark.Events;
using Ark.Shared.Data;
using Ark.World;

namespace Ark;

public partial class GameBootstrap
{
    // ═══════════════════════════════════════════════════════════════════════
    //                    玩法模式切换
    // ═══════════════════════════════════════════════════════════════════════

    private void OnGameplayModeSelected(GameplayMode newMode)
    {
        if (newMode == _currentMode) return;

        var prev = _currentMode;
        _currentMode = newMode;
        _modeSwitchPanel?.SetMode(newMode);

        _eventBus.Publish(new GameplayModeChangedEvent(prev, newMode));
        ApplyGameplayMode(newMode);

        // 通知 ModeHudManager 切换 HUD 层
        _modeHudManager?.OnServerModeChanged(newMode switch
        {
            GameplayMode.Life   => ClientGameplayMode.Civilian,
            GameplayMode.Combat => ClientGameplayMode.Combat,
            GameplayMode.Space  => ClientGameplayMode.Space,
            _ => ClientGameplayMode.Combat,
        });

        GD.Print($"[GameBootstrap] Mode: {prev} → {newMode}");
    }

    private void ApplyGameplayMode(GameplayMode mode)
    {
        // ── 1. 环境切换 ──
        ApplyEnvironment(mode);

        // ── 2. 子系统可见性 ──
        ApplySubsystemVisibility(mode);

        // ── 3. HUD 切换 ──
        ApplyHudVisibility(mode);
    }

    private void ApplyEnvironment(GameplayMode mode)
    {
        _worldEnvManager?.ApplyModeOverride(mode);
    }

    private void ApplySubsystemVisibility(GameplayMode mode)
    {
        // ── 战斗子系统（Node 基类 → 使用 ProcessMode 控制）──
        bool combatActive = mode == GameplayMode.Combat;
        SetNodeActive(_weaponVisuals, combatActive);
        SetNodeActive(_enemyVisuals, combatActive);

        // ── 建造子系统 ──
        bool buildActive = mode == GameplayMode.Life || mode == GameplayMode.Combat;
        SetNodeActive(_buildPlacement, buildActive);
        SetNodeActive(_buildingVisuals, buildActive);

        // ── 玩家控制器（CharacterBody3D → 有 Visible）──
        // Life + Combat: 地面角色可见可控, Space: 隐藏+暂停
        bool playerActive = mode != GameplayMode.Space;
        if (_player != null)
        {
            _player.Visible = playerActive;
            _player.ProcessMode = playerActive
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }

        // ── 小队系统 ──
        // (SquadCameraManager is a plain class, not a Node — skip ProcessMode)

        // ── 火箭相机 ──
        if (mode != GameplayMode.Space)
            _rocketCamera?.Deactivate();
    }

    private void ApplyHudVisibility(GameplayMode mode)
    {
        // 选择 HUD 只在战斗/生活模式
        if (_selectionHUD != null) _selectionHUD.Visible = mode != GameplayMode.Space;
        // 发射控制面板仅在太空模式下由回调控制，切出时隐藏
        if (mode != GameplayMode.Space)
            _launchControlPanel?.Hide();

        // ModeHudManager 管理 CivilianHud / CombatModeHud / SpaceModeHud 可见性
        if (_modeHudManager != null) _modeHudManager.SetHudVisible(true);//.Visible = true;
    }
}
