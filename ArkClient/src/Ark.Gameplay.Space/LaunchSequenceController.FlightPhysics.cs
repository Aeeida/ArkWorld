using Godot;
using System;
using Ark.Events;
using Ark.UI;

namespace Ark.Gameplay.Space;

public partial class LaunchSequenceController
{
    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_phase == FlightPhase.Powered || _phase == FlightPhase.Coasting || _phase == FlightPhase.Descent)
            ProcessFlightInput(dt);

        switch (_phase)
        {
            case FlightPhase.PreLaunch:
                UpdatePreLaunch(dt);
                break;
            case FlightPhase.Powered:
                UpdatePoweredFlight(dt);
                break;
            case FlightPhase.Coasting:
                UpdateCoasting(dt);
                break;
            case FlightPhase.Descent:
                UpdateDescent(dt);
                break;
            case FlightPhase.Exploding:
                UpdateExplosion(dt);
                break;
        }

        if (_position.Y > _maxAltitude) _maxAltitude = _position.Y;
        float speed3D = _vel3D.Length();
        if (speed3D > _maxSpeed) _maxSpeed = speed3D;

        if (IsActive)
            EmitTelemetry();
    }

    private void UpdatePreLaunch(float dt)
    {
        _timer += dt;

        if (_rocketBody != null)
        {
            float intensity = _timer / PreLaunchDuration;
            float shake = Mathf.Sin(_timer * 40f) * 0.02f * intensity;
            _rocketBody.Position = new Vector3(_padPos.X + shake, _padPos.Y + 5f, _padPos.Z + shake * 0.7f);
        }

        if (_timer > PreLaunchDuration * 0.5f && _sparkParticles != null)
            _sparkParticles.Emitting = true;

        if (_timer >= PreLaunchDuration)
        {
            _timer = 0;
            _phase = FlightPhase.Powered;
            if (_thrustFlame != null) _thrustFlame.Emitting = true;
            if (_smokeTrail != null) _smokeTrail.Emitting = true;
            if (_thrustLight != null) _thrustLight.Visible = true;
            if (_sparkParticles != null) _sparkParticles.Emitting = false;
            GD.Print("[LaunchSequence] LIFTOFF!");
        }
    }

    private void UpdatePoweredFlight(float dt)
    {
        if (_rocketConfig == null) return;

        UpdateHoverAutopilot(dt);
        float effectiveThrottle = _engineCutoff ? 0f : _throttle;
        float thrustMagnitude = _currentThrust * effectiveThrottle * 1000f;
        if (_remainingFuel > 0 && _currentISP > 0 && thrustMagnitude > 0)
        {
            _fuelBurnRate = thrustMagnitude / (_currentISP * G0);
            float fuelUsed = _fuelBurnRate * dt * 0.005f;
            _remainingFuel = MathF.Max(0, _remainingFuel - fuelUsed);
            _currentMass = _rocketConfig.TotalDryMass + _remainingFuel * 0.005f;
        }
        else if (_remainingFuel <= 0)
        {
            thrustMagnitude = 0;
            _fuelBurnRate = 0;
            if (_vel3D.Y > 0)
            {
                _phase = FlightPhase.Coasting;
                if (_thrustFlame != null) _thrustFlame.Emitting = false;
                if (_thrustLight != null) _thrustLight.Visible = false;
                GD.Print("[LaunchSequence] Fuel exhausted — coasting");
                return;
            }
        }
        else
        {
            _fuelBurnRate = 0;
        }

        Vector3 thrustDir = GetThrustDirection();
        Vector3 thrustForce = thrustDir * thrustMagnitude;

        float alt = MathF.Max(_position.Y, 0);
        float gEffective = G0 * MathF.Pow(EarthRadius / (EarthRadius + alt), 2);
        Vector3 gravity = new Vector3(0, -_currentMass * 1000f * gEffective, 0);

        float airDensity = SeaLevelDensity * MathF.Exp(-alt / AtmoScaleHeight);
        float speed = _vel3D.Length();
        _dragForce = 0.5f * airDensity * speed * speed * _currentDragCd * CrossSectionArea;
        Vector3 dragVec = speed > 0.01f ? -_vel3D.Normalized() * _dragForce : Vector3.Zero;

        Vector3 netForce = thrustForce + gravity + dragVec;
        float massKg = _currentMass * 1000f;
        Vector3 accel = netForce / massKg;
        _acceleration = accel.Length();

        _vel3D += accel * dt;
        _position += _vel3D * dt;

        if (_position.Y <= 0 && _vel3D.Y < 0)
        {
            HandleGroundContact();
            return;
        }

        if (_position.Y > 100000f)
        {
            _phase = FlightPhase.Coasting;
            GD.Print($"[LaunchSequence] Reached space! Alt={_position.Y:F0}m, V={speed:F0}m/s");
            OnLaunchComplete?.Invoke();
        }

        UpdateReentryEffects();
        UpdateRocketPosition();
        UpdateParticleIntensity(effectiveThrottle);
        UpdateThrustLight(effectiveThrottle);
    }

    private void UpdateCoasting(float dt)
    {
        float alt = MathF.Max(_position.Y, 0);
        float gEffective = G0 * MathF.Pow(EarthRadius / (EarthRadius + alt), 2);
        Vector3 gravity = new Vector3(0, -_currentMass * 1000f * gEffective, 0);

        float airDensity = SeaLevelDensity * MathF.Exp(-alt / AtmoScaleHeight);
        float speed = _vel3D.Length();
        _dragForce = 0.5f * airDensity * speed * speed * _currentDragCd * CrossSectionArea;
        Vector3 dragVec = speed > 0.01f ? -_vel3D.Normalized() * _dragForce : Vector3.Zero;

        if (!_engineCutoff && _throttle > 0 && _remainingFuel > 0)
        {
            _phase = FlightPhase.Powered;
            if (_thrustFlame != null) _thrustFlame.Emitting = true;
            if (_thrustLight != null) _thrustLight.Visible = true;
            GD.Print("[LaunchSequence] Engine restarted — back to powered flight");
            return;
        }

        Vector3 netForce = gravity + dragVec;
        Vector3 accel = netForce / (_currentMass * 1000f);
        _acceleration = accel.Length();
        _vel3D += accel * dt;
        _position += _vel3D * dt;

        UpdateRocketPosition();
        UpdateReentryEffects();

        if (_vel3D.Y < 0 && _position.Y < 70000f)
        {
            _phase = FlightPhase.Descent;
            GD.Print("[LaunchSequence] Descent phase");
            DeployParachute();
        }

        if (_position.Y <= 0)
            HandleGroundContact();
    }

    private void UpdateDescent(float dt)
    {
        float alt = MathF.Max(_position.Y, 0);
        float gEffective = G0 * MathF.Pow(EarthRadius / (EarthRadius + alt), 2);
        Vector3 gravity = new Vector3(0, -_currentMass * 1000f * gEffective, 0);

        float airDensity = SeaLevelDensity * MathF.Exp(-alt / AtmoScaleHeight);
        float cd = _currentDragCd;
        if (_parachuteDeployed && alt < 10000f)
            cd += ParachuteDragCd * MathF.Min(1f, (10000f - alt) / 5000f);

        float speed = _vel3D.Length();
        _dragForce = 0.5f * airDensity * speed * speed * cd * CrossSectionArea;
        Vector3 dragVec = speed > 0.01f ? -_vel3D.Normalized() * _dragForce : Vector3.Zero;

        if (!_engineCutoff && _throttle > 0 && _remainingFuel > 0)
        {
            _phase = FlightPhase.Powered;
            if (_thrustFlame != null) _thrustFlame.Emitting = true;
            if (_thrustLight != null) _thrustLight.Visible = true;
            GD.Print("[LaunchSequence] Retro-burn — back to powered flight");
            return;
        }

        Vector3 netForce = gravity + dragVec;
        Vector3 accel = netForce / (_currentMass * 1000f);
        _acceleration = accel.Length();
        _vel3D += accel * dt;
        _position += _vel3D * dt;

        UpdateRocketPosition();
        UpdateReentryEffects();

        if (_position.Y <= 0)
            HandleGroundContact();
    }

    private void HandleGroundContact()
    {
        _position.Y = 0;
        _impactSpeed = _vel3D.Length();
        _vel3D = Vector3.Zero;
        _acceleration = 0;
        _flightElapsed = (float)Time.GetTicksMsec() / 1000f - _flightStartTime;
        _fuelConsumed = _totalFuelCapacity - _remainingFuel;

        if (_thrustFlame != null) _thrustFlame.Emitting = false;
        if (_smokeTrail != null) _smokeTrail.Emitting = false;
        if (_thrustLight != null) _thrustLight.Visible = false;
        if (_reentryHeat != null) _reentryHeat.Emitting = false;

        bool safeLanding = _parachuteDeployed && _impactSpeed < CrashSpeed;

        if (safeLanding)
        {
            _phase = FlightPhase.Landed;
            _endReason = _impactSpeed < SafeLandingSpeed ? "安全着陆" : "硬着陆（轻微损伤）";
            GD.Print($"[LaunchSequence] {_endReason}! Impact={_impactSpeed:F1}m/s");
            OnLandedSafe?.Invoke();
            EmitFlightReport(true);
        }
        else
        {
            _endReason = _parachuteDeployed
                ? $"着陆速度过高 ({_impactSpeed:F1}m/s)"
                : $"无降落伞坠毁 ({_impactSpeed:F1}m/s)";
            GD.Print($"[LaunchSequence] CRASH! {_endReason}");
            BeginExplosion();
        }
    }

    private void DeployParachute()
    {
        if (_parachuteDeployed) return;
        if (_rocketConfig != null && _rocketConfig.HasParachute)
        {
            _parachuteDeployed = true;
            GD.Print("[LaunchSequence] Parachute deployed!");
        }
    }

    private void RecalculateEngineStats()
    {
        if (_rocketConfig == null || _currentStage >= _rocketConfig.Stages.Count) return;
        float totalThrust = 0, weightedISP = 0;
        for (int s = _currentStage; s < _rocketConfig.Stages.Count; s++)
        {
            foreach (var id in _rocketConfig.Stages[s].PartIds)
            {
                var p = RocketPartDef.Get(id);
                if (p != null && p.Thrust > 0 && p.ISP > 0)
                {
                    totalThrust += p.Thrust;
                    weightedISP += p.Thrust * p.ISP;
                }
            }
        }
        _currentThrust = totalThrust;
        _currentISP = totalThrust > 0 ? weightedISP / totalThrust : 280f;

        float cd = 0;
        for (int s = _currentStage; s < _rocketConfig.Stages.Count; s++)
            foreach (var id in _rocketConfig.Stages[s].PartIds)
            {
                var p = RocketPartDef.Get(id);
                if (p != null) cd += p.DragCoefficient;
            }
        _currentDragCd = MathF.Max(cd, 0.01f);
    }

    private void EmitTelemetry()
    {
        float fuelPct = _totalFuelCapacity > 0 ? (_remainingFuel / _totalFuelCapacity) * 100f : 0;
        float twr = _currentMass > 0 ? (_currentThrust * _throttle) / (_currentMass * G0) : 0;

        OnTelemetryUpdate?.Invoke(new TelemetryData
        {
            Altitude = _position.Y,
            Velocity = _vel3D.Y,
            Speed3D = _vel3D.Length(),
            HorizontalSpeed = new Vector2(_vel3D.X, _vel3D.Z).Length(),
            Acceleration = _acceleration,
            TWR = twr,
            FuelPercent = fuelPct,
            FuelBurnRate = _fuelBurnRate,
            Heading = _yaw,
            Pitch = _pitch,
            Roll = _roll,
            DragForce = _dragForce,
            PhaseName = _phase switch
            {
                FlightPhase.PreLaunch => "倒计时...",
                FlightPhase.Powered => _hoverMode ? "悬停中" : "动力飞行",
                FlightPhase.Coasting => "惯性滑行",
                FlightPhase.Descent => _parachuteDeployed ? "降落伞减速" : "自由下落",
                FlightPhase.Landed => "已着陆",
                FlightPhase.Exploding => "爆炸中...",
                FlightPhase.Crashed => "已坠毁",
                _ => "待命"
            },
            Mass = _currentMass,
            Throttle = _engineCutoff ? 0 : _throttle,
            Stage = _currentStage,
            HoverMode = _hoverMode,
            EngineCutoff = _engineCutoff,
        });
    }

    private void UpdateReentryEffects()
    {
        float airDensity = SeaLevelDensity * MathF.Exp(-MathF.Max(_position.Y, 0) / AtmoScaleHeight);
        bool showHeat = _vel3D.Length() > 500f && airDensity > 0.01f;
        if (_reentryHeat != null) _reentryHeat.Emitting = showHeat;
    }
}
