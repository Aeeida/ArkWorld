using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using GameServer.Networking.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.Transport;

/// <summary>
/// High-frequency TCP transport for position/combat/fleet sync.
/// Uses System.IO.Pipelines with a 4-byte length-prefix frame protocol.
/// </summary>
public sealed class TcpTransportServer : IAsyncDisposable
{
    private readonly ILogger<TcpTransportServer> _logger;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _zones = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public TcpTransportServer(ILogger<TcpTransportServer> logger)
    {
        _logger = logger;
    }

    public event Func<string, ReadOnlyMemory<byte>, Task>? OnMessageReceived;
    public event Func<string, Task>? OnClientConnected;
    public event Func<string, Task>? OnClientDisconnected;

    public async Task StartAsync(IPEndPoint endPoint, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(endPoint);
        _listener.Start();
        _logger.LogInformation("TCP Transport listening on {EndPoint}", endPoint);

        _ = AcceptClientsAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();

        foreach (var (id, client) in _clients)
        {
            client.Close();
            _clients.TryRemove(id, out _);
        }

        _logger.LogInformation("TCP Transport stopped");
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }

    // ── Send / Broadcast ─────────────────────────────────────────────

    public async Task SendAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!_clients.TryGetValue(connectionId, out var client) || !client.Connected)
            return;

        var stream = client.GetStream();
        var lengthPrefix = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, data.Length);
            await stream.WriteAsync(lengthPrefix.AsMemory(0, 4), ct);
            await stream.WriteAsync(data, ct);
            await stream.FlushAsync(ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(lengthPrefix);
        }
    }

    public async Task BroadcastAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var tasks = _clients.Keys.Select(id => SendAsync(id, data, ct));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Broadcasts data only to TCP connections in the specified spatial zone (AOI).
    /// </summary>
    public async Task BroadcastToZoneAsync(string zoneId, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!_zones.TryGetValue(zoneId, out var members))
            return;

        string[] snapshot;
        lock (members) { snapshot = [.. members]; }

        var tasks = snapshot.Select(id => SendAsync(id, data, ct));
        await Task.WhenAll(tasks);
    }

    public void JoinZone(string connectionId, string zoneId)
    {
        var members = _zones.GetOrAdd(zoneId, _ => []);
        lock (members) { members.Add(connectionId); }
    }

    public void LeaveZone(string connectionId, string zoneId)
    {
        if (!_zones.TryGetValue(zoneId, out var members))
            return;
        lock (members) { members.Remove(connectionId); }
    }

    // ── Accept Loop ──────────────────────────────────────────────────

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                client.NoDelay = true; // Disable Nagle for low latency
                var connectionId = Guid.NewGuid().ToString("N");
                _clients.TryAdd(connectionId, client);

                _logger.LogInformation("TCP client connected: {ConnectionId}", connectionId);

                if (OnClientConnected is not null)
                    await OnClientConnected(connectionId);

                _ = HandleClientAsync(connectionId, client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error accepting TCP client"); }
        }
    }

    // ── Per-Client Read Loop (Pipelines + Length-Prefix Framing) ─────

    private async Task HandleClientAsync(string connectionId, TcpClient client, CancellationToken ct)
    {
        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: 1024 * 64,
            resumeWriterThreshold: 1024 * 32,
            useSynchronizationContext: false));

        try
        {
            var stream = client.GetStream();
            var fillTask = FillPipeAsync(stream, pipe.Writer, ct);
            var readTask = ReadPipeAsync(connectionId, pipe.Reader, ct);
            await Task.WhenAll(fillTask, readTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TCP client {ConnectionId} error", connectionId);
        }
        finally
        {
            client.Close();
            _clients.TryRemove(connectionId, out _);

            // Remove from all zones
            foreach (var (_, members) in _zones)
                lock (members) { members.Remove(connectionId); }

            if (OnClientDisconnected is not null)
                await OnClientDisconnected(connectionId);

            _logger.LogInformation("TCP client disconnected: {ConnectionId}", connectionId);
        }
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

    private async Task ReadPipeAsync(string connectionId, PipeReader reader, CancellationToken ct)
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
                        await OnMessageReceived(connectionId, frame.ToArray());
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
    /// Reads a single length-prefixed frame: [4-byte big-endian length][payload].
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

public sealed class TcpConnectionManager(TcpTransportServer server) : IConnectionManager
{
    public Task SendAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
        server.SendAsync(connectionId, data, ct);

    public Task BroadcastAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
        server.BroadcastAsync(data, ct);

    public Task BroadcastToZoneAsync(string zoneId, ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
        server.BroadcastToZoneAsync(zoneId, data, ct);

    public Task DisconnectAsync(string connectionId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public IReadOnlyCollection<string> GetConnectedIds() => [];
}

public static class DependencyInjection
{
    public static IServiceCollection AddTcpTransport(this IServiceCollection services)
    {
        services.AddSingleton<TcpTransportServer>();
        services.AddSingleton<TcpConnectionManager>();
        return services;
    }
}
