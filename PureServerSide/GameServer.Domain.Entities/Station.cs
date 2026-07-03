using GameServer.Domain.Core;

namespace GameServer.Domain.Entities;

public class Station : AggregateRoot<Guid>
{
    public required string Name { get; init; }
    public required string SolarSystemId { get; init; }
    public Guid? OwnerId { get; set; }
    public double SecurityLevel { get; init; } = 1.0;
    public bool HasMarket { get; init; } = true;
    public bool HasRepairShop { get; init; } = true;
}

public class Guild : AggregateRoot<Guid>
{
    public required string Name { get; init; }
    public Guid FounderId { get; init; }
    public DateTime FoundedAt { get; init; } = DateTime.UtcNow;
    private readonly List<Guid> _memberIds = [];
    public IReadOnlyList<Guid> MemberIds => _memberIds.AsReadOnly();

    public void AddMember(Guid playerId)
    {
        if (!_memberIds.Contains(playerId))
        {
            _memberIds.Add(playerId);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public bool RemoveMember(Guid playerId)
    {
        var removed = _memberIds.Remove(playerId);
        if (removed) UpdatedAt = DateTime.UtcNow;
        return removed;
    }
}

public class Fleet : AggregateRoot<Guid>
{
    public Guid LeaderId { get; init; }
    public required string Name { get; init; }
    private readonly List<Guid> _memberIds = [];
    public IReadOnlyList<Guid> MemberIds => _memberIds.AsReadOnly();

    public void AddMember(Guid playerId)
    {
        if (!_memberIds.Contains(playerId))
        {
            _memberIds.Add(playerId);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public bool RemoveMember(Guid playerId)
    {
        var removed = _memberIds.Remove(playerId);
        if (removed) UpdatedAt = DateTime.UtcNow;
        return removed;
    }
}

public class InventoryItem : AggregateRoot<Guid>
{
    public Guid OwnerId { get; init; }
    public required string ItemId { get; init; }
    public int Quantity { get; set; }
    public required string Rarity { get; init; }

    public void AddQuantity(int amount)
    {
        Quantity += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool RemoveQuantity(int amount)
    {
        if (Quantity < amount) return false;
        Quantity -= amount;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}

public class GameInstance : AggregateRoot<Guid>
{
    public required string TemplateId { get; init; }
    public Guid LeaderId { get; init; }
    public required string Difficulty { get; init; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public bool IsCompleted { get; set; }
    private readonly List<Guid> _playerIds = [];
    public IReadOnlyList<Guid> PlayerIds => _playerIds.AsReadOnly();

    public void AddPlayer(Guid playerId)
    {
        if (!_playerIds.Contains(playerId))
            _playerIds.Add(playerId);
    }

    public void Complete()
    {
        IsCompleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
