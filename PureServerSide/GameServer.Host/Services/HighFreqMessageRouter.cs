using System.Numerics;
using Game.Shared.Protocols.Serialization;
using GameLayer.Core;
using GameServer.Grains.Interfaces;
using GameServer.Networking.Core;
using GameServer.Networking.Transport;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Host.Services;

/// <summary>
/// Routes decoded TCP binary frames to the appropriate game logic.
/// Integrates with <see cref="ServerWorldState"/> for position tracking
/// and zone management, and with Orleans grains for persistent state.
/// </summary>
public sealed class HighFreqMessageRouter : IRealtimeMessageRouter
{
    private const byte AuthHandshakeId = 0xFF;
    private const float MaxPlayerMoveSpeed = 10f;
    private const float MoveInputDeltaTime = 0.1f;

    private readonly ISessionManager _sessions;
    private readonly IGrainFactory _grainFactory;
    private readonly TcpTransportServer _tcp;
    private readonly ServerWorldState _world;
    private readonly ILogger<HighFreqMessageRouter> _logger;
    private int _moveReceiveCounter;

    public HighFreqMessageRouter(
        ISessionManager sessions,
        IGrainFactory grainFactory,
        TcpTransportServer tcp,
        ServerWorldState world,
        ILogger<HighFreqMessageRouter> logger)
    {
        _sessions = sessions;
        _grainFactory = grainFactory;
        _tcp = tcp;
        _world = world;
        _logger = logger;

        _tcp.OnMessageReceived += HandleMessageAsync;
        _tcp.OnClientDisconnected += HandleDisconnectAsync;
    }

    private async Task HandleMessageAsync(string tcpConnectionId, ReadOnlyMemory<byte> data)
    {
        if (data.Length < 1) return;

        var session = await _sessions.GetSessionAsync(tcpConnectionId);
        if (session is null)
        {
            // First message on TCP must be an auth handshake: [0xFF][16-byte PlayerId]
            if (data.Span[0] == AuthHandshakeId && data.Length >= 17)
            {
                var playerId = new Guid(data.Span.Slice(1, 16));
                await _sessions.BindTcpAsync(playerId, tcpConnectionId);

                // Bind TCP connection to the player's current zone
                var entity = _world.Entities.GetByNetworkId(playerId);
                if (entity?.ZoneId is not null)
                {
                    session = await _sessions.GetSessionAsync(tcpConnectionId);
                    if (session is not null)
                        session.CurrentZoneId = entity.ZoneId;
                    _tcp.JoinZone(tcpConnectionId, entity.ZoneId);
                }

                _logger.LogInformation("TCP bound to player {PlayerId}", playerId);
            }
            return;
        }

        session.LastActivityAt = DateTime.UtcNow;
        var packetId = data.Span[0];

        await HandleSessionMessageAsync(session, data, "TCP");
    }

    public async Task HandleFallbackMessageAsync(GameSession session, ReadOnlyMemory<byte> data)
    {
        session.LastActivityAt = DateTime.UtcNow;
        await HandleSessionMessageAsync(session, data, "SignalR");
    }

    private async Task HandleSessionMessageAsync(GameSession session, ReadOnlyMemory<byte> data, string transport)
    {
        var packetId = data.Span[0];

        switch (packetId)
        {
            case CombatPacketParser.MovePacketId:
                await HandleMoveAsync(session, data, transport);
                break;

            case CombatPacketParser.DamagePacketId:
                await HandleDamageAsync(session, data);
                break;

            case CombatPacketParser.ObserverPositionPacketId:
                HandleObserverPosition(session, data);
                break;

            case CombatPacketParser.WatchPointPacketId:
                HandleWatchPoint(session, data);
                break;
        }
    }

    private async Task HandleMoveAsync(GameSession session, ReadOnlyMemory<byte> data, string transport)
    {
        if (!CombatPacketParser.TryParseMovePacket(data.Span, out _, out var x, out var y, out var z, out var rotation))
            return;

        var entity = _world.Entities.GetByNetworkId(session.PlayerId);
        if (entity is null)
            return;

        var receiveIndex = Interlocked.Increment(ref _moveReceiveCounter);
        if (receiveIndex % 25 == 1)
        {
            _logger.LogInformation("MOVE recv #{Index} via {Transport}: player={PlayerId}, input=({X:F1},{Y:F1},{Z:F1}), sessionTcp={HasTcp}",
                receiveIndex, transport, session.PlayerId, x, y, z, session.TcpConnectionId is not null);
        }

        var newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation);
        var desiredVelocity = new Vector3((float)x, 0f, (float)z);
        if (desiredVelocity.LengthSquared() > MaxPlayerMoveSpeed * MaxPlayerMoveSpeed)
            desiredVelocity = Vector3.Normalize(desiredVelocity) * MaxPlayerMoveSpeed;

