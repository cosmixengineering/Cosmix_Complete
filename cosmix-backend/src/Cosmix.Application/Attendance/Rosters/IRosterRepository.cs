using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.Attendance.Rosters;

public interface IRosterRepository
{
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct);
    /// <summary>Bounded, tracked assignment history, including the referenced Shift.</summary>
    Task<IReadOnlyList<EmployeeShiftAssignment>> AssignmentsAsync(Guid employeeId, CancellationToken ct);
    Task<Shift?> FindShiftAsync(Guid shiftId, CancellationToken ct);
    Task<IReadOnlyList<ShiftVersion>> VersionsAsync(Guid shiftId, CancellationToken ct);
    /// <summary>
    /// Server-derived resources for every date slice. The caller must authorize ALL returned
    /// slices through at least one of each slice's alternative memberships.
    /// </summary>
    Task<IReadOnlyList<SchedulingResourceScope>> ResourcesAsync(Guid employeeId, DateOnly from, DateOnly? to, CancellationToken ct);
    /// <summary>Write eligibility, including current employment and complete requested interval coverage.</summary>
    Task ValidateEligibilityAsync(Guid employeeId, DateOnly from, DateOnly? to, Guid? siteId,
        DateTimeOffset now, CancellationToken ct);
    Task<Guid?> EffectiveSiteAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct);
    Task<bool> HasUnsupportedScheduleAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> HasUnsupportedScheduleInRangeAsync(Guid employeeId, DateOnly from, DateOnly? to, CancellationToken ct);
    void Add(EmployeeShiftAssignment assignment);
    void ExpectVersion(EmployeeShiftAssignment assignment, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
