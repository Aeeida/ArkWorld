using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IInstanceGrain : IGrainWithGuidKey
{
    Task InitializeAsync(string templateId, Guid leaderId);
    Task<bool> AddPlayerAsync(Guid playerId);
    Task<bool> RemovePlayerAsync(Guid playerId);
    Task CompleteAsync();
    Task<InstanceGrainState> GetStateAsync();
}

[GenerateSerializer]
public sealed class InstanceGrainState
{
    [Id(0)] public string TemplateId { get; set; } = string.Empty;
    [Id(1)] public Guid LeaderId { get; set; }
    [Id(2)] public string Difficulty { get; set; } = "Normal";
    [Id(3)] public List<Guid> PlayerIds { get; set; } = [];
    [Id(4)] public bool IsCompleted { get; set; }
    [Id(5)] public DateTime CreatedAt { get; set; }
}
