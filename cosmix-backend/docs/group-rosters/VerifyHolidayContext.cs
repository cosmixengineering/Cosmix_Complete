#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyHolidayContext.packages.lock.json

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Holidays;

// Standalone pure application contract checks. No calendar applicability decision,
// authorization, SQL, private files, authentication or HTTP execution is implied.
// dotnet run --file docs/group-rosters/VerifyHolidayContext.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Application" --property:NuGetAudit=false
var helper = typeof(RosterSchedule).Assembly.GetType("Cosmix.Application.Attendance.Rosters.RosterHolidayContext");
if (helper is null)
{
    Console.Error.WriteLine("FAIL: explicit selected-revision holiday context helper is absent.");
    return 1;
}
var checks = 0;
var failures = new List<string>();
void Check(bool condition, string name)
{
    if (condition) { checks++; Console.WriteLine("PASS: " + name); }
    else { failures.Add(name); Console.Error.WriteLine("FAIL: " + name); }
}
object Resolve(HolidayCalendar? calendar, DateOnly date)
{
    try { return helper.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [calendar, date])!; }
    catch (TargetInvocationException error) when (error.InnerException is not null)
    { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
}
T Value<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;
object[] Holidays(object context) => ((IEnumerable)context.GetType().GetProperty("Holidays")!.GetValue(context)!).Cast<object>().ToArray();
void Set(object value, string property, object? replacement) => value.GetType().GetProperty(property)!.SetValue(value, replacement);
void Reject(Action action, string name, string code = "holiday_calendar_revision_conflict")
{
    try { action(); Check(false, name + " did not reject"); }
    catch (AuthenticationFailure failure) { Check(failure.Code == code, name + ": " + failure.Code); }
}
var date = new DateOnly(2030, 1, 1);
const string calendarSecret = "PRIVATE-calendar-notification-do-not-expose";
const string holidaySecret = "PRIVATE-holiday-notification-do-not-expose";
HolidayCalendar Calendar(DateOnly? from = null, DateOnly? to = null) =>
    new("FEDERAL-EXPLICIT", "Synthetic approved calendar", from ?? date, to ?? date.AddDays(10), calendarSecret);
Holiday Holiday(HolidayCalendar calendar, DateOnly? on = null, string name = "Synthetic holiday") =>
    new(calendar.Id, on ?? date, name, holidaySecret);

var empty = Resolve(null, date);
Check(Value<string>(empty, "Status") == "not_configured", "no selected calendar is explicitly not configured");
Check(Value<DateOnly>(empty, "BusinessDate") == date && Value<Guid?>(empty, "CalendarId") is null &&
    Value<string?>(empty, "CalendarCode") is null && Value<int?>(empty, "CalendarRevision") is null,
    "not-configured context preserves date without inventing calendar identity");
Check(Holidays(empty).Length == 0 && Value<bool>(empty, "IsInformational") && Value<bool>(empty, "DoesNotCancelDuty"),
    "not-configured context remains informational and does not cancel duty");
Reject(() => Resolve(null, default), "default business date is rejected even without a calendar", "invalid_business_date");

var original = Calendar();
var holiday = Holiday(original, name: "Original holiday name");
original.Holidays.Add(holiday);
original.Holidays.Add(Holiday(original, date.AddDays(2), "Different date"));
var configured = Resolve(original, date);
Check(Value<string>(configured, "Status") == "configured", "calendar start date is inclusive");
Check(Value<Guid?>(configured, "CalendarId") == original.Id && Value<string?>(configured, "CalendarCode") == original.Code &&
    Value<int?>(configured, "CalendarRevision") == 1, "context retains exact selected calendar revision identity");
Check(Value<DateOnly>(configured, "BusinessDate") == date, "context retains the requested business date");
Check(Holidays(configured).Length == 1 && Value<Guid>(Holidays(configured)[0], "Id") == holiday.Id &&
    Value<string>(Holidays(configured)[0], "Name") == "Original holiday name", "only the matching date's safe holiday identity/name are returned");
