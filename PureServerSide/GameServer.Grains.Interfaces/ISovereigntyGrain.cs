using Orleans;

namespace GameServer.Grains.Interfaces;

public interface ISovereigntyGrain : IGrainWithStringKey
{
    Task<SovereigntyGrainState> GetStateAsync();
    Task<bool> ClaimAsync(Guid allianceId);
    Task<Guid> ContestAsync(Guid attackingAllianceId);
    Task<bool> ResolveContestAsync(Guid contestId, Guid winnerId);
    Task<Guid> PlaceStructureAsync(Guid allianceId, string structureType);
    Task<bool> DestroyStructureAsync(Guid structureId);
    Task<bool> SetTaxRateAsync(Guid allianceId, decimal taxRate);
}

[GenerateSerializer]
public sealed class SovereigntyGrainState
{
    [Id(0)] public string SolarSystemId { get; set; } = string.Empty;
    [Id(1)] public Guid? OwnerAllianceId { get; set; }
    [Id(2)] public DateTime? ClaimedAt { get; set; }
    [Id(3)] public string Status { get; set; } = "Unclaimed";
    [Id(4)] public decimal TaxRate { get; set; }
    [Id(5)] public List<StructureState> Structures { get; set; } = [];
    [Id(6)] public List<SovereigntyContest> ActiveContests { get; set; } = [];
}

[GenerateSerializer]
public sealed class StructureState
{
    [Id(0)] public Guid StructureId { get; set; }
    [Id(1)] public string StructureType { get; set; } = string.Empty;
    [Id(2)] public Guid OwnerAllianceId { get; set; }
    [Id(3)] public double HealthPercent { get; set; } = 100.0;
    [Id(4)] public string Status { get; set; } = "Online";
    [Id(5)] public DateTime BuiltAt { get; set; }
}

[GenerateSerializer]
public sealed class SovereigntyContest
{
    [Id(0)] public Guid ContestId { get; set; }
    [Id(1)] public Guid AttackingAllianceId { get; set; }
    [Id(2)] public string Status { get; set; } = "InProgress";
    [Id(3)] public DateTime StartedAt { get; set; }
    [Id(4)] public DateTime EndsAt { get; set; }
}
