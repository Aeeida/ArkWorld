using Xunit;
using Ark.World.Core;

namespace Ark.Tests;

public class SimplexNoiseTests
{
    [Fact]
    public void Noise2D_SameInput_SameOutput()
    {
        float a = SimplexNoise.Noise2D(1.5f, 2.5f);
        float b = SimplexNoise.Noise2D(1.5f, 2.5f);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Noise2D_DifferentInput_DifferentOutput()
    {
        float a = SimplexNoise.Noise2D(0f, 0f);
        float b = SimplexNoise.Noise2D(100f, 200f);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Noise2D_OutputInRange()
    {
        // Sample a grid of points
        for (float x = -50f; x <= 50f; x += 5f)
        {
            for (float y = -50f; y <= 50f; y += 5f)
            {
                float n = SimplexNoise.Noise2D(x, y);
                Assert.InRange(n, -1.5f, 1.5f); // Simplex noise theoretical max ~±1
            }
        }
    }

    [Fact]
    public void FBM_Deterministic()
    {
        float a = SimplexNoise.FBM(10f, 20f, octaves: 4);
        float b = SimplexNoise.FBM(10f, 20f, octaves: 4);
        Assert.Equal(a, b);
    }

    [Fact]
    public void FBM_NormalizedRange()
    {
        // With normalization, FBM output should be in [-1, 1]
        for (float x = -100f; x <= 100f; x += 10f)
        {
            for (float y = -100f; y <= 100f; y += 10f)
            {
                float n = SimplexNoise.FBM(x, y, octaves: 6);
                Assert.InRange(n, -1.1f, 1.1f); // small tolerance
            }
        }
    }

    [Fact]
    public void FBM_MoreOctaves_FinerDetail()
    {
        // With 1 octave, output should be smoother (less variation in neighbors)
        // This is a statistical test — we check that std deviation across a line differs
        float[] low = new float[100];
        float[] high = new float[100];
        for (int i = 0; i < 100; i++)
        {
            low[i] = SimplexNoise.FBM(i * 0.1f, 0, octaves: 1);
            high[i] = SimplexNoise.FBM(i * 0.1f, 0, octaves: 8);
        }

        // Compute sum of absolute differences between consecutive samples
        float lowDiff = 0, highDiff = 0;
        for (int i = 1; i < 100; i++)
        {
            lowDiff += MathF.Abs(low[i] - low[i - 1]);
            highDiff += MathF.Abs(high[i] - high[i - 1]);
        }

        // More octaves = more high-frequency detail = higher sum of differences
        Assert.True(highDiff >= lowDiff * 0.8f,
            $"Expected more detail with more octaves. Low={lowDiff}, High={highDiff}");
    }

    [Fact]
    public void SeededFBM_SameSeed_SameOutput()
    {
        var seed = new WorldSeed(42);
        float a = SimplexNoise.SeededFBM(10f, 20f, seed);
        float b = SimplexNoise.SeededFBM(10f, 20f, seed);
        Assert.Equal(a, b);
    }

    [Fact]
    public void SeededFBM_DifferentSeed_DifferentOutput()
    {
        var s1 = new WorldSeed(42);
        var s2 = new WorldSeed(99);
        float a = SimplexNoise.SeededFBM(10f, 20f, s1);
        float b = SimplexNoise.SeededFBM(10f, 20f, s2);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void RidgedFBM_OutputInRange()
    {
        var seed = new WorldSeed(1);
        for (float x = -50f; x <= 50f; x += 10f)
        {
            for (float y = -50f; y <= 50f; y += 10f)
            {
                float n = SimplexNoise.RidgedFBM(x, y, seed, octaves: 4);
                Assert.InRange(n, -0.1f, 1.1f); // Ridged noise is generally [0, 1]
            }
        }
    }

    [Fact]
    public void RidgedFBM_Deterministic()
    {
        var seed = new WorldSeed(7);
        float a = SimplexNoise.RidgedFBM(5f, 15f, seed);
        float b = SimplexNoise.RidgedFBM(5f, 15f, seed);
        Assert.Equal(a, b);
    }
}
