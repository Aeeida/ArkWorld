using MessagePack;

namespace Game.Shared.Protocols.Messages;

[MessagePackObject]
public sealed class LoginRequestMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.LoginRequest;

    [Key(0)]
    public required string Username { get; init; }

    [Key(1)]
    public required string PasswordHash { get; init; }
}

[MessagePackObject]
public sealed class LoginResponseMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.LoginResponse;

    [Key(0)]
    public required bool Success { get; init; }

    [Key(1)]
    public string? Token { get; init; }

    [Key(2)]
    public string? ErrorMessage { get; init; }
}
