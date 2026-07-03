using GameServer.Domain.Core;

namespace GameServer.Infrastructure.Persistence;

public interface IUnitOfWork : GameServer.Application.Core.Behaviors.IUnitOfWork, IDisposable
{
}

public class GenericRepository<TEntity, TId>(GameDbContext context) : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        await context.Set<TEntity>().FindAsync([id], cancellationToken: ct);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        await Task.FromResult<IReadOnlyList<TEntity>>(
            context.Set<TEntity>().ToList().AsReadOnly());

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await context.Set<TEntity>().AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        context.Set<TEntity>().Update(entity);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TId id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is not null)
        {
            context.Set<TEntity>().Remove(entity);
            await context.SaveChangesAsync(ct);
        }
    }
}
