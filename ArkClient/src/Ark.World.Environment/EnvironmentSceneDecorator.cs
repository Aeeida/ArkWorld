using Godot;
using Ark.World.Core;

namespace Ark.World.Environment;

/// <summary>
/// 环境场景装饰器 — 为每种环境预设添加独特的视觉元素。
///
/// 职责：
///   • 根据 EnvironmentPreset 创建/销毁场景装饰（粒子、灯光、道具）
///   • 跟随玩家位置移动环境粒子（雾、尘、萤火虫等）
///   • 可被服务器指令动态切换/扩展
///
/// 设计原则：
///   • 每种预设的装饰元素定义为独立方法，便于扩展
///   • 所有节点挂载在单一根节点下，切换时整体替换
///   • 资源按预设分组管理，避免泄漏
/// </summary>
public sealed class EnvironmentSceneDecorator : IWorldSystem
{
    public string SystemId => "SceneDecorator";

    private readonly Node3D _root;
    private Node3D? _activeDecor;
    private EnvironmentPreset _currentPreset = EnvironmentPreset.Natural;

    // 跟随位置（玩家）
    private Vector3 _followPosition;

    /// <summary>场景根节点。</summary>
    public Node3D SceneRoot => _root;

    public EnvironmentSceneDecorator()
    {
        _root = new Node3D { Name = "SceneDecorator" };
    }

    public void Initialize(WorldSeed seed)
    {
        // 默认不创建装饰
    }

    /// <summary>
    /// 切换到指定预设的场景装饰。
    /// </summary>
    public void SwitchPreset(EnvironmentPreset preset)
    {
        if (preset == _currentPreset && _activeDecor != null) return;
        _currentPreset = preset;

        // 清除旧装饰
        ClearDecor();

        // 创建新装饰
        _activeDecor = preset switch
        {
            EnvironmentPreset.BeautifulWild  => BuildWildDecor(),
            EnvironmentPreset.DarkForest     => BuildDarkForestDecor(),
            EnvironmentPreset.HorrorDungeon  => BuildDungeonDecor(),
            EnvironmentPreset.ModernCity      => BuildCityDecor(),
            EnvironmentPreset.RuinArchaeology => BuildRuinsDecor(),
            EnvironmentPreset.MysticSky       => BuildSkyDecor(),
            EnvironmentPreset.SpaceUniverse   => BuildSpaceDecor(),
            _ => BuildNaturalDecor(),
        };

        if (_activeDecor != null)
            _root.AddChild(_activeDecor);
    }

    /// <summary>更新跟随位置。</summary>
    public void UpdateFollowPosition(Vector3 playerPos)
    {
        _followPosition = playerPos;

        // 移动跟随型粒子效果到玩家附近
        if (_activeDecor != null)
        {
            // 只移动标记为跟随的子节点
            foreach (var child in _activeDecor.GetChildren())
            {
                if (child is Node3D node && node.HasMeta("follow_player"))
                    node.GlobalPosition = new Vector3(playerPos.X, playerPos.Y + (float)node.GetMeta("follow_offset_y", 5f), playerPos.Z);
            }
        }
    }

    public void Update(float deltaTime)
    {
        // 动态粒子由 GPU 驱动，无需每帧逻辑
    }

    public void Shutdown()
    {
        ClearDecor();
    }

