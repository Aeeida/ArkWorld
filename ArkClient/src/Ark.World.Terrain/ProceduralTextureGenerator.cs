using Godot;

namespace Ark.World.Terrain;

/// <summary>
/// 程序化纹理生成器 — 运行时生成地形所需的噪声纹理。
/// 不依赖外部图片文件，纯代码创建。
/// </summary>
public static class ProceduralTextureGenerator
{
    /// <summary>
    /// 生成细节噪声纹理 — 偏亮灰度 [0.75, 1.0]。
    /// 与 StandardMaterial3D 的 VertexColorUseAsAlbedo 配合：
    ///   最终 Albedo = 顶点色 × 纹理色
    /// 纹理偏亮(0.75~1.0)确保乘法后群系颜色不被压暗。
    /// </summary>
    public static ImageTexture GenerateDetailNoise(int width, int height, int seed = 0)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Seed = seed,
            Frequency = 0.05f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 6,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
        };

        var noise2 = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
            Seed = seed + 17,
            Frequency = 0.08f,
            CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.EuclideanSquared,
            CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance,
        };

        var image = Image.CreateEmpty(width, height, true, Image.Format.Rgba8);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float n1 = (noise.GetNoise2D(x, y) + 1f) * 0.5f;
                float n2 = (noise2.GetNoise2D(x, y) + 1f) * 0.5f;
                float combined = n1 * 0.7f + n2 * 0.3f;

                // 映射到 [0.75, 1.0] — 非常亮的灰度
                float val = 0.75f + combined * 0.25f;

                image.SetPixel(x, y, new Color(val, val, val, 1f));
            }
        }

        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// 从噪声生成法线贴图 — 增加表面凹凸细节。
    /// </summary>
    public static ImageTexture GenerateNormalMap(int width, int height, int seed = 33)
    {
        // 先生成高度场
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Seed = seed,
            Frequency = 0.06f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
        };

        // 生成高度图
        var heightMap = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                heightMap[y * width + x] = (noise.GetNoise2D(x, y) + 1f) * 0.5f;
            }
        }

        // 从高度图导出法线（中心差分）
        var image = Image.CreateEmpty(width, height, true, Image.Format.Rgba8);
        float strength = 2.0f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int xL = (x - 1 + width) % width;
                int xR = (x + 1) % width;
                int yU = (y - 1 + height) % height;
                int yD = (y + 1) % height;

                float dhdx = heightMap[y * width + xR] - heightMap[y * width + xL];
                float dhdy = heightMap[yD * width + x] - heightMap[yU * width + x];

                // 法线贴图编码：[0,1] 范围，(0.5, 0.5, 1.0) = 平面
                float nx = -dhdx * strength * 0.5f + 0.5f;
                float ny = -dhdy * strength * 0.5f + 0.5f;

                nx = Mathf.Clamp(nx, 0f, 1f);
                ny = Mathf.Clamp(ny, 0f, 1f);

                image.SetPixel(x, y, new Color(nx, ny, 1f, 1f));
            }
        }

        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
