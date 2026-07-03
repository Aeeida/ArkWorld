namespace GameServer.Networking.Core;

public interface IConnectionManager
{
    Task SendAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct = default);
    Task BroadcastAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    Task BroadcastToZoneAsync(string zoneId, ReadOnlyMemory<byte> data, CancellationToken ct = default);
    Task DisconnectAsync(string connectionId, CancellationToken ct = default);
    IReadOnlyCollection<string> GetConnectedIds();
}

/// <summary>
/// 快照广播接口 — 将二进制实体快照通过 SignalR 发送给没有 TCP 连接的客户端。
/// WorldTickService 同时通过 TCP 和此接口广播，确保所有客户端都能接收实体数据。
/// </summary>
public interface ISnapshotBroadcaster
{
    /// <summary>向指定 SignalR 连接 ID 发送二进制快照帧。</summary>
    Task SendSnapshotAsync(string connectionId, byte[] snapshotFrame, CancellationToken ct = default);

    /// <summary>向多个 SignalR 连接 ID 批量发送快照帧。</summary>
    Task BroadcastSnapshotAsync(IEnumerable<string> connectionIds, byte[] snapshotFrame, CancellationToken ct = default);
}

public interface IRealtimeMessageRouter
{
    Task HandleFallbackMessageAsync(GameSession session, ReadOnlyMemory<byte> data);
}

public interface ISessionManager
{
    Task<GameSession?> GetSessionAsync(string connectionId);
    Task<GameSession?> GetSessionByPlayerAsync(Guid playerId);
    Task<GameSession> CreateSessionAsync(string connectionId, Guid playerId);
    Task RemoveSessionAsync(string connectionId);
    Task BindTcpAsync(Guid playerId, string tcpConnectionId);
    Task UnbindTcpAsync(Guid playerId, string? expectedTcpConnectionId = null);
    IReadOnlyCollection<GameSession> GetAllSessions();
}

/// <summary>
/// Represents a player's unified session across both SignalR and TCP transports.
/// </summary>
public sealed class GameSession
{
    public required string ConnectionId { get; init; }
    public required Guid PlayerId { get; init; }

    /// <summary>TCP connection ID for high-frequency binary traffic (null if not yet bound).</summary>
    public string? TcpConnectionId { get; set; }

    /// <summary>Current spatial zone for AOI-scoped broadcasts (character's home zone).</summary>
    public string? CurrentZoneId { get; set; }

    /// <summary>Current world/surface identifier for world-scoped snapshots.</summary>
    public string? CurrentWorldId { get; set; }

    /// <summary>
    /// Observer-based AOI zone set — computed from camera/observer position and radius.
    /// When set, snapshot broadcast covers these zones instead of only CurrentZoneId.
    /// This enables scenarios like tracking an unmanned rocket across multiple zones.
    /// </summary>
    public HashSet<string>? ObserverZoneIds { get; set; }

    /// <summary>Observer view radius reported by the client (default: one zone size).</summary>
    public float ObserverRadius { get; set; }

    /// <summary>
    /// Multiple remote observation points — independent of character or camera.
    /// Each watch point covers a sphere of zones at an arbitrary world position.
    /// Use cases: teleport destination pre-loading, remote character control,
    /// rocket trajectory monitoring, multi-base surveillance, etc.
    /// </summary>
    public Dictionary<Guid, WatchPointInfo> WatchPoints { get; } = [];

    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single remote observation point — covers a sphere of zones around an
/// arbitrary world position without requiring a character or camera anchor.
/// </summary>
public sealed class WatchPointInfo
{
    public required Guid Id { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Z { get; init; }
    public required float Radius { get; init; }

    /// <summary>Pre-computed zone IDs covered by this watch point.</summary>
    public required HashSet<string> ZoneIds { get; init; }
}

public sealed class InMemorySessionManager : ISessionManager
{
    private readonly Dictionary<string, GameSession> _sessions = [];
    private readonly Dictionary<Guid, GameSession> _playerIndex = [];
    private readonly Dictionary<string, GameSession> _tcpIndex = [];
    private readonly Lock _lock = new();

    public Task<GameSession?> GetSessionAsync(string connectionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(connectionId, out var session))
                return Task.FromResult<GameSession?>(session);

            _tcpIndex.TryGetValue(connectionId, out session);
            return Task.FromResult(session);
        }
    }

    public Task<GameSession?> GetSessionByPlayerAsync(Guid playerId)
    {
        lock (_lock)
        {
            _playerIndex.TryGetValue(playerId, out var session);
            return Task.FromResult(session);
        }
    }

    public Task<GameSession> CreateSessionAsync(string connectionId, Guid playerId)
    {
        var session = new GameSession { ConnectionId = connectionId, PlayerId = playerId };
        lock (_lock)
        {
            _sessions[connectionId] = session;
            _playerIndex[playerId] = session;
        }
        return Task.FromResult(session);
    }

    public Task RemoveSessionAsync(string connectionId)
    {
        lock (_lock)
        {
            if (_sessions.Remove(connectionId, out var session))
            {
                _playerIndex.Remove(session.PlayerId);
                if (session.TcpConnectionId is not null)
                    _tcpIndex.Remove(session.TcpConnectionId);
            }
        }
        return Task.CompletedTask;
    }

    public Task BindTcpAsync(Guid playerId, string tcpConnectionId)
    {
        lock (_lock)
        {
            if (_playerIndex.TryGetValue(playerId, out var session))
            {
                if (session.TcpConnectionId is not null)
                    _tcpIndex.Remove(session.TcpConnectionId);

                session.TcpConnectionId = tcpConnectionId;
                _tcpIndex[tcpConnectionId] = session;
            }
        }
        return Task.CompletedTask;
    }

    public Task UnbindTcpAsync(Guid playerId, string? expectedTcpConnectionId = null)
    {
        lock (_lock)
        {
            if (_playerIndex.TryGetValue(playerId, out var session) && session.TcpConnectionId is not null)
            {
                if (expectedTcpConnectionId is not null && session.TcpConnectionId != expectedTcpConnectionId)
                {
                    _tcpIndex.Remove(expectedTcpConnectionId);
                    return Task.CompletedTask;
                }

                _tcpIndex.Remove(session.TcpConnectionId);
                session.TcpConnectionId = null;
            }
            else if (expectedTcpConnectionId is not null)
            {
                _tcpIndex.Remove(expectedTcpConnectionId);
            }
        }
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<GameSession> GetAllSessions()
    {
        lock (_lock) { return _sessions.Values.ToList().AsReadOnly(); }
    }
}
