using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public static class LeaveBenefitUsage
{
    public static IReadOnlyDictionary<Guid, LeaveBenefitUsageEntry> Active(
        Guid employee, DateOnly from, DateOnly to, IReadOnlyList<LeaveBenefitUsageEntry> rows)
    {
        if (rows is null || rows.Count > 10000 || rows.Any(x => x is null) || rows.Select(x => x.Id).Distinct().Count() != rows.Count ||
            rows.Any(x => x.Id == Guid.Empty || x.EmployeeId != employee || x.YearFrom != from || x.YearTo != to ||
                x.LeaveTypeId == Guid.Empty || x.PolicyVersionId == Guid.Empty || x.StandardDayMinutes is < 1 or > 1440 ||
                x.Type is not (LeaveLedgerEntryType.Utilization or LeaveLedgerEntryType.Reservation or LeaveLedgerEntryType.Release or LeaveLedgerEntryType.Encashment)))
            throw AttendanceOperation.Conflict("leave_benefit_usage_conflict");
        var indexed = rows.ToDictionary(x => x.Id);
        var releases = new HashSet<Guid>();
        foreach (var row in rows)
        {
            if (row.Type != LeaveLedgerEntryType.Release)
            {
                if (row.Minutes >= 0 || row.Minutes == int.MinValue || row.ReversesId is not null ||
                    row.Type == LeaveLedgerEntryType.Utilization && row.RequestId is null ||
                    row.Type is LeaveLedgerEntryType.Reservation or LeaveLedgerEntryType.Encashment && row.RequestId is not null ||
                    row.RequestId == Guid.Empty)
                    throw AttendanceOperation.Conflict("leave_benefit_usage_conflict");
                continue;
            }
            if (row.ReversesId is not Guid originalId || !indexed.TryGetValue(originalId, out var original) ||
                original.Type is not (LeaveLedgerEntryType.Utilization or LeaveLedgerEntryType.Reservation) ||
                !releases.Add(originalId) || row.Minutes <= 0 || (long)row.Minutes != -(long)original.Minutes ||
                row.PolicyVersionId != original.PolicyVersionId || row.LeaveTypeId != original.LeaveTypeId ||
                row.RequestId != original.RequestId || row.StandardDayMinutes != original.StandardDayMinutes || row.RecordedAt < original.RecordedAt)
                throw AttendanceOperation.Conflict("leave_benefit_usage_conflict");
        }
        return rows.Where(x => x.Type != LeaveLedgerEntryType.Release && !releases.Contains(x.Id)).ToDictionary(x => x.Id);
    }

    public static string Fingerprint(IReadOnlyList<LeaveBenefitUsageEntry> rows) =>
        LeaveProvenance.Digest(rows.OrderBy(x => x.Id).ToArray());

    public static int AllocatedMinutes(LeaveBenefitAwardProof award, IReadOnlyList<LeaveBenefitAllocation> allocations,
        IReadOnlyDictionary<Guid, LeaveBenefitUsageEntry> active, bool allowReleased = false)
    {
        if (allocations is null || allocations.Count > 1000 || allocations.Any(x => x is null) ||
            allocations.Select(x => x.LedgerEntryId).Distinct().Count() != allocations.Count ||
            allocations.Any(x => x.LedgerEntryId == Guid.Empty || x.Minutes <= 0))
            throw AttendanceOperation.Invalid("invalid_leave_benefit_allocation");
        var settings = LeavePolicySettings.Parse(award.Policy.RulesJson, award.Policy.Schema);
        var day = settings.StandardDayMinutes;
        long total = 0;
        foreach (var allocation in allocations)
        {
            if (!active.TryGetValue(allocation.LedgerEntryId, out var source))
            {
                if (allowReleased) continue;
                throw AttendanceOperation.Conflict("leave_benefit_allocation_source_changed");
            }
            if (source.Type is not (LeaveLedgerEntryType.Utilization or LeaveLedgerEntryType.Encashment) ||
                source.EmployeeId != award.EmployeeId || source.LeaveTypeId != award.LeaveTypeId ||
                source.RecordedAt < award.RecordedAtUtc || allocation.Minutes > -(long)source.Minutes ||
                source.Type == LeaveLedgerEntryType.Encashment && settings.Benefits?.EncashmentEligible != true)
                throw AttendanceOperation.Conflict("leave_benefit_allocation_source_invalid");
            // Retain source-day units; never round an allocation into extra entitlement.
            var numerator = (long)allocation.Minutes * day;
            if (numerator % source.StandardDayMinutes != 0)
                throw AttendanceOperation.Conflict("leave_benefit_allocation_fractional_minute");
            total += numerator / source.StandardDayMinutes;
            if (total > award.Minutes) throw AttendanceOperation.Conflict("leave_benefit_allocation_exceeds_award");
        }
        return (int)total;
    }
}
