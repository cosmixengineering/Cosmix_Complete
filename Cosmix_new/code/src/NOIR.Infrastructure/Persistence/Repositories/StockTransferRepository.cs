namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for StockTransfer aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class StockTransferRepository : Repository<StockTransfer, Guid>, IRepository<StockTransfer, Guid>, IScopedService
{
    public StockTransferRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
