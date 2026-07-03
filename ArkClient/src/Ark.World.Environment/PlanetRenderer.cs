using Godot;
using Ark.World.Core;

namespace Ark.World.Environment;

/// <summary>
/// 星球渲染器 — 随着摄像头升高逐步显示星球球体 + 大气光环壳。
///
/// 视觉过渡流程：
///   地面 (0~500m)   → 不可见
///   中空 (500~3000m) → 大气外壳边缘开始发光，球面若隐若现
///   高空 (3000~10000m) → 球面明显可见，大陆/海洋/云层纹理清晰
///   太空 (>10000m) → 完整球体 + 大气光晕壳
///
/// 组成：
///   1. 星球球体 — 程序化大陆/海洋/极地/山脉/云层
///   2. 大气外壳 — 稍大的半透明球体，rim 光模拟大气散射
///   3. 地形融合 — 球面顶部（地图区域）透明，让实际地形可见
/// </summary>
public sealed class PlanetRenderer
{
    private readonly Node3D _root;
    private MeshInstance3D? _planetMesh;
    private MeshInstance3D? _atmosphereMesh;
    private ShaderMaterial? _planetMaterial;
    private ShaderMaterial? _atmosphereMaterial;

    public Node3D SceneRoot => _root;

    public PlanetRenderer()
    {
        _root = new Node3D { Name = "PlanetRenderer" };
    }

