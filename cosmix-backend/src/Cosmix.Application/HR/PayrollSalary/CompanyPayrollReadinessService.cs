using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.HR.PayrollSalary;

/// <summary>
/// Evaluates company policy configuration and employee master-data readiness for payroll processing.
/// Strictly enforces privacy: does not expose confidential employee compensation amounts.
/// </summary>
public sealed class CompanyPayrollReadinessService(ICompanyPayrollReadinessRepository repository, TimeProvider clock)
{
    public async Task<CompanyPayrollReadinessReport> EvaluateAsync(int year, int month, CancellationToken ct = default)
    {
        if (year is < 1 or > 9998 || month is < 1 or > 12)
            throw AttendanceOperation.Invalid("payroll_readiness_period_invalid");
        var targetMonth = new DateOnly(year, month, 1);
        var now = clock.GetUtcNow();

        var data = await repository.FetchReadinessDataAsync(targetMonth, ct);

        var blockingIssues = new List<PayrollReadinessIssue>();
        var warnings = new List<PayrollReadinessIssue>();

        if (data.ApprovedSalaryPolicyCount == 0)
            blockingIssues.Add(new("SalaryPolicy", "missing_salary_policy",
                $"No approved salary calculation policy is effective for {targetMonth:yyyy-MM}. Configure and approve proration, rounding and earnings treatment before preparing payroll."));

        // 1. Check Base Compensation coverage
        foreach (var emp in data.ActiveEmployees)
        {
            if (!data.EmployeesWithSalary.Contains(emp.Id))
            {
                blockingIssues.Add(new PayrollReadinessIssue(
                    Category: "Salary",
                    Code: "missing_base_salary",
                    Message: $"Active employee {emp.EmployeeCode} ({emp.FullLegalName}) has no approved base salary structure covering {targetMonth:yyyy-MM}.",
                    EmployeeId: emp.Id,
                    EmployeeCode: emp.EmployeeCode,
                    EmployeeName: emp.FullLegalName,
                    IsBlocking: true));
            }
        }

        // 2. Check Shift / Roster Assignment coverage
        foreach (var emp in data.ActiveEmployees)
        {
            if (!data.EmployeesWithShift.Contains(emp.Id))
            {
                blockingIssues.Add(new PayrollReadinessIssue(
                    Category: "AttendanceShift",
                    Code: "missing_shift_assignment",
                    Message: $"Active employee {emp.EmployeeCode} ({emp.FullLegalName}) has no active shift or roster assignment for {targetMonth:yyyy-MM}.",
                    EmployeeId: emp.Id,
                    EmployeeCode: emp.EmployeeCode,
                    EmployeeName: emp.FullLegalName,
                    IsBlocking: true));
            }
        }

        // 3. Check Bank Details coverage
        foreach (var emp in data.ActiveEmployees)
        {
            if (!data.EmployeesWithBank.Contains(emp.Id))
            {
                warnings.Add(new PayrollReadinessIssue(
                    Category: "BankDetails",
                    Code: "missing_bank_account",
                    Message: $"Active employee {emp.EmployeeCode} ({emp.FullLegalName}) has no verified bank account details recorded.",
                    EmployeeId: emp.Id,
                    EmployeeCode: emp.EmployeeCode,
                    EmployeeName: emp.FullLegalName,
                    IsBlocking: false));
            }
        }

        // 4. Check Statutory Registrations / Coverage
        foreach (var emp in data.ActiveEmployees)
        {
            if (!data.EmployeesWithStatutory.Contains(emp.Id))
            {
                warnings.Add(new PayrollReadinessIssue(
                    Category: "Statutory",
                    Code: "missing_statutory_decision",
                    Message: $"Active employee {emp.EmployeeCode} ({emp.FullLegalName}) has no active statutory coverage (EOBI/PESSI) decision for {targetMonth:yyyy-MM}.",
                    EmployeeId: emp.Id,
                    EmployeeCode: emp.EmployeeCode,
                    EmployeeName: emp.FullLegalName,
                    IsBlocking: false));
            }
        }

        // 5. Check Finance Chart of Accounts & Posting Profiles
        if (data.ActivePostingProfilesCount == 0)
        {
            blockingIssues.Add(new PayrollReadinessIssue(
                Category: "FinanceLedger",
                Code: "missing_payroll_posting_profile",
                Message: $"No approved PKR payroll posting profile with valid active account mappings covers the whole period {targetMonth:yyyy-MM}.",
                IsBlocking: true));
        }

        if (data.ActiveAccountsCount == 0)
        {
            blockingIssues.Add(new PayrollReadinessIssue(
                Category: "FinanceLedger",
                Code: "missing_ledger_accounts",
                Message: "Chart of Accounts setup is incomplete: No active payroll ledger accounts are provisioned.",
                IsBlocking: true));
        }

        // 6. Check Policy Approvals
        if (data.PendingPolicyApprovalsCount > 0)
        {
            warnings.Add(new PayrollReadinessIssue(
                Category: "PolicyApproval",
                Code: "unapproved_policy_version_pending",
                Message: $"{data.PendingPolicyApprovalsCount} policy version(s) remain in draft/unapproved status.",
                IsBlocking: false));
        }

        // 7. Ready Employees Count (Active employees with Base Salary + Shift)
        var readyEmployeesCount = data.ActiveEmployees.Count(e =>
            data.EmployeesWithSalary.Contains(e.Id) && data.EmployeesWithShift.Contains(e.Id));

        var coverage = new PayrollConfigurationCoverageSummary(
            EmployeesWithSalaryCount: data.EmployeesWithSalary.Count,
            EmployeesWithShiftCount: data.EmployeesWithShift.Count,
            EmployeesWithBankCount: data.EmployeesWithBank.Count,
            EmployeesWithStatutoryCount: data.EmployeesWithStatutory.Count,
            ActivePostingProfilesCount: data.ActivePostingProfilesCount,
            ActiveCostCentersCount: data.ActiveAccountsCount,
            PendingPolicyApprovalsCount: data.PendingPolicyApprovalsCount);

        var isReady = blockingIssues.Count == 0;

        return new CompanyPayrollReadinessReport(
            IsReady: isReady,
            EvaluationPeriod: targetMonth,
            EvaluatedAtUtc: now,
            TotalActiveEmployees: data.ActiveEmployees.Count,
            ReadyEmployeesCount: readyEmployeesCount,
            BlockingIssues: blockingIssues,
            Warnings: warnings,
            Coverage: coverage);
    }
}
