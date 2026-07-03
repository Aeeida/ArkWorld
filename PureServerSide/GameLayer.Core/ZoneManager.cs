using System.Collections.Concurrent;

namespace GameLayer.Core;

/// <summary>
/// Manages spatial zones for AOI (Area of Interest) scoping.
/// Uses a 3D grid (X/Y/Z) so vertical distance (e.g. rockets, flying)
/// is also partitioned, and supports multi-zone neighbor expansion
/// for wide observer visibility.
/// </summary>
public sealed class ZoneManager
{
    private readonly ConcurrentDictionary<string, HashSet<Guid>> _zoneMembers = new();
    private readonly ConcurrentDictionary<Guid, string> _playerZones = new();
    private readonly float _zoneSize;

    public event Action<Guid, string>? OnPlayerEnteredZone;
    public event Action<Guid, string>? OnPlayerLeftZone;

    public float ZoneSize => _zoneSize;

    public ZoneManager(float zoneSize = 200f)
    {
        _zoneSize = zoneSize;
    }

    /// <summary>
    /// Computes a zone ID from a 3D world position using a spatial grid.
    /// </summary>
    public string GetZoneId(float x, float y, float z)
    {
        var zx = (int)MathF.Floor(x / _zoneSize);
        var zy = (int)MathF.Floor(y / _zoneSize);
        var zz = (int)MathF.Floor(z / _zoneSize);
        return $"zone_{zx}_{zy}_{zz}";
    }

    /// <summary>
    /// Updates a player's zone based on their current 3D position.
    /// Returns the new zone ID if changed, or null if unchanged.
    /// </summary>
    public string? UpdatePlayerZone(Guid playerId, float x, float y, float z)
    {
        var newZone = GetZoneId(x, y, z);

        if (_playerZones.TryGetValue(playerId, out var oldZone))
        {
            if (oldZone == newZone) return null;

            // Leave old zone
            if (_zoneMembers.TryGetValue(oldZone, out var oldMembers))
                lock (oldMembers) { oldMembers.Remove(playerId); }

            OnPlayerLeftZone?.Invoke(playerId, oldZone);
        }

        // Enter new zone
        _playerZones[playerId] = newZone;
        var members = _zoneMembers.GetOrAdd(newZone, _ => []);
        lock (members) { members.Add(playerId); }

        OnPlayerEnteredZone?.Invoke(playerId, newZone);
        return newZone;
    }

    /// <summary>
    /// Removes a player from their current zone entirely.
    /// </summary>
    public void RemovePlayer(Guid playerId)
    {
        if (_playerZones.TryRemove(playerId, out var oldZone))
        {
            if (_zoneMembers.TryGetValue(oldZone, out var members))
                lock (members) { members.Remove(playerId); }

            OnPlayerLeftZone?.Invoke(playerId, oldZone);
        }
    }

    /// <summary>
    /// Gets the current zone ID for a player, or null if not assigned.
    /// </summary>
    public string? GetPlayerZone(Guid playerId) =>
        _playerZones.GetValueOrDefault(playerId);

    /// <summary>
    /// Gets all player IDs in a given zone.
    /// </summary>
    public IReadOnlyList<Guid> GetPlayersInZone(string zoneId)
    {
        if (_zoneMembers.TryGetValue(zoneId, out var members))
            lock (members) { return [.. members]; }
        return [];
    }

    /// <summary>
    /// Gets the 3×3×3 = 27 neighboring zone IDs (including self) for AOI overlap.
    /// </summary>
    public IEnumerable<string> GetNeighborZones(string zoneId)
    {
        // Parse zone_X_Y_Z
        var parts = zoneId.Split('_');
        if (parts.Length != 4
            || !int.TryParse(parts[1], out var zx)
            || !int.TryParse(parts[2], out var zy)
            || !int.TryParse(parts[3], out var zz))
        {
            yield return zoneId;
            yield break;
        }

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
            yield return $"zone_{zx + dx}_{zy + dy}_{zz + dz}";
    }

    /// <summary>
    /// Returns zone IDs covering a sphere around <paramref name="center"/>
    /// with the given <paramref name="radius"/>. This allows an observer
    /// (e.g. camera or rocket) to receive entities far beyond its home zone.
    /// </summary>
    public HashSet<string> GetZonesInRadius(float cx, float cy, float cz, float radius)
    {
        var minX = (int)MathF.Floor((cx - radius) / _zoneSize);
        var maxX = (int)MathF.Floor((cx + radius) / _zoneSize);
        var minY = (int)MathF.Floor((cy - radius) / _zoneSize);
        var maxY = (int)MathF.Floor((cy + radius) / _zoneSize);
        var minZ = (int)MathF.Floor((cz - radius) / _zoneSize);
        var maxZ = (int)MathF.Floor((cz + radius) / _zoneSize);

        var result = new HashSet<string>();
        for (int ix = minX; ix <= maxX; ix++)
        for (int iy = minY; iy <= maxY; iy++)
        for (int iz = minZ; iz <= maxZ; iz++)
            result.Add($"zone_{ix}_{iy}_{iz}");

        return result;
    }
}
