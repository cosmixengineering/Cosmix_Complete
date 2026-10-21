#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property InterceptorsNamespaces=Microsoft.AspNetCore.OpenApi.Generated
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Api.OpenApi;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Common.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

// Actual MVC discovery and OpenAPI generation only. Never executes Program or starts a listener.
var checks = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL: " + name); checks++; }
var assembly = typeof(MyAttendanceCorrectionsController).Assembly;
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions {
    EnvironmentName = "Development", ApplicationName = assembly.GetName().Name,
    ContentRootPath = Directory.GetCurrentDirectory()
});
builder.WebHost.UseKestrel();
builder.Services.AddLogging(); builder.Services.AddRouting(); builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(assembly);
builder.Services.AddOpenApi(options => {
    options.AddDocumentTransformer<BearerDocumentTransformer>();
    options.AddOperationTransformer<BearerOperationTransformer>();
    options.AddOperationTransformer<OrganizationOperationTransformer>();
    options.AddSchemaTransformer<AttendancePolicySchemaTransformer>();
});
await using var app = builder.Build(); app.MapControllers();
var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync(default);
var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
    .OfType<ControllerActionDescriptor>().ToArray();
IOpenApiSchema Schema(IOpenApiSchema schema) => schema is OpenApiSchemaReference reference
    ? reference.Target ?? throw new Exception("Unresolved schema") : schema;
OpenApiOperation Operation(string path, HttpMethod method, string permission, bool self = false)
{
    Check(document.Paths.TryGetValue(path, out var item) && item.Operations?.ContainsKey(method) == true,
        "registered " + method + " " + path);
    var operation = document.Paths[path].Operations![method];
    Check(operation.Description?.Contains(permission, StringComparison.Ordinal) == true, "documents exact permission: " + path);
    var schemes = operation.Security!.Select(x => x.Keys.Select(y => y.Reference.Id).ToArray()).ToArray();
    Check(schemes.Any(x => x.Contains("WebSession") && x.Contains("BrowserContext")) &&
        (self ? schemes.Any(x => x.Contains("Bearer")) : schemes.All(x => !x.Contains("Bearer"))), "self/management security split: " + path);
    Check(new[] { "200", "400", "401", "403", "404", "409", "503" }.All(x => operation.Responses!.ContainsKey(x)),
        "typed success and failure contracts: " + path);
    if (method == HttpMethod.Post)
    {
        Check(operation.Parameters?.Any(x => x.Name == "Idempotency-Key" && x.In == ParameterLocation.Header && x.Required) == true,
            "required idempotency header: " + path);
        Check(schemes.Any(x => x.Contains("CsrfToken")), "web writes require CSRF metadata: " + path);
        Check(operation.RequestBody?.Content?.ContainsKey("application/json") == true, "JSON write contract: " + path);
    }
    return operation;
}
var policies = "/api/v1/attendance/corrections/alternatives/policies";
Operation(policies, HttpMethod.Get, CorrectionPermissions.AlternativesConfigure);
var configure = Operation(policies, HttpMethod.Post, CorrectionPermissions.AlternativesConfigure);
var mine = Operation("/api/v1/me/attendance/corrections/alternatives/requirements", HttpMethod.Get, CorrectionPermissions.Self, self: true);
Check(mine.Parameters!.Count == 1 && mine.Parameters[0].Name == "kind" && mine.Parameters[0].In == ParameterLocation.Query && mine.Parameters[0].Required,
    "safe own requirements accept only required case kind, no employee/date/authority selectors");
var ownRequirements = Schema(mine.Responses!["200"].Content!["application/json"].Schema!);
Check(ownRequirements.Properties!.Keys.Order().SequenceEqual(new[] { "status", "kind", "policyId", "version", "effectiveFrom", "requiresSupportingDocuments", "allowedDocumentTypes" }.Order()),
    "safe requirements expose no policy actor, approval reference or protected documents");
var policyBody = Schema(configure.RequestBody!.Content!["application/json"].Schema!);
Check(!policyBody.AdditionalPropertiesAllowed && new[] { "kind", "effectiveFrom", "expectedLatestVersion", "requiresSupportingDocuments", "allowedDocumentTypes", "approvalReference", "reason" }
    .All(policyBody.Required!.Contains), "explicit required evidence policy with strict unknown-member rejection");

