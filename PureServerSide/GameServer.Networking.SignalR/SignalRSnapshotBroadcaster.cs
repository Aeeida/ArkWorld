using GameServer.Networking.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR;

/// <summary>
/// 通过 SignalR 向客户端发送二进制快照帧。
/// 当客户端没有 TCP 连接时，WorldTickService 通过此服务降级推送实体状态。
/// 客户端监听 "OnServerSnapshot" 事件，载荷格式与 TCP 快照完全一致。
/// </summary>
public sealed class SignalRSnapshotBroadcaster(
    IHubContext<GameHub> hubContext,
    ILogger<SignalRSnapshotBroadcaster> logger) : ISnapshotBroadcaster
{
    public async Task SendSnapshotAsync(string connectionId, byte[] snapshotFrame, CancellationToken ct = default)
    {
        try
        {
            await hubContext.Clients.Client(connectionId).SendAsync("OnServerSnapshot", snapshotFrame, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send SignalR snapshot to {ConnectionId}", connectionId);
        }
    }

    public async Task BroadcastSnapshotAsync(IEnumerable<string> connectionIds, byte[] snapshotFrame, CancellationToken ct = default)
    {
        var ids = connectionIds.ToList();
        if (ids.Count == 0) return;

        try
        {
            await hubContext.Clients.Clients(ids).SendAsync("OnServerSnapshot", snapshotFrame, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast SignalR snapshot to {Count} clients", ids.Count);
        }
    }
}
