using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed record FinancialDecisionRequest(string ExpectedVersion, string Reason);
public sealed record AccountRequest(string Code, string Name, PayrollAccountKind Kind, string Reason);
public sealed record AccountStatusRequest(bool IsActive, string ExpectedVersion, string Reason);
public sealed record AccountResponse(Guid Id, string Code, string Name, string Kind, bool IsActive, string Version);
public sealed record AccountingPeriodRequest(DateOnly PeriodStart, DateOnly PeriodEndExclusive, string Currency, string Reason);
public sealed record AccountingPeriodResponse(Guid Id, DateOnly PeriodStart, DateOnly PeriodEndExclusive, string Currency,
    string State, string CreatedByUserId, string? ApprovedByUserId, string Version);
public sealed record PayrollDeductionMapping(string Component, string CreditAccountCode);
public sealed record PayrollStatutoryMapping(StatutoryScheme Scheme, string EmployerExpenseAccountCode, string PayableAccountCode);
public sealed record PayrollPostingRules(string SalaryExpenseAccountCode, string SalaryPayableAccountCode,
    IReadOnlyList<PayrollDeductionMapping> Deductions, IReadOnlyList<PayrollStatutoryMapping> Statutory);
public sealed record PostingProfileRequest(string Name, string Currency, DateOnly EffectiveFrom, DateOnly? EffectiveToExclusive,
    string SalaryExpenseAccountCode, string SalaryPayableAccountCode, IReadOnlyList<PayrollDeductionMapping> Deductions,
    IReadOnlyList<PayrollStatutoryMapping> Statutory, string Reason);
public sealed record PostingProfileResponse(Guid Id, string Name, string Currency, DateOnly EffectiveFrom, DateOnly? EffectiveToExclusive,
    string SalaryExpenseAccountCode, string SalaryPayableAccountCode, IReadOnlyList<PayrollDeductionMapping> Deductions,
    IReadOnlyList<PayrollStatutoryMapping> Statutory, string State, string CreatedByUserId, string? ApprovedByUserId, string Version);
public sealed record PayrollFinanceSetupResponse(IReadOnlyList<AccountResponse> Accounts, IReadOnlyList<AccountingPeriodResponse> Periods,
    IReadOnlyList<PostingProfileResponse> Profiles);

public interface IPayrollFinanceRepository
{
    Task<IReadOnlyList<PayrollLedgerAccount>> AccountsAsync(CancellationToken ct);
    Task<PayrollLedgerAccount?> AccountAsync(Guid id, CancellationToken ct);
    Task<PayrollLedgerAccount?> AccountAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<PayrollAccountingPeriod>> PeriodsAsync(CancellationToken ct);
    Task<PayrollAccountingPeriod?> PeriodAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PayrollPostingProfile>> ProfilesAsync(CancellationToken ct);
    Task<PayrollPostingProfile?> ProfileAsync(Guid id, CancellationToken ct);
    Task<bool> PeriodOverlapAsync(DateOnly start, DateOnly endExclusive, string currency, CancellationToken ct);
    Task<PayrollAccountingPeriod?> PeriodForDateAsync(DateOnly date, string currency, CancellationToken ct);
    Task<bool> EmployeeExistsAsync(Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeStatutoryCoverage>> CoverageAsync(Guid employeeId, CancellationToken ct);
    Task<EmployeeStatutoryCoverage?> CoverageByIdAsync(Guid id, CancellationToken ct);
    Task<StatutoryContributionEntry?> EntryAsync(Guid id, CancellationToken ct);
    Task<bool> EntryUsedAsync(Guid entryId, Guid? excludingBatchId, CancellationToken ct);
    void Add(PayrollLedgerAccount account);
    void Add(PayrollAccountingPeriod period);
    void Add(PayrollPostingProfile profile);
    void Add(EmployeeStatutoryCoverage coverage);
    void ExpectVersion(object entity, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
