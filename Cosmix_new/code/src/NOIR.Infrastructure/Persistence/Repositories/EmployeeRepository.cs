namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Employee aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class EmployeeRepository : Repository<Employee, Guid>, IRepository<Employee, Guid>, IScopedService
{
    public EmployeeRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
