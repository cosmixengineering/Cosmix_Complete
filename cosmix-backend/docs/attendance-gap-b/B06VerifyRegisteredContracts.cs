#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property InterceptorsNamespaces=Microsoft.AspNetCore.OpenApi.Generated
using System.Reflection;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Api.OpenApi;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Zones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

var checks = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL: " + name); checks++; }
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions {
    EnvironmentName = "Development", ApplicationName = typeof(ZoneOfflineObservationsController).Assembly.GetName().Name,
    ContentRootPath = Directory.GetCurrentDirectory()
});
builder.WebHost.UseKestrel();
builder.Host.UseDefaultServiceProvider(options => { options.ValidateScopes = true; options.ValidateOnBuild = false; });
builder.Services.AddLogging(); builder.Services.AddRouting(); builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(ZoneOfflineObservationsController).Assembly);
builder.Services.AddOpenApi(options => {
    options.AddDocumentTransformer<BearerDocumentTransformer>(); options.AddOperationTransformer<BearerOperationTransformer>();
    options.AddOperationTransformer<OrganizationOperationTransformer>(); options.AddSchemaTransformer<AttendancePolicySchemaTransformer>();
});
builder.Services.AddAttendanceZonesApplication(); builder.Services.AddAttendanceZonesInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddSingleton(new PrivateFileStorageOptions());
builder.Services.AddScoped<AttendanceOperation>();
builder.Services.AddSingleton(NoCalls.Make<ICurrentAuthentication>()); builder.Services.AddSingleton(NoCalls.Make<IAccountAccessReader>());
builder.Services.AddSingleton(NoCalls.Make<IAccessManagementRepository>()); builder.Services.AddSingleton(NoCalls.Make<ISchedulingJournalRepository>());
builder.Services.AddSingleton(NoCalls.Make<IAuthenticationRepository>()); builder.Services.AddSingleton(NoCalls.Make<IPrivateFileStore>());
builder.Services.AddSingleton(NoCalls.Make<IRosterRepository>()); builder.Services.AddSingleton(NoCalls.Make<IAttendanceCaptureRepository>());
builder.Services.AddSingleton(NoCalls.Make<IZoneRepository>());
await using var app = builder.Build(); app.MapControllers();
var defaults = app.Services.GetRequiredService<ZoneOfflineObservationOptions>();
Check(!defaults.Enabled && defaults.Policies.Count == 0, "registered host metadata has no reporting/policy activation default");
var storage = app.Services.GetRequiredService<IZoneClaimStore>();
Check(!storage.IsConfigured && ReferenceEquals(storage, app.Services.GetRequiredService<IZoneClaimStore>()), "real claim store is default-disabled and singleton for cursor/lease ownership");
Check(!app.Services.GetRequiredService<IZoneClaimRetentionStatus>().IsAvailable(TimeProvider.System.GetUtcNow()), "registered cleanup health starts unavailable");
Check(app.Services.GetServices<IHostedService>().OfType<ZoneClaimRetentionWorker>().Count() == 1, "feature entrypoint registers exactly one actual cleanup worker without starting it");
using (var scope = app.Services.CreateScope())
{
    var service = scope.ServiceProvider.GetRequiredService<ZoneOfflineObservationService>();
    Check(ReferenceEquals(service, scope.ServiceProvider.GetRequiredService<ZoneOfflineObservationService>()), "actual intake constructor resolves as scoped through legacy feature entrypoints");
    Check(scope.ServiceProvider.GetRequiredService<ZoneClaimRetentionService>() is not null, "actual scoped cleanup dependencies resolve");
    Check(ActivatorUtilities.CreateInstance<ZoneOfflineObservationsController>(scope.ServiceProvider) is not null,
        "registered offline controller can be activated without SQL/provider/private storage reads");
}
Check(NoCalls.Attempts == 0, "DI and metadata resolution invoked no auth/data/provider boundary");
var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
    .Where(x => x.ControllerTypeInfo.AsType() == typeof(ZoneOfflineObservationsController)).ToArray();
