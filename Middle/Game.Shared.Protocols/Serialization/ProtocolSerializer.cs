using System.Buffers;
using System.IO.Pipelines;
using MessagePack;

namespace Game.Shared.Protocols.Serialization;

public static class ProtocolSerializer
{
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    public static byte[] Serialize<T>(T message) =>
        MessagePackSerializer.Serialize(message, Options);

    public static T Deserialize<T>(ReadOnlyMemory<byte> data) =>
        MessagePackSerializer.Deserialize<T>(data, Options);

    public static T Deserialize<T>(ReadOnlySequence<byte> data) =>
        MessagePackSerializer.Deserialize<T>(in data, Options);

    public static void SerializeTo<T>(IBufferWriter<byte> writer, T message) =>
        MessagePackSerializer.Serialize(writer, message, Options);

    public static async ValueTask WriteMessageAsync<T>(PipeWriter writer, T message, CancellationToken ct = default)
    {
        var bytes = Serialize(message);
        var lengthPrefix = BitConverter.GetBytes(bytes.Length);

        var memory = writer.GetMemory(4 + bytes.Length);
        lengthPrefix.CopyTo(memory);
        bytes.CopyTo(memory[4..]);
        writer.Advance(4 + bytes.Length);

        await writer.FlushAsync(ct);
    }

    public static bool TryReadMessage<T>(ref ReadOnlySequence<byte> buffer, out T? message)
    {
        message = default;
        if (buffer.Length < 4) return false;

        Span<byte> lengthBytes = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(lengthBytes);
        var length = BitConverter.ToInt32(lengthBytes);

        if (buffer.Length < 4 + length) return false;

        var payload = buffer.Slice(4, length);
        message = Deserialize<T>(payload);
        buffer = buffer.Slice(4 + length);
        return true;
    }
}