        var newPos = entity.Position + desiredVelocity * MoveInputDeltaTime;
        newPos.Y = entity.Position.Y;

        // Update authoritative world state and check zone transition
        var newZone = _world.UpdateEntityPosition(session.PlayerId, newPos, newRotation);
        if (newZone is not null)
        {
            // Zone changed — update TCP zone membership
            if (session.TcpConnectionId is not null)
            {
                if (session.CurrentZoneId is not null)
                    _tcp.LeaveZone(session.TcpConnectionId, session.CurrentZoneId);
                _tcp.JoinZone(session.TcpConnectionId, newZone);
            }
            session.CurrentZoneId = newZone;
        }

        if (session.CurrentZoneId is null) return;

        // Rebroadcast move to all players in the same zone
        var buffer = new byte[1 + 16 + 24 + 4];
        CombatPacketParser.WriteMovePacket(buffer, session.PlayerId, newPos.X, newPos.Y, newPos.Z, rotation);
        await _tcp.BroadcastToZoneAsync(session.CurrentZoneId, buffer);
    }

    private async Task HandleDamageAsync(GameSession session, ReadOnlyMemory<byte> data)
    {
        if (!CombatPacketParser.TryParseDamagePacket(data.Span, out var attackerId, out var targetId, out var damage))
            return;

        if (attackerId != session.PlayerId)
            return;

        // Apply to authoritative world state
        var targetEntity = _world.Entities.GetByNetworkId(targetId);
        if (targetEntity is not null)
            targetEntity.Health = MathF.Max(0, targetEntity.Health - (float)damage);

        // Persist to grain
        var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(targetId);
        await playerGrain.TakeDamageAsync(damage);
    }

    private void HandleObserverPosition(GameSession session, ReadOnlyMemory<byte> data)
    {
        if (!CombatPacketParser.TryParseObserverPositionPacket(
                data.Span, out _, out var ox, out var oy, out var oz, out var viewRadius))
            return;

        // Clamp view radius to a sane range (one zone → ~10 zones radius)
        var zoneSize = _world.Zones.ZoneSize;
        viewRadius = MathF.Max(zoneSize, MathF.Min(viewRadius, zoneSize * 10f));
        session.ObserverRadius = viewRadius;

        // Compute the set of zones the observer can see
        session.ObserverZoneIds = _world.Zones.GetZonesInRadius(
            (float)ox, (float)oy, (float)oz, viewRadius);
    }

    private void HandleWatchPoint(GameSession session, ReadOnlyMemory<byte> data)
    {
        if (!CombatPacketParser.TryParseWatchPointPacket(
                data.Span, out var wpId, out var action,
                out var wx, out var wy, out var wz, out var radius))
            return;

        var zoneSize = _world.Zones.ZoneSize;

        switch (action)
        {
            case CombatPacketParser.WatchPointActionAdd:
                // Cap at 16 watch points per session to avoid abuse
                if (session.WatchPoints.Count >= 16 && !session.WatchPoints.ContainsKey(wpId))
                    return;

                radius = MathF.Max(zoneSize, MathF.Min(radius, zoneSize * 10f));
                var zones = _world.Zones.GetZonesInRadius((float)wx, (float)wy, (float)wz, radius);

                session.WatchPoints[wpId] = new WatchPointInfo
                {
                    Id = wpId,
                    X = (float)wx,
                    Y = (float)wy,
                    Z = (float)wz,
                    Radius = radius,
                    ZoneIds = zones
                };
                break;

            case CombatPacketParser.WatchPointActionRemove:
                session.WatchPoints.Remove(wpId);
                break;

            case CombatPacketParser.WatchPointActionClear:
                session.WatchPoints.Clear();
                break;
        }
    }

    private async Task HandleDisconnectAsync(string tcpConnectionId)
    {
        var session = await _sessions.GetSessionAsync(tcpConnectionId);
        if (session is not null)
        {
            if (session.CurrentZoneId is not null)
                _tcp.LeaveZone(tcpConnectionId, session.CurrentZoneId);
            await _sessions.UnbindTcpAsync(session.PlayerId, tcpConnectionId);

            _logger.LogInformation("TCP disconnected for player {PlayerId}; keeping entity alive and switching to SignalR fallback", session.PlayerId);
        }
    }
}
