using Ark.Analyzers.Attributes;
using MessagePack;

namespace Game.Shared.Protocols.Messages;

[MessagePackObject]
public sealed class EnterWorldRequestMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.EnterWorldRequest;

    [Key(0)]
    public required Guid PlayerId { get; init; }

    [Key(1)]
    public required string WorldId { get; init; }
}

[MessagePackObject]
public sealed class EnterWorldResponseMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.EnterWorldResponse;

    [Key(0)]
    public required bool Success { get; init; }

    [Key(1)]
    public string? ErrorMessage { get; init; }
}

[MessagePackObject]
[MapToEcsComponent("Ark.Ecs.Components.WorldPosition")]
public sealed class PlayerMoveMessage : INetworkMessage
{
    [IgnoreMember]
    [EcsIgnore]
    public MessageType Type => MessageType.PlayerMoveRequest;

    [Key(0)]
    [EcsIgnore]
    public required System.Guid PlayerId { get; init; }

    [Key(1)]
    [EcsFieldMap("X")]
    public required double X { get; init; }

    [Key(2)]
    [EcsFieldMap("Y")]
    public required double Y { get; init; }

    [Key(3)]
    [EcsFieldMap("Z")]
    public required double Z { get; init; }

    [Key(4)]
    [EcsIgnore]
    public required float Rotation { get; init; }
}

[MessagePackObject]
public sealed class CombatActionMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.CombatActionRequest;

    [Key(0)]
    public required Guid AttackerId { get; init; }

    [Key(1)]
    public required Guid TargetId { get; init; }

    [Key(2)]
    public required string AbilityId { get; init; }
}

[MessagePackObject]
public sealed class ChatMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.ChatMessage;

    [Key(0)]
    public required Guid SenderId { get; init; }

    [Key(1)]
    public required string Channel { get; init; }

    [Key(2)]
    public required string Content { get; init; }

    [Key(3)]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

[MessagePackObject]
public sealed class MarketOrderRequestMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.MarketOrderRequest;

    [Key(0)]
    public required Guid SellerId { get; init; }

    [Key(1)]
    public required string ItemId { get; init; }

    [Key(2)]
    public required int Quantity { get; init; }

    [Key(3)]
    public required decimal PricePerUnit { get; init; }

    [Key(4)]
    public required string StationId { get; init; }

    [Key(5)]
    public required bool IsBuyOrder { get; init; }
}

[MessagePackObject]
public sealed class VehicleInputMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.VehicleInputRequest;

    [Key(0)] public required Guid PlayerId { get; init; }
    [Key(1)] public required Guid VehicleEntityId { get; init; }
    [Key(2)] public required float Throttle { get; init; }
    [Key(3)] public required float Steering { get; init; }
    [Key(4)] public required bool Brake { get; init; }
}

[MessagePackObject]
public sealed class SpacecraftInputMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.SpacecraftInputRequest;

    [Key(0)] public required Guid PlayerId { get; init; }
    [Key(1)] public required Guid SpacecraftId { get; init; }
    [Key(2)] public required float Thrust { get; init; }
    [Key(3)] public required float Pitch { get; init; }
    [Key(4)] public required float Yaw { get; init; }
    [Key(5)] public required float Roll { get; init; }
}

[MessagePackObject]
public sealed class WeatherUpdateMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.WeatherUpdate;

    [Key(0)] public required byte WeatherId { get; init; }
    [Key(1)] public required float Intensity { get; init; }
    [Key(2)] public required float WindX { get; init; }
    [Key(3)] public required float WindY { get; init; }
    [Key(4)] public required float WindZ { get; init; }
}

[MessagePackObject]
public sealed class NearbyEntitiesMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.NearbyEntitiesUpdate;

    [Key(0)] public required string ZoneId { get; init; }
    [Key(1)] public required float QueryRadius { get; init; }
    [Key(2)] public required int EntityCount { get; init; }
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║          服务端权威：玩法模式切换 & 技能/功能系统 网络消息                           ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

[MessagePackObject]
public sealed class ModeChangeRequestMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.ModeChangeRequest;

    [Key(0)] public required Guid PlayerId { get; init; }
    /// <summary>目标模式 (GameplayMode enum)</summary>
    [Key(1)] public required byte TargetMode { get; init; }
}

[MessagePackObject]
public sealed class ModeChangeResponseMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.ModeChangeResponse;

    [Key(0)] public required bool Success { get; init; }
    [Key(1)] public required byte NewMode { get; init; }
    [Key(2)] public string? ErrorMessage { get; init; }
}

[MessagePackObject]
public sealed class AbilityUseRequestMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.AbilityUseRequest;

    [Key(0)] public required Guid PlayerId { get; init; }
    [Key(1)] public required string AbilityId { get; init; }
    [Key(2)] public Guid? TargetId { get; init; }
    /// <summary>AoE 落点（Ground 类型技能）</summary>
    [Key(3)] public float TargetX { get; init; }
    [Key(4)] public float TargetY { get; init; }
    [Key(5)] public float TargetZ { get; init; }
}

[MessagePackObject]
public sealed class AbilityUseResponseMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.AbilityUseResponse;

    [Key(0)] public required bool Success { get; init; }
    [Key(1)] public required string AbilityId { get; init; }
    [Key(2)] public float EffectValue { get; init; }
    [Key(3)] public float CooldownRemaining { get; init; }
    [Key(4)] public string? ErrorMessage { get; init; }
}

[MessagePackObject]
public sealed class AbilityBarSyncMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.AbilityBarSyncResponse;

    [Key(0)] public required Guid PlayerId { get; init; }
    [Key(1)] public required byte Mode { get; init; }
    /// <summary>MessagePack 序列化的 AbilityBarSyncDto 载荷。</summary>
    [Key(2)] public required byte[] Payload { get; init; }
}

[MessagePackObject]
public sealed class AbilityCooldownUpdateMessage : INetworkMessage
{
    [IgnoreMember]
    public MessageType Type => MessageType.AbilityCooldownUpdate;

    [Key(0)] public required Guid PlayerId { get; init; }
    [Key(1)] public required string AbilityId { get; init; }
    [Key(2)] public required float CooldownRemaining { get; init; }
}
