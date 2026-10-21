namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for BillOfMaterial aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class BillOfMaterialRepository : Repository<BillOfMaterial, Guid>, IRepository<BillOfMaterial, Guid>, IScopedService
{
    public BillOfMaterialRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
