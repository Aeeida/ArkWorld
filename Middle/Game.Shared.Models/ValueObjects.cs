using MessagePack;

namespace Game.Shared.Models;

[MessagePackObject]
public sealed record Currency([property: Key(0)] decimal Amount, [property: Key(1)] string CurrencyCode = "ISK")
{
    public static readonly Currency Zero = new(0m);

    public static Currency operator +(Currency a, Currency b) =>
        a.CurrencyCode == b.CurrencyCode
            ? new(a.Amount + b.Amount, a.CurrencyCode)
            : throw new InvalidOperationException("Cannot add different currencies.");

    public static Currency operator -(Currency a, Currency b) =>
        a.CurrencyCode == b.CurrencyCode
            ? new(a.Amount - b.Amount, a.CurrencyCode)
            : throw new InvalidOperationException("Cannot subtract different currencies.");

    public static Currency operator *(Currency c, int quantity) => new(c.Amount * quantity, c.CurrencyCode);

    public bool CanAfford(Currency cost) => Amount >= cost.Amount && CurrencyCode == cost.CurrencyCode;
}

[MessagePackObject]
public sealed record SkillLevel([property: Key(0)] string SkillId, [property: Key(1)] int Level, [property: Key(2)] long TrainingEndTicks = 0)
{
    public const int MaxLevel = 5;

    [IgnoreMember]
    public bool IsTraining => TrainingEndTicks > 0 && DateTimeOffset.UtcNow.Ticks < TrainingEndTicks;

    [IgnoreMember]
    public bool IsMaxed => Level >= MaxLevel;

    public SkillLevel WithLevel(int newLevel) => this with { Level = Math.Clamp(newLevel, 0, MaxLevel) };
}

[MessagePackObject]
public sealed record DamageInfo(
    [property: Key(0)] double ShieldDamage,
    [property: Key(1)] double ArmorDamage,
    [property: Key(2)] double HullDamage,
    [property: Key(3)] double TotalDamage,
    [property: Key(4)] bool IsKillingBlow);

[MessagePackObject]
public sealed record BuffEffect(
    [property: Key(0)] string BuffId,
    [property: Key(1)] string Name,
    [property: Key(2)] double Magnitude,
    [property: Key(3)] long ExpiresAtTicks)
{
    [IgnoreMember]
    public bool HasExpired => DateTimeOffset.UtcNow.Ticks >= ExpiresAtTicks;
}
