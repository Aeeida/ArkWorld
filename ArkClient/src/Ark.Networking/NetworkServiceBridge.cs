using System.Numerics;
using Ark.Abstractions;
using Ark.Shared.Data;
using Microsoft.Extensions.Logging;

namespace Ark.Networking;

/// <summary>
/// <see cref="INetworkService"/> 适配器 — 将 <see cref="NetworkManager"/> 桥接到
/// 游戏服务层（<see cref="Ark.Services.GameServices"/>）使用的统一网络接口。
/// 真实快照通过 TCP 0x10 包接收，由外部 SnapshotApplier 消费。
/// </summary>
public sealed class NetworkServiceBridge : INetworkService, IDisposable
{
    private readonly NetworkManager _manager;
    private readonly ILogger<NetworkServiceBridge>? _logger;
    private int _moveSendCounter;

    /// <summary>已认证的玩家 ID（登录成功后设置）。</summary>
    public Guid PlayerId { get; set; }

    public NetworkServiceBridge(NetworkManager manager, ILogger<NetworkServiceBridge>? logger = null)
    {
        _manager = manager;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string address, int port)
    {
        try
        {
            // SignalR hub 地址（匹配服务端 Program.cs: app.MapHub<GameHub>("/gamehub")）
            var signalRUrl = $"http://{address}:{port}/gamehub";
            var tcpPort = port + 2; // TCP 传输端口 = HTTP端口 + 2（避免与 Kestrel HTTP/HTTPS 冲突）

            await _manager.ConnectAsync(signalRUrl, address, tcpPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ConnectAsync failed: {Message}", ex.Message);
            return false;
        }
    }

    public void SendPlayerInput(PlayerInputData input)
    {
        if (_manager.ConnectionState != NetworkConnectionState.Connected) return;

        var sendIndex = Interlocked.Increment(ref _moveSendCounter);
        if (sendIndex % 25 == 1)
        {
            _logger?.LogDebug(
                "MOVE send #{SendIndex}: player={Player}, tcp={Tcp}, vel=({Vx:F1},{Vy:F1},{Vz:F1})",
                sendIndex, PlayerId, _manager.IsTcpConnected,
                input.MoveDirection.X, input.MoveDirection.Y, input.MoveDirection.Z);
        }

        // 通过高频通道发送移动意图（世界空间期望速度向量 + 朝向）
        _ = _manager.SendMoveAsync(
            PlayerId,
            input.MoveDirection.X,
            input.MoveDirection.Y,
            input.MoveDirection.Z,
            MathF.Atan2(-input.AimDirection.X, -input.AimDirection.Z));
    }

    public void Dispose()
    {
        _ = _manager.DisposeAsync();
    }
}