    public void Initialize()
    {
        float r = WorldConstants.PlanetRadius;

        // ── 1. 星球球体 ──
        var sphereMesh = new SphereMesh
        {
            Radius = r,
            Height = r * 2f,
            RadialSegments = 128,
            Rings = 80,
        };

        _planetMaterial = CreatePlanetMaterial();
        _planetMesh = new MeshInstance3D
        {
            Mesh = sphereMesh,
            MaterialOverride = _planetMaterial,
            Position = new Vector3(0, -r, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Name = "PlanetSphere",
            GIMode = GeometryInstance3D.GIModeEnum.Disabled,
        };
        _root.AddChild(_planetMesh);

        // ── 2. 大气外壳（稍大的半透明球体）──
        float atmoR = r * 1.025f;
        var atmoMesh = new SphereMesh
        {
            Radius = atmoR,
            Height = atmoR * 2f,
            RadialSegments = 64,
            Rings = 40,
        };

        _atmosphereMaterial = CreateAtmosphereMaterial();
        _atmosphereMesh = new MeshInstance3D
        {
            Mesh = atmoMesh,
            MaterialOverride = _atmosphereMaterial,
            Position = new Vector3(0, -r, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Name = "AtmosphereShell",
            GIMode = GeometryInstance3D.GIModeEnum.Disabled,
        };
        _root.AddChild(_atmosphereMesh);
    }

    public void Update(float cameraAltitude)
    {
        if (_planetMaterial == null || _planetMesh == null) return;

        float visibility;
        if (cameraAltitude < WorldConstants.PlanetVisibleMinAlt)
        {
            visibility = 0f;
        }
        else if (cameraAltitude > WorldConstants.PlanetVisibleMaxAlt)
        {
            visibility = 1f;
        }
        else
        {
            float t = (cameraAltitude - WorldConstants.PlanetVisibleMinAlt) /
                      (WorldConstants.PlanetVisibleMaxAlt - WorldConstants.PlanetVisibleMinAlt);
            visibility = t * t * (3f - 2f * t);
        }

        _planetMaterial.SetShaderParameter("visibility", visibility);
        _planetMaterial.SetShaderParameter("camera_altitude", cameraAltitude);
        _planetMesh.Visible = visibility > 0.001f;

        if (_atmosphereMaterial != null && _atmosphereMesh != null)
        {
            // 大气壳在稍低高度就开始显现（边缘光先出现）
            float atmoVis = cameraAltitude < 300f ? 0f
                : cameraAltitude > 5000f ? 1f
                : MathF.Min(1f, (cameraAltitude - 300f) / 4700f);
            _atmosphereMaterial.SetShaderParameter("visibility", atmoVis);
            _atmosphereMesh.Visible = atmoVis > 0.001f;
        }
    }

    public void Shutdown()
    {
        if (_planetMesh != null)
        {
            _planetMesh.GetParent()?.RemoveChild(_planetMesh);
            _planetMesh.Free();
            _planetMesh = null;
        }
        if (_atmosphereMesh != null)
        {
            _atmosphereMesh.GetParent()?.RemoveChild(_atmosphereMesh);
            _atmosphereMesh.Free();
            _atmosphereMesh = null;
        }
        _planetMaterial = null;
        _atmosphereMaterial = null;
    }

    private static ShaderMaterial CreatePlanetMaterial()
    {
        var shader = new Shader();
        shader.Code = """
            shader_type spatial;
            render_mode cull_back, depth_draw_opaque;

            uniform float visibility : hint_range(0.0, 1.0) = 0.0;
            uniform float camera_altitude = 0.0;

            // 地表颜色
            uniform vec3 deep_ocean_color : source_color  = vec3(0.04, 0.12, 0.35);
            uniform vec3 shallow_ocean_color : source_color = vec3(0.08, 0.28, 0.55);
            uniform vec3 beach_color : source_color        = vec3(0.76, 0.70, 0.50);
            uniform vec3 lowland_color : source_color       = vec3(0.22, 0.45, 0.18);
            uniform vec3 forest_color : source_color        = vec3(0.12, 0.32, 0.10);
            uniform vec3 highland_color : source_color      = vec3(0.50, 0.42, 0.28);
            uniform vec3 mountain_color : source_color      = vec3(0.55, 0.52, 0.48);
            uniform vec3 snow_color : source_color          = vec3(0.92, 0.94, 0.96);
            uniform vec3 desert_color : source_color        = vec3(0.82, 0.72, 0.45);
            uniform vec3 ice_color : source_color           = vec3(0.85, 0.90, 0.95);

            // ── 噪声工具 ──
            float hash(vec2 p) {
                vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return fract((p3.x + p3.y) * p3.z);
            }

            float noise(vec2 p) {
                vec2 i = floor(p);
                vec2 f = fract(p);
                f = f * f * (3.0 - 2.0 * f);
                return mix(mix(hash(i), hash(i + vec2(1,0)), f.x),
                           mix(hash(i + vec2(0,1)), hash(i + vec2(1,1)), f.x), f.y);
            }

            float fbm(vec2 p, int octaves) {
                float v = 0.0, a = 0.5;
                mat2 rot = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
                for (int i = 0; i < octaves; i++) {
                    v += noise(p) * a;
                    p = rot * p * 2.0;
                    a *= 0.5;
                }
                return v;
            }

            void fragment() {
                if (visibility < 0.001) discard;

                // 球面坐标
                vec2 uv = UV;
                float lat = (uv.y - 0.5) * 2.0; // -1=南极, +1=北极
                float absLat = abs(lat);

                // ── 大陆/海洋高度场 ──
                vec2 noiseCoord = uv * 12.0;
                float continental = fbm(noiseCoord, 6);
                float detail = fbm(noiseCoord * 4.0 + 7.31, 4) * 0.3;
                float elevation = continental + detail;

                // ── 气候带 ──
                float temperature = 1.0 - absLat; // 赤道热，极地冷
                temperature += (noise(uv * 20.0) - 0.5) * 0.15; // 局部变化

                // ── 地表着色 ──
                vec3 surface;
                if (elevation < 0.38) {
                    // 深海
                    surface = deep_ocean_color;
                } else if (elevation < 0.42) {
                    // 浅海
                    float t = (elevation - 0.38) / 0.04;
                    surface = mix(deep_ocean_color, shallow_ocean_color, t);
                } else if (elevation < 0.44) {
                    // 海滩
                    float t = (elevation - 0.42) / 0.02;
                    surface = mix(shallow_ocean_color, beach_color, t);
                } else if (elevation < 0.55) {
                    // 低地 → 由气候决定草地/沙漠
                    float t = (elevation - 0.44) / 0.11;
                    vec3 warm = mix(lowland_color, desert_color, smoothstep(0.65, 0.85, 1.0 - temperature));
                    surface = mix(beach_color, warm, t);
                } else if (elevation < 0.65) {
                    // 森林/丘陵
                    float t = (elevation - 0.55) / 0.10;
                    surface = mix(lowland_color, forest_color, t);
                    surface = mix(surface, highland_color, smoothstep(0.3, 0.5, 1.0 - temperature));
                } else if (elevation < 0.75) {
                    // 高地/山脉
                    float t = (elevation - 0.65) / 0.10;
                    surface = mix(forest_color, mountain_color, t);
                } else {
                    // 雪山
                    float t = (elevation - 0.75) / 0.25;
                    surface = mix(mountain_color, snow_color, smoothstep(0.0, 0.5, t));
                }

                // ── 极地冰盖 ──
                float iceBlend = smoothstep(0.72, 0.92, absLat);
                surface = mix(surface, ice_color, iceBlend);

                // ── 云层（半透明白色覆盖）──
                float clouds = fbm(uv * 18.0 + vec2(TIME * 0.001, 0.0), 4);
                float cloudMask = smoothstep(0.45, 0.65, clouds);
                surface = mix(surface, vec3(0.95), cloudMask * 0.5);

                // ── 地图区域顶部透明（让实际地形可见）──
                // 球面顶部中心对应地图中心，逐步透明
                // 使用球面法线的 Y 分量：顶部 normal.y ≈ 1
                float topFade = smoothstep(0.97, 0.995, NORMAL.y);
                float finalAlpha = visibility * (1.0 - topFade);

                // ── 光照 ──
                float NdotL = max(dot(NORMAL, VIEW), 0.0);
                float ambient = 0.15;
                surface *= (ambient + (1.0 - ambient) * NdotL);

                ALBEDO = surface;
                ALPHA = finalAlpha;
                EMISSION = surface * 0.08 * visibility;
            }
            """;

        var mat = new ShaderMaterial
        {
            Shader = shader,
            RenderPriority = -10,
        };
        mat.SetShaderParameter("visibility", 0.0f);
        mat.SetShaderParameter("camera_altitude", 0.0f);
        return mat;
    }

    private static ShaderMaterial CreateAtmosphereMaterial()
    {
        var shader = new Shader();
        shader.Code = """
            shader_type spatial;
            render_mode unshaded, cull_front, depth_draw_never;

            uniform float visibility : hint_range(0.0, 1.0) = 0.0;
            uniform vec3 atmo_color : source_color = vec3(0.35, 0.6, 1.0);
            uniform vec3 sunset_color : source_color = vec3(1.0, 0.45, 0.2);

            void fragment() {
                if (visibility < 0.001) discard;

                // Rim 光 = 大气散射效果
                float rim = 1.0 - max(dot(NORMAL, VIEW), 0.0);
                rim = pow(rim, 4.0);

                // 日落色在边缘最强
                float sunset = pow(rim, 8.0);
                vec3 col = mix(atmo_color, sunset_color, sunset * 0.4);

                ALBEDO = col;
                ALPHA = rim * visibility * 0.7;
            }
            """;

        var mat = new ShaderMaterial
        {
            Shader = shader,
            RenderPriority = -5,
        };
        mat.SetShaderParameter("visibility", 0.0f);
        mat.SetShaderParameter("atmo_color", new Color(0.35f, 0.6f, 1.0f));
        mat.SetShaderParameter("sunset_color", new Color(1.0f, 0.45f, 0.2f));
        return mat;
    }
}
