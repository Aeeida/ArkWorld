namespace GameServer.Domain.Core;

public abstract record ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(0, (hash, component) =>
                HashCode.Combine(hash, component?.GetHashCode() ?? 0));
    }
}
