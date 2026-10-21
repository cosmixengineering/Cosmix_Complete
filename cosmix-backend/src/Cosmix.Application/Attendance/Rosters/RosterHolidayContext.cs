using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.HR.Holidays;

namespace Cosmix.Application.Attendance.Rosters;

public sealed record RosterHolidayItem(Guid Id, string Name);

/// <summary>Informational context from one explicitly selected immutable calendar revision.</summary>
public sealed record RosterHolidayContextResponse(string Status, DateOnly BusinessDate,
    Guid? CalendarId, string? CalendarCode, int? CalendarRevision, IReadOnlyList<RosterHolidayItem> Holidays)
{
    public bool IsInformational => true;
    public bool DoesNotCancelDuty => true;
}

/// <summary>
/// Projects an exact calendar revision already selected by a trusted caller. This helper does
/// not select applicability, follow a successor, authorize access or change a duty schedule.
/// A missing selection is explicitly not configured, including when other calendars exist.
/// </summary>
public static class RosterHolidayContext
{
    public static RosterHolidayContextResponse Resolve(HolidayCalendar? selectedCalendar, DateOnly businessDate)
    {
        if (businessDate == default) throw SchedulingOperation.Invalid("invalid_business_date");
        if (selectedCalendar is null) return new("not_configured", businessDate, null, null, null, []);

        var calendar = selectedCalendar;
        if (calendar.Id == Guid.Empty || !ValidText(calendar.Code, 40) || calendar.EffectiveFrom == default ||
            calendar.EffectiveTo <= calendar.EffectiveFrom || calendar.Revision < 1 ||
            (calendar.Revision == 1 && calendar.SupersedesCalendarId.HasValue) ||
            (calendar.Revision > 1 && (!calendar.SupersedesCalendarId.HasValue ||
                calendar.SupersedesCalendarId == Guid.Empty || calendar.SupersedesCalendarId == calendar.Id)) ||
            calendar.Holidays is null)
            throw Corrupt();

        var dates = new HashSet<DateOnly>();
        var identities = new HashSet<Guid>();
        var matching = new List<RosterHolidayItem>();
        // Validate the entire supplied revision, not only a matching day. Returning an empty
        // day must not turn malformed, cross-revision or duplicate evidence into a valid result.
        foreach (var holiday in calendar.Holidays)
        {
            if (holiday is null || holiday.Id == Guid.Empty || holiday.HolidayCalendarId != calendar.Id ||
                holiday.HolidayDate < calendar.EffectiveFrom ||
                (calendar.EffectiveTo.HasValue && holiday.HolidayDate >= calendar.EffectiveTo.Value) ||
                !ValidText(holiday.Name, 200) || !dates.Add(holiday.HolidayDate) || !identities.Add(holiday.Id))
                throw Corrupt();
            if (holiday.HolidayDate == businessDate) matching.Add(new(holiday.Id, holiday.Name));
        }

        var status = businessDate < calendar.EffectiveFrom ||
            (calendar.EffectiveTo.HasValue && businessDate >= calendar.EffectiveTo.Value)
            ? "outside_calendar_period"
            : !calendar.IsActive ? "inactive_calendar" : "configured";
        IReadOnlyList<RosterHolidayItem> holidays = status == "configured"
            ? Array.AsReadOnly(matching.ToArray()) : [];
        return new(status, businessDate, calendar.Id, calendar.Code, calendar.Revision, holidays);
    }

    private static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Any(char.IsControl);

    private static Cosmix.Application.Authentication.AuthenticationFailure Corrupt() =>
        SchedulingOperation.Conflict("holiday_calendar_revision_conflict");
}
