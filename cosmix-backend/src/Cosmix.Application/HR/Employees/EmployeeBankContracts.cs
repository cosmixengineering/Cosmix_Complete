using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.HR.EmployeeRecords;

namespace Cosmix.Application.HR.Employees;

public static class EmployeeBankPermissions
{
    public const string Verify = "hr.employees.bank.verify";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeBankTransitionRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedEmployeeVersion,
    [Range(0, int.MaxValue)] int ExpectedVerificationVersion,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [Required, StringLength(8000, MinimumLength = 1)] string Proof);

public sealed record EmployeeBankStatus(Guid EmployeeId, Guid BankDetailsId, int BankRevision,
    string MaskedAccountIdentifier, bool IsVerified, int VerificationVersion, string? RecordedByUserId,
    DateTimeOffset? RecordedAt, string EmployeeVersion);
public sealed record EmployeeBankVerificationSummary(Guid Id, Guid EmployeeId, Guid BankDetailsId,
    int Version, bool IsVerified, string RecordedByUserId, DateTimeOffset RecordedAt);
public sealed record EmployeeBankVerificationEvidence(EmployeeBankVerificationSummary Verification,
    string Reason, string Proof);
public sealed record EmployeeBankHistoryQuery([Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);

public interface IEmployeeBankRepository
{
    Task<EmployeeBankDetails?> FindBankAsync(Guid employeeId, Guid bankDetailsId, CancellationToken ct);
    Task<EmployeeBankDetails?> CurrentBankAsync(Guid employeeId, DateOnly asOf, CancellationToken ct);
    Task<EmployeeBankVerification?> LatestAsync(Guid employeeId, Guid bankDetailsId, CancellationToken ct);
    Task<EmployeeBankVerification?> FindVerificationAsync(Guid employeeId, Guid bankDetailsId, Guid verificationId, CancellationToken ct);
    Task<PagedResult<EmployeeBankVerificationSummary>> HistoryAsync(Guid employeeId, Guid bankDetailsId, EmployeeBankHistoryQuery query, CancellationToken ct);
    Task<bool> HasConflictingHistoryAsync(Guid employeeId, Guid bankDetailsId, CancellationToken ct);
    void AddVerification(EmployeeBankVerification verification);
}
