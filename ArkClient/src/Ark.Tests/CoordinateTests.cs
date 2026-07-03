using Xunit;
using Ark.World.Core;

namespace Ark.Tests;

public class CoordinateTests
{
    // ═══ ChunkCoord ═══

    [Theory]
    [InlineData(0f, 0f, 64f, 0, 0)]
    [InlineData(63f, 63f, 64f, 0, 0)]
    [InlineData(64f, 64f, 64f, 1, 1)]
    [InlineData(-1f, -1f, 64f, -1, -1)]
    [InlineData(128f, 0f, 64f, 2, 0)]
    [InlineData(-64f, 0f, 64f, -1, 0)]
    public void FromWorldPos_ConvertsCorrectly(float wx, float wz, float size, int ex, int ez)
    {
        var coord = ChunkCoord.FromWorldPos(wx, wz, size);
        Assert.Equal(ex, coord.X);
        Assert.Equal(ez, coord.Z);
    }

    [Fact]
    public void ManhattanDistance_SameChunk_IsZero()
    {
        var a = new ChunkCoord(3, 5);
        Assert.Equal(0, a.ManhattanDistance(a));
    }

    [Fact]
    public void ManhattanDistance_Adjacent_IsOne()
    {
        var a = new ChunkCoord(0, 0);
        var b = new ChunkCoord(1, 0);
        Assert.Equal(1, a.ManhattanDistance(b));
    }

    [Fact]
    public void ManhattanDistance_Diagonal_IsTwo()
    {
        var a = new ChunkCoord(0, 0);
        var b = new ChunkCoord(1, 1);
        Assert.Equal(2, a.ManhattanDistance(b));
    }

    [Fact]
    public void ChebyshevDistance_SameChunk_IsZero()
    {
        var a = new ChunkCoord(3, 5);
        Assert.Equal(0, a.ChebyshevDistance(a));
    }

    [Fact]
    public void ChebyshevDistance_Diagonal_IsOne()
    {
        var a = new ChunkCoord(0, 0);
        var b = new ChunkCoord(1, 1);
        Assert.Equal(1, a.ChebyshevDistance(b));
    }

    [Fact]
    public void ChebyshevDistance_FarAway()
    {
        var a = new ChunkCoord(0, 0);
        var b = new ChunkCoord(5, 3);
        Assert.Equal(5, a.ChebyshevDistance(b));
    }

    [Fact]
    public void ChebyshevDistance_Symmetric()
    {
        var a = new ChunkCoord(2, 8);
        var b = new ChunkCoord(-3, 4);
        Assert.Equal(a.ChebyshevDistance(b), b.ChebyshevDistance(a));
    }

    [Fact]
    public void ChunkCoord_Equality()
    {
        var a = new ChunkCoord(5, 10);
        var b = new ChunkCoord(5, 10);
        Assert.Equal(a, b);
    }

    [Fact]
    public void ChunkCoord_Inequality()
    {
        var a = new ChunkCoord(5, 10);
        var b = new ChunkCoord(5, 11);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ChunkCoord_ToString_ContainsCoords()
    {
        var c = new ChunkCoord(3, -7);
        string s = c.ToString();
        Assert.Contains("3", s);
        Assert.Contains("-7", s);
    }

    // ═══ BiomeId ═══

    [Fact]
    public void BiomeId_None_IsInvalid()
    {
        Assert.False(BiomeId.None.IsValid);
    }

    [Fact]
    public void BiomeId_Plains_IsValid()
    {
        Assert.True(BiomeId.Plains.IsValid);
    }

    [Fact]
    public void BiomeId_AllPredefined_AreValid()
    {
        Assert.True(BiomeId.Forest.IsValid);
        Assert.True(BiomeId.Mountain.IsValid);
        Assert.True(BiomeId.Desert.IsValid);
        Assert.True(BiomeId.Swamp.IsValid);
        Assert.True(BiomeId.Tundra.IsValid);
        Assert.True(BiomeId.Ocean.IsValid);
        Assert.True(BiomeId.Cave.IsValid);
        Assert.True(BiomeId.City.IsValid);
        Assert.True(BiomeId.Ruins.IsValid);
        Assert.True(BiomeId.SkyIsland.IsValid);
        Assert.True(BiomeId.Space.IsValid);
    }

    [Fact]
    public void BiomeId_AllPredefined_UniqueValues()
    {
        var all = new[]
        {
            BiomeId.Plains, BiomeId.Forest, BiomeId.Mountain, BiomeId.Desert,
            BiomeId.Swamp, BiomeId.Tundra, BiomeId.Ocean, BiomeId.Cave,
            BiomeId.City, BiomeId.Ruins, BiomeId.SkyIsland, BiomeId.Space
        };
        var unique = new HashSet<ushort>(all.Select(b => b.Value));
        Assert.Equal(all.Length, unique.Count);
    }

    [Fact]
    public void BiomeId_Equality()
    {
        Assert.Equal(BiomeId.Forest, new BiomeId(2));
    }
}
