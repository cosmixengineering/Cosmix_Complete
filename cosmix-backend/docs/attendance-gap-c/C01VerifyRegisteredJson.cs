#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property InterceptorsNamespaces=Microsoft.AspNetCore.OpenApi.Generated
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Controllers.HR;
using Cosmix.Api.OpenApi;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
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
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
var guard = new NoConnections();
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions {
    EnvironmentName = "Development", ApplicationName = typeof(AdvancedAttendancePoliciesController).Assembly.GetName().Name,
    ContentRootPath = Directory.GetCurrentDirectory()
});
builder.WebHost.UseKestrel();
builder.Host.UseDefaultServiceProvider(options => { options.ValidateScopes = true; options.ValidateOnBuild = false; });
builder.Services.AddLogging(); builder.Services.AddRouting(); builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(AdvancedAttendancePoliciesController).Assembly);
builder.Services.AddOpenApi(options => {
    options.AddDocumentTransformer<BearerDocumentTransformer>(); options.AddOperationTransformer<BearerOperationTransformer>();
    options.AddOperationTransformer<OrganizationOperationTransformer>(); options.AddSchemaTransformer<AttendancePolicySchemaTransformer>();
});
builder.Services.AddAdvancedAttendancePoliciesApplication().AddAdvancedAttendancePoliciesApplication();
builder.Services.AddAdvancedAttendancePoliciesInfrastructure().AddAdvancedAttendancePoliciesInfrastructure();
builder.Services.AddScoped(_ => new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=c01-no-connection.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true").AddInterceptors(guard).Options));
builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddScoped<AttendanceOperation>();
builder.Services.AddSingleton(NoCalls.Make<ICurrentAuthentication>()); builder.Services.AddSingleton(NoCalls.Make<IAccountAccessReader>());
builder.Services.AddSingleton(NoCalls.Make<IAccessManagementRepository>()); builder.Services.AddSingleton(NoCalls.Make<ISchedulingJournalRepository>());
builder.Services.AddSingleton(NoCalls.Make<IAuthenticationRepository>()); builder.Services.AddSingleton(NoCalls.Make<IPrivateFileStore>());
builder.Services.AddSingleton(NoCalls.Make<IAttendanceSummaryRepository>());
await using var app = builder.Build(); app.MapControllers();
using (var scope = app.Services.CreateScope()) {
    var provider = scope.ServiceProvider;
    Check(provider.GetRequiredService<IAdvancedAttendancePolicyReader>() is AdvancedPolicyReader, "registered frozen reader resolves to real C01 implementation");
    Check(ReferenceEquals(provider.GetRequiredService<IAdvancedAttendancePolicyRepository>(), provider.GetRequiredService<IAdvancedPolicyPublicationRepository>()),
        "frozen and extended repository contracts share one scoped instance");
    Check(provider.GetServices<IAdvancedAttendancePolicyReader>().Count() == 1 && provider.GetServices<IAdvancedPolicyPublicationRepository>().Count() == 1,
        "feature entrypoints are idempotent and grant nothing");
    Check(ActivatorUtilities.CreateInstance<AdvancedAttendancePoliciesController>(provider) is not null, "actual controller activates through scoped feature DI");
}
var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
    .Where(x => x.ControllerTypeInfo.AsType() == typeof(AdvancedAttendancePoliciesController)).ToArray();
Check(actions.Length == 8 && actions.All(x => x.EndpointMetadata.OfType<AuthorizeAttribute>().Any()), "actual MVC discovers eight authenticated family actions");
Check(actions.All(x => x.EndpointMetadata.OfType<ResponseCacheAttribute>().Any(c => c.NoStore)), "all registered family responses are no-store");
foreach (var action in actions.Where(x => new[] { "Publish", "Assign", "End", "Replace" }.Contains(x.MethodInfo.Name))) {
    Check(action.EndpointMetadata.OfType<IRequestSizeLimitMetadata>().Single().MaxRequestBodySize == 32768, "bounded registered body: " + action.MethodInfo.Name);
    Check(action.FilterDescriptors.Select(x => x.Filter).OfType<StrictAttendancePolicyJsonAttribute>().Count() == 1, "registered strict JSON filter: " + action.MethodInfo.Name);
}
var options = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
var family = AdvancedAttendanceFamily.EarlyCredit;
var rules = new AdvancedAttendanceRules(1, family, ["OFFICE"], new("Explicit approved reference",
    AdvancedAttendanceRulesCodec.ReviewPermission(family), true, [AdvancedEvidenceKind.WrittenInstruction]), EarlyCredit: new(3600, true, true));
var request = new PublishAdvancedPolicyRequest(family, null, 0, new(2026, 10, 20), null, rules, "Future publication");
var json = JsonSerializer.Serialize(request, options);
var publish = actions.Single(x => x.MethodInfo.Name == "Publish");
var filter = publish.FilterDescriptors.Select(x => x.Filter).OfType<IAsyncResourceFilter>().Single();
async Task Body(string body, int? status, string name)
{
    var http = new DefaultHttpContext(); http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
    var context = new ResourceExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary()), [], []);
    var proceeded = false;
    await filter.OnResourceExecutionAsync(context, () => {
        proceeded = true;
        using var reader = new StreamReader(http.Request.Body, leaveOpen: true);
        var original = reader.ReadToEnd(); Check(original == body, "strict filter retains exact binder bytes");
        Check(JsonSerializer.Deserialize<PublishAdvancedPolicyRequest>(original, options) is not null, "actual MVC JSON options bind allowed publication body");
        return Task.FromResult(new ResourceExecutedContext(context, []));
    });
    Check((context.Result as ObjectResult)?.StatusCode == status && proceeded == (status is null), name);
    if (context.Result is ObjectResult { Value: ProblemDetails problem }) Check(problem.Detail is null, "JSON failure echoes no sensitive rule content");
}
await Body(json, null, "explicit nullable concurrency token is valid on first publication");
await Body(json.Replace("\"expectedVersion\":null,", ""), 400, "omitted expectedVersion is rejected even though null is allowed");
await Body(json.Replace("\"expectedRevision\":0", "\"expectedRevision\":0,\"expectedRevision\":1"), 400, "duplicate outer revision rejected");
await Body(json.Replace("\"maximumPreStartSeconds\":3600", "\"maximumPreStartSeconds\":3600,\"maximumPreStartSeconds\":1800"), 400, "duplicate nested rule rejected");
await Body(json.Replace("\"maximumPreStartSeconds\":3600", "\"maximumPreStartSeconds\":3600,\"\\u006daximumPreStartSeconds\":1800"), 400, "escaped duplicate nested rule rejected");
await Body(json.Replace("\"expectedRevision\":0", "\"expectedRevision\":\"0\""), 400, "quoted numeric concurrency field rejected");
await Body(json.Replace("\"expectedRevision\":0", "\"expectedRevision\":0,\"approved\":true"), 400, "client cannot add approval authority");
await Body(json.Replace("\"expectedRevision\":0", "\"ExpectedRevision\":0"), 400, "case alias rejected by registered strict filter");
await Body(json.Replace("\"allowedEvidence\":[5]", "\"allowedEvidence\":[]"), null, "typed evidence emptiness remains explicit codec validation, never a JSON default");
await Body("{}", 400, "missing family and nested required choices rejected");
await Body("null", 400, "null request body rejected");
await Body("{", 400, "malformed request body rejected");
await Body(new string(' ', 32769), 413, "chunked-sized raw body bounded before binding");
var resolution = new AdvancedPolicyResolution("configured", Guid.NewGuid(), family, request.EffectiveFrom, Guid.NewGuid(), [], rules, new string('A', 64));
Check(JsonSerializer.Deserialize<AdvancedPolicyResolution>(JsonSerializer.Serialize(resolution, options), options)?.State == "configured", "actual MVC response JSON preserves parent-facing configured state");
var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync(default);
var prefix = "/api/v1/hr/advanced-attendance-policies";
Check(document.Paths.Count(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)) == 7, "actual OpenAPI registers seven family paths for eight actions");
var publishOperation = document.Paths[prefix + "/versions"].Operations![HttpMethod.Post];
IOpenApiSchema Schema(IOpenApiSchema schema) => schema is OpenApiSchemaReference reference ? reference.Target! : schema;
var publishSchema = Schema(publishOperation.RequestBody!.Content!["application/json"].Schema!);
Check(publishSchema.Required!.Contains("expectedVersion") && publishSchema.Required.Contains("expectedRevision") && publishSchema.Required.Contains("rules"),
    "actual registered schema requires explicit concurrency and complete typed rules");
Check(!publishSchema.AdditionalPropertiesAllowed, "actual registered publication schema forbids unknown client fields");
Check(NoCalls.Attempts == 0 && guard.Attempts == 0, "DI/MVC/OpenAPI/JSON checks start no host and cross no SQL/private/auth/provider boundary");
Console.WriteLine($"PASS: {checks} C01 registered DI/MVC/OpenAPI/strict JSON assertions; zero SQL, host starts or provider calls.");

public class NoCalls : DispatchProxy
{
    public static int Attempts;
    protected override object? Invoke(MethodInfo? method, object?[]? args) { Attempts++; throw new Exception("Forbidden metadata IO: " + method?.Name); }
    public static T Make<T>() where T : class => Create<T, NoCalls>();
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("C01 SQL forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("C01 SQL forbidden"); }
}
