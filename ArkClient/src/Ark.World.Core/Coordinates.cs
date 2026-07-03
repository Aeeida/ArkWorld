using System.Runtime.CompilerServices;

namespace Ark.World.Core;

/// <summary>
/// 区块坐标 — 世界按固定大小区块划分，此结构标识一个区块。
/// </summary>
public readonly record struct ChunkCoord(int X, int Z)
{
    /// <summary>从世界坐标转换为区块坐标。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChunkCoord FromWorldPos(float worldX, float worldZ, float chunkSize)
    {
        return new ChunkCoord(
            (int)MathF.Floor(worldX / chunkSize),
            (int)MathF.Floor(worldZ / chunkSize));
    }

    /// <summary>区块左下角的世界坐标。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Godot.Vector3 ToWorldOrigin(float chunkSize)
        => new(X * chunkSize, 0, Z * chunkSize);

    /// <summary>区块中心的世界坐标。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Godot.Vector3 ToWorldCenter(float chunkSize)
        => new(X * chunkSize + chunkSize * 0.5f, 0, Z * chunkSize + chunkSize * 0.5f);

    /// <summary>曼哈顿距离。</summary>
    public int ManhattanDistance(ChunkCoord other)
        => Math.Abs(X - other.X) + Math.Abs(Z - other.Z);

    /// <summary>切比雪夫距离。</summary>
    public int ChebyshevDistance(ChunkCoord other)
        => Math.Max(Math.Abs(X - other.X), Math.Abs(Z - other.Z));

    public override string ToString() => $"Chunk({X},{Z})";
}

/// <summary>
/// 生物群系标识 — 每个区域对应一个群系，决定地形风格、植被、天气。
/// </summary>
public readonly record struct BiomeId(ushort Value)
{
    // 预定义群系 ID
    public static readonly BiomeId Plains     = new(1);
    public static readonly BiomeId Forest     = new(2);
    public static readonly BiomeId Mountain   = new(3);
    public static readonly BiomeId Desert     = new(4);
    public static readonly BiomeId Swamp      = new(5);
    public static readonly BiomeId Tundra     = new(6);
    public static readonly BiomeId Ocean      = new(7);
    public static readonly BiomeId Cave       = new(8);
    public static readonly BiomeId City       = new(9);
    public static readonly BiomeId Ruins      = new(10);
    public static readonly BiomeId SkyIsland  = new(11);
    public static readonly BiomeId Space      = new(12);

    public static readonly BiomeId None = new(0);

    public bool IsValid => Value > 0;
    public override string ToString() => $"Biome({Value})";
}
