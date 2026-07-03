using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Environment;

/// <summary>
/// 降水渲染器 — 根据 WeatherState 创建/管理雨雪粒子效果。
/// 跟随玩家位置移动。
/// </summary>
public sealed class PrecipitationRenderer : IWorldSystem
{
    public string SystemId => "Precipitation";

    private readonly WeatherState _weatherState;
    private GpuParticles3D? _rainParticles;
    private GpuParticles3D? _snowParticles;
    private Node3D _root;

    public Node3D SceneRoot => _root;

    public PrecipitationRenderer(WeatherState weatherState)
    {
        _weatherState = weatherState;
        _root = new Node3D { Name = "Precipitation" };
    }

    public void Initialize(WorldSeed seed)
    {
        // ── 雨 ──
        _rainParticles = new GpuParticles3D
        {
            Name = "Rain",
            Amount = 2000,
            Lifetime = 1.5f,
            Explosiveness = 0f,
            Emitting = false,
        };
        _rainParticles.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 5f,
            InitialVelocityMin = 15f,
            InitialVelocityMax = 25f,
            Gravity = new Vector3(0, -9.8f, 0),
            ScaleMin = 0.02f,
            ScaleMax = 0.05f,
            Color = new Color(0.6f, 0.7f, 0.85f, 0.5f),
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(30f, 0.5f, 30f),
        };
        _rainParticles.DrawPass1 = new QuadMesh { Size = new Vector2(0.03f, 0.3f) };
        _root.AddChild(_rainParticles);

        // ── 雪 ──
        _snowParticles = new GpuParticles3D
        {
            Name = "Snow",
            Amount = 800,
            Lifetime = 4f,
            Explosiveness = 0f,
            Emitting = false,
        };
        _snowParticles.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 20f,
            InitialVelocityMin = 1f,
            InitialVelocityMax = 3f,
            Gravity = new Vector3(0, -1.5f, 0),
            ScaleMin = 0.05f,
            ScaleMax = 0.12f,
            Color = new Color(0.95f, 0.95f, 1f, 0.8f),
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(25f, 0.5f, 25f),
        };
        _snowParticles.DrawPass1 = new QuadMesh { Size = new Vector2(0.1f, 0.1f) };
        _root.AddChild(_snowParticles);
    }

    public void Update(float deltaTime)
    {
        bool isRain = _weatherState.CurrentType is WeatherType.LightRain or WeatherType.HeavyRain
                      or WeatherType.Thunderstorm or WeatherType.AcidRain;
        bool isSnow = _weatherState.CurrentType == WeatherType.Snow;

        if (_rainParticles != null)
        {
            _rainParticles.Emitting = isRain;
            if (isRain && _rainParticles.ProcessMaterial is ParticleProcessMaterial rainMat)
            {
                _rainParticles.Amount = (int)(500 + 2500 * _weatherState.PrecipitationIntensity);
                rainMat.InitialVelocityMax = 15f + 20f * _weatherState.PrecipitationIntensity;
                // 风吹偏
                rainMat.Direction = new Vector3(
                    MathF.Sin(Mathf.DegToRad(_weatherState.WindDirection)) * _weatherState.WindSpeed * 0.05f,
                    -1f,
                    MathF.Cos(Mathf.DegToRad(_weatherState.WindDirection)) * _weatherState.WindSpeed * 0.05f
                ).Normalized();
            }
        }

        if (_snowParticles != null)
        {
            _snowParticles.Emitting = isSnow;
            if (isSnow && _snowParticles.ProcessMaterial is ParticleProcessMaterial snowMat)
            {
                _snowParticles.Amount = (int)(200 + 1000 * _weatherState.PrecipitationIntensity);
            }
        }
    }

    /// <summary>更新降水跟随位置（应设为玩家头顶上方）。</summary>
    public void UpdateFollowPosition(Vector3 playerPos)
    {
        _root.Position = new Vector3(playerPos.X, playerPos.Y + 30f, playerPos.Z);
    }

    public void Shutdown()
    {
        _rainParticles = null;
        _snowParticles = null;
    }
}
