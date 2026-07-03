using Godot;
using Ark.Ecs.Components;
using Ark.Services.Remote;

namespace Ark.Player;

public partial class TpsPlayerController
{
    // ═══════════════════════════════════════════════════════════════════════
    //                     建造模式 / 调试 UI
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>建造相机模式的输入处理（从 _Input 中提取）。</summary>
    private void HandleBuildModeInput(InputEvent @event)
    {
        // ─── 建造相机模式：RMB 按住旋转相机 ───
        if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
        {
            _buildModeOrbiting = rmb.Pressed;
            Input.MouseMode    = _buildModeOrbiting
                ? Input.MouseModeEnum.Captured
                : Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseMotion buildMotion && _buildModeOrbiting)
        {
            _targetYaw   -= buildMotion.Relative.X * MouseSensitivity;
            _targetPitch -= buildMotion.Relative.Y * MouseSensitivity;
            _targetPitch  = Mathf.Clamp(_targetPitch, Mathf.DegToRad(-80f), Mathf.DegToRad(-10f));
        }

        // 建造模式滚轮缩放
        if (@event is InputEventMouseButton wheelBuild)
        {
            if (wheelBuild.ButtonIndex == MouseButton.WheelUp)
                _currentZoom = Mathf.Max(_currentZoom - ZoomSpeed * 2f, CameraMinZoom);
            else if (wheelBuild.ButtonIndex == MouseButton.WheelDown)
                _currentZoom = Mathf.Min(_currentZoom + ZoomSpeed * 2f, CameraMaxZoom * 2f);
        }
    }

    /// <summary>
    /// 由 BuildPlacementController 调用：进入/退出建造模式时暂停视角控制。
    /// </summary>
    /// <summary>
    /// 已废弃 — 由 SetBuildCameraMode 取代。
    /// </summary>
    public void SetBuildModeActive(bool active)
    {
        SetBuildCameraMode(active);
    }

    /// <summary>
    /// 设置建造相机模式（俯视）— 由 SquadModule 调用。
    /// </summary>
    public void SetBuildCameraMode(bool active)
    {
        _buildCameraMode = active;

        if (active)
        {
            _savedPitch = _targetPitch;
            _savedYaw   = _targetYaw;
            _targetPitch = Mathf.DegToRad(-50f);
            _mouseCaptured     = false;
            _buildModeOrbiting = false;
        }
        else
        {
            _targetPitch = _savedPitch;
            _targetYaw   = _savedYaw;
            _buildModeOrbiting = false;
            _mouseCaptured     = true;
            Input.MouseMode    = Input.MouseModeEnum.Captured;
        }

        SyncLocalControlStateToEcs();
    }

    /// <summary>
    /// 启用/禁用相机（切换到其他角色时调用）。
    /// </summary>
    public void SetCameraActive(bool active)
    {
        _isActive = active;

        if (_camera != null)
        {
            _camera.Current = active;
        }

        if (active)
        {
            _mouseCaptured     = true;
            _buildCameraMode   = false;
            _buildModeOrbiting = false;
            _isFiring          = false;
            Input.MouseMode    = Input.MouseModeEnum.Captured;
        }
        else
        {
            _mouseCaptured     = false;
            _buildCameraMode   = false;
            _buildModeOrbiting = false;
            _isFiring          = false;
            // 注意：不清除当前受控载具运行态 — 角色记住自己的载具控制上下文
        }

        // 切换时显示/隐藏调试 UI
        if (_debugCanvas != null) _debugCanvas.Visible = active;
        SyncLocalControlStateToEcs();
    }

