using MessagePack;

namespace Game.Shared.Protocols;

[Union(0, typeof(Messages.LoginRequestMessage))]
[Union(1, typeof(Messages.LoginResponseMessage))]
[Union(2, typeof(Messages.EnterWorldRequestMessage))]
[Union(3, typeof(Messages.EnterWorldResponseMessage))]
[Union(4, typeof(Messages.PlayerMoveMessage))]
[Union(5, typeof(Messages.CombatActionMessage))]
[Union(6, typeof(Messages.ChatMessage))]
[Union(7, typeof(Messages.MarketOrderRequestMessage))]
[Union(8, typeof(Messages.VehicleInputMessage))]
[Union(9, typeof(Messages.SpacecraftInputMessage))]
[Union(10, typeof(Messages.WeatherUpdateMessage))]
[Union(11, typeof(Messages.NearbyEntitiesMessage))]
public interface INetworkMessage
{
    MessageType Type { get; }
}
