#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text;
using System.Text.Json.Serialization;
using Cosmix.Api.Controllers.HR;
using Cosmix.Domain.HR.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

var filter = typeof(AttendancePolicySettingsController).GetMethod("Validate")!.GetCustomAttributes(true)
    .OfType<IAsyncResourceFilter>().SingleOrDefault() ?? throw new Exception("Policy endpoint must reject duplicate JSON before typed model binding");
var count = 0;
async Task Check(string json, int? expectedStatus, string name)
{
    var http = new DefaultHttpContext();
    http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
    var action = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
    var context = new ResourceExecutingContext(action, [], []);
    var called = false;
    await filter.OnResourceExecutionAsync(context, async () =>
    {
        called = true;
        using var reader = new StreamReader(http.Request.Body, leaveOpen: true);
        if (await reader.ReadToEndAsync() != json) throw new Exception("Model binder must see the unmodified request");
        return new ResourceExecutedContext(action, []);
    });
    var status = (context.Result as ObjectResult)?.StatusCode;
    if (status != expectedStatus || called != (expectedStatus is null)) throw new Exception(name);
    if (context.Result is ObjectResult { Value: ProblemDetails problem } && problem.Detail is not null)
        throw new Exception("Parser must not echo policy content");
    count++;
}
var rules = new AttendancePolicyRules(1, new(900, 3600, 9000),
    new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
        AttendanceEarlyExitTreatment.ActualWorkedTime, 16200, AttendanceNonScheduledDayTreatment.NoCredit, AttendanceNonScheduledDayTreatment.NoCredit),
    new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
        AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.WholeCurrencyUnitHalfUp));
var valid = "{\"rules\":" + AttendancePolicyRulesCodec.Canonicalize(rules) + "}";
await Check(valid, null, "Explicit supported body proceeds to normal authenticated MVC binding");
await Check("{\"rules\":null}", null, "Unresolved rules proceed to field validation");
await Check("{\"rules\":null,\"rules\":null}", 400, "Duplicate outer member refused");
await Check(valid.Replace("\"salaryBasis\":\"base_salary\"", "\"salaryBasis\":\"base_salary\",\"salaryBasis\":\"gross_salary\""), 400, "Conflicting duplicate salary basis refused");
await Check(valid.Replace("\"salaryBasis\":\"base_salary\"", "\"salaryBasis\":\"base_salary\",\"\\u0073alaryBasis\":\"gross_salary\""), 400, "Escaped duplicate key refused");
await Check(valid.Replace("\"salaryBasis\":\"base_salary\"", "\"salaryBasis\":\"base_salary\",\"SalaryBasis\":\"gross_salary\""), 400, "Case alias cannot bypass strict field names");
await Check(valid.Replace("\"base_salary\"", "1"), 400, "Numeric enum refused");
await Check(valid.Replace("\"occurrenceThreshold\":3", "\"occurrenceThreshold\":\"3\""), 400, "Quoted numeric rule refused");
await Check("{\"rules\":null,\"approved\":true}", 400, "Unknown authority field refused");
await Check("{", 400, "Malformed JSON safe failure");
await Check("null", 400, "Null body refused");
await Check(new string(' ', 32769), 413, "Bounded raw body without Content-Length");
var typedConstructor = typeof(StrictAttendancePolicyJsonAttribute).GetConstructor([typeof(Type)])
    ?? throw new Exception("Publication must validate its full typed envelope before model binding");
filter = (IAsyncResourceFilter)typedConstructor.Invoke([typeof(PublishEnvelope)]);
await Check("{\"rules\":null,\"reason\":\"Configure future policy\"}", null, "Publication envelope supported without dropping its strict rules");
await Check("{\"rules\":null,\"reason\":\"First\",\"reason\":\"Changed\"}", 400, "Duplicate publication reason rejected");
await Check("{\"rules\":null,\"reason\":\"Configure\",\"approved\":true}", 400, "Client cannot add publication authority");
Console.WriteLine($"PASS: {count} policy JSON boundary checks; real MVC resource filter, no host or SQL.");

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishEnvelope(AttendancePolicyRules? Rules, string Reason);
