using Game.Shared.Protocols;
using Game.Shared.Protocols.Serialization;

namespace GameServer.Networking.Core;

public interface IMessageSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T message);
    T Deserialize<T>(ReadOnlyMemory<byte> data);
}

public sealed class MessagePackNetworkSerializer : IMessageSerializer
{
    public ReadOnlyMemory<byte> Serialize<T>(T message) =>
        ProtocolSerializer.Serialize(message);

    public T Deserialize<T>(ReadOnlyMemory<byte> data) =>
        ProtocolSerializer.Deserialize<T>(data);
}
