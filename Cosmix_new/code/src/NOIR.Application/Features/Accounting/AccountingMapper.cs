namespace NOIR.Application.Features.Accounting;

/// <summary>
/// Mapper for Accounting entities to DTOs.
/// </summary>
public static class AccountingMapper
{
    /// <summary>
    /// Maps an Account entity to a full AccountDto.
    /// </summary>
    public static AccountDto ToDto(Account account, int childCount = 0) => new(
        account.Id,
        account.Code,
        account.Name,
        account.Type,
        account.ParentId,
        account.IsActive,
        account.IsSystem,
        account.Description,
        childCount,
        account.CreatedAt,
        account.ModifiedAt);

    /// <summary>
    /// Maps an Account entity to an AccountListDto.
    /// </summary>
    public static AccountListDto ToListDto(Account account, int childCount = 0) => new(
        account.Id,
        account.Code,
        account.Name,
        account.Type,
        account.ParentId,
        account.IsActive,
        childCount);

    /// <summary>
    /// Maps a JournalLine entity to a JournalLineDto with optional account snapshot.
    /// </summary>
    public static JournalLineDto ToLineDto(JournalLine line, string? accountCode = null, string? accountName = null) => new(
        line.Id,
        line.AccountId,
        accountCode,
        accountName,
        line.Debit,
        line.Credit,
        line.Memo);

    /// <summary>
    /// Maps a JournalEntry entity to a full JournalEntryDto with computed totals.
    /// </summary>
    public static JournalEntryDto ToEntryDto(JournalEntry entry, IReadOnlyList<JournalLineDto>? lines = null)
    {
        var lineDtos = lines != null
            ? new List<JournalLineDto>(lines)
            : entry.Lines.Select(line => ToLineDto(line)).ToList();

        return new JournalEntryDto(
            entry.Id,
            entry.EntryNumber,
            entry.EntryDate,
            entry.Reference,
            entry.Description,
            entry.Status,
            entry.SourceType,
            entry.SourceId,
            entry.Lines.Sum(l => l.Debit),
            entry.Lines.Sum(l => l.Credit),
            entry.PostedByUserId,
            entry.PostedAt,
            entry.VoidReason,
            entry.CreatedAt,
            lineDtos);
    }

    /// <summary>
    /// Maps a JournalEntry entity to a JournalEntryListDto (entry must have Lines loaded).
    /// </summary>
    public static JournalEntryListDto ToEntryListDto(JournalEntry entry) => new(
        entry.Id,
        entry.EntryNumber,
        entry.EntryDate,
        entry.Reference,
        entry.Description,
        entry.Status,
        entry.Lines.Sum(l => l.Debit),
        entry.Lines.Sum(l => l.Credit),
        entry.CreatedAt);
}
