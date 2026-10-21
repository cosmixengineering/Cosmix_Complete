using Cosmix.Application.Authentication;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Application.HR.Dashboard;

public sealed record HrDashboardMonthlySalaryTotal(string Currency, int EmployeeCount,
    decimal BaseAmount, decimal GrossAmount);

// Frozen component totals describe an HR handoff, never salary disbursement or proof of payment.
public sealed record HrDashboardPayrollHandoffTotal(string Currency, string State, int BatchCount,
    int LockedBatchCount, int EmployeeCount, int LineCount, decimal SignedComponentTotal);

public sealed record HrDashboardPayrollSummary(DateTimeOffset GeneratedAtUtc, DateOnly BusinessDate,
    string TimeZone, DateOnly PeriodStart, DateOnly PeriodEndExclusive, int ActiveEmployees,
    int MonthlySalaryEmployeeCount, int MissingSalaryEmployeeCount, int NonMonthlySalaryEmployeeCount,
    int FutureSalaryVersions, int PendingCompensationChangeCount,
    IReadOnlyList<HrDashboardMonthlySalaryTotal> EffectiveMonthlySalary,
    IReadOnlyList<HrDashboardPayrollHandoffTotal> Handoff);

public sealed record HrDashboardPayrollSources(int FutureSalaryVersions, int PendingCompensationChangeCount,
    IReadOnlyList<PayrollHandoffBatch> Batches, IReadOnlyList<PayrollHandoffLine> Lines);

public interface IHrDashboardPayrollRepository
{
    Task<HrDashboardPayrollSources> ReadAsync(IReadOnlyList<Guid> employeeIds, DateOnly businessDate,
        DateOnly monthStart, DateOnly monthEndExclusive, CancellationToken ct);
}

/// <summary>Read-only source aggregation. Salary is the effective monthly wage as of the business
/// date; prepared handoff components remain separate. Existing financial permissions, compensation
/// history checks and the administration authorization transaction remain the authority.</summary>
public sealed class HrDashboardPayrollService(EmployeeOperation operation,
    IHrDashboardPopulationRepository population, IEmployeeCompensationRepository compensation,
    IHrDashboardPayrollRepository repository)
{
    public Task<HrDashboardPayrollSummary> SummaryAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeCompensationPermissions.Read, PayrollHandoffPermissions.Read],
            "hr.dashboard.payroll.read", null, context, async (actor, token) =>
            {
                var date = RosterSchedule.Today(actor.Now);
                var start = new DateOnly(date.Year, date.Month, 1);
                var employees = await population.ActiveEmployeeIdsAsync(actor.Now, date, token);
                if (employees is null || employees.Count > HrDashboardPopulationRules.MaximumEmployees ||
                    employees.Any(x => x == Guid.Empty) || employees.Distinct().Count() != employees.Count)
                    throw EmployeeOperation.Conflict("hr_dashboard_population_invalid");
                var candidates = new List<CompensationRecord>();
                foreach (var employeeId in employees)
                {
                    token.ThrowIfCancellationRequested();
                    // Reuse the owning salary reader's full chain/approval validation, rather than
                    // letting a guessed salary or an older open version fill a missing interval.
                    if (await compensation.HasConflictingHistoryAsync(employeeId, token))
                        throw EmployeeOperation.Conflict("compensation_history_requires_review");
                    var window = await compensation.WindowAsync(employeeId, date, token);
                    if (window.Candidate is { } candidate)
                    {
                        if (candidate.EmployeeId != employeeId)
                            throw EmployeeOperation.Conflict("hr_dashboard_compensation_source_invalid");
                        candidates.Add(candidate);
                    }
                }
                var sources = await repository.ReadAsync(employees, date, start, start.AddMonths(1), token);
                return Project(actor.Now.ToUniversalTime(), date, RosterSchedule.TimeZoneId, employees, candidates,
                    sources.FutureSalaryVersions, sources.PendingCompensationChangeCount, sources.Batches, sources.Lines);
            }, ct);

    private static HrDashboardPayrollSummary Project(DateTimeOffset now, DateOnly date, string timeZone,
        IReadOnlyList<Guid> employeeIds, IReadOnlyList<CompensationRecord> candidates,
        int futureSalaryVersions, int pendingChanges, IReadOnlyList<PayrollHandoffBatch> batches,
        IReadOnlyList<PayrollHandoffLine> lines)
    {
        var start = new DateOnly(date.Year, date.Month, 1);
        var end = start.AddMonths(1);
        var population = employeeIds.ToHashSet();
        var selectedCandidates = candidates.Where(x => population.Contains(x.EmployeeId)).ToArray();
        if (selectedCandidates.GroupBy(x => x.EmployeeId).Any(x => x.Count() != 1))
            throw EmployeeOperation.Conflict("hr_dashboard_compensation_source_invalid");
        // Candidate means newest started row, including an expired newest row. Never choose an
        // earlier open row after excluding an expired row; that would erase a real coverage gap.
        var effective = selectedCandidates.Where(x => x.EffectiveFrom <= date &&
            (!x.EffectiveTo.HasValue || date < x.EffectiveTo.Value)).ToArray();
        var monthly = effective.Where(x => x.WageBasis == WageBasis.Monthly).ToArray();
        var salary = monthly.GroupBy(x => x.Currency, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new HrDashboardMonthlySalaryTotal(x.Key, x.Count(),
                x.Sum(row => row.BaseAmount), x.Sum(row => row.GrossAmount))).ToArray();
        var selectedBatches = batches.Where(x => x.PeriodStart == start && x.PeriodEnd == end)
            .ToDictionary(x => x.Id);
        var selectedLines = lines.Where(x => population.Contains(x.EmployeeId) &&
            selectedBatches.ContainsKey(x.PayrollHandoffBatchId)).ToArray();
        if (selectedLines.Select(x => x.Id).Distinct().Count() != selectedLines.Length ||
            selectedLines.Any(x => x.Currency != selectedBatches[x.PayrollHandoffBatchId].Currency))
            throw EmployeeOperation.Conflict("hr_dashboard_handoff_source_invalid");
        var handoff = selectedLines.GroupBy(x => new
            {
                x.Currency,
                State = selectedBatches[x.PayrollHandoffBatchId].LockedAt.HasValue ? "locked" : "prepared"
            })
            .OrderBy(x => x.Key.Currency, StringComparer.Ordinal).ThenBy(x => x.Key.State, StringComparer.Ordinal)
            .Select(group =>
            {
                var batchIds = group.Select(x => x.PayrollHandoffBatchId).Distinct().ToArray();
                return new HrDashboardPayrollHandoffTotal(group.Key.Currency, group.Key.State,
                    batchIds.Length, batchIds.Count(id => selectedBatches[id].LockedAt.HasValue),
                    group.Select(x => x.EmployeeId).Distinct().Count(), group.Count(), group.Sum(x => x.Amount));
            }).ToArray();
        return new(now, date, timeZone, start, end, population.Count, monthly.Length,
            population.Count - effective.Length, effective.Length - monthly.Length,
            futureSalaryVersions, pendingChanges, salary, handoff);
    }
}
