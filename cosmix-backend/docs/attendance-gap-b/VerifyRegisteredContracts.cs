#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property InterceptorsNamespaces=Microsoft.AspNetCore.OpenApi.Generated
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Api.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

// Real MVC discovery and OpenAPI generation. No host start, SQL, private keys, workers or provider calls.
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions {
    EnvironmentName = "Development", ApplicationName = typeof(OfficeBoundariesController).Assembly.GetName().Name,
    ContentRootPath = Directory.GetCurrentDirectory()
});
builder.WebHost.UseKestrel();
builder.Services.AddLogging();
builder.Services.AddRouting();
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(OfficeBoundariesController).Assembly);
builder.Services.AddOpenApi(options => {
    options.AddDocumentTransformer<BearerDocumentTransformer>();
    options.AddOperationTransformer<BearerOperationTransformer>();
    options.AddOperationTransformer<OrganizationOperationTransformer>();
    options.AddSchemaTransformer<AttendancePolicySchemaTransformer>();
});
await using var app = builder.Build();
app.MapControllers();
var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1")
    .GetOpenApiDocumentAsync(default);
var checks = 0;
void Check(bool condition,string name) { if (!condition) throw new Exception(name); checks++; }
OpenApiOperation Operation(string path,HttpMethod method) {
    Check(document.Paths.TryGetValue(path,out var item) && item.Operations is not null && item.Operations.ContainsKey(method),
        "Registered operation: " + method + " " + path);
    return document.Paths[path].Operations![method];
}
var officePath = "/api/v1/company/offices/{branchId}/attendance-boundaries";
var officeRead = Operation(officePath,HttpMethod.Get);
var officePublish = Operation(officePath,HttpMethod.Post);
Check(officePublish.Parameters?.Any(x => x.Name == "Idempotency-Key" && x.In == ParameterLocation.Header && x.Required) == true,
    "Office write requires idempotency header");
Check(officePublish.Responses?["201"].Headers?.ContainsKey("Location") == true,"Created office version documents exact history Location");
Check(officePublish.Security?.Any(x => x.Keys.Any(y => y.Reference.Id == "CsrfToken")) == true,
    "Office write includes actual cookie/CSRF security scheme");
Check(officeRead.Description?.Contains("org.company.read") == true,"Office read documents existing company authority");
var policyPath = "/api/v1/attendance/capture-validation/policy";
var policyPublish = Operation(policyPath,HttpMethod.Post);
Operation(policyPath + "/preview",HttpMethod.Get);
Operation(policyPath + "/history",HttpMethod.Get);
Check(policyPublish.Security?.Any(x => x.Keys.Any(y => y.Reference.Id == "CsrfToken")) == true,
    "Capture policy management has web CSRF metadata");
Check(policyPublish.Description?.Contains("fixed Admin/SuperAdmin") == true,
    "Capture policy authority is distinct from salary/legal activation");
var reconcile = Operation("/api/v1/site-reports/mine/reconcile",HttpMethod.Get);
Check(reconcile.Parameters?.Any(x => x.Name == "Submission-Key" && x.In == ParameterLocation.Header && x.Required) == true,
    "Permanent own-report reconciliation accepts original key in header");
Check(document.Paths.Keys.All(x => !x.Contains("attendance/import",StringComparison.OrdinalIgnoreCase)),
    "Removed B08 import is not advertised as a workflow");
Check(document.Paths.Keys.Any(x => x.Contains("check-in",StringComparison.Ordinal)) &&
    document.Paths.Keys.Any(x => x.Contains("check-out",StringComparison.Ordinal)),"Existing capture routes remain registered");
Console.WriteLine($"PASS: {checks} registered MVC/OpenAPI checks; {document.Paths.Count} paths. No HTTP login, SQL, worker or device acceptance.");
