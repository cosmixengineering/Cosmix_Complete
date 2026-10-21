#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property InterceptorsNamespaces=Microsoft.AspNetCore.OpenApi.Generated
using Cosmix.Api.Controllers.HR;
using Cosmix.Api.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions {
    EnvironmentName = "Development", ApplicationName = typeof(LeavesController).Assembly.GetName().Name,
    ContentRootPath = Directory.GetCurrentDirectory()
});
builder.WebHost.UseKestrel();
builder.Services.AddLogging(); builder.Services.AddRouting(); builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(LeavesController).Assembly);
builder.Services.AddOpenApi(options => {
    options.AddDocumentTransformer<BearerDocumentTransformer>();
    options.AddOperationTransformer<BearerOperationTransformer>();
    options.AddOperationTransformer<OrganizationOperationTransformer>();
    options.AddOperationTransformer<DeprecatedOperationTransformer>();
    options.AddSchemaTransformer<AttendancePolicySchemaTransformer>();
});
await using var app = builder.Build(); app.MapControllers();
var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync(default);
var checks = 0; var failures = new List<string>();
void Check(bool condition, string name) { checks++; if (!condition) { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
OpenApiOperation Operation(string path, HttpMethod method) {
    Check(document.Paths.TryGetValue(path, out var item) && item.Operations?.ContainsKey(method) == true, "Registered " + method + " " + path);
    return document.Paths[path].Operations![method];
}
void Write(OpenApiOperation operation, bool idempotent) {
    Check(operation.Security?.Any(s => s.Keys.Any(k => k.Reference.Id == "CsrfToken")) == true, "Cookie write documents CSRF");
    if (idempotent) Check(operation.Parameters?.Any(p => p.Name == "Idempotency-Key" && p.In == ParameterLocation.Header && p.Required) == true,
        "Write requires idempotency header");
    foreach (var status in new[] { "400", "401", "403", "409" }) Check(operation.Responses?.ContainsKey(status) == true, "Write documents " + status);
}
var submit = Operation("/api/v1/leaves/mine/encashment", HttpMethod.Post); Write(submit, true);
Check(submit.Description?.Contains("policy", StringComparison.OrdinalIgnoreCase) == true &&
    submit.Description.Contains("payment", StringComparison.OrdinalIgnoreCase), "Encashment documents policy dependency and no payment");
var mine = Operation("/api/v1/leaves/mine/encashment", HttpMethod.Get);
Check(mine.Description?.Contains("attendance.leaves.self", StringComparison.Ordinal) == true, "Encashment read documents self authority");
var decide = Operation("/api/v1/leaves/admin/encashment/{id}/decisions", HttpMethod.Post); Write(decide, true);
Check(decide.Description?.Contains("ExpectedApprovalVersion", StringComparison.Ordinal) == true &&
    decide.Description.Contains("Admin", StringComparison.Ordinal), "Encashment review documents actual Admin/version contract");
var recovery = Operation("/api/v1/leaves/{id}/recovery-commitments", HttpMethod.Post); Write(recovery, true);
Check(recovery.Description?.Contains("fulfilment", StringComparison.OrdinalIgnoreCase) == true, "A commitment is not documented as completed recovery work");
var ownRecovery = Operation("/api/v1/leaves/{id}/recovery-commitments", HttpMethod.Get);
const string benefits = "/api/v1/leaves/admin/employees/{employeeId}/benefits";
Write(Operation(benefits, HttpMethod.Post), true);
Write(Operation(benefits + "/{id}/revoke", HttpMethod.Post), true);
Operation(benefits + "/{id}/correction-impact", HttpMethod.Get);
var correctBenefit = Operation(benefits + "/{id}/correct", HttpMethod.Post);
Write(correctBenefit, true);
var resolveBenefit = Operation(benefits + "/{id}/correction-resolution", HttpMethod.Post);
Write(resolveBenefit, true);
Check(resolveBenefit.Description?.Contains("ExpectedResolutionVersion") == true &&
    resolveBenefit.Description.Contains("waiver") && resolveBenefit.Description.Contains("not a salary deduction"),
    "Benefit resolution documents exact version, configured waiver and no salary recovery");
Check(correctBenefit.Description?.Contains("UsageFingerprint") == true &&
    correctBenefit.Description.Contains("pending Admin review") && correctBenefit.Description.Contains("not a salary deduction"),
    "Benefit correction documents exact usage binding and HR-only pending recovery boundary");
Operation(benefits, HttpMethod.Get);
var myBenefits = Operation("/api/v1/leaves/mine/benefits", HttpMethod.Get);
Check(myBenefits.Description?.Contains("attendance.leaves.self") == true, "Benefit reads document self-bound identity");
Check(Operation(benefits, HttpMethod.Post).Description?.Contains("attendance.leaves.benefits.review") == true &&
    Operation(benefits, HttpMethod.Post).Description?.Contains("not a cash payment") == true,
    "Benefit award documents explicit authority and no payment");
const string recoveryWork = "/api/v1/leaves/mine/{leaveId}/recovery-commitments/{commitmentId}";
Write(Operation(recoveryWork + "/work-claims", HttpMethod.Post), true);
Operation(recoveryWork + "/work-source", HttpMethod.Get);
Operation(recoveryWork + "/progress", HttpMethod.Get);
Operation("/api/v1/leaves/mine/recovery-work/{id}", HttpMethod.Get);
Operation("/api/v1/leaves/admin/recovery-work/queue", HttpMethod.Get);
Operation("/api/v1/leaves/admin/recovery-work/{employeeId}/{id}", HttpMethod.Get);
Write(Operation("/api/v1/leaves/admin/recovery-work/{employeeId}/{id}/decisions", HttpMethod.Post), true);
Operation("/api/v1/leaves/admin/{leaveId}/recovery-commitments/{commitmentId}/progress", HttpMethod.Get);
var teamCalendar = Operation("/api/v1/leaves/team-calendar", HttpMethod.Get);
Check(teamCalendar.Description?.Contains("attendance.leaves.team-calendar.read") == true &&
    teamCalendar.Description.Contains("No type, reason, evidence, balance or pay data"), "Team calendar documents independent limited authority");
var ownCalendar = Operation("/api/v1/leaves/mine/calendar", HttpMethod.Get);
Check(ownCalendar.Description?.Contains("identity comes only from the current session") == true,
    "Own leave calendar documents server-bound identity");
Check(ownRecovery.Description?.Contains("owner", StringComparison.OrdinalIgnoreCase) == true, "Recovery GET documents parent ownership");
Check(ownRecovery.Security?.Any(s => s.Keys.Any(k => k.Reference.Id == "Bearer")) == true,
    "Mobile owner recovery GET documents its actual Bearer authentication");
Check(recovery.Security?.All(s => s.Keys.All(k => k.Reference.Id != "Bearer")) == true,
    "Admin recovery write does not advertise mobile Bearer authority");
var policy = Operation("/api/v1/hr/leave-settings/policies", HttpMethod.Post); Write(policy, true);
Check(policy.Description?.Contains("fixed Admin", StringComparison.Ordinal) == true &&
    policy.Description.Contains("version", StringComparison.OrdinalIgnoreCase), "Policy publication documents fixed-role versioned authority");
var read = Operation("/api/v1/hr/reports/{reportType}", HttpMethod.Get);
Check(read.Description?.Contains("hr.reports.read") == true, "Report read retains source permission matrix");
var export = Operation("/api/v1/hr/reports/{reportType}/export", HttpMethod.Post); Write(export, true);
Check(export.Description?.Contains("hr.reports.export") == true, "Report export requires separate permission");
Check(export.Responses?.ContainsKey("202") == true && export.Responses.ContainsKey("503") &&
    export.Description?.Contains("Pagination is ignored") == true,
    "Full export documents asynchronous bounded generation and unavailable worker");
Check(export.Description?.Contains("current authorization") == true &&
    export.Description.Contains("authenticated download"), "Export replay and download retain authorization");
Check(read.Description?.Contains("returned count") == true, "Preview documents actual page context");
Write(Operation("/api/v1/hr/reports/{reportType}/export-page", HttpMethod.Post), false);
Operation("/api/v1/hr/reports/exports/{id}", HttpMethod.Get);
Check(Operation("/api/v1/hr/reports/exports/{id}/download", HttpMethod.Get).Responses?.ContainsKey("410") == true,
    "Retained export download documents expiry");
const string vehiclePolicies = "/api/v1/hr/vehicles/policies";
Operation("/api/v1/hr/vehicles/fuel-claims/{id}/historical-closure-context", HttpMethod.Get);
var historicalFuelClosure = Operation("/api/v1/hr/vehicles/fuel-claims/{id}/historical-closure", HttpMethod.Post);
Write(historicalFuelClosure, true);
Check(historicalFuelClosure.Description?.Contains("does not create a replacement claim") == true,
    "Historical fuel closure cannot imply repricing or payment");
var fuelComparison = Operation("/api/v1/hr/vehicles/fuel-claims/{id}/reconciliation-context", HttpMethod.Get);
Check(fuelComparison.Description?.Contains("not a refund, debt, posting or payment") == true,
    "HR comparison is not evidence of an Accounts transaction");
var fuelProposal = Operation("/api/v1/hr/vehicles/fuel-claims/{id}/reconciliations", HttpMethod.Post);
Write(fuelProposal, true);
var fuelReview = Operation("/api/v1/hr/vehicles/fuel-claims/{id}/reconciliations/{requestId}/review", HttpMethod.Post);
Write(fuelReview, true);
Check(fuelReview.Description?.Contains("Reviewer must differ") == true && fuelReview.Description.Contains("Accounts"),
    "Fuel reconciliation requires independent review without Accounts authority");
Operation("/api/v1/hr/vehicles/fuel-claims/{id}/reconciliations", HttpMethod.Get);
const string vehicleAttribution = "/api/v1/hr/vehicles/trips/{tripId}/attributions";
Operation("/api/v1/hr/vehicles/trips/{tripId}/attribution-context", HttpMethod.Get);
Operation(vehicleAttribution, HttpMethod.Get);
Write(Operation(vehicleAttribution, HttpMethod.Post), true);
Write(Operation(vehicleAttribution + "/{requestId}/review", HttpMethod.Post), true);
Write(Operation(vehiclePolicies, HttpMethod.Post), true);
Write(Operation(vehiclePolicies + "/{id}/versions", HttpMethod.Post), true);
foreach (var suffix in new[] { "", "/{id}", "/{id}/versions", "/{id}/versions/{versionId}" })
    Operation(vehiclePolicies + suffix, HttpMethod.Get);
Operation(vehiclePolicies + "/validate", HttpMethod.Post);
Operation(vehiclePolicies + "/{id}/publication-preview", HttpMethod.Post);
const string vehicleAdjustments = "/api/v1/hr/vehicles/{vehicleId}/monthly-adjustments";
Operation(vehicleAdjustments + "/preview", HttpMethod.Post);
Write(Operation(vehicleAdjustments, HttpMethod.Post), true);
Write(Operation(vehicleAdjustments + "/{requestId}/review", HttpMethod.Post), true);
Operation(vehicleAdjustments, HttpMethod.Get);
Check(Operation(vehicleAdjustments + "/entitlement", HttpMethod.Get).Description?.Contains("null usable kilometres") == true,
    "Monthly distance handoff distinguishes pending from zero");
const string incidentAmendments = "/api/v1/hr/vehicles/incidents/{id}/amendments";
Write(Operation(incidentAmendments, HttpMethod.Post), true);
Write(Operation(incidentAmendments + "/{requestId}/review", HttpMethod.Post), true);
Check(Operation(vehiclePolicies + "/{id}/versions", HttpMethod.Post).Description?.Contains("attendance.vehicle.policy.manage") == true,
    "Vehicle policy publication requires dedicated explicit authority");
Operation("/api/v1/hr/employees/{id}/tracking", HttpMethod.Get);
var retired = Operation("/api/v1/hr/employees/{id}/tracking/record-checkin", HttpMethod.Post);
Check(retired.Deprecated && retired.Responses?.ContainsKey("410") == true, "Legacy capture is deprecated and reports Gone");
Check(document.Paths.Keys.All(x => !x.Contains("attendance/import", StringComparison.OrdinalIgnoreCase)), "No removed attendance import advertised");
var finances = Operation("/api/v1/self-service/finances", HttpMethod.Get);
Check(finances.Description?.Contains("Requires explicit ess.finances.read", StringComparison.Ordinal) == true &&
    finances.Description.Contains("auth.self.read alone is insufficient", StringComparison.Ordinal), "Financial self read documents explicit permission");
Check(finances.Description?.Contains("audited", StringComparison.Ordinal) == true &&
    finances.Description.Contains("not posting, settlement or payment", StringComparison.Ordinal), "Financial view documents audit and handoff boundary");
var repayment = Operation("/api/v1/hr/advances-loans/agreements/{id}/repayments", HttpMethod.Post); Write(repayment, true);
Check(repayment.Description?.Contains("payroll_posting_evidence_required", StringComparison.Ordinal) == true,
    "Manual loan repayment cannot impersonate confirmed payroll posting");
var loanApproval = Operation("/api/v1/hr/advances-loans/requests/{id}/approve", HttpMethod.Post); Write(loanApproval, true);
Check(loanApproval.Description?.Contains("interest-free", StringComparison.Ordinal) == true &&
    loanApproval.Description.Contains("must be zero", StringComparison.Ordinal), "Approval documents principal-only contract");
var posting = Operation("/api/v1/payroll-handoff/batches/{id}/lines/{lineId}/confirmed-loan-posting", HttpMethod.Post); Write(posting, true);
Check(posting.Description?.Contains("Handoff processing alone is not posting", StringComparison.Ordinal) == true &&
    posting.Description.Contains("Manual authorized evidence", StringComparison.Ordinal), "Posting documents manual provenance and handoff separation");
var posted = Operation("/api/v1/payroll-handoff/batches/{id}/lines/{lineId}/confirmed-loan-posting", HttpMethod.Get);
Check(posted.Description?.Contains("after the HTTP retry window", StringComparison.Ordinal) == true, "Posting evidence has a durable read contract");
Operation("/api/v1/self-service/leaves/balance", HttpMethod.Get);
var reversal = Operation("/api/v1/hr/advances-loans/agreements/{id}/reversals", HttpMethod.Post); Write(reversal, true);
Check(reversal.Description?.Contains("repayment_already_reversed", StringComparison.Ordinal) == true,
    "Loan reversal documents duplicate protection");
const string deploymentPath = "/api/v1/hr/employees/{employeeId}/deployments";
foreach (var suffix in new[] { "", "/{id}/decision", "/{id}/home-breaks", "/{id}/home-breaks/{breakId}/decision",
    "/{id}/claims", "/{id}/claims/{claimId}/decision" })
    Write(Operation(deploymentPath + suffix, HttpMethod.Post), true);
var deploymentCalculation = Operation(deploymentPath + "/{id}/calculation", HttpMethod.Get);
Check(deploymentCalculation.Description?.Contains("pending with null money", StringComparison.Ordinal) == true &&
    deploymentCalculation.Description.Contains("company-configured", StringComparison.Ordinal) &&
    deploymentCalculation.Description.Contains("no 40-hour treatment is inferred", StringComparison.Ordinal),
    "Deployment calculation does not invent missing policy values");
const string financialPath = "/api/v1/attendance/employees/{employeeId}/payroll-inputs/{year}/{month}/financial-adjustments";
foreach (var suffix in new[] { "", "/{adjustmentId}/posting", "/{adjustmentId}/cancel" })
    Write(Operation(financialPath + suffix, HttpMethod.Post), true);
Operation(financialPath + "/preview", HttpMethod.Get);
Check(Operation(financialPath, HttpMethod.Post).Description?.Contains("not posting, legal activation or payment", StringComparison.Ordinal) == true,
    "Financial adjustment approval documents no implied settlement");
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} generated MVC/OpenAPI contract checks; {document.Paths.Count} paths. No host, SQL, auth/provider or client acceptance.");
return failures.Count == 0 ? 0 : 1;
