using Xunit;
using Ark.World.Core;

namespace Ark.Tests;

public class WorldSeedTests
{
    [Fact]
    public void Constructor_StoresValue()
    {
        var seed = new WorldSeed(42);
        Assert.Equal(42L, seed.Value);
    }

    [Fact]
    public void Derive_SameDomain_ReturnsSameResult()
    {
        var seed = new WorldSeed(12345);
        var a = seed.Derive("height");
        var b = seed.Derive("height");
        Assert.Equal(a.Value, b.Value);
    }

    [Fact]
    public void Derive_DifferentDomain_ReturnsDifferentResult()
    {
        var seed = new WorldSeed(12345);
        var a = seed.Derive("height");
        var b = seed.Derive("biome");
        Assert.NotEqual(a.Value, b.Value);
    }

    [Fact]
    public void Derive_DifferentSeed_DifferentResult()
    {
        var a = new WorldSeed(1).Derive("terrain");
        var b = new WorldSeed(2).Derive("terrain");
        Assert.NotEqual(a.Value, b.Value);
    }

    [Fact]
    public void DeriveForChunk_Deterministic()
    {
        var seed = new WorldSeed(999);
        var coord = new ChunkCoord(3, 7);
        var a = seed.DeriveForChunk(coord);
        var b = seed.DeriveForChunk(coord);
        Assert.Equal(a.Value, b.Value);
    }

    [Fact]
    public void DeriveForChunk_DifferentCoords_DifferentSeeds()
    {
        var seed = new WorldSeed(999);
        var a = seed.DeriveForChunk(new ChunkCoord(0, 0));
        var b = seed.DeriveForChunk(new ChunkCoord(1, 0));
        var c = seed.DeriveForChunk(new ChunkCoord(0, 1));
        Assert.NotEqual(a.Value, b.Value);
        Assert.NotEqual(a.Value, c.Value);
    }

    [Fact]
    public void ToFloat01_InRange()
    {
        // Test many seeds
        for (long v = -100; v <= 100; v++)
        {
            float f = new WorldSeed(v).ToFloat01();
            Assert.InRange(f, 0f, 1f);
        }
    }

    [Fact]
    public void FromString_Deterministic()
    {
        var a = WorldSeed.FromString("hello world");
        var b = WorldSeed.FromString("hello world");
        Assert.Equal(a.Value, b.Value);
    }

    [Fact]
    public void FromString_DifferentStrings_DifferentSeeds()
    {
        var a = WorldSeed.FromString("alpha");
        var b = WorldSeed.FromString("beta");
        Assert.NotEqual(a.Value, b.Value);
    }

    [Fact]
    public void ImplicitConversion_ToLong()
    {
        var seed = new WorldSeed(777);
        long val = seed;
        Assert.Equal(777L, val);
    }

    [Fact]
    public void ToString_ContainsValue()
    {
        var seed = new WorldSeed(42);
        Assert.Contains("42", seed.ToString());
    }
}
