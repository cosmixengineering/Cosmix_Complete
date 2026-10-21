namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for ProductionOrder aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class ProductionOrderRepository : Repository<ProductionOrder, Guid>, IRepository<ProductionOrder, Guid>, IScopedService
{
    public ProductionOrderRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
