using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IGuildGrain : IGrainWithGuidKey
{
    Task InitializeAsync(Guid founderId, string name);
    Task<bool> AddMemberAsync(Guid playerId);
    Task<bool> RemoveMemberAsync(Guid playerId);
    Task<GuildGrainState> GetStateAsync();
}

[GenerateSerializer]
public sealed class GuildGrainState
{
    [Id(0)] public Guid FounderId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public List<Guid> MemberIds { get; set; } = [];
}