Check(Value<bool>(configured, "IsInformational") && Value<bool>(configured, "DoesNotCancelDuty"),
    "a configured holiday is informational and never cancels duty");
var ordinaryDay = Resolve(original, date.AddDays(1));
Check(Value<string>(ordinaryDay, "Status") == "configured" && Holidays(ordinaryDay).Length == 0,
    "configured non-holiday date is distinct from not configured");
var before = Resolve(original, date.AddDays(-1));
var after = Resolve(original, date.AddDays(10));
Check(Value<string>(before, "Status") == "outside_calendar_period" && Holidays(before).Length == 0,
    "date before the selected interval returns outside-calendar-period");
Check(Value<string>(after, "Status") == "outside_calendar_period" && Holidays(after).Length == 0,
    "selected calendar end is exclusive");
Check(Value<Guid?>(after, "CalendarId") == original.Id && Value<int?>(after, "CalendarRevision") == 1,
    "out-of-period context retains the explicit selected revision");
Check(Value<bool>(before, "DoesNotCancelDuty") && Value<bool>(after, "DoesNotCancelDuty"),
    "out-of-period context does not alter duty");

var corrected = original.CreateRevision("Corrected calendar", calendarSecret, true);
var correctedHoliday = Holiday(corrected, name: "Corrected holiday name");
corrected.Holidays.Add(correctedHoliday);
var originalAgain = Resolve(original, date);
var correction = Resolve(corrected, date);
Check(Value<Guid?>(originalAgain, "CalendarId") == original.Id && Value<int?>(originalAgain, "CalendarRevision") == 1 &&
    Value<string>(Holidays(originalAgain)[0], "Name") == "Original holiday name",
    "superseded selected revision remains exact and is never replaced by latest");
Check(Value<Guid?>(correction, "CalendarId") == corrected.Id && Value<int?>(correction, "CalendarRevision") == 2 &&
    Value<string>(Holidays(correction)[0], "Name") == "Corrected holiday name",
    "a separately selected corrected revision retains its own holiday evidence");
Check(Value<Guid>(Holidays(originalAgain)[0], "Id") != Value<Guid>(Holidays(correction)[0], "Id"),
    "original and corrected contexts retain different immutable holiday identities");
var deactivated = corrected.CreateRevision("Inactive calendar", calendarSecret, false);
deactivated.Holidays.Add(Holiday(deactivated));
var inactive = Resolve(deactivated, date);
Check(Value<string>(inactive, "Status") == "inactive_calendar" && Holidays(inactive).Length == 0,
    "inactive selected revision is explicit and produces no active holiday claim");
Check(Value<Guid?>(inactive, "CalendarId") == deactivated.Id && Value<int?>(inactive, "CalendarRevision") == 3 &&
    Value<bool>(inactive, "DoesNotCancelDuty"), "inactive context retains selected identity without cancelling duty");
Check(Value<string>(Resolve(deactivated, date.AddDays(11)), "Status") == "outside_calendar_period",
    "an outside date remains outside even when selected revision is inactive");

var unbounded = new HolidayCalendar("UNBOUNDED", "Synthetic unbounded", date, null, calendarSecret);
unbounded.Holidays.Add(Holiday(unbounded, DateOnly.MaxValue));
var max = Resolve(unbounded, DateOnly.MaxValue);
Check(Value<string>(max, "Status") == "configured" && Holidays(max).Length == 1,
    "calendar context supports the maximum date without day arithmetic overflow");

