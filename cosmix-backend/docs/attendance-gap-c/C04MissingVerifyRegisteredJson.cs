#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property InterceptorsNamespaces=Microsoft.AspNetCore.OpenApi.Generated
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions {
    EnvironmentName = "Development", ApplicationName = typeof(KpiMissingEvidenceController).Assembly.GetName().Name
});
builder.WebHost.UseKestrel(); builder.Services.AddLogging(); builder.Services.AddRouting(); builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(KpiMissingEvidenceController).Assembly);
builder.Services.AddKpiMissingEvidenceApplication(); builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddScoped<AttendanceOperation>();
builder.Services.AddSingleton(MissingNoCalls.Make<ICurrentAuthentication>()); builder.Services.AddSingleton(MissingNoCalls.Make<IAccountAccessReader>());
builder.Services.AddSingleton(MissingNoCalls.Make<IAccessManagementRepository>()); builder.Services.AddSingleton(MissingNoCalls.Make<ISchedulingJournalRepository>());
builder.Services.AddSingleton(MissingNoCalls.Make<IAuthenticationRepository>()); builder.Services.AddSingleton(MissingNoCalls.Make<IPrivateFileStore>());
builder.Services.AddSingleton(MissingNoCalls.Make<IKpiMissingEvidenceRepository>());
builder.Services.AddScoped<IKpiMissingEvidenceSourceReader>(_ => MissingNoCalls.Make<IKpiMissingEvidenceSourceReader>());
await using var app = builder.Build(); app.MapControllers();
using (var scope = app.Services.CreateScope()) {
    Check(ActivatorUtilities.CreateInstance<KpiMissingEvidenceController>(scope.ServiceProvider) is not null, "actual controller and review service activate through feature DI");
    Check(scope.ServiceProvider.GetRequiredService<IReviewedKpiMissingEvidenceReader>() is KpiMissingEvidenceReader,
        "actual registered internal aggregation reader uses typed protected review producer");
}
var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
    .Where(x => x.ControllerTypeInfo.AsType() == typeof(KpiMissingEvidenceController)).ToArray();
Check(actions.Length == 5 && actions.All(x => x.EndpointMetadata.OfType<AuthorizeAttribute>().Any()), "five authenticated owned MVC actions including explicit revision discovered");
Check(actions.All(x => x.EndpointMetadata.OfType<ResponseCacheAttribute>().Any(c => c.NoStore)), "missing-evidence API responses are no-store");
var options = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
foreach (var method in new[] { "Prepare", "Decide", "Revise" }) {
    var action = actions.Single(x => x.MethodInfo.Name == method);
    Check(action.EndpointMetadata.OfType<IRequestSizeLimitMetadata>().Single().MaxRequestBodySize == 8192, "bounded write route " + method);
    var filter = action.FilterDescriptors.Select(x => x.Filter).OfType<IAsyncResourceFilter>().Single();
    var valid = method switch {
        "Prepare" => JsonSerializer.Serialize(new PrepareKpiMissingEvidenceRequest(new(2026, 10, 13), Guid.NewGuid(), new string('A', 64), "Actual source review"), options),
        "Decide" => JsonSerializer.Serialize(new DecideKpiMissingEvidenceRequest(Convert.ToBase64String(new byte[8]), ApprovalState.Approved, "Independent review"), options),
        _ => JsonSerializer.Serialize(new ReviseKpiMissingEvidenceRequest(Convert.ToBase64String(new byte[8]), new string('A', 64), "Independent revision") {
            ExpectedActiveRevision = Guid.NewGuid() }, options)
    };
    foreach (var (json, status) in new (string, int?)[] { (valid, null), ("{}", 400), ("null", 400),
        (valid[..^1] + ",\"missing\":true}", 400), (valid[..^1] + ",\"count\":1}", 400), (valid[..^1] + ",\"points\":1}", 400),
        (valid[..^1] + ",\"reason\":\"Duplicate\"}", 400) }) {
        var http = new DefaultHttpContext(); http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = new ResourceExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary()), [], []);
        var ran = false;
        await filter.OnResourceExecutionAsync(context, () => { ran = true; return Task.FromResult(new ResourceExecutedContext(context, [])); });
        Check((context.Result as ObjectResult)?.StatusCode == status && ran == (status is null), "strict registered JSON: " + method + " / " + (status?.ToString() ?? "valid"));
    }
}
var revisionAction = actions.Single(x => x.MethodInfo.Name == "Revise");
var revisionFilter = revisionAction.FilterDescriptors.Select(x => x.Filter).OfType<IAsyncResourceFilter>().Single();
var missingActive = new DefaultHttpContext(); missingActive.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
    "{\"expectedVersion\":\"AAAAAAAAAAA=\",\"expectedSourceFingerprint\":\"" + new string('A', 64) + "\",\"reason\":\"Review\"}"));
var missingContext = new ResourceExecutingContext(new ActionContext(missingActive, new RouteData(), new ActionDescriptor(), new ModelStateDictionary()), [], []);
await revisionFilter.OnResourceExecutionAsync(missingContext, () => throw new Exception("Omitted active revision was accepted"));
Check((missingContext.Result as ObjectResult)?.StatusCode == 400, "revision requires an explicit active revision identity, not a default");
Check(MissingNoCalls.Attempts == 0, "metadata and DI cross no source/auth/private boundary and start no host");
Console.WriteLine($"PASS: {checks} C04 missing actual MVC/DI/JSON assertions; no SQL or host start.");

public class MissingNoCalls : DispatchProxy {
    public static int Attempts;
    protected override object? Invoke(MethodInfo? method, object?[]? args) { Attempts++; throw new Exception("Forbidden metadata IO: " + method?.Name); }
    public static T Make<T>() where T : class => Create<T, MissingNoCalls>();
}