var queue = Operation("/api/v1/attendance/corrections/alternatives/review", HttpMethod.Get, CorrectionPermissions.AlternativesDecide);
var detailPath = "/api/v1/attendance/employees/{employeeId}/corrections/alternatives/{id}";
var detail = Operation(detailPath, HttpMethod.Get, CorrectionPermissions.AlternativesDecide);
var decide = Operation(detailPath + "/decide", HttpMethod.Post, CorrectionPermissions.AlternativesDecide);
var carryQueue = Operation("/api/v1/attendance/carry-forward/alternatives/review", HttpMethod.Get, CorrectionPermissions.AlternativesDecide);
var carryPath = "/api/v1/attendance/employees/{employeeId}/carry-forward/alternatives/{id}";
var carryDetail = Operation(carryPath, HttpMethod.Get, CorrectionPermissions.AlternativesDecide);
var carryDecide = Operation(carryPath + "/decide", HttpMethod.Post, CorrectionPermissions.AlternativesDecide);
foreach (var operation in new[] { queue, detail, decide, carryQueue, carryDetail, carryDecide })
    Check(operation.Description?.Contains("fixed ADMIN", StringComparison.Ordinal) == true && operation.Description.Contains("original") &&
        operation.Description.Contains("current"), "dedicated fixed Admin and retained/current scope contract");
Check(carryDecide.Description!.Contains("first later unlocked") && carryDecide.Description.Contains("never reopen") &&
    carryDecide.Description.Contains("no salary"), "factual-only locked carry-forward decision description");
foreach (var response in new[] { detail, carryDetail })
    Check(Schema(response.Responses!["200"].Content!["application/json"].Schema!).Properties!.ContainsKey("alternativeEvidence"),
        "protected alternative evidence is on authorized detail only");

var dedicated = actions.Where(x => x.ControllerName.Contains("Alternative", StringComparison.Ordinal)).ToArray();
Check(dedicated.Length == 9, "nine dedicated policy/self/review/detail/decision actions discovered by actual MVC");
foreach (var action in dedicated)
{
    Check(action.EndpointMetadata.OfType<AuthorizeAttribute>().Any() && !action.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any(), "authenticated " + action.DisplayName);
    Check(action.EndpointMetadata.OfType<ResponseCacheAttribute>().Any(x => x.NoStore), "no-store " + action.DisplayName);
    var post = action.MethodInfo.GetCustomAttribute<HttpPostAttribute>();
    if (post is not null)
        Check((long)action.MethodInfo.GetCustomAttributesData().Single(x => x.AttributeType == typeof(RequestSizeLimitAttribute)).ConstructorArguments[0].Value! == 8192,
            "bounded policy/decision request body");
}

var ordinarySubmit = document.Paths["/api/v1/me/attendance/corrections"].Operations![HttpMethod.Post];
var submitSchema = Schema(ordinarySubmit.RequestBody!.Content!["application/json"].Schema!);
Check(new[] { "alternativeKind", "expectedAlternativePolicyId", "alternativeEvidenceDocumentIds" }.All(submitSchema.Properties!.ContainsKey) &&
    !submitSchema.Required!.Contains("alternativeKind"), "existing intake exposes optional alternatives without breaking ordinary clients");
foreach (var path in new[] { "/api/v1/me/attendance/corrections/sources", "/api/v1/attendance/employees/{employeeId}/correction-requests/sources" })
    Check(Schema(document.Paths[path].Operations![HttpMethod.Get].Responses!["200"].Content!["application/json"].Schema!).Properties!.ContainsKey("officeSource"), "real Office source exposed: " + path);
var carryBody = Schema(document.Paths["/api/v1/me/attendance/carry-forward/requests"].Operations![HttpMethod.Post].RequestBody!.Content!["application/json"].Schema!);
Check(carryBody.Properties!.ContainsKey("alternativeKind") && !carryBody.Required!.Contains("alternativeKind") && carryBody.Required.Contains("expectedPriorAdjustmentId"),
    "carry-forward retains explicit nullable prior identity plus optional alternative case");

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var proposal = new CreateCorrectionRequest(new(2026,10,13), AttendanceCorrectionKind.ManualSession, null, null, null,
    new(2026,10,13,4,0,0,TimeSpan.Zero), new(2026,10,13,13,0,0,TimeSpan.Zero), null, "Synthetic example", "Synthetic duty evidence", new string('A',64));
