namespace NOIR.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for JournalEntry aggregate root.
/// Registered as scoped service via IScopedService marker interface.
/// </summary>
public sealed class JournalEntryRepository : Repository<JournalEntry, Guid>, IRepository<JournalEntry, Guid>, IScopedService
{
    public JournalEntryRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}
