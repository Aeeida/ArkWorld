using Godot;
using System;
using Ark.Events;
using Ark.UI;

namespace Ark.Gameplay.Space;

public partial class LaunchSequenceController
{
    private void BuildRocketVisual()
    {
        CleanupVisuals();

        _rocketBody = new Node3D { Name = "RocketBody" };
        AddChild(_rocketBody);

        float heightScale = Mathf.Clamp(_rocketConfig?.TotalMass ?? 5f, 3f, 20f);
        float radiusScale = Mathf.Clamp(heightScale * 0.12f, 0.5f, 2f);

        var bodyMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radiusScale, BottomRadius = radiusScale, Height = heightScale },
            Position = new Vector3(0, heightScale * 0.5f, 0),
        };
        bodyMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.85f, 0.88f),
            Metallic = 0.6f,
            Roughness = 0.3f
        };
        _rocketBody.AddChild(bodyMesh);

        var noseMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = radiusScale, Height = heightScale * 0.25f },
            Position = new Vector3(0, heightScale + heightScale * 0.125f, 0),
        };
        noseMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.3f, 0.2f),
            Metallic = 0.3f,
            Roughness = 0.5f
        };
        _rocketBody.AddChild(noseMesh);

        var nozzleMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radiusScale * 0.75f, BottomRadius = radiusScale * 1.1f, Height = heightScale * 0.1f },
            Position = new Vector3(0, -heightScale * 0.05f, 0),
        };
        nozzleMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.3f, 0.35f),
            Metallic = 0.8f,
            Roughness = 0.2f
        };
        _rocketBody.AddChild(nozzleMesh);

        _rocketBody.Position = new Vector3(_padPos.X, _padPos.Y + 5f, _padPos.Z);

        _thrustFlame = CreateFlameParticles(radiusScale);
        _thrustFlame.Emitting = false;
        _rocketBody.AddChild(_thrustFlame);

        _smokeTrail = CreateSmokeParticles();
        _smokeTrail.Emitting = false;
        _rocketBody.AddChild(_smokeTrail);

        _sparkParticles = CreateSparkParticles();
        _sparkParticles.Emitting = false;
        _rocketBody.AddChild(_sparkParticles);

        _reentryHeat = CreateReentryParticles();
        _reentryHeat.Emitting = false;
        _rocketBody.AddChild(_reentryHeat);

        _thrustLight = new OmniLight3D
        {
            Position = new Vector3(0, -1f, 0),
            LightColor = new Color(1f, 0.7f, 0.3f),
            LightEnergy = 3f,
            OmniRange = 15f,
            Visible = false,
        };
        _rocketBody.AddChild(_thrustLight);
    }

    private static GpuParticles3D CreateFlameParticles(float scale)
    {
        var particles = new GpuParticles3D
        {
            Name = "ThrustFlame",
            Amount = 300,
            Lifetime = 0.5f,
            SpeedScale = 2.0f,
            Position = new Vector3(0, -1f, 0),
            DrawOrder = GpuParticles3D.DrawOrderEnum.Lifetime,
        };
        particles.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 10f,
            InitialVelocityMin = 10f * scale,
            InitialVelocityMax = 20f * scale,
            Gravity = new Vector3(0, -2f, 0),
            ScaleMin = 0.3f * scale,
            ScaleMax = 0.8f * scale,
            Color = new Color(1f, 0.6f, 0.1f),
        };
        particles.DrawPass1 = new QuadMesh { Size = new Vector2(0.5f * scale, 0.5f * scale) };
        return particles;
    }

    private static GpuParticles3D CreateSmokeParticles()
    {
        var particles = new GpuParticles3D
        {
            Name = "SmokeTrail",
            Amount = 150,
            Lifetime = 3.0f,
            SpeedScale = 1.0f,
            Position = new Vector3(0, -2f, 0),
        };
        particles.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 25f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 8f,
            Gravity = new Vector3(0, 0.5f, 0),
            ScaleMin = 1.0f,
            ScaleMax = 4.0f,
            Color = new Color(0.6f, 0.6f, 0.6f, 0.5f),
        };
        particles.DrawPass1 = new QuadMesh { Size = new Vector2(2f, 2f) };
        return particles;
    }

    private static GpuParticles3D CreateSparkParticles()
    {
        var particles = new GpuParticles3D
        {
            Name = "Sparks",
            Amount = 80,
            Lifetime = 0.3f,
            SpeedScale = 3.0f,
            Position = new Vector3(0, -0.5f, 0),
        };
        particles.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 45f,
            InitialVelocityMin = 5f,
            InitialVelocityMax = 12f,
            Gravity = new Vector3(0, -9.8f, 0),
            ScaleMin = 0.05f,
            ScaleMax = 0.15f,
            Color = new Color(1f, 0.9f, 0.4f),
        };
        particles.DrawPass1 = new QuadMesh { Size = new Vector2(0.1f, 0.1f) };
        return particles;
    }

    private static GpuParticles3D CreateReentryParticles()
    {
        var particles = new GpuParticles3D
        {
            Name = "ReentryHeat",
            Amount = 100,
            Lifetime = 0.4f,
            SpeedScale = 1.5f,
            Position = new Vector3(0, 5f, 0),
        };
        particles.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 30f,
            InitialVelocityMin = 2f,
            InitialVelocityMax = 6f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.5f,
            ScaleMax = 1.5f,
            Color = new Color(1f, 0.4f, 0.1f, 0.7f),
        };
        particles.DrawPass1 = new QuadMesh { Size = new Vector2(0.8f, 0.8f) };
        return particles;
    }

    private void UpdateRocketPosition()
    {
        if (_rocketBody == null) return;

        _rocketBody.Position = new Vector3(
            _padPos.X + _position.X,
            _padPos.Y + 5f + _position.Y,
            _padPos.Z + _position.Z);

        Vector3 thrustDir = GetThrustDirection();
        float speed = _vel3D.Length();

        Vector3 bodyDir;
        if (_phase == FlightPhase.Powered || speed < 1f)
        {
            bodyDir = thrustDir;
        }
        else
        {
            float thrustInfluence = (_engineCutoff || _throttle < 0.01f) ? 0f : 0.6f;
            bodyDir = (thrustDir * thrustInfluence + _vel3D.Normalized() * (1f - thrustInfluence)).Normalized();
        }

        if (bodyDir.LengthSquared() > 0.001f)
        {
            var up = Vector3.Up;
            var dot = up.Dot(bodyDir);
            Quaternion baseQuat;
            if (dot > 0.9999f)
            {
                baseQuat = Quaternion.Identity;
            }
            else if (dot < -0.9999f)
            {
                baseQuat = new Quaternion(Vector3.Right, Mathf.Pi);
            }
            else
            {
                var axis = up.Cross(bodyDir).Normalized();
                float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
                baseQuat = new Quaternion(axis, angle);
            }

            var rollQuat = new Quaternion(bodyDir, Mathf.DegToRad(_roll));
            var finalQuat = (rollQuat * baseQuat).Normalized();
            _rocketBody.Quaternion = finalQuat;
        }
    }

    private void UpdateParticleIntensity(float factor)
    {
        if (_thrustFlame?.ProcessMaterial is ParticleProcessMaterial flameMat)
        {
            flameMat.InitialVelocityMax = 20f * factor;
            flameMat.Color = new Color(1f, 0.6f * factor, 0.1f * factor);
        }
        if (_smokeTrail?.ProcessMaterial is ParticleProcessMaterial smokeMat)
        {
            float atmoFactor = SeaLevelDensity * MathF.Exp(-MathF.Max(_position.Y, 0) / AtmoScaleHeight);
            smokeMat.InitialVelocityMax = 8f * MathF.Min(atmoFactor, 1f);
            smokeMat.Color = new Color(0.6f, 0.6f, 0.6f, 0.5f * MathF.Min(atmoFactor, 1f));
        }
    }

    private void UpdateThrustLight(float factor)
    {
        if (_thrustLight != null)
        {
            _thrustLight.LightEnergy = 3f * factor;
            _thrustLight.OmniRange = 15f * factor;
        }
    }

    public void CleanupVisuals()
    {
        if (_rocketBody != null && _rocketBody.IsInsideTree())
        {
            _rocketBody.QueueFree();
            _rocketBody = null;
        }
        _thrustFlame = null;
        _smokeTrail = null;
        _sparkParticles = null;
        _reentryHeat = null;
        _thrustLight = null;
        CleanupExplosion();
    }
}
