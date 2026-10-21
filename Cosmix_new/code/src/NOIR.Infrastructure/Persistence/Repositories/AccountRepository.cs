namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Account aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class AccountRepository : Repository<Account, Guid>, IRepository<Account, Guid>, IScopedService
{
    public AccountRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