Check(actions.Length == 2 && actions.All(x => x.EndpointMetadata.OfType<AuthorizeAttribute>().Any()), "real MVC discovers exactly authenticated submit/reconciliation actions");
Check(actions.All(x => x.EndpointMetadata.OfType<ResponseCacheAttribute>().Any(y => y.NoStore)), "registered actions retain no-store metadata");
var submitAction = actions.Single(x => x.MethodInfo.Name == "Submit");
Check(submitAction.EndpointMetadata.OfType<IRequestSizeLimitMetadata>().Single().MaxRequestBodySize == 8192, "actual registered intake body has bounded 8192-byte transport limit");
var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync(default);
OpenApiOperation Operation(string path, HttpMethod method)
{
    Check(document.Paths.TryGetValue(path, out var item) && item.Operations?.ContainsKey(method) == true, "registered " + method + " " + path);
    return document.Paths[path].Operations![method];
}
IOpenApiSchema Schema(IOpenApiSchema schema) => schema is OpenApiSchemaReference reference ? reference.Target ?? throw new Exception("Unresolved schema") : schema;
IOpenApiSchema Response(OpenApiOperation operation) => Schema(operation.Responses!["200"].Content!["application/json"].Schema!);
bool Nullable(IOpenApiSchema schema)
{
    var value = Schema(schema);
    return value.Type?.HasFlag(JsonSchemaType.Null) == true ||
        (value.AnyOf ?? []).Concat(value.OneOf ?? []).Any(x => Schema(x).Type?.HasFlag(JsonSchemaType.Null) == true);
}
IOpenApiSchema ObjectSchema(IOpenApiSchema raw)
{
    var schema = Schema(raw);
    if (schema.Properties is not null) return schema;
    foreach (var child in (schema.AllOf ?? []).Concat(schema.AnyOf ?? []).Concat(schema.OneOf ?? []))
    {
        var value = Schema(child);
        if (value.Properties is not null) return value;
    }
    throw new Exception("Expected a registered object schema");
}
void SafeResponse(IOpenApiSchema root, string name)
{
    var seen = new HashSet<IOpenApiSchema>(ReferenceEqualityComparer.Instance); var pending = new Queue<IOpenApiSchema>(); pending.Enqueue(root);
    var forbidden = new HashSet<string>(["writtenClaim", "writtenClaimPrivateReference", "locationPrivateReference", "privateEvidenceReference",
        "storageKey", "keyReference", "latitude", "longitude", "accuracyMeters", "reasonCode", "privateRestrictionReasonReference"], StringComparer.OrdinalIgnoreCase);
    while (pending.TryDequeue(out var raw))
    {
        var schema = Schema(raw); if (!seen.Add(schema)) continue;
        if (schema.Properties is not null)
            foreach (var (key, value) in schema.Properties) { Check(!forbidden.Contains(key), name + " excludes " + key); pending.Enqueue(value); }
        if (schema.Items is not null) pending.Enqueue(schema.Items);
        foreach (var child in (schema.AllOf ?? []).Concat(schema.AnyOf ?? []).Concat(schema.OneOf ?? [])) pending.Enqueue(child);
    }
}
var path = "/api/v1/me/attendance/zones/monitoring-windows/{windowId}/offline-observations";
var intake = Operation(path, HttpMethod.Post); var reconcile = Operation(path + "/reconciliation", HttpMethod.Get);
foreach (var operation in new[] { intake, reconcile })
{
    var header = operation.Parameters!.Single(x => x.Name == "Idempotency-Key" && x.In == ParameterLocation.Header);
    var key = Schema(header.Schema!);
    Check(header.Required && key.MinLength == 8 && key.MaxLength == 100 && key.Pattern == ZoneOfflineObservationService.KeyPattern,
        "registered original-key header uses exact 8-100 regex/case rule");
    Check(operation.Security?.Count == 1 && operation.Security.Single().Keys.Single().Reference.Id == "Bearer",
        "actual security transformer advertises mobile bearer only, not web/cookie authority");
    Check(new[] { "200", "400", "401", "403", "409", "503" }.All(x => operation.Responses!.ContainsKey(x)), "typed success and bounded current-auth/policy/key failures are registered");
}
Check(reconcile.RequestBody is null && reconcile.Parameters!.All(x => x.In is ParameterLocation.Path or ParameterLocation.Header),
    "reconciliation is GET with no employee/account/device/proof/password query authority");
Check(intake.Description?.Contains("unverified", StringComparison.OrdinalIgnoreCase) == true && intake.Description.Contains("24-hour") &&
    intake.Description.Contains("never a blind new-key"), "registered intake describes unverified evidence and safe original-key retry");
Check(reconcile.Description?.Contains("not the original frozen receipt") == true && reconcile.Description.Contains("not authorization to resubmit"),
    "registered recovery distinguishes current retained metadata and denies automatic resubmission");
var body = Schema(intake.RequestBody!.Content!["application/json"].Schema!);
Check(body.Properties!.Keys.Order().SequenceEqual(new[] { "assignmentId", "configurationVersionId", "sessionKey", "noticeSha256", "deviceCapturedAtUtc", "eventKindHint", "writtenClaim" }.Order()),
    "actual request schema exposes only original metadata, hint and written claim, never GPS/authority selectors");
Check(!body.AdditionalPropertiesAllowed && new[] { "assignmentId", "configurationVersionId", "sessionKey", "deviceCapturedAtUtc", "eventKindHint" }.All(body.Required!.Contains),
    "actual strict request schema rejects unknown members and requires original historical bindings");
SafeResponse(Response(intake), "receipt"); SafeResponse(Response(reconcile), "reconciliation");
var report = ObjectSchema(Response(reconcile).Properties!["report"]);
Check(report.Properties!.ContainsKey("evidenceStatus") && report.Properties.ContainsKey("eventKindHint") && report.Properties.ContainsKey("isOffline") &&
    Nullable(report.Properties["kind"]), "actual report schema separates nullable asserted Kind from untrusted EventKindHint and evidence status");
var history = Operation("/api/v1/me/attendance/zones/events", HttpMethod.Get);
var ack = Operation("/api/v1/me/attendance/zones/events/{eventId}/responses", HttpMethod.Post);
var review = Operation("/api/v1/attendance/sites/{siteId}/zones/events/{eventId}/review", HttpMethod.Post);
SafeResponse(Response(history), "own history"); SafeResponse(Response(ack), "ack/dispute"); SafeResponse(Response(review), "review");
Check(Nullable(Response(ack).Properties!["kind"]) && Nullable(Response(review).Properties!["kind"]), "existing response pipelines cannot advertise all metadata as verified entered/exited");
Check(Operation("/api/v1/me/attendance/zones/monitoring-windows/{windowId}/samples", HttpMethod.Post).Description?.Contains("no offline replay collection") == true,
    "registered live sample contract remains separate and strict-live");
Check(NoCalls.Attempts == 0, "registered OpenAPI generation started no host/worker and called no SQL/private/provider/auth boundary");
Console.WriteLine($"PASS: {checks} B06 actual feature DI/MVC/OpenAPI checks; {document.Paths.Count} registered paths; no host start/SQL/worker/provider/mobile acceptance.");

public class NoCalls : DispatchProxy
{
    public static int Attempts;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    { Attempts++; throw new Exception("Metadata crossed a forbidden boundary: " + method?.Name); }
    public static T Make<T>() where T : class => Create<T, NoCalls>();
}
