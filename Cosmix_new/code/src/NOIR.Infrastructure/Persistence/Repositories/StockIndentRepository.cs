namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for StockIndent aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class StockIndentRepository : Repository<StockIndent, Guid>, IRepository<StockIndent, Guid>, IScopedService
{
    public StockIndentRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
