namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Project aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class ProjectRepository : Repository<Project, Guid>, IRepository<Project, Guid>, IScopedService
{
    public ProjectRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
