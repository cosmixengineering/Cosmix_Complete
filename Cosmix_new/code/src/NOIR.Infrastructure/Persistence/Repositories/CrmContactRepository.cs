namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for CrmContact aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class CrmContactRepository : Repository<CrmContact, Guid>, IRepository<CrmContact, Guid>, IScopedService
{
    public CrmContactRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
