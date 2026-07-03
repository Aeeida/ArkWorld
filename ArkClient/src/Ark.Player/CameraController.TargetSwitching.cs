using Godot;
using Ark.Abstractions;

namespace Ark.Player;

public partial class CameraController
{
    public void SetTarget(ICameraTarget? target, bool preserveRotation = false)
    {
        if (target == _currentTarget) return;

        var oldTarget = _currentTarget;
        oldTarget?.OnCameraDetached();
        _currentTarget = target;

        if (target != null)
        {
            SetMode(target.PreferredCameraMode, preserveRotation);

            if (_cameraRig != null)
                _cameraRig.GlobalPosition = target.CameraAnchorPosition;

            if (!preserveRotation)
            {
                var euler = target.CameraAnchorRotation.GetEuler();
                _targetYaw = _currentYaw = euler.Y;
                _targetPitch = _currentPitch = 0;
            }

            target.OnCameraAttached();
        }

        OnTargetChanged?.Invoke(oldTarget, target);
        GD.Print($"[Camera] Target changed: {oldTarget?.GetType().Name ?? "null"} -> {target?.GetType().Name ?? "null"}");
    }

    public void SetMode(CameraMode mode, bool preserveRotation = false)
    {
        if (mode == _currentMode) return;

        var oldMode = _currentMode;

        if (!preserveRotation)
        {
            _savedYaw = _targetYaw;
            _savedPitch = _targetPitch;
        }

        _currentMode = mode;

        switch (mode)
        {
            case CameraMode.ThirdPerson:
            case CameraMode.FirstPerson:
                if (!preserveRotation)
                    _targetPitch = Mathf.Clamp(_targetPitch, Mathf.DegToRad(TpsMinPitch), Mathf.DegToRad(TpsMaxPitch));
                _targetZoom = _currentTarget?.DefaultCameraOffset.Z ?? TpsDefaultZoom;
                Input.MouseMode = Input.MouseModeEnum.Captured;
                break;

            case CameraMode.TopDown:
                if (!preserveRotation)
                    _targetPitch = Mathf.DegToRad(-50f);
                _targetZoom = TopDownDefaultZoom;
                Input.MouseMode = Input.MouseModeEnum.Visible;
                break;

            case CameraMode.Orbit:
            case CameraMode.Free:
                Input.MouseMode = Input.MouseModeEnum.Visible;
                break;
        }

        OnModeChanged?.Invoke(mode);
        GD.Print($"[Camera] Mode changed: {oldMode} -> {mode}");
    }

    public void EnterBuildMode()
    {
        SetMode(CameraMode.TopDown);
    }

    public void ExitBuildMode()
    {
        _targetYaw = _savedYaw;
        _targetPitch = _savedPitch;
        SetMode(_currentTarget?.PreferredCameraMode ?? CameraMode.ThirdPerson, true);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
