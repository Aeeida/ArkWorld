using GameServer.Domain.Core;
using GameServer.Domain.Events;

namespace GameServer.Domain.Entities;

public class Player : AggregateRoot<Guid>
{
    public string Name
    {
        get => field;
        set => field = !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Player name cannot be empty.");
    } = string.Empty;

    public int Level { get; private set; } = 1;
    public long Experience { get; private set; }
    public required string Faction { get; init; }
    public string CharacterClass { get; init; } = string.Empty;
    public double Health { get; set; } = 100;
    public double MaxHealth { get; set; } = 100;
    public Guid? GuildId { get; set; }
    public string? CurrentWorldId { get; set; }
    public decimal WalletBalance { get; private set; }

    // ── 分层宇宙位置 ──
    /// <summary>当前所在位置节点 ID（LocationNode 表主键）。</summary>
    public long? CurrentLocationId { get; set; }

    /// <summary>本地 X 坐标（相对 CurrentLocationId 的局部坐标）。</summary>
    public double LocalPositionX { get; set; }

    /// <summary>本地 Y 坐标。</summary>
    public double LocalPositionY { get; set; }

    /// <summary>本地 Z 坐标。</summary>
    public double LocalPositionZ { get; set; }

    /// <summary>当前所在星系 ID（快速查询缓存，避免递归遍历父级）。</summary>
    public long? SolarSystemId { get; set; }

    private readonly List<string> _activeBuffIds = [];
    public IReadOnlyList<string> ActiveBuffIds => _activeBuffIds.AsReadOnly();

    public static Player Create(Guid id, string name, string faction, string characterClass)
    {
        var player = new Player
        {
            Id = id,
            Name = name,
            Faction = faction,
            CharacterClass = characterClass
        };
        player.RaiseDomainEvent(new PlayerCreatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, id, name, faction));
        return player;
    }

    public bool GainExperience(long amount)
    {
        Experience += amount;
        var leveledUp = false;
        var oldLevel = Level;
        while (Experience >= ExperienceForNextLevel())
        {
            Experience -= ExperienceForNextLevel();
            Level++;
            MaxHealth = 100 + (Level - 1) * 20;
            Health = MaxHealth;
            leveledUp = true;
        }
        if (leveledUp)
        {
            RaiseDomainEvent(new PlayerLeveledUpEvent(
                Guid.NewGuid(), DateTime.UtcNow, Id, oldLevel, Level));
        }
        UpdatedAt = DateTime.UtcNow;
        return leveledUp;
    }

    public double TakeDamage(double damage)
    {
        var actual = Math.Min(Health, damage);
        Health = Math.Max(0, Health - damage);
        UpdatedAt = DateTime.UtcNow;
        return actual;
    }

    public void Heal(double amount)
    {
        Health = Math.Min(MaxHealth, Health + amount);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddBuff(string buffId)
    {
        if (!_activeBuffIds.Contains(buffId))
            _activeBuffIds.Add(buffId);
    }

    public void RemoveBuff(string buffId) => _activeBuffIds.Remove(buffId);

    public void Credit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Credit amount must be positive.");
        WalletBalance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool Debit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Debit amount must be positive.");
        if (WalletBalance < amount) return false;
        WalletBalance -= amount;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void EnterWorld(string worldId)
    {
        CurrentWorldId = worldId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LeaveWorld()
    {
        CurrentWorldId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsDead => Health <= 0;

    private long ExperienceForNextLevel() => Level * 1000L;
}
