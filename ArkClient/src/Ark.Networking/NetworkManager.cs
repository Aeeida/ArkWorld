using Ark.Networking.SignalR;
using Ark.Networking.Tcp;
using Game.Shared.Protocols.Serialization;
using Microsoft.Extensions.Logging;

namespace Ark.Networking;

/// <summary>
/// 网络管理器 — 统一管理 SignalR（业务 RPC）和 TCP（高频同步）两条通道。
/// 提供连接/断开/状态查询的单一入口。
/// </summary>
public sealed class NetworkManager : IAsyncDisposable
{
    private const bool UseTcpHighFrequencyTransport = false;

    private readonly ILogger<NetworkManager>? _logger;
    private CancellationTokenSource? _tcpReconnectCts;
    private Task? _tcpReconnectTask;
    private Guid _tcpPlayerId;
    private bool _suppressTcpReconnect;

    public SignalRClient SignalR { get; }
    public TcpTransportClient Tcp { get; }

    public NetworkConnectionState ConnectionState { get; private set; } = NetworkConnectionState.Disconnected;
    public bool IsTcpConnected => UseTcpHighFrequencyTransport && Tcp.IsConnected;

    /// <summary>网络统计信息（包数、字节数、延迟等）。</summary>
    public NetworkStats Stats { get; } = new();

    public event Action<NetworkConnectionState>? OnConnectionStateChanged;
    /// <summary>原始 TCP 帧回调 — 用于快照等自定义包解码。</summary>
    public event Func<ReadOnlyMemory<byte>, Task>? OnRawTcpMessage;

    public NetworkManager(ILogger<NetworkManager>? logger = null)
    {
        _logger = logger;
        SignalR = new SignalRClient();
        Tcp = new TcpTransportClient();

        SignalR.OnClosed += _ => UpdateState(NetworkConnectionState.Disconnected);
        SignalR.OnReconnecting += _ => UpdateState(NetworkConnectionState.Reconnecting);
        SignalR.OnReconnected += _ => UpdateState(NetworkConnectionState.Connected);

        Tcp.OnDisconnected += _ =>
        {
            _logger?.LogWarning("TCP channel disconnected");
            if (!_suppressTcpReconnect)
                StartTcpReconnectLoop();
        };

        Tcp.OnMessageReceived += HandleTcpMessage;
    }

    // ══════════════════════════════════════════════════════════════════
    // 连接管理
    // ══════════════════════════════════════════════════════════════════

    private string? _tcpHost;
    private int _tcpPort;

    /// <summary>TCP 传输服务器地址（连接时保存，供延迟 TCP 连接使用）。</summary>
    public string? TcpHost => _tcpHost;
    /// <summary>TCP 传输服务器端口。</summary>
    public int TcpPort => _tcpPort;

    /// <summary>
    /// 连接到游戏服务器（仅建立 SignalR 业务通道）。
    /// TCP 高频通道在角色认证后通过 <see cref="ConnectTcpAsync"/> 延迟建立，
    /// 因为服务端需要先创建 Session 才能绑定 TCP 连接。
    /// </summary>
    public async Task ConnectAsync(
        string signalRUrl,
        string tcpHost,
        int tcpPort,
        CancellationToken ct = default)
    {
        _tcpHost = tcpHost;
        _tcpPort = tcpPort;

        UpdateState(NetworkConnectionState.Connecting);

        try
        {
            _logger?.LogInformation("Connecting SignalR to {Url}", signalRUrl);
            await SignalR.ConnectAsync(signalRUrl, ct);

            UpdateState(NetworkConnectionState.Connected);
            _logger?.LogInformation("SignalR connected (TCP deferred until auth)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect SignalR");
            UpdateState(NetworkConnectionState.Disconnected);
            throw;
        }
    }

