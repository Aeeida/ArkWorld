using Godot;
using System;
using Ark.Events;
using Ark.UI;

namespace Ark.Gameplay.Space;

public partial class LaunchSequenceController
{
    private void ProcessFlightInput(float dt)
    {
        if (Input.IsActionPressed("move_forward"))
            _pitch = Mathf.Clamp(_pitch - PitchRate * dt, -90f, 90f);
        if (Input.IsActionPressed("move_backward"))
            _pitch = Mathf.Clamp(_pitch + PitchRate * dt, -90f, 90f);

        if (Input.IsActionPressed("move_left"))
            _yaw -= YawRate * dt;
        if (Input.IsActionPressed("move_right"))
            _yaw += YawRate * dt;
        _yaw = (_yaw % 360f + 360f) % 360f;

        if (Input.IsActionPressed("spacecraft_roll_left"))
            _roll -= RollRate * dt;
        if (Input.IsActionPressed("spacecraft_roll_right"))
            _roll += RollRate * dt;
        _roll = Mathf.Clamp(_roll, -180f, 180f);

        if (Input.IsActionPressed("spacecraft_throttle_up"))
        {
            _throttle = Mathf.Clamp(_throttle + ThrottleRate * dt, 0f, 1f);
            _engineCutoff = false;
            _hoverMode = false;
        }
        if (Input.IsActionPressed("spacecraft_throttle_down"))
        {
            _throttle = Mathf.Clamp(_throttle - ThrottleRate * dt, 0f, 1f);
            _hoverMode = false;
        }

        if (Input.IsActionJustPressed("spacecraft_hover") && IsActive)
        {
            _hoverMode = !_hoverMode;
            if (_hoverMode)
            {
                _hoverTargetAlt = _position.Y;
                _engineCutoff = false;
                GD.Print($"[FlightControl] Hover ON — target alt={_hoverTargetAlt:F0}m");
            }
            else
            {
                GD.Print("[FlightControl] Hover OFF");
            }
        }

        if (Input.IsActionJustPressed("spacecraft_engine_cutoff") && IsActive)
        {
            _engineCutoff = true;
            _throttle = 0;
            _hoverMode = false;
            GD.Print("[FlightControl] Engine CUTOFF");
        }
    }

    private Vector3 GetThrustDirection()
    {
        var localReactionDirection = _rocketConfig?.GetPrimaryReactionDirection() ?? Vector3.Up;

        var yaw = new Quaternion(Vector3.Up, Mathf.DegToRad(_yaw));
        var pitch = new Quaternion(Vector3.Right, Mathf.DegToRad(_pitch - 90f));
        var roll = new Quaternion(Vector3.Forward, Mathf.DegToRad(_roll));
        return (yaw * pitch * roll * localReactionDirection).Normalized();
    }

    private void UpdateHoverAutopilot(float dt)
    {
        if (!_hoverMode) return;

        float altError = _hoverTargetAlt - _position.Y;
        float vertVel = _vel3D.Y;

        float gEff = G0 * MathF.Pow(EarthRadius / (EarthRadius + MathF.Max(_position.Y, 0)), 2);
        float hoverThrottle = (_currentMass * 1000f * gEff) / MathF.Max(_currentThrust * 1000f, 1f);
        float correction = HoverKp * altError - HoverKd * vertVel;
        _throttle = Mathf.Clamp(hoverThrottle + correction, 0.05f, 1f);

        float pitchError = 90f - _pitch;
        _pitch += pitchError * 2f * dt;
        _roll *= MathF.Max(0, 1f - 3f * dt);
        _engineCutoff = false;
    }
}
