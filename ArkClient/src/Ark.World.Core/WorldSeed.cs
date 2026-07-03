namespace Ark.World.Core;

/// <summary>
/// 世界种子 — 64 位整数驱动整个宇宙/星球/区域的程序化生成。
///
/// 分层种子系统：从主种子派生出高度场种子、生态种子、洞穴种子等，
/// 各层可独立重载。支持区域覆盖（Region Override）。
/// </summary>
public readonly struct WorldSeed
{
    /// <summary>主种子。</summary>
    public long Value { get; }

    public WorldSeed(long value) => Value = value;

    /// <summary>派生子种子（确定性）— 用于不同层/系统。</summary>
    public WorldSeed Derive(string domain)
    {
        long hash = Value;
        foreach (char c in domain)
        {
            hash = hash * 31 + c;
            hash ^= hash >> 16;
        }
        return new WorldSeed(hash);
    }

    /// <summary>派生区域子种子。</summary>
    public WorldSeed DeriveForChunk(ChunkCoord coord)
    {
        long hash = Value;
        hash ^= (long)coord.X * 73856093L;
        hash ^= (long)coord.Z * 19349663L;
        hash ^= hash >> 17;
        hash *= unchecked((long)0xbf58476d1ce4e5b9UL);
        hash ^= hash >> 31;
        return new WorldSeed(hash);
    }

    /// <summary>从种子获取 [0,1) 范围的浮点数。</summary>
    public float ToFloat01()
    {
        uint bits = (uint)(Value ^ (Value >> 32));
        return (bits & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    public static WorldSeed FromString(string text)
    {
        long hash = 0;
        foreach (char c in text)
            hash = hash * 31 + c;
        return new WorldSeed(hash);
    }

    public static implicit operator long(WorldSeed s) => s.Value;
    public override string ToString() => $"Seed({Value})";
}
