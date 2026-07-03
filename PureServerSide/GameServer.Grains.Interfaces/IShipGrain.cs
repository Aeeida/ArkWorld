using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IShipGrain : IGrainWithGuidKey
{
    Task InitializeAsync(Guid ownerId, string shipType, double hull, double shield, double armor);
    Task<double> ApplyDamageAsync(double damage);
    Task RepairShieldAsync(double amount);
    Task<ShipGrainState> GetStateAsync();
    Task<bool> IsDestroyedAsync();
    Task SetLocationAsync(string solarSystemId);
}

[GenerateSerializer]
public sealed class ShipGrainState
{
    [Id(0)] public Guid OwnerId { get; set; }
    [Id(1)] public string ShipType { get; set; } = string.Empty;
    [Id(2)] public double HullPoints { get; set; }
    [Id(3)] public double MaxHull { get; set; }
    [Id(4)] public double ShieldPoints { get; set; }
    [Id(5)] public double MaxShield { get; set; }
    [Id(6)] public double ArmorPoints { get; set; }
    [Id(7)] public double MaxArmor { get; set; }
    [Id(8)] public string? CurrentSolarSystemId { get; set; }
    [Id(9)] public bool IsDestroyed { get; set; }
}
