namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Boq aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class BoqRepository : Repository<Boq, Guid>, IRepository<Boq, Guid>, IScopedService
{
    public BoqRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
