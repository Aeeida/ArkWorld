using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Environment;

/// <summary>
/// 大气控制器 — 将 DayNightCycle + WeatherSystem 的输出应用到 Godot 渲染节点。
/// 自行创建 WorldEnvironment、DirectionalLight3D（太阳/月亮）、雾、天空颜色。
/// </summary>
public sealed class AtmosphereController : IWorldSystem
{
    public string SystemId => "Atmosphere";

    private readonly DayNightCycle _dayNight;
    private readonly WeatherSystem _weather;

    private WorldEnvironment? _worldEnv;
    private DirectionalLight3D? _sunLight;
    private DirectionalLight3D? _moonLight;
    private Node3D _root;
    private bool _modeOverrideActive;

    /// <summary>场景根节点（包含所有大气相关子节点）。</summary>
    public Node3D SceneRoot => _root;

    public AtmosphereController(DayNightCycle dayNight, WeatherSystem weather)
    {
        _dayNight = dayNight;
        _weather = weather;
        _root = new Node3D { Name = "Atmosphere" };
    }

    private ProceduralSkyMaterial? _skyMaterial;

    public void Initialize(WorldSeed seed)
    {
        _sunLight = new DirectionalLight3D
        {
            Name = "Sun",
            ShadowEnabled = true,
            LightEnergy = 1.0f,
            LightColor = Colors.White,
        };
        _root.AddChild(_sunLight);

        _moonLight = new DirectionalLight3D
        {
            Name = "Moon",
            ShadowEnabled = false,
            LightEnergy = 0.05f,
            LightColor = new Color(0.6f, 0.65f, 0.8f),
        };
        _root.AddChild(_moonLight);

        // ── 程序化天空 ──
        _skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.25f, 0.45f, 0.85f),
            SkyHorizonColor = new Color(0.55f, 0.7f, 0.9f),
            GroundBottomColor = new Color(0.15f, 0.13f, 0.1f),
            GroundHorizonColor = new Color(0.4f, 0.38f, 0.35f),
            SunAngleMax = 30f,
            SunCurve = 0.15f,
        };

        var sky = new Sky
        {
            SkyMaterial = _skyMaterial,
            ProcessMode = Sky.ProcessModeEnum.Incremental,
        };

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.8f,
            AmbientLightEnergy = 0.6f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            SsaoEnabled = false,
            FogEnabled = false,
        };
        _worldEnv = new WorldEnvironment { Name = "WorldEnv", Environment = env };
        _root.AddChild(_worldEnv);

        GD.Print("[Atmosphere] Initialized with procedural sky");
    }

    public void Update(float deltaTime)
    {
        if (_worldEnv?.Environment == null || _sunLight == null) return;

        // 模式覆盖激活时跳过日夜/天气驱动的大气更新（保持 ApplyModeOverride 设定值）
        if (_modeOverrideActive) return;

        var env = _worldEnv.Environment;
        var ws = _weather.State;

        // ── 太阳位置/颜色 ──
        var sunDir = _dayNight.GetSunDirection();
        // 防止 sunDir 与 Up 平行导致 LookAt 崩溃
        var negSunDir = -sunDir;
        if (MathF.Abs(negSunDir.Y) > 0.999f)
            negSunDir.X += 0.001f;
        _sunLight.LookAtFromPosition(Vector3.Zero, negSunDir, Vector3.Up);
        _sunLight.LightColor = _dayNight.GetSunColor();
        _sunLight.LightEnergy = _dayNight.GetSunEnergy() * (1f - ws.CloudCoverage * 0.5f);

        // ── 月光 ──
        if (_moonLight != null)
        {
            var moonDir = new Vector3(-sunDir.X, MathF.Max(0.1f, -sunDir.Y), -sunDir.Z).Normalized();
            _moonLight.LookAtFromPosition(Vector3.Zero, -moonDir, Vector3.Up);
            _moonLight.LightEnergy = _dayNight.GetSunEnergy() < 0.2f ? 0.08f : 0f;
        }

        // ── 程序化天空颜色随日夜/天气变化 ──
        if (_skyMaterial != null)
        {
            var skyColor = _dayNight.GetSkyColor();
            skyColor = skyColor.Lerp(new Color(0.4f, 0.42f, 0.45f), ws.CloudCoverage * 0.6f);

            _skyMaterial.SkyTopColor = skyColor;
            _skyMaterial.SkyHorizonColor = skyColor.Lerp(Colors.White, 0.3f);

            // 地面色随天空联动
            float brightness = skyColor.R * 0.299f + skyColor.G * 0.587f + skyColor.B * 0.114f;
            _skyMaterial.GroundBottomColor = new Color(
                brightness * 0.3f, brightness * 0.25f, brightness * 0.2f);
            _skyMaterial.GroundHorizonColor = skyColor.Lerp(
                new Color(0.4f, 0.38f, 0.35f), 0.5f);
        }

        // ── 环境光 ──
        float ambientBase = _dayNight.GetAmbientEnergy();
        env.AmbientLightEnergy = ambientBase * (1f - ws.CloudCoverage * 0.3f);

        // ── 雾 ──
        bool fogNeeded = ws.FogDensity > 0.02f;
        env.FogEnabled = fogNeeded;
        if (fogNeeded)
        {
            var skyColor2 = _dayNight.GetSkyColor();
            env.FogLightColor = skyColor2.Lerp(Colors.White, 0.3f);
            env.FogDensity = ws.FogDensity * 0.01f;
        }
    }

    public void Shutdown()
    {
        _sunLight = null;
        _moonLight = null;
        _worldEnv = null;
        _skyMaterial = null;
    }

    /// <summary>为特定玩法模式覆盖大气设置（如太空模式深空黑底）。</summary>
    public void ApplyModeOverride(string mode, Color bgColor, Color ambientColor,
                                   float ambientEnergy, Color sunColor, float sunEnergy)
    {
        if (_worldEnv?.Environment == null || _sunLight == null) return;
        var env = _worldEnv.Environment;

        _modeOverrideActive = true;

        if (mode == "Space")
        {
            // 太空模式：关闭天空，使用纯色背景
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = bgColor;
        }
        else
        {
            // 恢复天空
            env.BackgroundMode = Godot.Environment.BGMode.Sky;
        }

        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor = ambientColor;
        env.AmbientLightEnergy = ambientEnergy;
        env.FogEnabled = false;
        _sunLight.LightColor = sunColor;
        _sunLight.LightEnergy = sunEnergy;
    }

    /// <summary>清除模式覆盖，恢复到日夜循环驱动的默认大气。</summary>
    public void ClearModeOverride()
    {
        if (_worldEnv?.Environment == null || _sunLight == null) return;
        var env = _worldEnv.Environment;

        _modeOverrideActive = false;

        env.BackgroundMode = Godot.Environment.BGMode.Sky;
        env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
        env.FogEnabled = true;
        // 颜色/强度将在下一帧由 Update 从 DayNightCycle 刷新
    }
}
