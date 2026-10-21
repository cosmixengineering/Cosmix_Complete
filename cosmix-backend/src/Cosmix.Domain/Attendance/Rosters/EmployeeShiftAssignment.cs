using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Rosters;

public sealed class EmployeeShiftAssignment
{
    private EmployeeShiftAssignment() { }

    public EmployeeShiftAssignment(Guid employeeId, Guid shiftId, DateOnly effectiveFrom,
        DateOnly? effectiveTo = null, Guid? siteId = null)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        ShiftId = ModelGuard.Id(shiftId, nameof(shiftId));
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        SiteId = siteId.HasValue ? ModelGuard.Id(siteId.Value, nameof(siteId)) : null;
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee Employee { get; private set; } = null!;
    public Guid ShiftId { get; private set; }
    public Cosmix.Domain.Attendance.Shifts.Shift Shift { get; private set; } = null!;
    public Guid? SiteId { get; private set; }
    public Cosmix.Domain.Attendance.Sites.Site? Site { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Shorten the interval only. Application enforces authority, future dates and period locks.</summary>
    public void EndOn(DateOnly effectiveTo)
    {
        ModelGuard.EffectiveDates(EffectiveFrom, effectiveTo);
        if (EffectiveTo.HasValue && effectiveTo >= EffectiveTo.Value)
            throw new InvalidOperationException("An assignment end cannot be extended or rewritten unchanged.");
        EffectiveTo = effectiveTo;
    }
}
