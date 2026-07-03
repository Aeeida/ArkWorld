using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IFleetGrain : IGrainWithGuidKey
{
    Task InitializeAsync(Guid leaderId, string name);
    Task<bool> AddMemberAsync(Guid playerId);
    Task<bool> RemoveMemberAsync(Guid playerId);
    Task<FleetGrainState> GetStateAsync();
    Task DisbandAsync();
}

[GenerateSerializer]
public sealed class FleetGrainState
{
    [Id(0)] public Guid LeaderId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public List<Guid> MemberIds { get; set; } = [];
    [Id(3)] public bool IsDisbanded { get; set; }
}
