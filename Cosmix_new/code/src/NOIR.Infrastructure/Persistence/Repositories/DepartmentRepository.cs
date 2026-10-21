namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Department aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class DepartmentRepository : Repository<Department, Guid>, IRepository<Department, Guid>, IScopedService
{
    public DepartmentRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
