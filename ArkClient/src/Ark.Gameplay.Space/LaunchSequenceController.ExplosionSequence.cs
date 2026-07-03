using Godot;
using System;
using Ark.Events;
using Ark.UI;

namespace Ark.Gameplay.Space;

public partial class LaunchSequenceController
{
    private void BeginExplosion()
    {
        _phase = FlightPhase.Exploding;
        _explosionTimer = 0;

        CleanupVisuals();

        var explosionPos = new Vector3(_padPos.X + _position.X, _padPos.Y + 2f + _position.Y, _padPos.Z + _position.Z);

        _explosionFireball = new GpuParticles3D
        {
            Name = "ExplosionFireball",
            Amount = 400,
            Lifetime = 1.2f,
            SpeedScale = 1.5f,
            Explosiveness = 0.95f,
            OneShot = true,
            Position = explosionPos,
        };
        _explosionFireball.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 180f,
            InitialVelocityMin = 8f,
            InitialVelocityMax = 25f,
            Gravity = new Vector3(0, -4f, 0),
            ScaleMin = 1.0f,
            ScaleMax = 4.0f,
            Color = new Color(1f, 0.5f, 0.05f),
        };
        _explosionFireball.DrawPass1 = new QuadMesh { Size = new Vector2(2f, 2f) };
        AddChild(_explosionFireball);
        _explosionFireball.Emitting = true;

        _explosionDebris = new GpuParticles3D
        {
            Name = "ExplosionDebris",
            Amount = 200,
            Lifetime = 2.0f,
            SpeedScale = 2.0f,
            Explosiveness = 0.9f,
            OneShot = true,
            Position = explosionPos,
        };
        _explosionDebris.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 180f,
            InitialVelocityMin = 10f,
            InitialVelocityMax = 35f,
            Gravity = new Vector3(0, -9.8f, 0),
            ScaleMin = 0.1f,
            ScaleMax = 0.5f,
            Color = new Color(0.4f, 0.4f, 0.4f),
        };
        _explosionDebris.DrawPass1 = new QuadMesh { Size = new Vector2(0.3f, 0.3f) };
        AddChild(_explosionDebris);
        _explosionDebris.Emitting = true;

        _explosionSmoke = new GpuParticles3D
        {
            Name = "ExplosionSmoke",
            Amount = 150,
            Lifetime = 4.0f,
            SpeedScale = 0.5f,
            Explosiveness = 0.3f,
            OneShot = false,
            Position = explosionPos,
        };
        _explosionSmoke.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 40f,
            InitialVelocityMin = 1f,
            InitialVelocityMax = 5f,
            Gravity = new Vector3(0, 1f, 0),
            ScaleMin = 2f,
            ScaleMax = 8f,
            Color = new Color(0.2f, 0.2f, 0.2f, 0.6f),
        };
        _explosionSmoke.DrawPass1 = new QuadMesh { Size = new Vector2(3f, 3f) };
        AddChild(_explosionSmoke);
        _explosionSmoke.Emitting = true;

        _explosionLight = new OmniLight3D
        {
            Position = explosionPos + new Vector3(0, 3f, 0),
            LightColor = new Color(1f, 0.6f, 0.1f),
            LightEnergy = 8f,
            OmniRange = 40f,
        };
        AddChild(_explosionLight);
    }

    private void UpdateExplosion(float dt)
    {
        _explosionTimer += dt;

        if (_explosionLight != null)
        {
            float fade = Mathf.Clamp(1f - _explosionTimer / 1.5f, 0f, 1f);
            _explosionLight.LightEnergy = 8f * fade;
        }

        if (_explosionTimer > 1.5f && _explosionSmoke != null)
            _explosionSmoke.Emitting = false;

        if (_explosionTimer >= ExplosionDuration)
        {
            CleanupExplosion();
            _phase = FlightPhase.Crashed;
            EmitFlightReport(false);
        }
    }

    private void EmitFlightReport(bool success)
    {
        OnFlightReport?.Invoke(new FlightReport
        {
            Success = success,
            VesselName = _rocketConfig?.VesselName ?? "未命名",
            MaxAltitude = _maxAltitude,
            MaxSpeed = _maxSpeed,
            FlightDuration = _flightElapsed,
            FuelConsumed = _fuelConsumed,
            TotalFuelCapacity = _totalFuelCapacity,
            InitialMass = _initialMass,
            FinalMass = _currentMass,
            StagesUsed = _stagesUsed,
            TotalStages = _rocketConfig?.Stages.Count ?? 0,
            ImpactSpeed = _impactSpeed,
            ParachuteDeployed = _parachuteDeployed,
            EndReason = _endReason,
        });
    }

    private void CleanupExplosion()
    {
        if (_explosionFireball != null && _explosionFireball.IsInsideTree())
        { _explosionFireball.QueueFree(); _explosionFireball = null; }
        if (_explosionDebris != null && _explosionDebris.IsInsideTree())
        { _explosionDebris.QueueFree(); _explosionDebris = null; }
        if (_explosionSmoke != null && _explosionSmoke.IsInsideTree())
        { _explosionSmoke.QueueFree(); _explosionSmoke = null; }
        if (_explosionLight != null && _explosionLight.IsInsideTree())
        { _explosionLight.QueueFree(); _explosionLight = null; }
    }
}
