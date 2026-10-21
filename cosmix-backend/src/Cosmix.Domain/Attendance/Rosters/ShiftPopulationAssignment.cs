using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Rosters;

public enum ShiftPopulationKind { Team = 1, Site = 2, StaffCategory = 3, Department = 4 }

/// <summary>Group defaults; the existing EmployeeShiftAssignment retains individual effective-dated assignments.</summary>
public sealed class ShiftPopulationAssignment : IAppendOnlyEntity
{
    private ShiftPopulationAssignment() { }
    public ShiftPopulationAssignment(Guid shiftId, ShiftPopulationKind populationKind, DateOnly effectiveFrom,
        DateOnly? effectiveTo, Guid approvalRequestId, Guid? teamId = null, Guid? siteId = null,
        Guid? staffCategoryId = null, Guid? departmentId = null)
        : this(shiftId, populationKind, effectiveFrom, effectiveTo,
            (Guid?)ModelGuard.Id(approvalRequestId, nameof(approvalRequestId)), teamId, siteId,
            staffCategoryId, departmentId, null) { }

    private ShiftPopulationAssignment(Guid shiftId, ShiftPopulationKind populationKind, DateOnly effectiveFrom,
        DateOnly? effectiveTo, Guid? approvalRequestId, Guid? teamId, Guid? siteId,
        Guid? staffCategoryId, Guid? departmentId, Guid? holidayCalendarId)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        if (!Enum.IsDefined(populationKind) || new[] { teamId, siteId, staffCategoryId, departmentId }.Count(x => x.HasValue) != 1
            || populationKind == ShiftPopulationKind.Team && !teamId.HasValue || populationKind == ShiftPopulationKind.Site && !siteId.HasValue
            || populationKind == ShiftPopulationKind.StaffCategory && !staffCategoryId.HasValue || populationKind == ShiftPopulationKind.Department && !departmentId.HasValue)
            throw new ArgumentException("Exactly one matching shift population is required.");
        Id = Guid.NewGuid(); ShiftId = ModelGuard.Id(shiftId, nameof(shiftId)); PopulationKind = populationKind; EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo;
        ApprovalRequestId = approvalRequestId; TeamId = AttendanceModelGuard.OptionalId(teamId, nameof(teamId));
        SiteId = AttendanceModelGuard.OptionalId(siteId, nameof(siteId)); StaffCategoryId = AttendanceModelGuard.OptionalId(staffCategoryId, nameof(staffCategoryId));
        DepartmentId = AttendanceModelGuard.OptionalId(departmentId, nameof(departmentId));
        HolidayCalendarId = AttendanceModelGuard.OptionalId(holidayCalendarId, nameof(holidayCalendarId));
    }

    /// <summary>
    /// Records the confirmed direct site-scheduling flow. The application must authorize the
    /// actor and atomically record reason, audit and idempotency evidence; no approval is fabricated.
    /// An optional calendar ID pins an exact immutable revision, never a latest-calendar lookup.
    /// </summary>
    public static ShiftPopulationAssignment CreateDirect(Guid shiftId, ShiftPopulationKind populationKind,
        DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? teamId = null, Guid? siteId = null,
        Guid? staffCategoryId = null, Guid? departmentId = null, Guid? holidayCalendarId = null)
    {
        if (populationKind != ShiftPopulationKind.Site)
            throw new ArgumentException("Direct scheduling is supported only for a site.", nameof(populationKind));
        return new(shiftId, populationKind, effectiveFrom, effectiveTo, null, teamId, siteId,
            staffCategoryId, departmentId, holidayCalendarId);
    }
    public Guid Id { get; private set; }
    public Guid ShiftId { get; private set; }
    public ShiftPopulationKind PopulationKind { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    /// <summary>Retained for legacy approved assignments; null for explicitly authorized direct writes.</summary>
    public Guid? ApprovalRequestId { get; private set; }
    public Guid? HolidayCalendarId { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? SiteId { get; private set; }
    public Guid? StaffCategoryId { get; private set; }
    public Guid? DepartmentId { get; private set; }
}
