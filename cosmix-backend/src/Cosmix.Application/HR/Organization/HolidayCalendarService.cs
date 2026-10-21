using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.HR.Holidays;

namespace Cosmix.Application.HR.Organization;

public sealed class HolidayCalendarService(IOrganizationRepository repository, OrganizationOperation operation)
{
    public Task<PagedResult<HolidayCalendarSummary>> ListAsync(OrganizationListRequest request, bool includeHistory, CancellationToken ct) =>
        operation.ReadAsync(Permissions.HolidaysRead, token =>
        {
            OrganizationOperation.Validate(request);
            return repository.ListCalendarsAsync(request, includeHistory, token);
        }, ct);

    public Task<HolidayCalendarResponse> GetAsync(Guid id, CancellationToken ct) =>
        operation.ReadAsync(Permissions.HolidaysRead, async token =>
            Map(await repository.FindCalendarAsync(id, token) ?? throw OrganizationOperation.Missing()), ct);

    public Task<PagedResult<OrganizationChangeResponse>> HistoryAsync(Guid id, OrganizationListRequest request, CancellationToken ct) =>
        operation.ReadAsync(Permissions.HolidaysRead, async token =>
        {
            OrganizationOperation.Validate(request);
            if (await repository.FindCalendarAsync(id, token) is null) throw OrganizationOperation.Missing();
            return await repository.HistoryAsync("HolidayCalendar", id, request, token);
        }, ct);

    public Task<HolidayCalendarResponse> CreateAsync(CreateHolidayCalendarRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        OrganizationOperation.Validate(request);
        ValidateDates(request.EffectiveFrom, request.EffectiveTo, request.Holidays);
        var input = new CreateHolidayCalendarRequest { Code = request.Code.ToUpperInvariant(), Name = request.Name.Trim(),
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
            GovernmentNotificationReference = request.GovernmentNotificationReference.Trim(), Holidays = Normalize(request.Holidays) };
        return operation.WriteAsync(Permissions.HolidaysManage, "HolidayCalendar", "organization.holidays.create", key,
            input, "Created", context, async token =>
            {
                if (await repository.CalendarOverlapsAsync(input.Code, input.EffectiveFrom, input.EffectiveTo, token))
                    throw OrganizationOperation.Conflict("holiday_calendar_interval_overlaps");
                var calendar = new HolidayCalendar(input.Code, input.Name, input.EffectiveFrom, input.EffectiveTo, input.GovernmentNotificationReference);
                Populate(calendar, input.Holidays);
                var result = await repository.AddCalendarAsync(calendar, token);
                return (calendar.Id, (object?)null, result);
            }, ct);
    }

    public Task<HolidayCalendarResponse> ReviseAsync(Guid id, ReviseHolidayCalendarRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        OrganizationOperation.Validate(request); OrganizationOperation.Version(request.ExpectedVersion);
        if (request.Holidays.Any(h => h is null)) throw OrganizationOperation.Invalid();
        foreach (var holiday in request.Holidays) OrganizationOperation.Validate(holiday);
        var input = new ReviseHolidayCalendarRequest { Name = request.Name.Trim(),
            GovernmentNotificationReference = request.GovernmentNotificationReference.Trim(), IsActive = request.IsActive,
            Holidays = Normalize(request.Holidays), ExpectedVersion = request.ExpectedVersion, Reason = request.Reason.Trim() };
        return operation.WriteAsync(Permissions.HolidaysManage, "HolidayCalendar", "organization.holidays.revise", key,
            new { Id = id, Input = input }, input.Reason, context, async token =>
            {
                var old = await repository.FindCalendarAsync(id, token) ?? throw OrganizationOperation.Missing();
                OrganizationOperation.ExpectVersion(Convert.ToBase64String(old.RowVersion), input.ExpectedVersion);
                if (await repository.HasSuccessorAsync(id, token)) throw OrganizationOperation.Conflict("holiday_revision_is_outdated");
                ValidateDates(old.EffectiveFrom, old.EffectiveTo, input.Holidays);
                var next = old.CreateRevision(input.Name, input.GovernmentNotificationReference, input.IsActive!.Value);
                Populate(next, input.Holidays);
                var result = await repository.AddCalendarAsync(next, token);
                return (next.Id, (object?)Map(old), result);
            }, ct);
    }

    private static HolidayInput[] Normalize(IEnumerable<HolidayInput> values) => values.OrderBy(h => h.Date)
        .Select(h => new HolidayInput { Date = h.Date, Name = h.Name.Trim(), NotificationReference = h.NotificationReference.Trim() }).ToArray();
    private static void Populate(HolidayCalendar calendar, IEnumerable<HolidayInput> holidays)
    {
        foreach (var holiday in holidays)
            calendar.Holidays.Add(new Holiday(calendar.Id, holiday.Date, holiday.Name, holiday.NotificationReference));
    }
    private static void ValidateDates(DateOnly from, DateOnly? to, HolidayInput[] holidays)
    {
        if (from == default || to <= from || holidays.Any(h => h is null) || holidays.Select(h => h.Date).Distinct().Count() != holidays.Length)
            throw OrganizationOperation.Invalid("invalid_holiday_interval_or_duplicates");
        foreach (var holiday in holidays)
        {
            OrganizationOperation.Validate(holiday);
            if (holiday.Date < from || (to.HasValue && holiday.Date >= to.Value))
                throw OrganizationOperation.Invalid("holiday_outside_calendar_interval");
        }
    }
    public static HolidayCalendarResponse Map(HolidayCalendar calendar) => new(
        new(calendar.Id, calendar.Code, calendar.Name, calendar.EffectiveFrom, calendar.EffectiveTo, calendar.Revision,
            calendar.IsActive, calendar.SupersedesCalendarId, Convert.ToBase64String(calendar.RowVersion)),
        calendar.GovernmentNotificationReference, calendar.Holidays.OrderBy(h => h.HolidayDate).ThenBy(h => h.Id)
            .Select(h => new HolidayResponse(h.Id, h.HolidayDate, h.Name, h.NotificationPrivateReference)).ToArray());
}
