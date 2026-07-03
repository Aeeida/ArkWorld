using System.Collections.Concurrent;
using Cortex.Mediator;
using GameServer.Api.Core;
using GameServer.Networking.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR;

public sealed partial class GameHub(
    ISessionManager sessionManager,
    IWorldPopulationService worldPopulation,
    IServiceProvider serviceProvider,
    ILogger<GameHub> logger) : Hub
{
    private const byte SnapshotPacketId = 0x10;

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        if (session is not null)
        {
            // 清理载具座位（防止重登后无法进入载具）
            var vehicles = serviceProvider.GetService<GameLayer.Vehicle.VehicleManager>();
            vehicles?.ForceExitPlayer(session.PlayerId);

            var world = serviceProvider.GetService<GameLayer.Core.ServerWorldState>();
            world?.RemovePlayer(session.PlayerId);
        }

        await sessionManager.RemoveSessionAsync(Context.ConnectionId);
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ══════════════════════════════════════════════════════════════════
    // 通用命令 / 消息 / 分组
    // ══════════════════════════════════════════════════════════════════

    public async Task<object?> SendCommand(string commandType, object payload)
    {
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        if (session is null)
        {
            await Clients.Caller.SendAsync("Error", "Not authenticated");
            return null;
        }

        session.LastActivityAt = DateTime.UtcNow;

        using var scope = serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            // Route commands through mediator based on commandType
            // The actual command objects would be deserialized from payload
            logger.LogInformation("Command {Type} from player {PlayerId}", commandType, session.PlayerId);
            return new { Success = true, CommandType = commandType };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing command {Type}", commandType);
            return new { Success = false, Error = ex.Message };
        }
    }

    public async Task SendGameMessage(byte[] data)
    {
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        if (session is null)
        {
            await Clients.Caller.SendAsync("Error", "Not authenticated");
            return;
        }
        session.LastActivityAt = DateTime.UtcNow;

        var router = serviceProvider.GetRequiredService<IRealtimeMessageRouter>();
        await router.HandleFallbackMessageAsync(session, data);

        await Clients.Caller.SendAsync("GameMessageAck", true);
    }

    public async Task BroadcastToWorld(string worldId, string eventName, object data)
    {
        // Broadcast to all clients in the same world group
        await Clients.Group(worldId).SendAsync(eventName, data);
    }

    public async Task JoinWorldGroup(string worldId)
    {
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        if (session is not null)
            session.CurrentZoneId = worldId;

        await Groups.AddToGroupAsync(Context.ConnectionId, worldId);
    }

    public async Task LeaveWorldGroup(string worldId)
    {
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        if (session is not null)
            session.CurrentZoneId = null;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, worldId);
    }

    private async Task<T> InvokeApiAsync<T>(Func<IGameServerApi, Task<T>> action)
    {
        using var scope = serviceProvider.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGameServerApi>();
        return await action(api);
    }

    private async Task ReplaceSessionAsync(System.Guid playerId)
    {
        var previousSession = await sessionManager.GetSessionAsync(Context.ConnectionId);
        var preserveTcpBinding = previousSession is not null
                              && previousSession.PlayerId == playerId
                              && previousSession.TcpConnectionId is not null;
        var preservedTcpConnectionId = preserveTcpBinding ? previousSession!.TcpConnectionId : null;
        var preservedZoneId = preserveTcpBinding ? previousSession!.CurrentZoneId : null;

        await sessionManager.RemoveSessionAsync(Context.ConnectionId);
        var session = await sessionManager.CreateSessionAsync(Context.ConnectionId, playerId);

        if (preservedZoneId is not null)
            session.CurrentZoneId = preservedZoneId;

        if (preservedTcpConnectionId is not null)
        {
            await sessionManager.BindTcpAsync(playerId, preservedTcpConnectionId);
            logger.LogInformation("Preserved TCP binding for player {PlayerId} on SignalR session replacement", playerId);
        }
    }
}

public sealed class SignalRConnectionManager(IHubContext<GameHub> hubContext) : IConnectionManager
{
    private readonly ConcurrentDictionary<string, bool> _connections = new();

    public async Task SendAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        await hubContext.Clients.Client(connectionId)
            .SendAsync("GameMessage", data.ToArray(), cancellationToken: ct);
    }

    public async Task BroadcastAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        await hubContext.Clients.All
            .SendAsync("GameMessage", data.ToArray(), cancellationToken: ct);
    }

    public async Task BroadcastToZoneAsync(string zoneId, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        await hubContext.Clients.Group(zoneId)
            .SendAsync("GameMessage", data.ToArray(), cancellationToken: ct);
    }

    public async Task DisconnectAsync(string connectionId, CancellationToken ct = default)
    {
        _connections.TryRemove(connectionId, out _);
        await Task.CompletedTask;
    }

    public IReadOnlyCollection<string> GetConnectedIds() =>
        _connections.Keys.ToList().AsReadOnly();

    public void TrackConnection(string connectionId) =>
        _connections.TryAdd(connectionId, true);

    public void UntrackConnection(string connectionId) =>
        _connections.TryRemove(connectionId, out _);
}

public static class DependencyInjection
{
    public static IServiceCollection AddSignalRNetworking(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 1024 * 64; // 64KB
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddSingleton<SignalRConnectionManager>();
        services.AddSingleton<IConnectionManager>(sp => sp.GetRequiredService<SignalRConnectionManager>());
        services.AddSingleton<ISnapshotBroadcaster, SignalRSnapshotBroadcaster>();

        return services;
    }
}
