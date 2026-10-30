using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollSalary;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class CompanyPayrollReadinessRepository(CosmixDbContext db,
    IEmployeeCompensationRepository compensation, IIndividualRosterResolver roster,
    PayrollFinanceSetupService finance) : ICompanyPayrollReadinessRepository
{
    public async Task<ReadinessAuditData> FetchReadinessDataAsync(DateOnly targetMonth, CancellationToken ct)
    {
        var untilExclusive = targetMonth.AddMonths(1);
        // This setup report covers currently active, approved employees who joined by this month.
        // Historical leavers and actual payroll eligibility are assessed by the month-close workflow.
        var activeEmployees = await db.Employees.AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active && !e.IsArchived &&
                e.OnboardingStatus == OnboardingStatus.Approved && e.ApprovedOnboardingDecisionId != null &&
                (e.JoinedOn == null || e.JoinedOn < untilExclusive))
            .Select(e => new ActiveEmployeeReadinessDto(
                e.Id,
                e.EmployeeNumber.ToString(),
                e.FullLegalName ?? "Unnamed Employee", e.JoinedOn))
            .ToListAsync(ct);

        var activeEmployeeIds = activeEmployees.Select(e => e.Id).ToHashSet();

        // Allowances are not basic salary. Reuse the compensation history integrity rules,
        // including independent revision approval, and inspect all employed days of the month.
        var salaries = await db.CompensationRecords.AsNoTracking()
            .Where(c => activeEmployeeIds.Contains(c.EmployeeId) && c.EffectiveFrom < untilExclusive)
            .Take(65537).ToArrayAsync(ct);
        if (salaries.Length > 65536) throw AttendanceOperation.Conflict("payroll_readiness_history_limit");
        var salaryByEmployee = salaries.ToLookup(x => x.EmployeeId);
        var employeesWithSalary = new HashSet<Guid>();
        var employeesWithShift = new HashSet<Guid>();
        foreach (var employee in activeEmployees)
        {
            ct.ThrowIfCancellationRequested();
            if (employee.JoinedOn is not DateOnly joined) continue;
            var from = joined > targetMonth ? joined : targetMonth;
            if (!await compensation.HasConflictingHistoryAsync(employee.Id, ct) &&
                PayrollReadinessCoverage.Salary(salaryByEmployee[employee.Id].ToArray(), from, untilExclusive))
                employeesWithSalary.Add(employee.Id);
            // Legacy individual assignments do not override the current site/office taxonomy.
            if (await PayrollReadinessCoverage.RosterAsync(roster, employee.Id, from, untilExclusive, ct))
                employeesWithShift.Add(employee.Id);
        }

        // 4. Bank details coverage
        var employeesWithBank = (await db.EmployeeBankDetails.AsNoTracking()
            .Where(b => activeEmployeeIds.Contains(b.EmployeeId)
                     && b.IsVerified && b.EffectiveFrom <= targetMonth
                     && (b.EffectiveTo == null || b.EffectiveTo > targetMonth)
                     && !db.EmployeeBankDetails.Any(next => next.EmployeeId == b.EmployeeId &&
                         next.EffectiveFrom <= targetMonth && next.Revision > b.Revision))
            .Select(b => b.EmployeeId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        // 5. Statutory coverage
        var employeesWithStatutory = (await db.EmployeeStatutoryCoverages.AsNoTracking()
            .Where(s => activeEmployeeIds.Contains(s.EmployeeId)
                     && s.EffectiveFrom <= targetMonth
                     && (s.EffectiveToExclusive == null || s.EffectiveToExclusive > targetMonth))
            .Select(s => s.EmployeeId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        // 6. Active Posting Profiles & Accounts
        var profiles = await db.PayrollPostingProfiles.AsNoTracking()
            .Where(p => p.EffectiveFrom <= targetMonth && p.State == PayrollFinanceApprovalState.Approved)
            .Take(1001).ToArrayAsync(ct);
        if (profiles.Length > 1000) throw AttendanceOperation.Conflict("payroll_readiness_history_limit");
        var activePostingProfilesCount = await PayrollPostingReadiness.CountApplicableAsync(profiles, finance, targetMonth, ct);

        var activeAccountsCount = await db.PayrollLedgerAccounts.AsNoTracking()
            .CountAsync(a => a.IsActive, ct);

        // 7. Pending Policy Approvals
        var pendingPolicyApprovalsCount = await db.PolicyVersions.AsNoTracking()
            .CountAsync(v => v.ApprovalRequestId == null || v.ApprovalRequest == null || v.ApprovalRequest.State != ApprovalState.Approved, ct);

        var approvedSalaryPolicyCount = await db.PayrollSalaryPolicies.AsNoTracking()
            .CountAsync(p => p.EffectiveFrom <= targetMonth && p.ApprovalRequest.State == ApprovalState.Approved, ct);

        return new ReadinessAuditData(
            ActiveEmployees: activeEmployees,
            EmployeesWithSalary: employeesWithSalary,
            EmployeesWithShift: employeesWithShift,
            EmployeesWithBank: employeesWithBank,
            EmployeesWithStatutory: employeesWithStatutory,
            ActivePostingProfilesCount: activePostingProfilesCount,
            ActiveAccountsCount: activeAccountsCount,
            PendingPolicyApprovalsCount: pendingPolicyApprovalsCount,
            ApprovedSalaryPolicyCount: approvedSalaryPolicyCount);
    }
}
