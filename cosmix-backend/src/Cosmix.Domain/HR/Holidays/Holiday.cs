using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Holidays;

public sealed class Holiday : IAppendOnlyEntity
{
    private Holiday() { }

    public Holiday(Guid holidayCalendarId, DateOnly holidayDate, string name, string notificationPrivateReference)
    {
        Id = Guid.NewGuid();
        HolidayCalendarId = ModelGuard.Id(holidayCalendarId, nameof(holidayCalendarId));
        HolidayDate = holidayDate;
        Name = ModelGuard.Required(name, 200, nameof(name));
        NotificationPrivateReference = ModelGuard.Required(notificationPrivateReference, 500, nameof(notificationPrivateReference));
    }

    public Guid Id { get; private set; }
    public Guid HolidayCalendarId { get; private set; }
    public DateOnly HolidayDate { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NotificationPrivateReference { get; private set; } = string.Empty;
    public HolidayCalendar HolidayCalendar { get; private set; } = null!;
}
