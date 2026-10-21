namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Pipeline aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class PipelineRepository : Repository<Pipeline, Guid>, IRepository<Pipeline, Guid>, IScopedService
{
    public PipelineRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
