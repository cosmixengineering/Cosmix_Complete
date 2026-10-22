using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.StatutoryContributions;

public sealed record CreateContributionRequest(
    StatutoryScheme Scheme,
    DateOnly ContributionMonth,
    decimal EmployeeContributionAmount,
    decimal EmployerContributionAmount,
    string Currency,
    string? PayableAccountCode,
    string? Notes,
    string? EmployerRegistrationNumber);

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
    string? PostedByUserId);

public sealed record ContributionListResponse(
    IReadOnlyList<ContributionResponse> Items,
    int TotalCount);

public sealed record AddRegistrationRequest(
    StatutoryScheme Scheme,
    string RegistrationNumber,
    DateOnly RegisteredAt);

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
    Task<IReadOnlyList<StatutoryContribution>> ListAsync(StatutoryScheme? scheme, int? year, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(StatutoryScheme? scheme, int? year, CancellationToken ct);
    Task<EmployeeStatutoryRegistration?> GetRegistrationAsync(Guid employeeId, StatutoryScheme scheme, CancellationToken ct);
    Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetRegistrationsForEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetAllRegistrationsForSchemeAsync(StatutoryScheme scheme, CancellationToken ct);
    Task AddContributionAsync(StatutoryContribution contribution, CancellationToken ct);
    Task AddRegistrationAsync(EmployeeStatutoryRegistration registration, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
