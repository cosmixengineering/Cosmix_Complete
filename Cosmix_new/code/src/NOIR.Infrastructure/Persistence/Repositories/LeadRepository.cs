namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Lead aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class LeadRepository : Repository<Lead, Guid>, IRepository<Lead, Guid>, IScopedService
{
    public LeadRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
