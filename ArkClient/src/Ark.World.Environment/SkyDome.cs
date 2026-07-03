using Godot;
using Ark.World.Core;

namespace Ark.World.Environment;

/// <summary>
/// 天穹罩 — 跟随摄像头的巨型倒置球体，遮挡 Godot 原生地平线。
///
/// 关键设计：
///   1. 完整球体（法线朝内），玩家始终处于球心
///   2. 下半球完全不透明使用地面色 → 物理遮挡 Godot 地平线
///   3. 上半球从地平线渐变到透明 → 让 ProceduralSky 太阳/云透出
///   4. 半径小于摄像头远裁面（随高度动态调整）
///   5. 每帧跟随摄像头位置
///   6. render_priority = -100，depth_draw_never → 总是在最远层
///
/// 为什么不用 TerrainBaseElevation：
///   float32 在 Y>10000 时精度不足，导致全场景抖动。
///
/// 性能：~3000 三角形，单一 MeshInstance3D
/// </summary>
public sealed class SkyDome
{
    private readonly Node3D _root;
    private MeshInstance3D? _domeMesh;
    private ShaderMaterial? _material;

    /// <summary>天穹半径 — 必须小于摄像头远裁面。</summary>
    private const float DomeRadius = 900f;

    private const int LatSegments = 40;
    private const int LonSegments = 48;

    public Node3D SceneRoot => _root;

    public Color SkyTopColor { get; set; } = new(0.25f, 0.45f, 0.85f);
    public Color HorizonColor { get; set; } = new(0.65f, 0.75f, 0.88f);
    public Color GroundColor { get; set; } = new(0.45f, 0.50f, 0.42f);
    public bool SpaceMode { get; set; }
    public float SpaceBlend { get; set; }
    public Vector3 SunDirection { get; set; } = new(0.4f, 0.6f, 0.3f);

    public SkyDome()
    {
        _root = new Node3D { Name = "SkyDome" };
    }

    public void Initialize()
    {
        BuildDomeMesh();
    }

    public void Update(Vector3 cameraPos, float cameraAltitude)
    {
        if (_domeMesh == null) return;

        // 天穹中心始终在摄像头位置
        _domeMesh.GlobalPosition = cameraPos;

        if (_material != null)
        {
            _material.SetShaderParameter("sky_top_color", SkyTopColor);
            _material.SetShaderParameter("horizon_color", HorizonColor);
            _material.SetShaderParameter("ground_color", GroundColor);
            _material.SetShaderParameter("space_mode", SpaceBlend);
            _material.SetShaderParameter("sun_direction", SunDirection);
        }
    }

    public void Shutdown()
    {
        if (_domeMesh != null)
        {
            _domeMesh.GetParent()?.RemoveChild(_domeMesh);
            _domeMesh.Free();
            _domeMesh = null;
        }
        _material = null;
    }

