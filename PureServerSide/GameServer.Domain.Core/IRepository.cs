namespace GameServer.Domain.Core;

public interface IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TId id, CancellationToken ct = default);
}