    private void UpdateDebugUI()
    {
        if (_debugLabel == null) return;

        var pos = GlobalPosition;
        var speed = new Vector2(_velocity.X, _velocity.Z).Length();

        // 武器/弹药信息
        string weaponInfo = "None";
        string ammoInfo = "";

        // 网络模式下载具武器从 ECS 缓存映射的 VehicleDef 查询
        int controlledVehicleId = ResolveEffectiveVehicleEntityId();
        bool vehicleControlActive = controlledVehicleId > 0;
        byte projectedSeatIndex = 0;
        byte projectedSeatType = 2;
        if (TryGetProjectedLocalControlState(out var localControlState)
            && (LocalControlSource)localControlState.ControlSource == LocalControlSource.VehicleSeat)
        {
            projectedSeatIndex = localControlState.SeatIndex;
            projectedSeatType = localControlState.SeatType;
        }

        if (vehicleControlActive && Ark.Services.GameServices.IsNetworkMode && _combatData != null)
        {
            if (TryGetRemoteVehicleDefId(controlledVehicleId, out var vehicleDefId))
            {
                var vDef = _combatData.VehicleDefs.Get(vehicleDefId);
                if (vDef is { } vd && vd.Seats.Length > 0)
                {
                    int seatIndex = projectedSeatIndex;
                    int si = Mathf.Clamp(seatIndex, 0, vd.Seats.Length - 1);
                    var sd = vd.Seats[si];
                    int wid = sd.HasWeapon ? sd.WeaponDefId : 0;
                    weaponInfo = wid > 0
                        ? _combatData.WeaponDefs.Get(wid)?.Name ?? $"Weapon #{wid}"
                        : "无载具武器";
                    int currentMag = _entity.TryGetComponent<AmmoState>(out var ammoState) ? ammoState.CurrentMag : 0;
                    int reserve = _entity.TryGetComponent<AmmoState>(out ammoState) ? ammoState.ReserveAmmo : 0;
                    ammoInfo = $"  Ammo: {currentMag}/? ({reserve})";
                }
            }
        }
        else if (Ark.Services.GameServices.IsNetworkMode)
        {
            if (_entity.TryGetComponent<WeaponState>(out var weapon) && weapon.WeaponDefId > 0)
            {
                weaponInfo = ResolveWeaponDef(weapon.WeaponDefId)?.Name ?? $"Weapon #{weapon.WeaponDefId}";
                if (_entity.TryGetComponent<AmmoState>(out var ammoState))
                    ammoInfo = $"  Ammo: {ammoState.CurrentMag}/{ammoState.MagCapacity} ({ammoState.ReserveAmmo})";
                if (_entity.TryGetComponent<RemoteCombatState>(out var remoteCombatState) && remoteCombatState.IsReloading != 0)
                    ammoInfo += "  [装填中]";
            }
        }
        else if (_store != null && _entity.Id != 0)
        {
            // 载具中显示载具武器，否则显示角色武器
            int weaponEntityId = vehicleControlActive ? controlledVehicleId : _entity.Id;
            var weaponEntity = _store.GetEntityById(weaponEntityId);
            if (!weaponEntity.IsNull &&
                weaponEntity.TryGetComponent<WeaponState>(out var ws) &&
                weaponEntity.TryGetComponent<AmmoState>(out var ammo))
            {
                weaponInfo = ws.Category switch
                {
                    0 => "Fist",
                    1 => "Pistol",
                    2 => "Rifle",
                    3 => "Shotgun",
                    4 => "Sniper",
                    5 => "Launcher",
                    6 => "Melee",
                    _ => $"W#{ws.WeaponDefId}"
                };
                ammoInfo = $"  Ammo: {ammo.CurrentMag}/{ammo.MagCapacity} ({ammo.ReserveAmmo})";
                if (ws.IsReloading != 0)
                    ammoInfo += "  [装填中]";
            }
        }

        // 生命值信息
        string healthInfo = "";
        if (_store != null && _entity.Id != 0 &&
            _entity.TryGetComponent<Health>(out var hp))
        {
            healthInfo = $"HP: {hp.Current:F0}/{hp.Max:F0}";
        }

        // 载具提示
        string vehicleHint = "";
        if (vehicleControlActive)
        {
            string seatName = projectedSeatType switch { 0 => "驾驶", 1 => "炮手", _ => "乘客" };
            vehicleHint = $"[F] 退出载具  [Tab] 换座  座位: {seatName}";
        }
        else if (_nearbyVehicleId > 0)
            vehicleHint = "[F] 进入载具";

        _debugLabel.Text = $"""
            === Squad Leader ===
            Position: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})
            Speed: {speed:F1} m/s  |  {healthInfo}
            Weapon: {weaponInfo}{ammoInfo}
            {vehicleHint}

            [Controls]
            WASD - Move   |  Space - Jump
            Shift - Sprint  |  Mouse - Look
            LMB - Fire    |  R - Reload
            F - Vehicle Enter/Exit
            B - Build Mode  |  ESC - Release mouse
            F1~F5 - Switch Squad  |  Tab - Formation
            """;
    }
}
