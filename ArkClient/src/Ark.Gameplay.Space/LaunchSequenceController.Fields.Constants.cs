using Godot;
using System;
using Ark.Events;
using Ark.UI;

namespace Ark.Gameplay.Space;

public partial class LaunchSequenceController
{
    // ═══ 飞行阶段 ═══
    public enum FlightPhase { Idle, PreLaunch, Powered, Coasting, Descent, Landed, Exploding, Crashed }

    // ═══ 物理常量 ═══
    private const float G0 = 9.81f;
    private const float EarthRadius = 6371000f;
    private const float SeaLevelDensity = 1.225f;
    private const float AtmoScaleHeight = 8500f;
    private const float CrossSectionArea = 3.14f;
    private const float PreLaunchDuration = 3.0f;
    private const float ParachuteDragCd = 50f;
    private const float SafeLandingSpeed = 8f;
    private const float CrashSpeed = 20f;

    // ═══ 姿态控制常量 ═══
    private const float PitchRate = 45f;
    private const float YawRate = 45f;
    private const float RollRate = 90f;
    private const float ThrottleRate = 0.5f;
    private const float HoverKp = 0.06f;
    private const float HoverKd = 0.15f;

    // ═══ 飞行状态（3D）═══
    private FlightPhase _phase = FlightPhase.Idle;
    private float _timer;
    private Vector3 _position;
    private Vector3 _vel3D;
    private float _acceleration;
    private float _yaw;
    private float _pitch = 90f;
    private float _roll;
    private float _throttle = 1f;
    private bool _engineCutoff;
    private bool _hoverMode;
    private float _hoverTargetAlt;
    private float _currentMass;
    private float _remainingFuel;
    private float _totalFuelCapacity;
    private float _currentThrust;
    private float _currentISP;
    private float _currentDragCd;
    private float _dragForce;
    private float _fuelBurnRate;
    private int _currentStage;
    private bool _parachuteDeployed;
    private Vector3 _padPos;
    private RocketConfig? _rocketConfig;

    // ═══ 飞行记录（用于飞行报告）═══
    private float _maxAltitude;
    private float _maxSpeed;
    private float _flightStartTime;
    private float _flightElapsed;
    private float _fuelConsumed;
    private float _initialMass;
    private int _stagesUsed;
    private string _endReason = "";
    private float _impactSpeed;

    // ═══ 坠毁爆炸序列 ═══
    private const float ExplosionDuration = 2.5f;
    private float _explosionTimer;
    private GpuParticles3D? _explosionFireball;
    private GpuParticles3D? _explosionDebris;
    private GpuParticles3D? _explosionSmoke;
    private OmniLight3D? _explosionLight;

    // ═══ 视觉节点 ═══
    private Node3D? _rocketBody;
    private GpuParticles3D? _thrustFlame;
    private GpuParticles3D? _smokeTrail;
    private GpuParticles3D? _sparkParticles;
    private GpuParticles3D? _reentryHeat;
    private OmniLight3D? _thrustLight;

    // ═══ 事件 ═══
    public event Action? OnLaunchComplete;
    public event Action? OnLandedSafe;
    public event Action<FlightReport>? OnFlightReport;
    public event Action<TelemetryData>? OnTelemetryUpdate;

    // ═══ 公共属性 ═══
    public float Altitude => _position.Y;
    public float Velocity => _vel3D.Length();
    public float VerticalSpeed => _vel3D.Y;
    public FlightPhase Phase => _phase;
    public bool IsActive => _phase != FlightPhase.Idle && _phase != FlightPhase.Landed
                         && _phase != FlightPhase.Crashed && _phase != FlightPhase.Exploding;
    public Node3D? RocketBody => _rocketBody;

    public void PlaceOnPad(Vector3 padPosition, RocketConfig config)
    {
        _padPos = padPosition;
        _rocketConfig = config;
        config.RebuildStages();

        _position = Vector3.Zero;
        _vel3D = Vector3.Zero;
        _acceleration = 0;
        _timer = 0;
        _currentStage = 0;
        _parachuteDeployed = false;
        _throttle = 1f;
        _yaw = 0;
        _pitch = 90f;
        _roll = 0;
        _engineCutoff = false;
        _hoverMode = false;
        _hoverTargetAlt = 0;

        _currentMass = config.TotalMass;
        _initialMass = config.TotalMass;
        _remainingFuel = config.TotalFuel;
        _totalFuelCapacity = config.TotalFuel;
        _currentThrust = config.TotalThrust;
        _currentDragCd = config.TotalDragCoefficient;

        _maxAltitude = 0;
        _maxSpeed = 0;
        _fuelConsumed = 0;
        _stagesUsed = 0;
        _endReason = "";
        _impactSpeed = 0;
        _explosionTimer = 0;

        float totalThrust = 0, weightedISP = 0;
        foreach (var id in config.InstalledPartIds)
        {
            var p = RocketPartDef.Get(id);
            if (p != null && p.Thrust > 0 && p.ISP > 0)
            {
                totalThrust += p.Thrust;
                weightedISP += p.Thrust * p.ISP;
            }
        }
        _currentISP = totalThrust > 0 ? weightedISP / totalThrust : 280f;

        _phase = FlightPhase.Idle;
        BuildRocketVisual();
        GD.Print($"[LaunchSequence] Placed on pad — Mass={_currentMass:F1}t, Thrust={_currentThrust:F0}kN, TWR={config.ThrustToWeightRatio:F2}");
    }

    public void BeginLaunch()
    {
        if (_rocketConfig == null) return;
        _timer = 0;
        _flightStartTime = (float)Time.GetTicksMsec() / 1000f;
        _phase = FlightPhase.PreLaunch;
        GD.Print("[LaunchSequence] Pre-launch countdown started");
    }

    public void BeginLaunch(Vector3 padPosition, RocketConfig config)
    {
        PlaceOnPad(padPosition, config);
        BeginLaunch();
    }

    public void SetThrottle(float value) => _throttle = Mathf.Clamp(value, 0f, 1f);

    public void PerformStaging()
    {
        if (_rocketConfig == null || _currentStage >= _rocketConfig.Stages.Count - 1) return;

        var droppedStage = _rocketConfig.Stages[_currentStage];
        _currentMass -= droppedStage.WetMass;
        _currentStage++;

        RecalculateEngineStats();
        _stagesUsed++;
        GD.Print($"[LaunchSequence] Staged! Now on stage {_currentStage}, mass={_currentMass:F1}t");
    }

    public void Abort()
    {
        GD.Print("[LaunchSequence] ABORT!");
        if (_position.Y > 0 && _vel3D.Y > 0)
        {
            _phase = FlightPhase.Descent;
            _throttle = 0;
            DeployParachute();
        }
        else
        {
            _phase = FlightPhase.Idle;
            CleanupVisuals();
        }
    }
}
