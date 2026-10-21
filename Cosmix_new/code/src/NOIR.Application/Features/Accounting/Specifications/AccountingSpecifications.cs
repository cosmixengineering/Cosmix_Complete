namespace NOIR.Application.Features.Accounting.Specifications;

/// <summary>
/// Specification to find an account by ID.
/// </summary>
public sealed class AccountByIdSpec : Specification<Account>
{
    public AccountByIdSpec(Guid id)
    {
        Query.Where(a => a.Id == id)
             .TagWith("GetAccountById");
    }
}

/// <summary>
/// Specification to find an account by ID for update (with tracking).
/// </summary>
public sealed class AccountByIdForUpdateSpec : Specification<Account>
{
    public AccountByIdForUpdateSpec(Guid id)
    {
        Query.Where(a => a.Id == id)
             .AsTracking()
             .TagWith("GetAccountByIdForUpdate");
    }
}

/// <summary>
/// Specification to check if an account code exists (for uniqueness validation).
/// </summary>
public sealed class AccountCodeExistsSpec : Specification<Account>
{
    public AccountCodeExistsSpec(string code, Guid? excludeId = null)
    {
        Query.Where(a => a.Code == code);

        if (excludeId.HasValue)
        {
            Query.Where(a => a.Id != excludeId.Value);
        }

        Query.TagWith("CheckAccountCodeExists");
    }
}

/// <summary>
/// Specification to find multiple accounts by their IDs.
/// </summary>
public sealed class AccountsByIdsSpec : Specification<Account>
{
    public AccountsByIdsSpec(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        Query.Where(a => idList.Contains(a.Id))
             .TagWith("GetAccountsByIds");
    }
}

/// <summary>
/// Specification to find accounts whose parent is one of the given IDs.
/// Used to compute child counts efficiently for a page of accounts.
/// </summary>
public sealed class AccountsByParentIdsSpec : Specification<Account>
{
    public AccountsByParentIdsSpec(IEnumerable<Guid> parentIds)
    {
        var parentIdList = parentIds.ToList();
        Query.Where(a => a.ParentId != null && parentIdList.Contains(a.ParentId.Value))
             .TagWith("GetAccountsByParentIds");
    }
}

/// <summary>
/// Specification for paginated account listing with filters.
/// </summary>
public sealed class AccountsPagedSpec : Specification<Account>
{
    public AccountsPagedSpec(string? search, AccountType? type, bool? isActive, int page, int pageSize)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(a => a.Code.Contains(search) || a.Name.Contains(search));
        }

        if (type.HasValue)
        {
            Query.Where(a => a.Type == type.Value);
        }

        if (isActive.HasValue)
        {
            Query.Where(a => a.IsActive == isActive.Value);
        }

        Query.OrderBy(a => a.Code)
             .Skip((page - 1) * pageSize)
             .Take(pageSize)
             .TagWith("GetAccountsPaged");
    }
}

/// <summary>
/// Specification for counting accounts with filters (for pagination).
/// </summary>
public sealed class AccountsCountSpec : Specification<Account>
{
    public AccountsCountSpec(string? search, AccountType? type, bool? isActive)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(a => a.Code.Contains(search) || a.Name.Contains(search));
        }

        if (type.HasValue)
        {
            Query.Where(a => a.Type == type.Value);
        }

        if (isActive.HasValue)
        {
            Query.Where(a => a.IsActive == isActive.Value);
        }

        Query.TagWith("CountAccounts");
    }
}

/// <summary>
/// Specification to find a journal entry by ID (including lines).
/// </summary>
public sealed class JournalEntryByIdSpec : Specification<JournalEntry>
{
    public JournalEntryByIdSpec(Guid id)
    {
        Query.Where(e => e.Id == id)
             .Include(e => e.Lines)
             .TagWith("GetJournalEntryById");
    }
}

/// <summary>
/// Specification to find a journal entry by ID for update (with tracking and lines).
/// </summary>
public sealed class JournalEntryByIdForUpdateSpec : Specification<JournalEntry>
{
    public JournalEntryByIdForUpdateSpec(Guid id)
    {
        Query.Where(e => e.Id == id)
             .Include(e => e.Lines)
             .AsTracking()
             .TagWith("GetJournalEntryByIdForUpdate");
    }
}

/// <summary>
/// Specification to check if a journal entry number already exists.
/// </summary>
public sealed class JournalEntryNumberExistsSpec : Specification<JournalEntry>
{
    public JournalEntryNumberExistsSpec(string entryNumber)
    {
        Query.Where(e => e.EntryNumber == entryNumber)
             .TagWith("CheckJournalEntryNumberExists");
    }
}

/// <summary>
/// Specification to check if any journal entry references a given account.
/// </summary>
public sealed class JournalEntriesUsingAccountSpec : Specification<JournalEntry>
{
    public JournalEntriesUsingAccountSpec(Guid accountId)
    {
        Query.Where(e => e.Lines.Any(l => l.AccountId == accountId))
             .TagWith("CheckJournalEntriesUsingAccount");
    }
}

/// <summary>
/// Specification for paginated journal entry listing with filters.
/// Includes lines so totals can be computed without extra queries.
/// </summary>
public sealed class JournalEntriesByFilterSpec : Specification<JournalEntry>
{
    public JournalEntriesByFilterSpec(
        JournalEntryStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search,
        int page,
        int pageSize)
    {
        ApplyFilters(status, fromDate, toDate, search);

        Query.Include(e => e.Lines)
             .OrderByDescending(e => e.EntryDate)
             .ThenByDescending(e => e.EntryNumber)
             .Skip((page - 1) * pageSize)
             .Take(pageSize)
             .TagWith("GetJournalEntriesPaged");
    }

    private void ApplyFilters(
        JournalEntryStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search)
    {
        if (status.HasValue)
        {
            Query.Where(e => e.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            Query.Where(e => e.EntryDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            Query.Where(e => e.EntryDate <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(e =>
                e.EntryNumber.Contains(search) ||
                (e.Reference != null && e.Reference.Contains(search)) ||
                (e.Description != null && e.Description.Contains(search)));
        }
    }
}

/// <summary>
/// Specification for counting journal entries with filters (for pagination).
/// </summary>
public sealed class JournalEntriesCountSpec : Specification<JournalEntry>
{
    public JournalEntriesCountSpec(
        JournalEntryStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search)
    {
        if (status.HasValue)
        {
            Query.Where(e => e.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            Query.Where(e => e.EntryDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            Query.Where(e => e.EntryDate <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(e =>
                e.EntryNumber.Contains(search) ||
                (e.Reference != null && e.Reference.Contains(search)) ||
                (e.Description != null && e.Description.Contains(search)));
        }

        Query.TagWith("CountJournalEntries");
    }
}
