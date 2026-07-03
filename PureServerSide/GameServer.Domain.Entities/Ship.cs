using GameServer.Domain.Core;

namespace GameServer.Domain.Entities;

public class Ship : AggregateRoot<Guid>
{
    public required string ShipType { get; init; }
    public Guid OwnerId { get; init; }

    public double HullPoints
    {
        get => field;
        set => field = Math.Max(0, value);
    } = 100;

    public double ShieldPoints
    {
        get => field;
        set => field = Math.Max(0, value);
    } = 50;

    public double ArmorPoints
    {
        get => field;
        set => field = Math.Max(0, value);
    } = 75;

    public double MaxHull { get; init; } = 100;
    public double MaxShield { get; init; } = 50;
    public double MaxArmor { get; init; } = 75;
    public string? CurrentSolarSystemId { get; set; }
    public bool IsDestroyed => HullPoints <= 0;

    public static Ship Create(Guid id, Guid ownerId, string shipType, double hull, double shield, double armor)
    {
        return new Ship
        {
            Id = id,
            OwnerId = ownerId,
            ShipType = shipType,
            HullPoints = hull,
            MaxHull = hull,
            ShieldPoints = shield,
            MaxShield = shield,
            ArmorPoints = armor,
            MaxArmor = armor
        };
    }

    public double ApplyDamage(double damage)
    {
        var remaining = damage;

        if (ShieldPoints > 0)
        {
            var shieldDamage = Math.Min(ShieldPoints, remaining);
            ShieldPoints -= shieldDamage;
            remaining -= shieldDamage;
        }

        if (remaining > 0 && ArmorPoints > 0)
        {
            var armorDamage = Math.Min(ArmorPoints, remaining);
            ArmorPoints -= armorDamage;
            remaining -= armorDamage;
        }

        if (remaining > 0)
        {
            HullPoints -= remaining;
        }

        UpdatedAt = DateTime.UtcNow;
        return damage - remaining;
    }

    public void RepairShield(double amount)
    {
        ShieldPoints = Math.Min(MaxShield, ShieldPoints + amount);
        UpdatedAt = DateTime.UtcNow;
    }
}
