using Orleans;

namespace GameServer.Grains.Interfaces;

public interface ISolarSystemGrain : IGrainWithStringKey
{
    Task<IReadOnlyList<Guid>> GetPlayersInSystemAsync();
    Task PlayerEnteredAsync(Guid playerId);
    Task PlayerLeftAsync(Guid playerId);
    Task<SolarSystemGrainState> GetStateAsync();
    Task SetSovereigntyAsync(Guid? allianceId);
}

[GenerateSerializer]
public sealed class SolarSystemGrainState
{
    [Id(0)] public string SystemId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public double SecurityLevel { get; set; } = 1.0;
    [Id(3)] public List<Guid> PlayerIds { get; set; } = [];
    [Id(4)] public Guid? SovereignAllianceId { get; set; }
}