var decision = new DecideCorrectionRequest("AAAAAAAAAAA=", ApprovalState.Approved, "Synthetic review");
var carryProposal = new SubmitCarryForwardRequest(new(2026,9,30), AttendanceCorrectionKind.ManualSession, null, null,
    proposal.RequestedTimeUtc!.Value, proposal.RequestedEndUtc, Guid.NewGuid(), Guid.NewGuid(), null, new(2026,10,1), new string('B',64), proposal.Reason, proposal.WrittenEvidence);
foreach (var json in new[] { JsonSerializer.Serialize(proposal,jsonOptions), JsonSerializer.Serialize(decision,jsonOptions), JsonSerializer.Serialize(carryProposal,jsonOptions) })
    Check(!json.Contains("alternativeKind") && !json.Contains("expectedAlternativePolicyId") && !json.Contains("alternativeEvidenceDocumentIds"), "legacy null alternative fields omitted, not serialized as null");
var explicitCase = proposal with { AlternativeKind = CorrectionAlternativeKind.Accessibility, ExpectedAlternativePolicyId = Guid.NewGuid(), AlternativeEvidenceDocumentIds = [] };
var restored = JsonSerializer.Deserialize<CreateCorrectionRequest>(JsonSerializer.Serialize(explicitCase,jsonOptions),jsonOptions)!;
Check(restored.AlternativeKind == explicitCase.AlternativeKind && restored.ExpectedAlternativePolicyId == explicitCase.ExpectedAlternativePolicyId && restored.AlternativeEvidenceDocumentIds is { Count: 0 },
    "explicit configured written-only case retains exact kind/policy/empty selected documents");
var absent = new CorrectionAlternativeRequirements("not_configured", CorrectionAlternativeKind.Medical,null,null,null,null,[]);
using var absentJson = JsonDocument.Parse(JsonSerializer.Serialize(absent,jsonOptions));
Check(absentJson.RootElement.GetProperty("requiresSupportingDocuments").ValueKind == JsonValueKind.Null && absentJson.RootElement.GetProperty("policyId").ValueKind == JsonValueKind.Null,
    "absent policy is explicit not_configured with unknown requirements, not an invented default");

// The dedicated route must reject a missing case BEFORE calling any service, even though the legacy DTO remains optional.
foreach (var action in dedicated.Where(x => x.MethodInfo.Name == "Decide"))
{
    var controller = Activator.CreateInstance(action.ControllerTypeInfo.AsType(), new object?[] { null })!;
    try
    {
        var task = (Task)action.MethodInfo.Invoke(controller, [Guid.NewGuid(),Guid.NewGuid(),decision,"synthetic-decision-0001",CancellationToken.None])!;
        await task;
        throw new Exception("Dedicated route accepted absent case");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is AuthenticationFailure { Code: "alternative_decision_kind_required", StatusCode: 400 }) { checks++; }
    catch (AuthenticationFailure exception) when (exception.Code == "alternative_decision_kind_required" && exception.StatusCode == 400) { checks++; }
}
void InvalidJson<T>(string json, string name)
{
    try { JsonSerializer.Deserialize<T>(json,jsonOptions); throw new Exception("Accepted invalid JSON: " + name); }
    catch (JsonException) { checks++; }
}
var explicitJson = JsonSerializer.Serialize(explicitCase,jsonOptions);
InvalidJson<CreateCorrectionRequest>(explicitJson[..^1] + ",\"faceVerified\":true}", "client face success override");
InvalidJson<DecideCorrectionRequest>(JsonSerializer.Serialize(decision,jsonOptions)[..^1] + ",\"actorRoleCode\":\"ADMIN\"}", "client Admin role override");
InvalidJson<ConfigureCorrectionAlternativePolicy>("{\"kind\":1,\"effectiveFrom\":\"2026-10-13\",\"expectedLatestVersion\":0,\"allowedDocumentTypes\":[],\"approvalReference\":\"Synthetic\",\"reason\":\"Synthetic policy\"}",
    "missing explicit supporting-document requirement");

