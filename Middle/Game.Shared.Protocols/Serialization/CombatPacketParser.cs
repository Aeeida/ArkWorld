using System.Buffers;

namespace Game.Shared.Protocols.Serialization;

public static class CombatPacketParser
{
    public const byte DamagePacketId = 0x01;
    public const byte HealPacketId = 0x02;
    public const byte BuffPacketId = 0x03;
    public const byte MovePacketId = 0x04;
    /// <summary>Observer/camera position update — drives multi-zone AOI on the server.</summary>
    public const byte ObserverPositionPacketId = 0x05;
    /// <summary>
    /// WatchPoint management — add/remove/clear arbitrary remote observation points.
    /// Enables pre-loading data at teleport destinations, remote character control, etc.
    /// </summary>
    public const byte WatchPointPacketId = 0x06;

    // ── Damage ──────────────────────────────────────────────────────

    public static bool TryParseDamagePacket(ReadOnlySpan<byte> data, out Guid attackerId, out Guid targetId, out double damage)
    {
        attackerId = default;
        targetId = default;
        damage = 0;

        if (data.Length < 1 + 16 + 16 + 8) return false;
        if (data[0] != DamagePacketId) return false;

        attackerId = new Guid(data.Slice(1, 16));
        targetId = new Guid(data.Slice(17, 16));
        damage = BitConverter.ToDouble(data.Slice(33, 8));
        return true;
    }

    public static int WriteDamagePacket(Span<byte> buffer, Guid attackerId, Guid targetId, double damage)
    {
        const int size = 1 + 16 + 16 + 8;
        if (buffer.Length < size) return 0;

        buffer[0] = DamagePacketId;
        attackerId.TryWriteBytes(buffer.Slice(1, 16));
        targetId.TryWriteBytes(buffer.Slice(17, 16));
        BitConverter.TryWriteBytes(buffer.Slice(33, 8), damage);
        return size;
    }

    // ── Move ────────────────────────────────────────────────────────

    public static bool TryParseMovePacket(ReadOnlySpan<byte> data, out Guid entityId, out double x, out double y, out double z, out float rotation)
    {
        entityId = default;
        x = y = z = 0;
        rotation = 0;

        if (data.Length < 1 + 16 + 24 + 4) return false;
        if (data[0] != MovePacketId) return false;

        entityId = new Guid(data.Slice(1, 16));
        x = BitConverter.ToDouble(data.Slice(17, 8));
        y = BitConverter.ToDouble(data.Slice(25, 8));
        z = BitConverter.ToDouble(data.Slice(33, 8));
        rotation = BitConverter.ToSingle(data.Slice(41, 4));
        return true;
    }

    public static int WriteMovePacket(Span<byte> buffer, Guid entityId, double x, double y, double z, float rotation)
    {
        const int size = 1 + 16 + 24 + 4;
        if (buffer.Length < size) return 0;

        buffer[0] = MovePacketId;
        entityId.TryWriteBytes(buffer.Slice(1, 16));
        BitConverter.TryWriteBytes(buffer.Slice(17, 8), x);
        BitConverter.TryWriteBytes(buffer.Slice(25, 8), y);
        BitConverter.TryWriteBytes(buffer.Slice(33, 8), z);
        BitConverter.TryWriteBytes(buffer.Slice(41, 4), rotation);
        return size;
    }

    // ── Observer Position ───────────────────────────────────────────
    // Format: [1:packetId][16:playerId][8:x][8:y][8:z][4:viewRadius]

    public static bool TryParseObserverPositionPacket(
        ReadOnlySpan<byte> data,
        out Guid playerId,
        out double x, out double y, out double z,
        out float viewRadius)
    {
        playerId = default;
        x = y = z = 0;
        viewRadius = 0;

        const int size = 1 + 16 + 24 + 4;
        if (data.Length < size) return false;
        if (data[0] != ObserverPositionPacketId) return false;

        playerId = new Guid(data.Slice(1, 16));
        x = BitConverter.ToDouble(data.Slice(17, 8));
        y = BitConverter.ToDouble(data.Slice(25, 8));
        z = BitConverter.ToDouble(data.Slice(33, 8));
        viewRadius = BitConverter.ToSingle(data.Slice(41, 4));
        return true;
    }