    private void BuildDomeMesh()
    {
        // 生成完整球体（法线朝内）— 包括下半球以遮挡 Godot 地平线
        int vertCount = (LatSegments + 1) * (LonSegments + 1);
        var vertices = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        int vi = 0;
        for (int lat = 0; lat <= LatSegments; lat++)
        {
            // 完整球体：从南极 (-π/2) 到北极 (+π/2)
            float t = (float)lat / LatSegments;
            float theta = Mathf.Lerp(-Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, t);

            float cosTheta = MathF.Cos(theta);
            float sinTheta = MathF.Sin(theta);

            for (int lon = 0; lon <= LonSegments; lon++)
            {
                float phi = (float)lon / LonSegments * Mathf.Tau;

                float x = cosTheta * MathF.Cos(phi) * DomeRadius;
                float y = sinTheta * DomeRadius;
                float z = cosTheta * MathF.Sin(phi) * DomeRadius;

                vertices[vi] = new Vector3(x, y, z);
                normals[vi] = -new Vector3(x, y, z).Normalized();
                // UV.y: 0 = 南极, 0.5 = 赤道/地平线, 1.0 = 天顶
                uvs[vi] = new Vector2((float)lon / LonSegments, t);
                vi++;
            }
        }

        int triCount = LatSegments * LonSegments * 2;
        var indices = new int[triCount * 3];
        int ii = 0;
        int rowLen = LonSegments + 1;
        for (int lat = 0; lat < LatSegments; lat++)
        {
            for (int lon = 0; lon < LonSegments; lon++)
            {
                int tl = lat * rowLen + lon;
                int tr = tl + 1;
                int bl = tl + rowLen;
                int br = bl + 1;

                // 内面缠绕
                indices[ii++] = tl;
                indices[ii++] = tr;
                indices[ii++] = bl;

                indices[ii++] = tr;
                indices[ii++] = br;
                indices[ii++] = bl;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        _material = CreateDomeMaterial();
        mesh.SurfaceSetMaterial(0, _material);

        _domeMesh = new MeshInstance3D
        {
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Name = "SkyDomeMesh",
            GIMode = GeometryInstance3D.GIModeEnum.Disabled,
        };
        _root.AddChild(_domeMesh);
    }

    private static ShaderMaterial CreateDomeMaterial()
    {
        var shader = new Shader();
        shader.Code = """
            shader_type spatial;
            render_mode unshaded, cull_front, depth_draw_never;

            uniform vec3 sky_top_color : source_color = vec3(0.25, 0.45, 0.85);
            uniform vec3 horizon_color : source_color = vec3(0.65, 0.75, 0.88);
            uniform vec3 ground_color  : source_color = vec3(0.45, 0.50, 0.42);
            uniform float space_mode : hint_range(0.0, 1.0) = 0.0;
            uniform vec3 sun_direction = vec3(0.4, 0.6, 0.3);

            // ── 星空噪声 ──
            float star_hash(vec2 p) {
                vec3 p3 = fract(vec3(p.xyx) * vec3(443.897, 441.423, 437.195));
                p3 += dot(p3, p3.yzx + 19.19);
                return fract((p3.x + p3.y) * p3.z);
            }

            float stars(vec3 dir) {
                // 球面方向 → 2D 网格（高密度）
                vec2 uv = vec2(atan(dir.z, dir.x) * 10.0, dir.y * 20.0);
                vec2 cell = floor(uv);
                vec2 f = fract(uv);

                float h = star_hash(cell);
                // 只有少数格子有星星（稀疏度控制）
                float threshold = 0.97;
                if (h < threshold) return 0.0;

                // 星星位置在格子内随机偏移
                vec2 starPos = vec2(star_hash(cell + 0.1), star_hash(cell + 0.2));
                float d = length(f - starPos);

                // 亮度随机 + 尺寸随机
                float brightness = (h - threshold) / (1.0 - threshold);
                float size = mix(0.06, 0.15, star_hash(cell + 0.3));
                float star = smoothstep(size, size * 0.3, d) * brightness;

                // 闪烁
                float twinkle = sin(h * 100.0 + TIME * (1.0 + h * 3.0)) * 0.3 + 0.7;
                return star * twinkle;
            }

            void fragment() {
                float t = UV.y; // 0=南极(地下), 0.5=地平线, 1.0=天顶

                // ── 球面方向重建（用于星星/太阳/月亮定位）──
                float theta = (t - 0.5) * 3.14159;
                float phi = UV.x * 6.28318;
                vec3 dir = vec3(cos(theta) * cos(phi), sin(theta), cos(theta) * sin(phi));

                vec3 col;
                float alpha;

                if (t < 0.45) {
                    col = ground_color;
                    alpha = 1.0;
                } else if (t < 0.55) {
                    float blend = (t - 0.45) / 0.10;
                    blend = blend * blend * (3.0 - 2.0 * blend);
                    col = mix(ground_color, horizon_color, blend);
                    alpha = 1.0;
                } else if (t < 0.70) {
                    float blend = (t - 0.55) / 0.15;
                    blend = blend * blend * (3.0 - 2.0 * blend);
                    col = mix(horizon_color, sky_top_color, blend);
                    alpha = mix(0.95, 0.3, blend);
                } else {
                    float blend = (t - 0.70) / 0.30;
                    col = mix(sky_top_color, sky_top_color * 0.8, blend);
                    alpha = 0.15;
                }

                // ── 太空模式：深空背景 + 星星 + 太阳 + 月亮 ──
                if (space_mode > 0.01) {
                    vec3 space_col = vec3(0.003, 0.003, 0.012);

                    // 星星（只在上半球）
                    float star_brightness = 0.0;
                    if (t > 0.5) {
                        star_brightness = stars(dir);
                        // 加入一些彩色星星
                        vec3 star_color = mix(vec3(1.0), vec3(0.8, 0.85, 1.0), star_hash(dir.xz));
                        space_col += star_color * star_brightness * 1.5;
                    }

                    // 太阳（明亮光盘 + 光晕）
                    vec3 sun_dir = normalize(sun_direction);
                    float sun_dot = max(dot(dir, sun_dir), 0.0);
                    float sun_disk = smoothstep(0.9997, 0.99995, sun_dot); // 极小光盘
                    float sun_glow = pow(max(sun_dot, 0.0), 256.0) * 0.8; // 内光晕
                    float sun_halo = pow(max(sun_dot, 0.0), 32.0) * 0.15; // 外光晕
                    space_col += vec3(1.0, 0.95, 0.85) * sun_disk * 8.0;
                    space_col += vec3(1.0, 0.90, 0.70) * sun_glow;
                    space_col += vec3(1.0, 0.85, 0.65) * sun_halo;

                    // 月亮（柔和光盘）
                    vec3 moon_dir = normalize(-sun_dir + vec3(0.2, 0.3, 0.1));
                    float moon_dot = max(dot(dir, moon_dir), 0.0);
                    float moon_disk = smoothstep(0.9990, 0.9998, moon_dot);
                    float moon_glow = pow(max(moon_dot, 0.0), 64.0) * 0.1;
                    space_col += vec3(0.75, 0.80, 0.90) * moon_disk * 2.0;
                    space_col += vec3(0.5, 0.55, 0.65) * moon_glow;

                    col = mix(col, space_col, space_mode);
                    alpha = mix(alpha, 1.0, space_mode);
                }

                ALBEDO = col;
                ALPHA = alpha;
            }
            """;

        var mat = new ShaderMaterial
        {
            Shader = shader,
            RenderPriority = -100,
        };
        mat.SetShaderParameter("sky_top_color", new Color(0.25f, 0.45f, 0.85f));
        mat.SetShaderParameter("horizon_color", new Color(0.65f, 0.75f, 0.88f));
        mat.SetShaderParameter("ground_color", new Color(0.45f, 0.50f, 0.42f));
        mat.SetShaderParameter("space_mode", 0.0f);
        mat.SetShaderParameter("sun_direction", new Vector3(0.4f, 0.6f, 0.3f));

        return mat;
    }
}
