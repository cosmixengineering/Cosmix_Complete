using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Offices;

namespace Cosmix.Application.Attendance.PayrollInputs;

/// <summary>Dated Office location metadata only. No Head Office fallback or legal/payment authority.</summary>
public sealed class OfficeAttendancePayrollJurisdictionReader(IAttendancePayrollJurisdictionReader sites,
    IOfficeBoundaryRepository boundaries) : IAttendancePayrollJurisdictionReader
{
    public async Task<IReadOnlyList<AttendancePayrollJurisdictionContext>> ReadAsync(
        IReadOnlyList<AttendancePayrollJurisdictionDay> days, CancellationToken ct)
    {
        if (days is null || days.Count > 366 || days.Any(x => x is null || x.BranchId == Guid.Empty ||
            x.BranchId.HasValue && (x.SiteId.HasValue || x.BusinessDate.Year is < 1901 or > 9997) ||
            !x.BranchId.HasValue && x.OfficeSource is not null))
            throw AttendanceOperation.Conflict("payroll_jurisdiction_contract_invalid");
        var siteDays = days.Where(x => !x.BranchId.HasValue).ToArray();
        var result = new List<AttendancePayrollJurisdictionContext>();
        if (siteDays.Length != 0) result.AddRange(await sites.ReadAsync(siteDays, ct));
        var history = new Dictionary<Guid, IReadOnlyList<OfficeBoundaryVersionResponse>>();
        foreach (var day in days.Where(x => x.BranchId.HasValue))
        {
            ct.ThrowIfCancellationRequested();
            var branch = day.BranchId!.Value; var source = day.OfficeSource;
            AttendancePayrollJurisdictionContext Missing(string state) => new(day.BusinessDate, null, null, null, state, branch);
            if (source is null) { result.Add(Missing("unavailable")); continue; }
            if (source.BranchId != branch || source.BusinessDate != day.BusinessDate || source.EmployeeId == Guid.Empty ||
                source.OfficeGeofenceVersionId == Guid.Empty || source.BoundaryVersionNumber < 1)
                throw AttendanceOperation.Conflict("payroll_jurisdiction_contract_invalid");
            if (!history.TryGetValue(branch, out var rows))
            {
                rows = await boundaries.VersionsAsync(branch, ct);
                OfficeBoundaryService.ValidateHistory(branch, rows);
                history.Add(branch, rows);
            }
            var boundary = rows.Where(x => x.EffectiveFrom <= day.BusinessDate).MaxBy(x => x.VersionNumber);
            if (boundary is null) { result.Add(Missing("unavailable")); continue; }
            if (boundary.Id != source.OfficeGeofenceVersionId || boundary.VersionNumber != source.BoundaryVersionNumber ||
                boundary.JurisdictionCode != source.JurisdictionCode)
            { result.Add(Missing("conflict")); continue; }
            result.Add(new(day.BusinessDate, null, boundary.Id, boundary.JurisdictionCode, "configured", branch));
        }
        return result.OrderBy(x => x.BusinessDate).ThenBy(x => x.SiteId).ThenBy(x => x.BranchId).ToArray();
    }
}
