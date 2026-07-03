using MessagePack;

namespace Game.Shared.Models;

[MessagePackObject]
public sealed record Vector3D([property: Key(0)] double X, [property: Key(1)] double Y, [property: Key(2)] double Z)
{
    public static readonly Vector3D Zero = new(0, 0, 0);

    public double DistanceTo(Vector3D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public Vector3D Normalize()
    {
        var len = Math.Sqrt(X * X + Y * Y + Z * Z);
        return len == 0 ? Zero : new(X / len, Y / len, Z / len);
    }

    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3D operator *(Vector3D v, double s) => new(v.X * s, v.Y * s, v.Z * s);
}

[MessagePackObject]
public sealed record WorldPosition(
    [property: Key(0)] string WorldId,
    [property: Key(1)] Vector3D Position,
    [property: Key(2)] float Rotation = 0f);

[MessagePackObject]
public sealed record SolarSystemInfo(
    [property: Key(0)] string SystemId,
    [property: Key(1)] string Name,
    [property: Key(2)] double SecurityLevel,
    [property: Key(3)] string? SovereignAllianceId,
    [property: Key(4)] int OnlinePlayerCount);

[MessagePackObject]
public sealed record InventorySlot(
    [property: Key(0)] int SlotIndex,
    [property: Key(1)] string ItemId,
    [property: Key(2)] int Quantity,
    [property: Key(3)] string Rarity);