    /// <summary>
    /// 延迟建立 TCP 高频通道 + 身份验证握手。
    /// 必须在 SignalR 认证完成（Session 已创建）后调用，
    /// 否则服务端无法将 TCP 连接绑定到会话。
    /// </summary>
    public async Task<bool> ConnectTcpAsync(Guid playerId, CancellationToken ct = default)
    {
        if (!UseTcpHighFrequencyTransport)
        {
            _tcpPlayerId = playerId;
            _logger?.LogInformation("TCP high-frequency transport disabled; using SignalR for player {PlayerId}", playerId);
            return false;
        }

        if (_tcpHost is null)
        {
            _logger?.LogWarning("ConnectTcpAsync called but no TCP host saved");
            return false;
        }

        var previousTcpPlayerId = _tcpPlayerId;
        _tcpPlayerId = playerId;

        if (Tcp.IsConnected && previousTcpPlayerId == playerId)
        {
            _logger?.LogInformation("TCP channel already connected for player {PlayerId}; reusing existing socket", playerId);
            return true;
        }

        // 如果已有旧的 TCP 连接（例如重新进入世界），先断开
        if (Tcp.IsConnected)
        {
            _suppressTcpReconnect = true;
            try { await Tcp.DisconnectAsync(); }
            finally { _suppressTcpReconnect = false; }
        }

        try
        {
            _logger?.LogInformation("Connecting TCP to {Host}:{Port} for player {PlayerId}", _tcpHost, _tcpPort, playerId);
            await Tcp.ConnectAsync(_tcpHost, _tcpPort, ct);
            await Tcp.SendAuthHandshakeAsync(playerId, ct);
            _tcpReconnectCts?.Cancel();
            _tcpReconnectTask = null;
            _logger?.LogInformation("TCP channel connected and authenticated");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "TCP channel failed to connect (non-fatal)");
            return false;
        }
    }

    /// <summary>
    /// 断开所有网络通道。
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Disconnecting all network channels");

        _tcpReconnectCts?.Cancel();
        _suppressTcpReconnect = true;
        if (Tcp.IsConnected)
            await Tcp.DisconnectAsync();
        await SignalR.DisconnectAsync(ct);
        _suppressTcpReconnect = false;

        UpdateState(NetworkConnectionState.Disconnected);
    }

    /// <summary>
    /// 注册服务端推送事件处理器。
    /// </summary>
    public void SetEventHandler(IServerEventHandler handler)
    {
        SignalR.SetEventHandler(handler);
    }

    // ══════════════════════════════════════════════════════════════════
    // 高频数据快捷方法
    // ══════════════════════════════════════════════════════════════════

    /// <summary>发送本地玩家位置更新（通过 TCP 高频通道）。</summary>
    public async Task SendMoveAsync(Guid entityId, double x, double y, double z, float rotation, CancellationToken ct = default)
    {
        await SignalR.SendGameMessageAsync(BuildMovePacket(entityId, x, y, z, rotation), ct);

        Stats.RecordSend(1 + 16 + 24 + 4); // packetId + guid + 3*double + float
    }

    public async ValueTask DisposeAsync()
    {
        await Tcp.DisposeAsync();
        await SignalR.DisposeAsync();
    }

    // ══════════════════════════════════════════════════════════════════
    // 内部
    // ══════════════════════════════════════════════════════════════════

    private void UpdateState(NetworkConnectionState newState)
    {
        if (ConnectionState == newState) return;
        ConnectionState = newState;

        if (newState == NetworkConnectionState.Connected)
            Stats.ConnectedAt = DateTime.UtcNow;
        else if (newState == NetworkConnectionState.Disconnected)
            Stats.ConnectedAt = null;

        OnConnectionStateChanged?.Invoke(newState);
        _logger?.LogInformation("Network state: {State}", newState);
    }

    private async Task HandleTcpMessage(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 1) return;

        Stats.RecordReceive(data.Length);
        var packetId = data.Span[0];

        // 转发原始帧给外部消费者（快照解码等）
        if (OnRawTcpMessage is not null)
            await OnRawTcpMessage(data);

        switch (packetId)
        {
        }
    }

    private void StartTcpReconnectLoop()
    {
        if (!UseTcpHighFrequencyTransport)
            return;

        if (_tcpPlayerId == Guid.Empty || _tcpHost is null)
            return;

        if (_tcpReconnectTask is { IsCompleted: false })
            return;

        _tcpReconnectCts?.Cancel();
        _tcpReconnectCts = new CancellationTokenSource();
        _tcpReconnectTask = ReconnectTcpLoopAsync(_tcpReconnectCts.Token);
    }

    private static byte[] BuildMovePacket(Guid entityId, double x, double y, double z, float rotation)
    {
        var buffer = new byte[1 + 16 + 24 + 4];
        CombatPacketParser.WriteMovePacket(buffer, entityId, x, y, z, rotation);
        return buffer;
    }

    private async Task ReconnectTcpLoopAsync(CancellationToken ct)
    {
        _logger?.LogInformation("Starting TCP reconnect loop for player {PlayerId}", _tcpPlayerId);

        while (!ct.IsCancellationRequested && !Tcp.IsConnected)
        {
            try
            {
                if (ConnectionState == NetworkConnectionState.Connected)
                {
                    var connected = await ConnectTcpAsync(_tcpPlayerId, ct);
                    if (connected)
                    {
                        _logger?.LogInformation("TCP reconnect succeeded for player {PlayerId}", _tcpPlayerId);
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "TCP reconnect attempt failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

// ── 连接状态枚举 ─────────────────────────────────────────────────────

public enum NetworkConnectionState : byte
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

// ── TCP 数据包结构 ───────────────────────────────────────────────────

public readonly record struct MovePacketData(
    Guid EntityId,
    double X,
    double Y,
    double Z,
    float Rotation);

public readonly record struct DamagePacketData(
    Guid AttackerId,
    Guid TargetId,
    double Damage);

/// <summary>
/// 网络统计信息 — 用于 HUD 显示和调试。
/// </summary>
public sealed class NetworkStats
{
    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;

    public long PacketsSent => Interlocked.Read(ref _packetsSent);
    public long PacketsReceived => Interlocked.Read(ref _packetsReceived);
    public long BytesSent => Interlocked.Read(ref _bytesSent);
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <summary>连接建立时刻（未连接时为 null）。</summary>
    public DateTime? ConnectedAt { get; internal set; }

    /// <summary>连接持续时间。</summary>
    public TimeSpan Uptime => ConnectedAt.HasValue
        ? DateTime.UtcNow - ConnectedAt.Value
        : TimeSpan.Zero;

    /// <summary>SignalR RPC 调用计数。</summary>
    private long _rpcCallCount;
    public long RpcCallCount => Interlocked.Read(ref _rpcCallCount);

    internal void RecordSend(int bytes)
    {
        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, bytes);
    }

    internal void RecordReceive(int bytes)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, bytes);
    }

    internal void RecordRpc()
    {
        Interlocked.Increment(ref _rpcCallCount);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _packetsSent, 0);
        Interlocked.Exchange(ref _packetsReceived, 0);
        Interlocked.Exchange(ref _bytesSent, 0);
        Interlocked.Exchange(ref _bytesReceived, 0);
        Interlocked.Exchange(ref _rpcCallCount, 0);
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F2} MB"
    };
}
