namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for CrmActivity aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class CrmActivityRepository : Repository<CrmActivity, Guid>, IRepository<CrmActivity, Guid>, IScopedService
{
    public CrmActivityRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
