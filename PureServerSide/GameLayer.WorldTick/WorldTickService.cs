using System.Buffers.Binary;
using System.Diagnostics;
using GameLayer.Building;
using GameLayer.Vehicle;
using GameLayer.Core;
using GameServer.Networking.Core;
using GameServer.Networking.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameLayer.WorldTick;

/// <summary>
/// Server-authoritative game loop running at a fixed tick rate (default 20 Hz).
/// Each tick:
///   1. Advances world time
///   2. Updates construction progress
///   3. Generates per-zone snapshots
///   4. Broadcasts snapshots to clients in each zone via TCP + SignalR fallback
/// This is the heartbeat that drives the server-side simulation and
/// feeds data to Ark clients' SnapshotApplier.
/// </summary>
public sealed class WorldTickService : BackgroundService
{
    private const int TickRateHz = 20;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000.0 / TickRateHz);

    /// <summary>SignalR 快照推送倍率（1 = 每 tick 推送一次）。</summary>
    private const int SignalRTickDivisor = 1; // SignalR-only mode: 20Hz

    // Snapshot packet: [1:packetId=0x10][8:tick][4:serverTime][N:entityStates]
    private const byte SnapshotPacketId = 0x10;

    private readonly ServerWorldState _world;
    private readonly BuildingManager _building;
    private readonly VehicleManager _vehicles;
    private readonly IBuildingDamagePersistenceSink? _buildingPersistence;
    private readonly IWorldPopulationService? _population;
    private readonly TcpTransportServer _tcp;
    private readonly ISessionManager _sessions;
    private readonly ISnapshotBroadcaster? _signalRBroadcaster;
    private readonly ILogger<WorldTickService> _logger;

    public WorldTickService(
        ServerWorldState world,
        BuildingManager building,
        VehicleManager vehicles,
        IBuildingDamagePersistenceSink? buildingPersistence,
        IWorldPopulationService? population,
        TcpTransportServer tcp,
        ISessionManager sessions,
        ILogger<WorldTickService> logger,
        ISnapshotBroadcaster? signalRBroadcaster = null)
    {
        _world = world;
        _building = building;
        _vehicles = vehicles;
        _buildingPersistence = buildingPersistence;
        _population = population;
        _tcp = tcp;
        _sessions = sessions;
        _logger = logger;
        _signalRBroadcaster = signalRBroadcaster;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorldTickService started at {TickRate} Hz (SignalR snapshots: {SignalRHz} Hz)",
            TickRateHz, TickRateHz / SignalRTickDivisor);

        var sw = new Stopwatch();

        while (!stoppingToken.IsCancellationRequested)
        {
            sw.Restart();

            try
            {
                var deltaTime = (float)TickInterval.TotalSeconds;

                // 1. Advance world simulation
                _world.Tick(deltaTime);

                // 2. Update building construction
                _building.TickConstruction(deltaTime);
                _building.SyncSnapshotState();
                var persistenceDeltas = _building.DrainPersistenceDeltas();
                if (_buildingPersistence is not null && persistenceDeltas.Count > 0)
                    await _buildingPersistence.PersistAsync(persistenceDeltas, stoppingToken);

                // 2.5. Update ambient NPC / monster movement
                _population?.Tick(deltaTime);

                // 2.6. Sync seat-relative player state and vehicle runtime into snapshot fields
                _vehicles.SyncSnapshotState(_world);

                // 3. Broadcast zone snapshots
                await BroadcastZoneSnapshotsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during world tick {Tick}", _world.CurrentTick);
            }

            sw.Stop();

            // Sleep for remaining tick time
            var sleepTime = TickInterval - sw.Elapsed;
            if (sleepTime > TimeSpan.Zero)
                await Task.Delay(sleepTime, stoppingToken);
        }

        _logger.LogInformation("WorldTickService stopped");
    }

    /// <summary>
    /// Builds a snapshot for each session's observer AOI and broadcasts to clients.
    /// Each session receives entities from its ObserverZoneIds (multi-zone set)
    /// or from its CurrentZoneId + neighbor zones as a fallback.
    /// </summary>
    private async Task BroadcastZoneSnapshotsAsync(CancellationToken ct)
    {
        var allSessions = _sessions.GetAllSessions();

        // Periodic diagnostic (every 100 ticks ≈ 5 seconds)
        if (_world.CurrentTick % 100 == 0)
        {
            var totalEntities = _world.Entities.Count;
            _logger.LogTrace(
                "[Tick {Tick}] Sessions={Sessions}, TotalEntities={Entities}",
                _world.CurrentTick, allSessions.Count, totalEntities);

            foreach (var s in allSessions)
            {
                _logger.LogInformation(
                    "  Session: ConnId={ConnId}, Player={Player}, World={World}, Zone={Zone}, ObsZones={ObsCount}, WatchPts={WpCount}, TCP={Tcp}",
                    s.ConnectionId[..Math.Min(8, s.ConnectionId.Length)],
                    s.PlayerId, s.CurrentWorldId, s.CurrentZoneId,
                    s.ObserverZoneIds?.Count ?? 0,
                    s.WatchPoints.Count,
                    s.TcpConnectionId is not null);
            }
        }

        if (allSessions.Count == 0) return;

        var isSignalRTick = _signalRBroadcaster is not null
                         && _world.CurrentTick % SignalRTickDivisor == 0;

        // Cache zone snapshots to avoid re-serializing the same zone for multiple sessions
        var zoneSnapshotCache = new Dictionary<string, byte[]>();

        foreach (var session in allSessions)
        {
            if (string.IsNullOrWhiteSpace(session.CurrentWorldId))
                continue;

            // Determine which zones this session should see
            var zonesForSession = session.ObserverZoneIds;
            if (zonesForSession is null || zonesForSession.Count == 0)
            {
                if (session.CurrentZoneId is null && session.WatchPoints.Count == 0) continue;
                // Fallback: current zone + 3D neighbors (27 zones)
                zonesForSession = session.CurrentZoneId is not null
                    ? [.. _world.Zones.GetNeighborZones(session.CurrentZoneId)]
                    : [];
            }

            // Merge all watch point zones into the visible set
            if (session.WatchPoints.Count > 0)
            {
                // Avoid mutating the observer set — create a union copy
                var merged = new HashSet<string>(zonesForSession);
                foreach (var wp in session.WatchPoints.Values)
                    merged.UnionWith(wp.ZoneIds);
                zonesForSession = merged;
            }

            // Collect all entities across all visible zones (deduplicated by set membership)
            var visibleEntities = new List<GameLayer.Core.ServerEntity>();
            foreach (var zoneId in zonesForSession)
            {
                foreach (var entity in _world.Entities.GetInWorldZone(session.CurrentWorldId, zoneId))
                    visibleEntities.Add(entity);
            }

            if (visibleEntities.Count == 0) continue;

            // Serialize merged snapshot
            var entityStates = GameLayer.Core.SnapshotSerializer.Serialize(visibleEntities);
            if (entityStates.Length <= 4) continue;

            // Build snapshot frame: [1:packetId][8:tick][4:serverTime][N:entityStates]
            var frame = new byte[1 + 8 + 4 + entityStates.Length];
            frame[0] = SnapshotPacketId;
            BinaryPrimitives.WriteUInt64LittleEndian(frame.AsSpan(1), _world.CurrentTick);
            BinaryPrimitives.WriteSingleLittleEndian(frame.AsSpan(9), _world.WorldTime);
            entityStates.CopyTo(frame.AsSpan(13));

            // TCP broadcast — send each session its own merged frame
            if (session.TcpConnectionId is not null)
            {
                await _tcp.SendAsync(session.TcpConnectionId, frame, ct);
            }

            // SignalR fallback
            if (isSignalRTick && session.TcpConnectionId is null)
            {
                _logger.LogTrace(
                    "[Tick {Tick}] SignalR snapshot: player={Player}, zones={ZoneCount}, entities={Count}",
                    _world.CurrentTick, session.PlayerId, zonesForSession.Count, visibleEntities.Count);
                await _signalRBroadcaster!.SendSnapshotAsync(session.ConnectionId, frame, ct);
            }
        }
    }
}