foreach (var corruption in new Action<HolidayCalendar>[]
{
    x => Set(x, "Revision", 0),
    x => Set(x, "Revision", -1),
    x => Set(x, "Revision", 2),
    x => Set(x, "SupersedesCalendarId", Guid.NewGuid()),
    x => { Set(x, "Revision", 2); Set(x, "SupersedesCalendarId", Guid.Empty); },
    x => { Set(x, "Revision", 2); Set(x, "SupersedesCalendarId", x.Id); },
    x => Set(x, "Id", Guid.Empty),
    x => Set(x, "EffectiveFrom", default(DateOnly)),
    x => Set(x, "EffectiveTo", x.EffectiveFrom),
    x => Set(x, "Code", " ")
})
{
    var broken = Calendar(); corruption(broken);
    Reject(() => Resolve(broken, date), "corrupt selected revision metadata fails closed");
}
var wrongOwner = Calendar();
wrongOwner.Holidays.Add(Holiday(Calendar()));
Reject(() => Resolve(wrongOwner, date), "holiday from a different calendar revision fails closed");
var duplicate = Calendar();
duplicate.Holidays.Add(Holiday(duplicate)); duplicate.Holidays.Add(Holiday(duplicate));
Reject(() => Resolve(duplicate, date), "duplicate holiday date fails closed");
Reject(() => Resolve(duplicate, date.AddDays(1)), "duplicate holiday date invalidates the revision even on another day");
var duplicateIdentity = Calendar();
var one = Holiday(duplicateIdentity); var two = Holiday(duplicateIdentity, date.AddDays(1));
Set(two, "Id", one.Id);
duplicateIdentity.Holidays.Add(one); duplicateIdentity.Holidays.Add(two);
Reject(() => Resolve(duplicateIdentity, date), "duplicate holiday identity fails closed");
foreach (var badDate in new[] { default(DateOnly), date.AddDays(-1), date.AddDays(10) })
{
    var bad = Calendar(); bad.Holidays.Add(Holiday(bad, badDate));
    Reject(() => Resolve(bad, date), "holiday outside its selected calendar interval fails closed");
}
var nullHoliday = Calendar(); nullHoliday.Holidays.Add(null!);
Reject(() => Resolve(nullHoliday, date), "null holiday record fails closed");
var badIdentity = Calendar(); var blankId = Holiday(badIdentity); Set(blankId, "Id", Guid.Empty); badIdentity.Holidays.Add(blankId);
Reject(() => Resolve(badIdentity, date), "holiday identity cannot be empty");
var badName = Calendar(); var unnamed = Holiday(badName); Set(unnamed, "Name", " "); badName.Holidays.Add(unnamed);
Reject(() => Resolve(badName, date), "holiday display name cannot be empty");

var json = JsonSerializer.Serialize(configured, configured.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
using var parsed = JsonDocument.Parse(json);
Check(!json.Contains(calendarSecret, StringComparison.Ordinal) && !json.Contains(holidaySecret, StringComparison.Ordinal) &&
    !json.Contains("reference", StringComparison.OrdinalIgnoreCase), "serialized context exposes no private notification references");
var holidayKeys = parsed.RootElement.GetProperty("holidays")[0].EnumerateObject().Select(x => x.Name).Order().ToArray();
Check(holidayKeys.SequenceEqual(new[] { "id", "name" }), "holiday JSON contains only the safe id/name contract");
Check(parsed.RootElement.GetProperty("isInformational").GetBoolean() && parsed.RootElement.GetProperty("doesNotCancelDuty").GetBoolean(),
    "clients receive explicit informational and does-not-cancel-duty flags");
Check(!parsed.RootElement.EnumerateObject().Any(x => x.Name is "startsAt" or "endsAt" or "payableMinutes" or "isAuthorized"),
    "context contract makes no schedule, pay or authority claim");
original.Holidays.Clear();
Check(Holidays(configured).Length == 1 && Value<string>(Holidays(configured)[0], "Name") == "Original holiday name",
    "resolved context owns its result and is not changed by later in-memory source edits");
Check(failures.Count == 0, "all holiday-context expectations passed");
Console.WriteLine($"Holiday context: {checks} passed, {failures.Count} failed. Pure selected-revision context only; no applicability or API workflow claimed.");
return failures.Count == 0 ? 0 : 1;
