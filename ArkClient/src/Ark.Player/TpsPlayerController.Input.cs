using Godot;
using Ark.Ecs.Components;

namespace Ark.Player;

public partial class TpsPlayerController
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          输入处理
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Input(InputEvent @event)
    {
        // 未激活时忽略输入
        if (!_isActive) return;
        if (IsExternalControlLocked) return;

        bool vehicleControlActive = IsVehicleControlActive();

        // B 键由 SquadModule 统一处理，这里不响应

        // ═══ F 键：载具进入/退出 ═══
        if (@event.IsActionPressed("interact"))
        {
            if (vehicleControlActive)
            {
                ExitVehicle();
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (_nearbyVehicleId > 0)
            {
                EnterVehicle(_nearbyVehicleId);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // ═══ Tab 键：载具换座（SquadModule 不存在时由此处理）═══
        if (@event.IsActionPressed("squad_formation") && vehicleControlActive)
        {
            CycleSeat();
            GetViewport().SetInputAsHandled();
            return;
        }

        // ═══ R 键：换弹 ═══
        if (@event.IsActionPressed("reload") && !_buildCameraMode)
        {
            if (Ark.Services.GameServices.IsNetworkMode)
            {
                if (vehicleControlActive
                    && _entity.TryGetComponent<MountedWeaponRuntimeState>(out var mountedRuntime)
                    && mountedRuntime.FaultCode != 0)
                {
                    QueueNetworkSeatWeaponRequest(NetworkSeatWeaponActionKind.ClearFault);
                }
                else if (vehicleControlActive
                    && _entity.TryGetComponent<MountedWeaponRuntimeState>(out mountedRuntime)
                    && Input.IsActionPressed("sprint"))
                {
                    QueueNetworkSeatWeaponRequest(NetworkSeatWeaponActionKind.Maintain);
                }
                else
                {
                    QueueNetworkReloadRequest();
                }
            }
            else
            {
                int reloadTarget = vehicleControlActive ? ResolveEffectiveVehicleEntityId() : _entity.Id;
                _combatModule?.TryReload(reloadTarget);
            }
        }

        // ═══ 射击控制 ═══
        if (vehicleControlActive)
        {
            // 载具模式：SPACE 开炮
            if (@event.IsActionPressed("jump"))
            {
                _isFiring = true;
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionReleased("jump"))
            {
                _isFiring = false;
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseButton lmb &&
            lmb.ButtonIndex == MouseButton.Left && _mouseCaptured && !_buildCameraMode)
        {
            // 步兵/小队模式：LMB 射击
            _isFiring = lmb.Pressed;
        }

        if (_buildCameraMode)
        {
            HandleBuildModeInput(@event);
            return;
        }

        // ─── 普通 TPS 模式 ───
        if (@event is InputEventMouseMotion mouseMotion && _mouseCaptured)
        {
            _targetYaw   -= mouseMotion.Relative.X * MouseSensitivity;
            _targetPitch -= mouseMotion.Relative.Y * MouseSensitivity;
            _targetPitch  = Mathf.Clamp(_targetPitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            _mouseCaptured  = !_mouseCaptured;
            Input.MouseMode = _mouseCaptured
                ? Input.MouseModeEnum.Captured
                : Input.MouseModeEnum.Visible;

            // 释放鼠标时停止射击
            if (!_mouseCaptured) _isFiring = false;
            SyncLocalControlStateToEcs();
        }

        // ─── 滚轮缩放 ───
        if (@event is InputEventMouseButton wheelEvent)
        {
            if (wheelEvent.ButtonIndex == MouseButton.WheelUp)
                _currentZoom = Mathf.Max(_currentZoom - ZoomSpeed, CameraMinZoom);
            else if (wheelEvent.ButtonIndex == MouseButton.WheelDown)
                _currentZoom = Mathf.Min(_currentZoom + ZoomSpeed, CameraMaxZoom);
        }
    }
}
