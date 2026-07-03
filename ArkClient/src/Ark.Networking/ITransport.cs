namespace Ark.Networking;

/// <summary>
/// 网络传输层接口 — 抽象底层网络通信。
/// TODO: 实现 ENet/WebSocket 传输、可靠/不可靠通道、数据包序列化。
/// </summary>
public interface ITransport
{
    bool IsConnected { get; }
    float Latency { get; }

    void Send(ReadOnlySpan<byte> data, bool reliable = true);
    event Action<ReadOnlyMemory<byte>>? OnDataReceived;
    event Action<string>? OnDisconnected;
}
