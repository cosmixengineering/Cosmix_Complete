namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for ProjectTask aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class ProjectTaskRepository : Repository<ProjectTask, Guid>, IRepository<ProjectTask, Guid>, IScopedService
{
    public ProjectTaskRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