    public static int WriteObserverPositionPacket(
        Span<byte> buffer,
        Guid playerId,
        double x, double y, double z,
        float viewRadius)
    {
        const int size = 1 + 16 + 24 + 4;
        if (buffer.Length < size) return 0;

        buffer[0] = ObserverPositionPacketId;
        playerId.TryWriteBytes(buffer.Slice(1, 16));
        BitConverter.TryWriteBytes(buffer.Slice(17, 8), x);
        BitConverter.TryWriteBytes(buffer.Slice(25, 8), y);
        BitConverter.TryWriteBytes(buffer.Slice(33, 8), z);
        BitConverter.TryWriteBytes(buffer.Slice(41, 4), viewRadius);
        return size;
    }

    // ── WatchPoint ──────────────────────────────────────────────────
    // Multiple arbitrary observation points without character/camera anchors.
    // Action: 1=Add/Update, 2=Remove, 3=ClearAll
    // Add format:    [1:packetId][16:watchPointId][1:action=1][8:x][8:y][8:z][4:radius]
    // Remove format: [1:packetId][16:watchPointId][1:action=2]
    // Clear format:  [1:packetId][16:zeros][1:action=3]

    public const byte WatchPointActionAdd = 1;
    public const byte WatchPointActionRemove = 2;
    public const byte WatchPointActionClear = 3;

    public static bool TryParseWatchPointPacket(
        ReadOnlySpan<byte> data,
        out Guid watchPointId,
        out byte action,
        out double x, out double y, out double z,
        out float radius)
    {
        watchPointId = default;
        action = 0;
        x = y = z = 0;
        radius = 0;

        // Minimum: packetId(1) + watchPointId(16) + action(1) = 18
        if (data.Length < 1 + 16 + 1) return false;
        if (data[0] != WatchPointPacketId) return false;

        watchPointId = new Guid(data.Slice(1, 16));
        action = data[17];

        if (action == WatchPointActionAdd)
        {
            // Need additional 24 (xyz) + 4 (radius) = 28 bytes
            if (data.Length < 1 + 16 + 1 + 24 + 4) return false;
            x = BitConverter.ToDouble(data.Slice(18, 8));
            y = BitConverter.ToDouble(data.Slice(26, 8));
            z = BitConverter.ToDouble(data.Slice(34, 8));
            radius = BitConverter.ToSingle(data.Slice(42, 4));
        }

        return true;
    }

    public static int WriteWatchPointAddPacket(
        Span<byte> buffer,
        Guid watchPointId,
        double x, double y, double z,
        float radius)
    {
        const int size = 1 + 16 + 1 + 24 + 4; // 46
        if (buffer.Length < size) return 0;

        buffer[0] = WatchPointPacketId;
        watchPointId.TryWriteBytes(buffer.Slice(1, 16));
        buffer[17] = WatchPointActionAdd;
        BitConverter.TryWriteBytes(buffer.Slice(18, 8), x);
        BitConverter.TryWriteBytes(buffer.Slice(26, 8), y);
        BitConverter.TryWriteBytes(buffer.Slice(34, 8), z);
        BitConverter.TryWriteBytes(buffer.Slice(42, 4), radius);
        return size;
    }

    public static int WriteWatchPointRemovePacket(Span<byte> buffer, Guid watchPointId)
    {
        const int size = 1 + 16 + 1; // 18
        if (buffer.Length < size) return 0;

        buffer[0] = WatchPointPacketId;
        watchPointId.TryWriteBytes(buffer.Slice(1, 16));
        buffer[17] = WatchPointActionRemove;
        return size;
    }

    public static int WriteWatchPointClearPacket(Span<byte> buffer)
    {
        const int size = 1 + 16 + 1; // 18
        if (buffer.Length < size) return 0;

        buffer[0] = WatchPointPacketId;
        buffer.Slice(1, 16).Clear(); // zero guid
        buffer[17] = WatchPointActionClear;
        return size;
    }
}