    private void ClearDecor()
    {
        if (_activeDecor != null)
        {
            if (GodotObject.IsInstanceValid(_activeDecor))
            {
                // 停止所有粒子发射，防止 Free 时 GPU 粒子系统状态异常
                foreach (var child in _activeDecor.GetChildren())
                {
                    if (child is GpuParticles3D particles)
                        particles.Emitting = false;
                }
                if (_activeDecor.GetParent() != null)
                    _activeDecor.GetParent().RemoveChild(_activeDecor);
                _activeDecor.Free();
            }
            _activeDecor = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                     各预设装饰构建
    // ═══════════════════════════════════════════════════════════════════════

    private static Node3D BuildNaturalDecor()
    {
        var root = new Node3D { Name = "Decor_Natural" };

        // 微风粒子 — 飘浮的白色微粒
        var windParticles = CreateAmbientParticles("WindDust",
            amount: 200, lifetime: 6f,
            color: new Color(1f, 1f, 0.9f, 0.15f),
            velocity: 2f, spread: 45f,
            boxExtents: new Vector3(40f, 15f, 40f),
            scaleMin: 0.02f, scaleMax: 0.06f);
        MarkFollowPlayer(windParticles, 10f);
        root.AddChild(windParticles);

        return root;
    }

    private static Node3D BuildWildDecor()
    {
        var root = new Node3D { Name = "Decor_Wild" };

        // 蝴蝶/花粉粒子
        var pollenParticles = CreateAmbientParticles("Pollen",
            amount: 300, lifetime: 8f,
            color: new Color(1f, 0.95f, 0.6f, 0.25f),
            velocity: 1.5f, spread: 60f,
            boxExtents: new Vector3(35f, 10f, 35f),
            scaleMin: 0.03f, scaleMax: 0.08f);
        MarkFollowPlayer(pollenParticles, 8f);
        root.AddChild(pollenParticles);

        // 蝴蝶粒子（较大、较少、暖色）
        var butterflies = CreateAmbientParticles("Butterflies",
            amount: 30, lifetime: 12f,
            color: new Color(1f, 0.6f, 0.3f, 0.6f),
            velocity: 0.8f, spread: 80f,
            boxExtents: new Vector3(20f, 6f, 20f),
            scaleMin: 0.1f, scaleMax: 0.2f);
        MarkFollowPlayer(butterflies, 5f);
        root.AddChild(butterflies);

        // 暖色环境光
        var warmLight = new OmniLight3D
        {
            Name = "WarmAmbient",
            LightColor = new Color(1f, 0.9f, 0.7f),
            LightEnergy = 0.3f,
            OmniRange = 60f,
            OmniAttenuation = 2f,
            Position = new Vector3(0, 15f, 0),
        };
        MarkFollowPlayer(warmLight, 15f);
        root.AddChild(warmLight);

        return root;
    }

    private static Node3D BuildDarkForestDecor()
    {
        var root = new Node3D { Name = "Decor_DarkForest" };

        // 浓雾粒子
        var fog = CreateAmbientParticles("ForestFog",
            amount: 400, lifetime: 10f,
            color: new Color(0.4f, 0.5f, 0.35f, 0.2f),
            velocity: 0.5f, spread: 90f,
            boxExtents: new Vector3(40f, 4f, 40f),
            scaleMin: 0.3f, scaleMax: 0.8f);
        MarkFollowPlayer(fog, 2f);
        root.AddChild(fog);

        // 萤火虫
        var fireflies = CreateAmbientParticles("Fireflies",
            amount: 60, lifetime: 6f,
            color: new Color(0.5f, 1f, 0.3f, 0.7f),
            velocity: 0.3f, spread: 180f,
            boxExtents: new Vector3(25f, 8f, 25f),
            scaleMin: 0.04f, scaleMax: 0.08f);
        MarkFollowPlayer(fireflies, 4f);
        root.AddChild(fireflies);

        // 阴暗绿色环境光
        var forestLight = new OmniLight3D
        {
            Name = "ForestAmbient",
            LightColor = new Color(0.3f, 0.5f, 0.2f),
            LightEnergy = 0.5f,
            OmniRange = 40f,
            OmniAttenuation = 1.5f,
            Position = new Vector3(0, 8f, 0),
        };
        MarkFollowPlayer(forestLight, 8f);
        root.AddChild(forestLight);

        return root;
    }

    private static Node3D BuildDungeonDecor()
    {
        var root = new Node3D { Name = "Decor_Dungeon" };

        // 灰色灰尘粒子 — 漂浮在空气中
        var dust = CreateAmbientParticles("DungeonDust",
            amount: 250, lifetime: 8f,
            color: new Color(0.7f, 0.7f, 0.75f, 0.3f),
            velocity: 0.2f, spread: 180f,
            boxExtents: new Vector3(20f, 10f, 20f),
            scaleMin: 0.02f, scaleMax: 0.06f);
        MarkFollowPlayer(dust, 5f);
        root.AddChild(dust);

        // 主照明：灰白色全局灯光（确保可见）
        var mainLight = new OmniLight3D
        {
            Name = "DungeonMainLight",
            LightColor = new Color(0.75f, 0.75f, 0.8f),
            LightEnergy = 1.2f,
            OmniRange = 50f,
            OmniAttenuation = 1.5f,
            Position = new Vector3(0, 12f, 0),
        };
        MarkFollowPlayer(mainLight, 12f);
        root.AddChild(mainLight);

        // 幽暗绿色点光源 — 营造恐怖气氛
        var eerieLight = new OmniLight3D
        {
            Name = "EerieGlow",
            LightColor = new Color(0.3f, 0.6f, 0.4f),
            LightEnergy = 0.6f,
            OmniRange = 25f,
            OmniAttenuation = 2f,
            Position = new Vector3(8f, 3f, -5f),
        };
        MarkFollowPlayer(eerieLight, 3f);
        root.AddChild(eerieLight);

        // 微弱的蓝紫色辅助灯
        var auxLight = new OmniLight3D
        {
            Name = "AuxLight",
            LightColor = new Color(0.4f, 0.35f, 0.6f),
            LightEnergy = 0.4f,
            OmniRange = 30f,
            OmniAttenuation = 2f,
            Position = new Vector3(-6f, 6f, 4f),
        };
        MarkFollowPlayer(auxLight, 6f);
        root.AddChild(auxLight);

        return root;
    }

    private static Node3D BuildCityDecor()
    {
        var root = new Node3D { Name = "Decor_City" };

        // 霓虹灯粒子
        var neonDust = CreateAmbientParticles("NeonDust",
            amount: 150, lifetime: 5f,
            color: new Color(0.8f, 0.4f, 1f, 0.15f),
            velocity: 1f, spread: 40f,
            boxExtents: new Vector3(30f, 20f, 30f),
            scaleMin: 0.01f, scaleMax: 0.04f);
        MarkFollowPlayer(neonDust, 10f);
        root.AddChild(neonDust);

        // 暖橙路灯
        var streetLight = new OmniLight3D
        {
            Name = "StreetLight",
            LightColor = new Color(1f, 0.85f, 0.6f),
            LightEnergy = 0.8f,
            OmniRange = 35f,
            OmniAttenuation = 1.8f,
            Position = new Vector3(0, 10f, 0),
        };
        MarkFollowPlayer(streetLight, 10f);
        root.AddChild(streetLight);

        // 蓝色霓虹辅灯
        var neonLight = new OmniLight3D
        {
            Name = "NeonGlow",
            LightColor = new Color(0.3f, 0.5f, 1f),
            LightEnergy = 0.4f,
            OmniRange = 20f,
            OmniAttenuation = 2f,
            Position = new Vector3(10f, 5f, -8f),
        };
        MarkFollowPlayer(neonLight, 5f);
        root.AddChild(neonLight);

        return root;
    }

    private static Node3D BuildRuinsDecor()
    {
        var root = new Node3D { Name = "Decor_Ruins" };

        // 沙尘粒子
        var sandDust = CreateAmbientParticles("SandDust",
            amount: 350, lifetime: 6f,
            color: new Color(0.8f, 0.7f, 0.5f, 0.2f),
            velocity: 3f, spread: 30f,
            boxExtents: new Vector3(40f, 8f, 40f),
            scaleMin: 0.04f, scaleMax: 0.12f);
        MarkFollowPlayer(sandDust, 6f);
        root.AddChild(sandDust);

        // 暖黄阳光效果
        var sunBeam = new OmniLight3D
        {
            Name = "SunBeam",
            LightColor = new Color(1f, 0.9f, 0.65f),
            LightEnergy = 0.6f,
            OmniRange = 45f,
            OmniAttenuation = 1.5f,
            Position = new Vector3(0, 20f, 0),
        };
        MarkFollowPlayer(sunBeam, 20f);
        root.AddChild(sunBeam);

        return root;
    }

    private static Node3D BuildSkyDecor()
    {
        var root = new Node3D { Name = "Decor_Sky" };

        // 云雾粒子
        var cloudMist = CreateAmbientParticles("CloudMist",
            amount: 200, lifetime: 12f,
            color: new Color(0.9f, 0.92f, 1f, 0.15f),
            velocity: 1.5f, spread: 50f,
            boxExtents: new Vector3(50f, 20f, 50f),
            scaleMin: 0.5f, scaleMax: 1.5f);
        MarkFollowPlayer(cloudMist, 0f);
        root.AddChild(cloudMist);

        // 闪烁光粒（魔法感）
        var sparkles = CreateAmbientParticles("Sparkles",
            amount: 100, lifetime: 4f,
            color: new Color(0.7f, 0.85f, 1f, 0.5f),
            velocity: 0.5f, spread: 180f,
            boxExtents: new Vector3(30f, 15f, 30f),
            scaleMin: 0.03f, scaleMax: 0.07f);
        MarkFollowPlayer(sparkles, 8f);
        root.AddChild(sparkles);

        // 蓝白天空光
        var skyGlow = new OmniLight3D
        {
            Name = "SkyGlow",
            LightColor = new Color(0.7f, 0.8f, 1f),
            LightEnergy = 0.5f,
            OmniRange = 60f,
            OmniAttenuation = 1.5f,
            Position = new Vector3(0, 30f, 0),
        };
        MarkFollowPlayer(skyGlow, 30f);
        root.AddChild(skyGlow);

        return root;
    }

    private static Node3D BuildSpaceDecor()
    {
        var root = new Node3D { Name = "Decor_Space" };

        // 星尘粒子
        var starDust = CreateAmbientParticles("StarDust",
            amount: 500, lifetime: 15f,
            color: new Color(0.8f, 0.85f, 1f, 0.3f),
            velocity: 0.1f, spread: 180f,
            boxExtents: new Vector3(60f, 60f, 60f),
            scaleMin: 0.02f, scaleMax: 0.05f);
        MarkFollowPlayer(starDust, 0f);
        root.AddChild(starDust);

        // 辐射闪光
        var radiation = CreateAmbientParticles("Radiation",
            amount: 20, lifetime: 3f,
            color: new Color(0.3f, 1f, 0.5f, 0.4f),
            velocity: 5f, spread: 180f,
            boxExtents: new Vector3(40f, 40f, 40f),
            scaleMin: 0.05f, scaleMax: 0.15f);
        MarkFollowPlayer(radiation, 0f);
        root.AddChild(radiation);

        return root;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          工具方法
    // ═══════════════════════════════════════════════════════════════════════

    private static GpuParticles3D CreateAmbientParticles(
        string name, int amount, float lifetime,
        Color color, float velocity, float spread,
        Vector3 boxExtents, float scaleMin, float scaleMax)
    {
        var particles = new GpuParticles3D
        {
            Name = name,
            Amount = amount,
            Lifetime = lifetime,
            Explosiveness = 0f,
            Emitting = true,
        };

        particles.ProcessMaterial = new ParticleProcessMaterial
        {
            Direction = new Vector3(0.2f, 0.1f, 0.3f).Normalized(),
            Spread = spread,
            InitialVelocityMin = velocity * 0.3f,
            InitialVelocityMax = velocity,
            Gravity = new Vector3(0, -0.05f, 0),
            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            Color = color,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = boxExtents,
        };

        particles.DrawPass1 = new QuadMesh
        {
            Size = new Vector2(1f, 1f),
            Material = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            },
        };

        return particles;
    }

    private static void MarkFollowPlayer(Node3D node, float offsetY)
    {
        node.SetMeta("follow_player", true);
        node.SetMeta("follow_offset_y", offsetY);
    }
}
