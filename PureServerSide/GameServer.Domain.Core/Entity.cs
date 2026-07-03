namespace GameServer.Domain.Core;

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public DateTime UpdatedAt
    {
        get => field;
        protected set => field = value;
    } = DateTime.UtcNow;

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();
}
