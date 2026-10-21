using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.WorkSessions;

/// <summary>A revision of an interpreted continuous duty session. Closure/correction creates another revision.</summary>
public sealed class WorkSession : IAppendOnlyEntity
{
    private WorkSession() { }
    public WorkSession(Guid sessionKey, int revision, Guid employeeId, DateOnly attendanceDate,
        DateTimeOffset startsAtUtc, DateTimeOffset? endsAtUtc, Guid shiftVersionId, Guid? policyVersionId,
        DateTimeOffset calculatedAtUtc, RawPunchEvent? checkIn, RawPunchEvent? checkOut = null,
        Guid? correctionId = null, Guid? siteId = null, bool endsContinuousDuty = false,
        Guid? branchId = null, Guid? officeRosterAssignmentId = null, Guid? employeeAssignmentId = null,
        Guid? officeGeofenceVersionId = null, Guid? holidayCalendarId = null)
    {
        if (endsAtUtc.HasValue) AttendanceModelGuard.Window(startsAtUtc, endsAtUtc.Value);
        if (checkIn is not null && (checkIn.EmployeeId != employeeId || checkIn.Action != PunchAction.CheckIn))
            throw new ArgumentException("Check In must belong to the session employee.");
        if (checkOut is not null && (checkOut.EmployeeId != employeeId || checkOut.Action != PunchAction.CheckOut || !endsAtUtc.HasValue))
            throw new ArgumentException("Check Out must belong to the session employee and have an end time.");
        if (!correctionId.HasValue && (checkIn is null || startsAtUtc != checkIn.ReceivedAtUtc
            || (endsAtUtc.HasValue && (checkOut is null || endsAtUtc != checkOut.ReceivedAtUtc))))
            throw new ArgumentException("Changed or manual times require a separate correction record.");
        if (endsContinuousDuty && !endsAtUtc.HasValue) throw new ArgumentException("An open session cannot end continuous duty.");
        Id = Guid.NewGuid(); SessionKey = ModelGuard.Id(sessionKey, nameof(sessionKey)); Revision = AttendanceModelGuard.Revision(revision);
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); AttendanceDate = attendanceDate;
        StartsAtUtc = ModelGuard.Utc(startsAtUtc); EndsAtUtc = endsAtUtc.HasValue ? ModelGuard.Utc(endsAtUtc.Value) : null;
        CheckInPunchId = checkIn?.Id; CheckOutPunchId = checkOut?.Id;
        CorrectionId = AttendanceModelGuard.OptionalId(correctionId, nameof(correctionId));
        SiteId = AttendanceModelGuard.OptionalId(siteId, nameof(siteId));
        BranchId = AttendanceModelGuard.OptionalId(branchId, nameof(branchId));
        OfficeRosterAssignmentId = AttendanceModelGuard.OptionalId(officeRosterAssignmentId, nameof(officeRosterAssignmentId));
        EmployeeAssignmentId = AttendanceModelGuard.OptionalId(employeeAssignmentId, nameof(employeeAssignmentId));
        OfficeGeofenceVersionId = AttendanceModelGuard.OptionalId(officeGeofenceVersionId, nameof(officeGeofenceVersionId));
        HolidayCalendarId = AttendanceModelGuard.OptionalId(holidayCalendarId, nameof(holidayCalendarId));
        if (BranchId.HasValue && (SiteId.HasValue || !OfficeRosterAssignmentId.HasValue || !EmployeeAssignmentId.HasValue || !OfficeGeofenceVersionId.HasValue) ||
            !BranchId.HasValue && (OfficeRosterAssignmentId.HasValue || OfficeGeofenceVersionId.HasValue))
            throw new ArgumentException("Office duty requires exact membership, roster and boundary evidence.");
        if (BranchId.HasValue && new[] { checkIn, checkOut }.Where(x => x is not null).Any(x =>
                x!.BranchId != BranchId || x.OfficeRosterAssignmentId != OfficeRosterAssignmentId || x.EmployeeAssignmentId != EmployeeAssignmentId ||
                x.OfficeGeofenceVersionId != OfficeGeofenceVersionId || x.HolidayCalendarId != HolidayCalendarId))
            throw new ArgumentException("Office raw evidence must match the retained duty sources.");
        if (BranchId.HasValue && !CorrectionId.HasValue && new[] { checkIn, checkOut }.Where(x => x is not null).Any(x =>
                x!.BusinessDate != attendanceDate || x.ShiftVersionId != shiftVersionId))
            throw new ArgumentException("Accepted office raw evidence must retain its duty day and shift.");
        ShiftVersionId = ModelGuard.Id(shiftVersionId, nameof(shiftVersionId)); PolicyVersionId = AttendanceModelGuard.OptionalId(policyVersionId, nameof(policyVersionId));
        CalculatedAtUtc = ModelGuard.Utc(calculatedAtUtc); EndsContinuousDuty = endsContinuousDuty;
    }
    public Guid Id { get; private set; }
    public Guid SessionKey { get; private set; }
    public int Revision { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public Guid? CheckInPunchId { get; private set; }
    public Guid? CheckOutPunchId { get; private set; }
    public Guid? CorrectionId { get; private set; }
    public Guid? SiteId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? OfficeRosterAssignmentId { get; private set; }
    public Guid? EmployeeAssignmentId { get; private set; }
    public Guid? OfficeGeofenceVersionId { get; private set; }
    public Guid? HolidayCalendarId { get; private set; }
    public Guid ShiftVersionId { get; private set; }
    public Guid? PolicyVersionId { get; private set; }
    public DateTimeOffset CalculatedAtUtc { get; private set; }
    public bool EndsContinuousDuty { get; private set; }
    public RawPunchEvent? CheckInPunch { get; private set; }
    public RawPunchEvent? CheckOutPunch { get; private set; }
}
