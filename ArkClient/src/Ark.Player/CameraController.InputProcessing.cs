using Godot;
using Ark.Abstractions;

namespace Ark.Player;

public partial class CameraController
{
    public override void _Input(InputEvent @event)
    {
        if (!_inputEnabled) return;

        switch (_currentMode)
        {
            case CameraMode.ThirdPerson:
            case CameraMode.FirstPerson:
                ProcessTpsInput(@event);
                break;
            case CameraMode.TopDown:
                ProcessTopDownInput(@event);
                break;
            case CameraMode.Orbit:
                ProcessOrbitInput(@event);
                break;
            case CameraMode.Free:
                ProcessFreeInput(@event);
                break;
        }

        if (@event is InputEventMouseButton wheel)
        {
            float min = _currentTarget?.MinZoom ?? 2f;
            float max = _currentTarget?.MaxZoom ?? 20f;

            if (wheel.ButtonIndex == MouseButton.WheelUp)
                _targetZoom = Mathf.Max(_targetZoom - ZoomSpeed, min);
            else if (wheel.ButtonIndex == MouseButton.WheelDown)
                _targetZoom = Mathf.Min(_targetZoom + ZoomSpeed, max);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        UpdateCameraTransform(dt);

        if (_currentTarget is IControllableCameraTarget controllable && controllable.CanReceiveInput)
            ProcessControllableInput(controllable);
    }

    private void ProcessTpsInput(InputEvent @event)
    {
        if (!(_currentTarget?.AllowCameraRotation ?? true)) return;

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _targetYaw -= motion.Relative.X * MouseSensitivity;
            _targetPitch -= motion.Relative.Y * MouseSensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch, Mathf.DegToRad(TpsMinPitch), Mathf.DegToRad(TpsMaxPitch));
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    private void ProcessTopDownInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
        {
            _isOrbiting = rmb.Pressed;
            Input.MouseMode = _isOrbiting ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseMotion motion && _isOrbiting)
        {
            _targetYaw -= motion.Relative.X * MouseSensitivity;
            _targetPitch -= motion.Relative.Y * MouseSensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch, Mathf.DegToRad(TopDownMinPitch), Mathf.DegToRad(TopDownMaxPitch));
        }
    }

    private void ProcessOrbitInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
        {
            _isOrbiting = rmb.Pressed;
            Input.MouseMode = _isOrbiting ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseMotion motion && _isOrbiting)
        {
            _targetYaw -= motion.Relative.X * MouseSensitivity;
            _targetPitch -= motion.Relative.Y * MouseSensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
        }
    }

    private void ProcessFreeInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _targetYaw -= motion.Relative.X * MouseSensitivity;
            _targetPitch -= motion.Relative.Y * MouseSensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    private void ProcessControllableInput(IControllableCameraTarget target)
    {
        var moveInput = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        bool sprint = Input.IsActionPressed("sprint");
        if (moveInput != Vector2.Zero || sprint)
            target.ProcessMovementInput(moveInput, sprint);

        if (Input.IsActionJustPressed("jump"))
            target.ProcessJumpInput();
    }
}
