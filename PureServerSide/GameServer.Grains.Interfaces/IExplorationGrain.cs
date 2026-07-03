using Orleans;

namespace GameServer.Grains.Interfaces;

public interface IExplorationGrain : IGrainWithStringKey
{
    Task<ExplorationGrainState> GetStateAsync();
    Task<ScanResult> ScanAsync(Guid playerId);
    Task<HarvestResult> HarvestResourceAsync(Guid playerId, string resourceNodeId);
    Task<Guid> TriggerDynamicEventAsync(string eventType);
    Task RefreshResourceNodesAsync();
}

[GenerateSerializer]
public sealed class ExplorationGrainState
{
    [Id(0)] public string SolarSystemId { get; set; } = string.Empty;
    [Id(1)] public List<ResourceNode> ResourceNodes { get; set; } = [];
    [Id(2)] public List<ExplorationSite> Sites { get; set; } = [];
    [Id(3)] public List<ActiveWorldEvent> ActiveEvents { get; set; } = [];
}

[GenerateSerializer]
public sealed class ResourceNode
{
    [Id(0)] public string NodeId { get; set; } = string.Empty;
    [Id(1)] public string ResourceType { get; set; } = string.Empty;
    [Id(2)] public int RemainingQuantity { get; set; }
    [Id(3)] public double X { get; set; }
    [Id(4)] public double Y { get; set; }
    [Id(5)] public double Z { get; set; }
}

[GenerateSerializer]
public sealed class ExplorationSite
{
    [Id(0)] public string SiteId { get; set; } = string.Empty;
    [Id(1)] public string Type { get; set; } = string.Empty;
    [Id(2)] public string Difficulty { get; set; } = "Normal";
    [Id(3)] public double X { get; set; }
    [Id(4)] public double Y { get; set; }
    [Id(5)] public double Z { get; set; }
}

[GenerateSerializer]
public sealed class ActiveWorldEvent
{
    [Id(0)] public Guid EventId { get; set; }
    [Id(1)] public string EventType { get; set; } = string.Empty;
    [Id(2)] public DateTime StartsAt { get; set; }
    [Id(3)] public DateTime ExpiresAt { get; set; }
}

[GenerateSerializer]
public sealed class ScanResult
{
    [Id(0)] public int PlayerCount { get; set; }
    [Id(1)] public List<ResourceNode> ResourceNodes { get; set; } = [];
    [Id(2)] public List<string> AnomalyIds { get; set; } = [];
}

[GenerateSerializer]
public sealed class HarvestResult
{
    [Id(0)] public string ResourceNodeId { get; set; } = string.Empty;
    [Id(1)] public string ResourceType { get; set; } = string.Empty;
    [Id(2)] public int QuantityHarvested { get; set; }
    [Id(3)] public bool NodeDepleted { get; set; }
}
