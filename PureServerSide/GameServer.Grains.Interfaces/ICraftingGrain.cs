using Orleans;

namespace GameServer.Grains.Interfaces;

public interface ICraftingGrain : IGrainWithGuidKey
{
    Task<CraftingGrainState> GetStateAsync();
    Task<CraftingJob> StartCraftingAsync(string blueprintId, int quantity);
    Task<bool> CancelCraftingAsync(Guid jobId);
    Task<bool> CompleteCraftingAsync(Guid jobId);
    Task<bool> LearnBlueprintAsync(string blueprintId);
}

[GenerateSerializer]
public sealed class CraftingGrainState
{
    [Id(0)] public HashSet<string> LearnedBlueprints { get; set; } = [];
    [Id(1)] public List<CraftingJob> ActiveJobs { get; set; } = [];
    [Id(2)] public List<CraftingJob> CompletedJobs { get; set; } = [];
}

[GenerateSerializer]
public sealed class CraftingJob
{
    [Id(0)] public Guid JobId { get; set; }
    [Id(1)] public string BlueprintId { get; set; } = string.Empty;
    [Id(2)] public int Quantity { get; set; }
    [Id(3)] public string Status { get; set; } = "Pending";
    [Id(4)] public DateTime StartedAt { get; set; }
    [Id(5)] public DateTime CompletesAt { get; set; }
}
