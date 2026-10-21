using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Domain.Attendance.Shifts;

namespace Cosmix.Domain.Attendance.Rosters;

public sealed class OfficeRosterAssignment : IAppendOnlyEntity
{
    private OfficeRosterAssignment() { }
    public OfficeRosterAssignment(Guid branchId, Guid shiftId, DateOnly effectiveFrom, DateOnly? effectiveTo,
        Guid? holidayCalendarId, string recordedBy, DateTimeOffset recordedAt)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid(); BranchId = ModelGuard.Id(branchId, nameof(branchId));
        ShiftId = ModelGuard.Id(shiftId, nameof(shiftId)); EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo;
        HolidayCalendarId = holidayCalendarId.HasValue ? ModelGuard.Id(holidayCalendarId.Value, nameof(holidayCalendarId)) : null;
        RecordedBy = ModelGuard.Required(recordedBy, 200, nameof(recordedBy)); RecordedAtUtc = ModelGuard.Utc(recordedAt);
    }
    public Guid Id { get; private set; }
    public Guid BranchId { get; private set; }
    public Branch Branch { get; private set; } = null!;
    public Guid ShiftId { get; private set; }
    public Shift Shift { get; private set; } = null!;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public Guid? HolidayCalendarId { get; private set; }
    public HolidayCalendar? HolidayCalendar { get; private set; }
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
