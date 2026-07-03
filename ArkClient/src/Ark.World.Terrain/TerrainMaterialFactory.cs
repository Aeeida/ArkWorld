using Godot;

namespace Ark.World.Terrain;

/// <summary>
/// 地形材质工厂 — 生成保证可见、不透明的地形材质。
///
/// 渲染管线：
///   1. 顶点色（群系颜色）作为基础 Albedo
///   2. 程序化噪声纹理通过 Triplanar 叠加细节
///   3. 法线贴图增加表面凹凸
///   4. 双面渲染 + 禁用透明 + 禁用距离淡化 = 绝对可见
/// </summary>
public static class TerrainMaterialFactory
{
    private static StandardMaterial3D? _cachedMaterial;
    private static readonly object _lock = new();

    /// <summary>
    /// 获取共享的地形材质（所有区块共用一份）。
    /// </summary>
    public static StandardMaterial3D GetShared()
    {
        lock (_lock)
        {
            if (_cachedMaterial != null) return _cachedMaterial;
            _cachedMaterial = Create();
            return _cachedMaterial;
        }
    }

    /// <summary>
    /// 创建地形材质 — 使用最保守的设置确保可见性。
    /// </summary>
    public static StandardMaterial3D Create()
    {
        // 主纹理：偏亮灰度噪声 [0.75, 1.0]
        // 与顶点色相乘后不会过暗
        var albedoTex = ProceduralTextureGenerator.GenerateDetailNoise(512, 512);

        // 法线贴图：增加表面凹凸感
        var normalTex = ProceduralTextureGenerator.GenerateNormalMap(256, 256);

        var material = new StandardMaterial3D
        {
            // ═══ 基础色 ═══
            // VertexColorUseAsAlbedo=true → final_albedo = vertex_color * AlbedoTexture
            // 顶点色是群系颜色(0.2~0.8)，纹理是偏亮灰度(0.75~1.0)
            // 乘积保持群系色调且有纹理细节
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = albedoTex,
            AlbedoColor = Colors.White,

            // ═══ 三面投影 ═══
            // 世界空间纹理映射，消除陡坡拉伸
            // Scale=0.15 → 纹理每 ~6.7m 重复一次（合适的地面细节密度）
            Uv1Triplanar = true,
            Uv1TriplanarSharpness = 3f,
            Uv1Scale = new Vector3(0.15f, 0.15f, 0.15f),

            // ═══ 法线贴图 ═══
            NormalEnabled = true,
            NormalTexture = normalTex,
            NormalScale = 0.6f,

            // ═══ 表面参数 ═══
            Roughness = 0.9f,
            Metallic = 0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Burley,

            // ═══ 绝对不透明 ═══
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,

            // ═══ 双面渲染 — 彻底消除面朝向问题 ═══
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,

            // ═══ 禁用距离淡化 — 从任何高度都可见 ═══
            DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.Disabled,

            // ═══ 禁用 Detail 层 — 减少一层乘法暗化 ═══
            DetailEnabled = false,
        };

        GD.Print("[TerrainMaterial] Created: opaque, double-sided, no fade, triplanar");
        return material;
    }
}


