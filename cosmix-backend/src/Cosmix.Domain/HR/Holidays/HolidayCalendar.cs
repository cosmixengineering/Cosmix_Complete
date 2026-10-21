using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Holidays;

public sealed class HolidayCalendar : IAppendOnlyEntity
{
    private HolidayCalendar() { }

    public HolidayCalendar(string code, string name, DateOnly effectiveFrom, DateOnly? effectiveTo, string governmentNotificationReference)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        var normalizedCode = ModelGuard.Code(code, 40);
        var validatedName = ModelGuard.Required(name, 200, nameof(name));
        var validatedReference = ModelGuard.Required(governmentNotificationReference, 500, nameof(governmentNotificationReference));
        Id = Guid.NewGuid();
        Code = normalizedCode;
        Name = validatedName;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        GovernmentNotificationReference = validatedReference;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string GovernmentNotificationReference { get; private set; } = string.Empty;
    public int Revision { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;
    public Guid? SupersedesCalendarId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public ICollection<Holiday> Holidays { get; private set; } = new List<Holiday>();

    public HolidayCalendar CreateRevision(string name, string governmentNotificationReference, bool isActive) =>
        new(Code, name, EffectiveFrom, EffectiveTo, governmentNotificationReference)
        {
            Revision = checked(Revision + 1),
            IsActive = isActive,
            SupersedesCalendarId = Id
        };
}
