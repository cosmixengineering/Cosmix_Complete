using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Compensation;

namespace Cosmix.Application.HR.PayrollSalary;

/// <summary>Coverage of actual compensation history and the same dated roster used by attendance.
/// This is configuration readiness, not authority to calculate, approve or pay payroll.</summary>
public static class PayrollReadinessCoverage
{
    public static bool Salary(IReadOnlyList<CompensationRecord> history, DateOnly from, DateOnly untilExclusive)
    {
        for (var day = from; day < untilExclusive; day = day.AddDays(1))
        {
            // Expiry of a newer version never revives an older open-ended salary.
            var row = history.Where(x => x.EffectiveFrom <= day)
                .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Revision).FirstOrDefault();
            if (row is null || row.EffectiveTo <= day) return false;
        }
        return from < untilExclusive;
    }

    public static async Task<bool> RosterAsync(IIndividualRosterResolver resolver, Guid employeeId,
        DateOnly from, DateOnly untilExclusive, CancellationToken ct)
    {
        for (var day = from; day < untilExclusive; day = day.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var roster = await resolver.ResolveAsync(employeeId, day, ct);
                if (roster.Status is not ("scheduled" or "rest_day")) return false;
            }
            catch (AuthenticationFailure failure) when (MonthLockSetup.IsMissingConfiguration(failure))
            { return false; }
        }
        return from < untilExclusive;
    }
}
