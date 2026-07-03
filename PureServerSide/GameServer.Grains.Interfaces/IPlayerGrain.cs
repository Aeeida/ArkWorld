using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IPlayerGrain : IGrainWithGuidKey
{
    Task<string> GetNameAsync();
    Task SetNameAsync(string name);
    Task<int> GetLevelAsync();
    Task<bool> GainExperienceAsync(long amount);
    Task<bool> TryLevelUpAsync();
    Task<double> GetHealthAsync();
    Task TakeDamageAsync(double damage);
    Task HealAsync(double amount);
    Task SetWorldAsync(string? worldId);
    Task<string?> GetWorldAsync();
    Task SetPositionAsync(double x, double y, double z, float rotation);
    Task SetZoneAsync(string? zoneId);
    Task SetEquippedWeaponAsync(int weaponDefId);
    Task<bool> RespawnAsync();
    Task<PlayerGrainState> GetStateAsync();
}

[GenerateSerializer]
public sealed class PlayerGrainState
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public int Level { get; set; } = 1;
    [Id(2)] public long Experience { get; set; }
    [Id(3)] public string Faction { get; set; } = string.Empty;
    [Id(4)] public string CharacterClass { get; set; } = string.Empty;
    [Id(5)] public double Health { get; set; } = 100;
    [Id(6)] public double MaxHealth { get; set; } = 100;
    [Id(7)] public string? CurrentWorldId { get; set; }
    [Id(8)] public Guid? GuildId { get; set; }
    [Id(9)] public double PositionX { get; set; }
    [Id(10)] public double PositionY { get; set; }
    [Id(11)] public double PositionZ { get; set; }
    [Id(12)] public float Rotation { get; set; }
    [Id(13)] public string? CurrentZoneId { get; set; }
    [Id(14)] public int EquippedWeaponDefId { get; set; }
}
