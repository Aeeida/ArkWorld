using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Game.Shared.Protocols.Serialization;
using Microsoft.Extensions.Logging;

namespace Ark.Networking.Tcp;

/// <summary>
/// 高频 TCP 传输客户端 — 用于位置同步 / 战斗等低延迟二进制通信。
/// 协议：4 字节大端长度前缀 + 载荷（与服务端 TcpTransportServer 完全匹配）。
/// </summary>
public sealed class TcpTransportClient : IAsyncDisposable
{
    private readonly ILogger<TcpTransportClient>? _logger;
    private TcpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private volatile bool _isConnected;
    private int _disconnectRaised;

    public TcpTransportClient(ILogger<TcpTransportClient>? logger = null)
    {
        _logger = logger;
    }

    public bool IsConnected => _isConnected;

    /// <summary>收到服务端二进制帧时触发（已去除长度前缀）。</summary>
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;

    /// <summary>连接断开时触发。</summary>
    public event Action<Exception?>? OnDisconnected;

    // ══════════════════════════════════════════════════════════════════
    // 连接 / 断开
    // ══════════════════════════════════════════════════════════════════

    public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(endPoint.Address, endPoint.Port, _cts.Token);
        _isConnected = true;
        Interlocked.Exchange(ref _disconnectRaised, 0);

        _logger?.LogInformation("TCP connected to {EndPoint}", endPoint);

        _readTask = ReadLoopAsync(_cts.Token);
    }

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        var endPoint = new IPEndPoint(IPAddress.Parse(host), port);
        await ConnectAsync(endPoint, ct);
    }

    /// <summary>
    /// 发送身份验证握手：[0xFF][16 字节 PlayerId]。
    /// 必须在连接后立即调用，否则服务端将拒绝后续消息。
    /// </summary>
    public async Task SendAuthHandshakeAsync(Guid playerId, CancellationToken ct = default)
    {
        const byte authHandshakeId = 0xFF;
        var buffer = new byte[1 + 16];
        buffer[0] = authHandshakeId;
        playerId.TryWriteBytes(buffer.AsSpan(1, 16));
        await SendRawAsync(buffer, ct);
        _logger?.LogInformation("TCP auth handshake sent for player {PlayerId}", playerId);
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();

        if (_readTask is not null)
        {
            try { await _readTask; }
            catch (OperationCanceledException) { }
        }

        _client?.Close();
        _client = null;
        NotifyDisconnected(null);
        _logger?.LogInformation("TCP disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _cts?.Dispose();
        _sendLock.Dispose();
    }

    // ══════════════════════════════════════════════════════════════════
    // 发送
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 发送带长度前缀的二进制帧：[4 字节大端长度][载荷]。
    /// </summary>
    public async Task SendRawAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_client is null || !_isConnected)
            throw new InvalidOperationException("TCP not connected");

        await _sendLock.WaitAsync(ct);
        var stream = _client.GetStream();
        var lengthPrefix = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, data.Length);
            await stream.WriteAsync(lengthPrefix.AsMemory(0, 4), ct);
            await stream.WriteAsync(data, ct);
            await stream.FlushAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "TCP send failed");
            NotifyDisconnected(ex);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(lengthPrefix);
            _sendLock.Release();
        }
    }

    /// <summary>发送位置更新包。</summary>
    public Task SendMoveAsync(Guid entityId, double x, double y, double z, float rotation, CancellationToken ct = default)
    {
        var buffer = new byte[1 + 16 + 24 + 4]; // packetId + guid + 3*double + float
        CombatPacketParser.WriteMovePacket(buffer, entityId, x, y, z, rotation);
        return SendRawAsync(buffer, ct);
    }

    /// <summary>发送伤害包。</summary>
    public Task SendDamageAsync(Guid attackerId, Guid targetId, double damage, CancellationToken ct = default)
    {
        var buffer = new byte[1 + 16 + 16 + 8]; // packetId + 2*guid + double
        CombatPacketParser.WriteDamagePacket(buffer, attackerId, targetId, damage);
        return SendRawAsync(buffer, ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // 接收循环（Pipelines + 长度前缀分帧）
    // ══════════════════════════════════════════════════════════════════

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_client is null) return;

        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: 1024 * 64,
            resumeWriterThreshold: 1024 * 32,
            useSynchronizationContext: false));

        try
        {
            var stream = _client.GetStream();
            var fillTask = FillPipeAsync(stream, pipe.Writer, ct);
            var readTask = ReadPipeAsync(pipe.Reader, ct);
            await Task.WhenAll(fillTask, readTask);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "TCP read loop error");
            NotifyDisconnected(ex);
        }
        finally
        {
            NotifyDisconnected(null);
        }
    }

    private void NotifyDisconnected(Exception? ex)
    {
        _isConnected = false;

        try
        {
            _client?.Close();
        }
        catch { }

        if (Interlocked.Exchange(ref _disconnectRaised, 1) == 0)
            OnDisconnected?.Invoke(ex);
    }

    private static async Task FillPipeAsync(NetworkStream stream, PipeWriter writer, CancellationToken ct)
    {
        const int minimumBufferSize = 4096;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var memory = writer.GetMemory(minimumBufferSize);
                var bytesRead = await stream.ReadAsync(memory, ct);
                if (bytesRead == 0) break;

                writer.Advance(bytesRead);
                var result = await writer.FlushAsync(ct);
                if (result.IsCompleted) break;
            }
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    private async Task ReadPipeAsync(PipeReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(ct);
                var buffer = result.Buffer;

                while (TryReadFrame(ref buffer, out var frame))
                {
                    if (OnMessageReceived is not null)
                        await OnMessageReceived(frame.ToArray());
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    /// <summary>
    /// 读取单个长度前缀帧：[4 字节大端长度][载荷]。
    /// </summary>
    private static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame)
    {
        frame = default;

        if (buffer.Length < 4)
            return false;

        Span<byte> lengthBytes = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(lengthBytes);
        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);

        if (payloadLength is <= 0 or > 1024 * 64)
        {
            buffer = buffer.Slice(4);
            return false;
        }

        var totalFrameLength = 4 + payloadLength;
        if (buffer.Length < totalFrameLength)
            return false;

        frame = buffer.Slice(4, payloadLength);
        buffer = buffer.Slice(totalFrameLength);
        return true;
    }
}
