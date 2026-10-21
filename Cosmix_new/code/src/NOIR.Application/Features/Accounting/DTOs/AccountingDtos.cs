namespace NOIR.Application.Features.Accounting.DTOs;

/// <summary>
/// Full account details DTO.
/// </summary>
public sealed record AccountDto(
    Guid Id,
    string Code,
    string Name,
    AccountType Type,
    Guid? ParentId,
    bool IsActive,
    bool IsSystem,
    string? Description,
    int ChildCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// Account list item DTO (minimal data for listings).
/// </summary>
public sealed record AccountListDto(
    Guid Id,
    string Code,
    string Name,
    AccountType Type,
    Guid? ParentId,
    bool IsActive,
    int ChildCount);

/// <summary>
/// Full journal entry DTO with lines.
/// </summary>
public sealed record JournalEntryDto(
    Guid Id,
    string EntryNumber,
    DateOnly EntryDate,
    string? Reference,
    string? Description,
    JournalEntryStatus Status,
    string? SourceType,
    Guid? SourceId,
    decimal TotalDebit,
    decimal TotalCredit,
    string? PostedByUserId,
    DateTimeOffset? PostedAt,
    string? VoidReason,
    DateTimeOffset CreatedAt,
    List<JournalLineDto> Lines);

/// <summary>
/// Journal entry list item DTO (minimal data for listings).
/// </summary>
public sealed record JournalEntryListDto(
    Guid Id,
    string EntryNumber,
    DateOnly EntryDate,
    string? Reference,
    string? Description,
    JournalEntryStatus Status,
    decimal TotalDebit,
    decimal TotalCredit,
    DateTimeOffset CreatedAt);

/// <summary>
/// Journal line DTO with optional account snapshot.
/// </summary>
public sealed record JournalLineDto(
    Guid Id,
    Guid AccountId,
    string? AccountCode,
    string? AccountName,
    decimal Debit,
    decimal Credit,
    string? Memo);

/// <summary>
/// Request to create a new account.
/// </summary>
public sealed record CreateAccountRequest(
    string Code,
    string Name,
    AccountType Type,
    Guid? ParentId,
    string? Description);

/// <summary>
/// Request to update an existing account.
/// </summary>
public sealed record UpdateAccountRequest(
    string Code,
    string Name,
    Guid? ParentId,
    string? Description);

/// <summary>
/// Request to create a new journal entry with its lines.
/// </summary>
public sealed record CreateJournalEntryRequest(
    DateOnly EntryDate,
    string? Reference,
    string? Description,
    List<CreateJournalLineRequest> Lines);

/// <summary>
/// Request to create a single journal line.
/// </summary>
public sealed record CreateJournalLineRequest(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Memo);