// Read the shipped example pack and check its actual registered paths and JSON contracts, without issuing requests.
var exampleValues = new Dictionary<string,string> {
    ["baseUrl"] = "https://localhost:7240", ["employeeId"] = Guid.NewGuid().ToString(), ["kind"] = "2",
    ["effectiveFrom"] = "2026-10-14", ["expectedLatestVersion"] = "0", ["requiresSupportingDocuments"] = "false",
    ["allowedDocumentTypesJson"] = "[]", ["approvalReference"] = "Synthetic explicitly approved written-only policy",
    ["businessDate"] = "2026-09-30", ["from"] = "2026-09-01", ["to"] = "2026-10-01",
    ["startUtc"] = "2026-09-30T04:00:00Z", ["endUtc"] = "2026-09-30T13:00:00Z",
    ["assessmentIdJson"] = "null", ["summaryIdJson"] = "null", ["sourceFingerprint"] = new string('A',64),
    ["policyId"] = Guid.NewGuid().ToString(), ["evidenceDocumentIdsJson"] = "[]", ["requestId"] = Guid.NewGuid().ToString(),
    ["requestVersion"] = "AAAAAAAAAAA=", ["sourceLockEventId"] = Guid.NewGuid().ToString(), ["sourceSummaryId"] = Guid.NewGuid().ToString(),
    ["priorAdjustmentIdJson"] = "null", ["targetMonthStart"] = "2026-10-01", ["carryFingerprint"] = new string('B',64),
    ["carryRequestId"] = Guid.NewGuid().ToString(), ["carryVersion"] = "AAAAAAAAAAA="
};
string Expand(string value) => Regex.Replace(value, "\\{\\{([a-zA-Z0-9]+)\\}\\}", match => exampleValues[match.Groups[1].Value]);
var exampleCount = 0;
foreach (var block in File.ReadAllText("docs/attendance-gap-b/B03Contracts.http").Split("###"))
{
    var lines = block.Split('\n').Select(x => x.Trim()).ToArray();
    var requestLine = lines.SingleOrDefault(x => x.StartsWith("GET {{baseUrl}}/",StringComparison.Ordinal) || x.StartsWith("POST {{baseUrl}}/",StringComparison.Ordinal));
    if (requestLine is null) continue;
    var parts = Expand(requestLine).Split(' '); var method = new HttpMethod(parts[0]); var uri = new Uri(parts[1]);
    // Literal routes such as /sources outrank /{id}; don't mistake metadata wildcard matches for MVC ambiguity.
    var matches = document.Paths.Where(x => x.Value.Operations?.ContainsKey(method) == true && Regex.IsMatch(uri.AbsolutePath,
        "^" + Regex.Escape(Regex.Replace(x.Key,"\\{[^}]+\\}","__SEGMENT__")).Replace("__SEGMENT__","[^/]+") + "$"))
        .OrderBy(x => Regex.Matches(x.Key,"\\{").Count).ToArray();
    Check(matches.Length > 0, "client example path has a registered method");
    var registered = matches[0];
    Check(matches.Count(x => Regex.Matches(x.Key,"\\{").Count == Regex.Matches(registered.Key,"\\{").Count) == 1,
        "unique highest-precedence client example route");
    Check(registered.Value.Operations!.ContainsKey(method), "client example method/path registered: " + uri.AbsolutePath);
    var operation = registered.Value.Operations[method];
    if (method == HttpMethod.Post)
    {
        Check(lines.Any(x => x.StartsWith("Idempotency-Key: ",StringComparison.Ordinal)), "write example has original retry key");
        if (!uri.AbsolutePath.StartsWith("/api/v1/me/",StringComparison.Ordinal))
            Check(lines.Any(x => x.StartsWith("X-CSRF-TOKEN: ",StringComparison.Ordinal)), "web write example includes CSRF");
        using var body = JsonDocument.Parse(Expand(lines.Single(x => x.StartsWith('{'))));
        var schema = Schema(operation.RequestBody!.Content!["application/json"].Schema!);
        var fields = body.RootElement.EnumerateObject().Select(x => x.Name).ToHashSet();
        Check(fields.All(schema.Properties!.ContainsKey) && schema.Required!.All(fields.Contains), "example JSON has exact known/required members");
    }
    exampleCount++;
}
Check(exampleCount == 15, "all fifteen client examples were inspected, not executed");
Console.WriteLine($"PASS: {checks} B03 actual MVC/OpenAPI/JSON/decision-boundary checks; {document.Paths.Count} paths. No host start, HTTP, SQL, provider or mobile acceptance.");
