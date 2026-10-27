using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.StatutoryContributions;

public sealed record StatutoryContributionEmployeeItemDto(
    Guid EmployeeId,
    decimal EmployeeContribution,
    decimal EmployerContribution,
    string? RegistrationNumber = null);

public sealed record CreateContributionRequest(
    StatutoryScheme Scheme,
    DateOnly ContributionMonth,
    decimal? EmployeeContributionAmount = null,
    decimal? EmployerContributionAmount = null,
    string Currency = "",
    string? PayableAccountCode = null,
    string? Notes = null,
    string? EmployerRegistrationNumber = null,
    IReadOnlyList<StatutoryContributionEmployeeItemDto>? EmployeeItems = null,
    IReadOnlyList<ContributionEntryRequest>? Entries = null,
    Guid? ObligationId = null,
    string Reason = "");

public sealed record ContributionResponse(
    Guid Id,
    string Scheme,
    DateOnly ContributionMonth,
    decimal EmployeeContributionAmount,
    decimal EmployerContributionAmount,
    decimal TotalContributionAmount,
    string Currency,
    string State,
    string? PayableAccountCode,
    string? Notes,
    string? EmployerRegistrationNumber,
    DateTimeOffset CreatedAt,
    string CreatedByUserId,
    DateTimeOffset? ApprovedAt,
    string? ApprovedByUserId,
    DateTimeOffset? PostedAt,
    string? PostedByUserId,
    Guid? JournalEntryId = null,
    IReadOnlyList<StatutoryContributionEmployeeItemDto>? EmployeeItems = null,
    string Version = "",
    IReadOnlyList<ContributionEntryResponse>? Entries = null,
    Guid? ObligationId = null,
    Guid? PostingJournalEntryId = null);

public sealed record ContributionListResponse(
    IReadOnlyList<ContributionResponse> Items,
    int TotalCount);

public sealed record AddRegistrationRequest(
    StatutoryScheme Scheme,
    string RegistrationNumber,
    DateOnly RegisteredAt,
    string Reason = "");

public sealed record ContributionEntryRequest(Guid EmployeeId, decimal EmployeeContributionAmount, decimal EmployerContributionAmount,
    string EvidenceReference, string EvidenceSha256);
public sealed record ContributionEntryResponse(Guid Id, Guid EmployeeId, Guid RegistrationId, string RegistrationNumber,
    decimal EmployeeContributionAmount, decimal EmployerContributionAmount, string EvidenceReference, string EvidenceSha256);
public sealed record PostContributionRequest(Guid ObligationId, Guid PostingProfileId, DateOnly PostingDate, string ExpectedVersion, string Reason);
public sealed record StatutoryCoverageRequest(DateOnly EffectiveFrom, DateOnly? EffectiveToExclusive, IReadOnlyList<StatutoryScheme> Schemes,
    string EvidenceReference, string EvidenceSha256, string Reason);
public sealed record StatutoryCoverageResponse(Guid Id, Guid EmployeeId, DateOnly EffectiveFrom, DateOnly? EffectiveToExclusive,
    IReadOnlyList<StatutoryScheme> Schemes, string EvidenceReference, string EvidenceSha256, string State, string Version,
    string CreatedByUserId, string? ApprovedByUserId);
public sealed record StatutoryEntryProof(Guid ContributionId, Guid EmployeeId, Guid RegistrationId, string RegistrationNumber,
    StatutoryScheme Scheme, DateOnly Month, string Currency, decimal EmployeeAmount, decimal EmployerAmount,
    string EvidenceReference, string EvidenceSha256);
public sealed record StatutoryCoverageProof(Guid EmployeeId, DateOnly EffectiveFrom, DateOnly? EffectiveToExclusive,
    IReadOnlyList<StatutoryScheme> Schemes, string EvidenceReference, string EvidenceSha256);

public sealed record RegistrationResponse(
    Guid Id,
    Guid EmployeeId,
    string Scheme,
    string RegistrationNumber,
    DateOnly RegisteredAt,
    DateTimeOffset RecordedAt);

public interface IStatutoryContributionRepository
{
    Task<StatutoryContribution?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<StatutoryContribution?> GetBySchemeAndMonthAsync(StatutoryScheme scheme, DateOnly month, CancellationToken ct);
    Task<IReadOnlyList<StatutoryContribution>> ListAsync(StatutoryScheme? scheme, int? year, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(StatutoryScheme? scheme, int? year, CancellationToken ct);
    Task<EmployeeStatutoryRegistration?> GetRegistrationAsync(Guid employeeId, StatutoryScheme scheme, CancellationToken ct);
    Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetRegistrationsForEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetAllRegistrationsForSchemeAsync(StatutoryScheme scheme, CancellationToken ct);
    Task<Cosmix.Domain.HR.PayrollAccounting.PayrollObligation?> GetFinalizedObligationForMonthAsync(DateOnly month, CancellationToken ct);
    Task<Cosmix.Domain.HR.PayrollAccounting.PayrollObligation?> GetObligationByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Cosmix.Domain.HR.PayrollAccounting.PayrollObligation?>(null);
    Task<Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry?> GetJournalEntryByStatutoryContributionIdAsync(Guid contributionId, CancellationToken ct);
    Task AddJournalEntryAsync(Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry entry, CancellationToken ct);
    Task AddContributionAsync(StatutoryContribution contribution, CancellationToken ct);
    Task AddRegistrationAsync(EmployeeStatutoryRegistration registration, CancellationToken ct);
    Task AcquireStatutoryPostingLockAsync(Guid contributionId, CancellationToken ct) => Task.CompletedTask;
    Task SaveChangesAsync(CancellationToken ct);
}
