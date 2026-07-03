using System.Runtime.CompilerServices;

namespace Ark.World.Core;

/// <summary>
/// 2D/3D Simplex Noise 实现 — 确定性，适用于程序化地形生成。
/// 基于 Stefan Gustavson 的公开域 Simplex Noise 算法。
/// </summary>
public static class SimplexNoise
{
    // 排列表（固定，线程安全）
    private static readonly byte[] Perm = new byte[512];
    private static readonly byte[] PermMod12 = new byte[512];

    private static readonly byte[] BasePermutation =
    [
        151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,
        142,8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,
        203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
        74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,
        105,92,41,55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,
        187,208,89,18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
        52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,
        16,58,17,182,189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,
        101,155,167,43,172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,
        104,218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,
        235,249,14,239,107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,
        45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,
        215,61,156,180
    ];

    static SimplexNoise()
    {
        for (int i = 0; i < 512; i++)
        {
            Perm[i] = BasePermutation[i & 255];
            PermMod12[i] = (byte)(Perm[i] % 12);
        }
    }

    // 梯度向量
    private static readonly float[] Grad3 =
    [
        1,1,0, -1,1,0, 1,-1,0, -1,-1,0,
        1,0,1, -1,0,1, 1,0,-1, -1,0,-1,
        0,1,1, 0,-1,1, 0,1,-1, 0,-1,-1
    ];

    private const float F2 = 0.3660254037844386f;  // (sqrt(3)-1)/2
    private const float G2 = 0.21132486540518713f; // (3-sqrt(3))/6

    /// <summary>
    /// 2D Simplex Noise，返回值约 [-1, 1]。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Noise2D(float x, float y)
    {
        float s = (x + y) * F2;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);
        float t = (i + j) * G2;

        float x0 = x - (i - t);
        float y0 = y - (j - t);

        int i1, j1;
        if (x0 > y0) { i1 = 1; j1 = 0; }
        else { i1 = 0; j1 = 1; }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1f + 2f * G2;
        float y2 = y0 - 1f + 2f * G2;

        int ii = i & 255;
        int jj = j & 255;

        float n0 = Contribution2D(x0, y0, ii, jj);
        float n1 = Contribution2D(x1, y1, ii + i1, jj + j1);
        float n2 = Contribution2D(x2, y2, ii + 1, jj + 1);

        return 70f * (n0 + n1 + n2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Contribution2D(float x, float y, int gi, int gj)
    {
        float t = 0.5f - x * x - y * y;
        if (t < 0) return 0;
        t *= t;
        int idx = PermMod12[(gi & 255) + Perm[gj & 255]] * 3;
        return t * t * (Grad3[idx] * x + Grad3[idx + 1] * y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastFloor(float x)
    {
        int xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }

    /// <summary>
    /// Fractal Brownian Motion (FBM) — 多频率叠加生成自然地形。
    /// </summary>
    /// <param name="x">X 坐标。</param>
    /// <param name="y">Y 坐标。</param>
    /// <param name="octaves">叠加层数（越多细节越丰富，4~8 典型值）。</param>
    /// <param name="persistence">每层振幅衰减因子（0.5 = 每层减半）。</param>
    /// <param name="lacunarity">每层频率倍增因子（2.0 = 每层频率翻倍）。</param>
    /// <param name="frequency">基础频率。</param>
    /// <returns>FBM 值，约 [-1, 1]。</returns>
    public static float FBM(float x, float y, int octaves = 6, float persistence = 0.5f,
                            float lacunarity = 2.0f, float frequency = 1.0f)
    {
        float total = 0;
        float amplitude = 1;
        float maxAmplitude = 0;
        float freq = frequency;

        for (int i = 0; i < octaves; i++)
        {
            total += Noise2D(x * freq, y * freq) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            freq *= lacunarity;
        }

        return total / maxAmplitude; // 归一化到 [-1, 1]
    }

    /// <summary>
    /// 带种子偏移的 FBM — 用世界种子偏移坐标实现确定性随机。
    /// </summary>
    public static float SeededFBM(float x, float y, WorldSeed seed, int octaves = 6,
                                   float persistence = 0.5f, float lacunarity = 2.0f,
                                   float frequency = 0.005f)
    {
        // 用种子生成固定偏移量
        float offsetX = (seed.Value & 0xFFFF) * 0.37f;
        float offsetY = ((seed.Value >> 16) & 0xFFFF) * 0.37f;
        return FBM(x + offsetX, y + offsetY, octaves, persistence, lacunarity, frequency);
    }

    /// <summary>
    /// 脊化噪声（Ridged Noise）— 用于山脉、峡谷。
    /// </summary>
    public static float RidgedFBM(float x, float y, WorldSeed seed, int octaves = 6,
                                   float persistence = 0.5f, float lacunarity = 2.0f,
                                   float frequency = 0.005f)
    {
        float offsetX = (seed.Value & 0xFFFF) * 0.37f;
        float offsetY = ((seed.Value >> 16) & 0xFFFF) * 0.37f;

        float total = 0;
        float amplitude = 1;
        float maxAmplitude = 0;
        float freq = frequency;

        for (int i = 0; i < octaves; i++)
        {
            float n = 1f - MathF.Abs(Noise2D((x + offsetX) * freq, (y + offsetY) * freq));
            n *= n; // 更尖锐
            total += n * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            freq *= lacunarity;
        }

        return total / maxAmplitude;
    }
}
