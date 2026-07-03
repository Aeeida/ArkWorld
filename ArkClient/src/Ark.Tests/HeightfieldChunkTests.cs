using Xunit;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.Tests;

public class HeightfieldChunkTests
{
    private static HeightfieldChunk CreateSmallChunk(int resolution = 5)
        => new(new ChunkCoord(0, 0), resolution);

    [Fact]
    public void Constructor_InitializesWithZeroHeights()
    {
        var chunk = CreateSmallChunk();
        for (int i = 0; i < chunk.Heights.Length; i++)
            Assert.Equal(0f, chunk.Heights[i]);
    }

    [Fact]
    public void Constructor_InitializesWithCorrectResolution()
    {
        var chunk = CreateSmallChunk(17);
        Assert.Equal(17, chunk.Resolution);
        Assert.Equal(17 * 17, chunk.Heights.Length);
        Assert.Equal(17 * 17, chunk.Biomes.Length);
    }

    [Fact]
    public void GetHeight_WithinBounds_ReturnsValue()
    {
        var chunk = CreateSmallChunk();
        chunk.Heights[0] = 10f;
        Assert.Equal(10f, chunk.GetHeight(0, 0));
    }

    [Fact]
    public void GetHeight_OutOfBounds_ReturnsZero()
    {
        var chunk = CreateSmallChunk();
        Assert.Equal(0f, chunk.GetHeight(-1, 0));
        Assert.Equal(0f, chunk.GetHeight(100, 0));
    }

    [Fact]
    public void SetHeight_UpdatesValue()
    {
        var chunk = CreateSmallChunk();
        chunk.SetHeight(2, 3, 42f);
        Assert.Equal(42f, chunk.GetHeight(2, 3));
    }

    [Fact]
    public void SetHeight_SetsDirtyFlag()
    {
        var chunk = CreateSmallChunk();
        Assert.False(chunk.IsDirty);
        chunk.SetHeight(0, 0, 1f);
        Assert.True(chunk.IsDirty);
    }

    [Fact]
    public void SetHeight_OutOfBounds_NoEffect()
    {
        var chunk = CreateSmallChunk();
        chunk.SetHeight(-1, 0, 99f);
        Assert.False(chunk.IsDirty);
    }

    [Fact]
    public void SampleHeight_AtGridPoint_ReturnsExactHeight()
    {
        var chunk = CreateSmallChunk(5);
        float chunkSize = 4f; // 4m, 5 verts → step = 1m
        // Set corner (0,0) to 10
        chunk.SetHeight(0, 0, 10f);
        float h = chunk.SampleHeight(0f, 0f, chunkSize);
        Assert.Equal(10f, h, 0.01f);
    }

    [Fact]
    public void SampleHeight_MidPoint_InterpolatesLinearly()
    {
        var chunk = CreateSmallChunk(3);
        float chunkSize = 2f; // 2m, 3 verts → step = 1m

        // Set up: (0,0)=0, (1,0)=10, (0,1)=0, (1,1)=10
        chunk.SetHeight(0, 0, 0f);
        chunk.SetHeight(1, 0, 10f);
        chunk.SetHeight(0, 1, 0f);
        chunk.SetHeight(1, 1, 10f);

        // Midpoint in X at Z=0: should be 5
        float h = chunk.SampleHeight(0.5f, 0f, chunkSize);
        Assert.Equal(5f, h, 0.5f);
    }

    [Fact]
    public void SampleHeight_BilinearInterpolation()
    {
        var chunk = CreateSmallChunk(3);
        float chunkSize = 2f;

        // (0,0)=0, (1,0)=10, (0,1)=20, (1,1)=30
        chunk.SetHeight(0, 0, 0f);
        chunk.SetHeight(1, 0, 10f);
        chunk.SetHeight(0, 1, 20f);
        chunk.SetHeight(1, 1, 30f);

        // Center should be average-ish: (0+10+20+30)/4 = 15
        float h = chunk.SampleHeight(0.5f, 0.5f, chunkSize);
        Assert.InRange(h, 14f, 16f);
    }

    [Fact]
    public void SampleHeight_AtEdge_ClampsSafely()
    {
        var chunk = CreateSmallChunk(5);
        float chunkSize = 4f;
        chunk.SetHeight(4, 4, 100f);

        // Sample at the far edge
        float h = chunk.SampleHeight(chunkSize, chunkSize, chunkSize);
        // Should not crash, and should give reasonable value near the edge
        Assert.True(float.IsFinite(h));
    }

    [Fact]
    public void Coord_ReturnsConstructorValue()
    {
        var coord = new ChunkCoord(3, -5);
        var chunk = new HeightfieldChunk(coord, 5);
        Assert.Equal(coord, chunk.Coord);
    }

    [Fact]
    public void LodLevel_DefaultsToZero()
    {
        var chunk = CreateSmallChunk();
        Assert.Equal(0, chunk.LodLevel);
    }
}
