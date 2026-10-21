namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for CrmCompany aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class CrmCompanyRepository : Repository<CrmCompany, Guid>, IRepository<CrmCompany, Guid>, IScopedService
{
    public CrmCompanyRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
