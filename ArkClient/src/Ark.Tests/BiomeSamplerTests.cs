using Xunit;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.Tests;

public class BiomeSamplerTests
{
    [Fact]
    public void Sample_SameCoords_SameResult()
    {
        var sampler = new BiomeSampler(new WorldSeed(42));
        var a = sampler.Sample(100f, 200f);
        var b = sampler.Sample(100f, 200f);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Sample_ReturnsValidBiome()
    {
        var sampler = new BiomeSampler(new WorldSeed(42));
        // Sample many points
        for (float x = -500; x <= 500; x += 100)
        {
            for (float z = -500; z <= 500; z += 100)
            {
                var biome = sampler.Sample(x, z);
                Assert.True(biome.IsValid, $"Invalid biome at ({x},{z}): {biome}");
            }
        }
    }

    [Fact]
    public void Sample_DifferentSeeds_ProduceDifferentDistributions()
    {
        var s1 = new BiomeSampler(new WorldSeed(1));
        var s2 = new BiomeSampler(new WorldSeed(999999));

        // Sample a grid and count differences
        int diffCount = 0;
        for (float x = 0; x < 1000; x += 50)
        {
            for (float z = 0; z < 1000; z += 50)
            {
                if (s1.Sample(x, z) != s2.Sample(x, z))
                    diffCount++;
            }
        }
        Assert.True(diffCount > 0, "Different seeds should produce at least some different biomes");
    }

    [Fact]
    public void SampleBlended_ReturnsValidPrimaryBiome()
    {
        var sampler = new BiomeSampler(new WorldSeed(42));
        var (primary, _, _) = sampler.SampleBlended(100f, 200f);
        Assert.True(primary.IsValid);
    }

    [Fact]
    public void SampleBlended_WeightInRange()
    {
        var sampler = new BiomeSampler(new WorldSeed(42));
        for (float x = -200; x <= 200; x += 50)
        {
            for (float z = -200; z <= 200; z += 50)
            {
                var (_, _, weight) = sampler.SampleBlended(x, z);
                Assert.InRange(weight, 0f, 1f);
            }
        }
    }

    [Fact]
    public void SampleBlended_PrimaryEqualsSample()
    {
        var sampler = new BiomeSampler(new WorldSeed(42));
        var direct = sampler.Sample(300f, 400f);
        var (primary, _, _) = sampler.SampleBlended(300f, 400f);
        Assert.Equal(direct, primary);
    }

    [Fact]
    public void SampleBlended_SameAsPrimary_WeightIsOne()
    {
        // Deep inside a biome region, primary == secondary → weight == 1
        var sampler = new BiomeSampler(new WorldSeed(42));
        // Try many points, at least some should have weight = 1 (well inside a biome)
        int fullWeightCount = 0;
        for (float x = 0; x < 2000; x += 50)
        {
            for (float z = 0; z < 2000; z += 50)
            {
                var (primary, secondary, weight) = sampler.SampleBlended(x, z);
                if (primary == secondary)
                {
                    Assert.Equal(1f, weight);
                    fullWeightCount++;
                }
            }
        }
        Assert.True(fullWeightCount > 0, "Should have some points deep inside a biome");
    }

    [Fact]
    public void Sample_ProducesMultipleBiomeTypes()
    {
        var sampler = new BiomeSampler(new WorldSeed(42));
        var biomes = new HashSet<BiomeId>();

        // Large grid should cover multiple biome types
        for (float x = -2000; x <= 2000; x += 100)
        {
            for (float z = -2000; z <= 2000; z += 100)
            {
                biomes.Add(sampler.Sample(x, z));
            }
        }
        Assert.True(biomes.Count >= 3, $"Expected at least 3 biome types, got {biomes.Count}: {string.Join(", ", biomes)}");
    }
}
