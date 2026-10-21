#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// Compiled controller/DTO contract inspection only; no host, SQL, login or HTTP acceptance.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); checks++; }
var controller = typeof(SiteReportsController);
Check(controller.GetMethod("Reconcile") is not null, "B07 authenticated own submission reconciliation endpoint exists");
var action = controller.GetMethod("QueueSites") ?? throw new Exception("FAIL: B07 queue discovery endpoint missing");
Check(controller.GetCustomAttribute<RouteAttribute>()?.Template == "api/v1/site-reports" &&
    action.GetCustomAttribute<HttpGetAttribute>()?.Template == "queue/sites", "discovery is a GET in the existing report controller");
Check(controller.GetCustomAttribute<AuthorizeAttribute>() is not null &&
    action.GetCustomAttribute<AllowAnonymousAttribute>() is null, "discovery requires authentication");
Check(controller.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore == true, "queue metadata is non-cacheable");
Check(action.GetParameters()[0].ParameterType == typeof(SiteReportQueueListRequest) &&
    action.GetParameters()[0].GetCustomAttribute<FromQueryAttribute>() is not null, "discovery binds its bounded query contract");
Check(action.ReturnType == typeof(Task<PagedResult<SiteReportQueueSiteResponse>>), "discovery returns the existing page shape");
var request = new SiteReportQueueListRequest();
Check(request.Page == 1 && request.PageSize == 25, "discovery page defaults match the module");
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
using var json = JsonDocument.Parse(JsonSerializer.Serialize(new PagedResult<SiteReportQueueSiteResponse>(
    [new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "SITE-01", "Historical site", false)], 1, 1, 25), options));
Check(json.RootElement.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "items", "page", "pageSize", "totalCount" }.Order()),
    "serialized response retains items/totalCount/page/pageSize");
Check(json.RootElement.GetProperty("items")[0].EnumerateObject().Select(x => x.Name).Order()
    .SequenceEqual(new[] { "siteId", "code", "name", "isActive" }.Order()), "serialized queue item exposes only safe site metadata");
var reconcile = controller.GetMethod("Reconcile")!;
Check(reconcile.GetCustomAttribute<HttpGetAttribute>()?.Template == "mine/reconcile" &&
    reconcile.GetCustomAttribute<AllowAnonymousAttribute>() is null &&
    reconcile.ReturnType == typeof(Task<SiteReportReconciliationResponse>), "own reconciliation is an authenticated GET returning a distinct current-state contract");
var key = reconcile.GetParameters()[0];
Check(key.ParameterType == typeof(string) && key.GetCustomAttribute<FromHeaderAttribute>()?.Name == "Submission-Key" &&
    key.GetCustomAttribute<RequiredAttribute>() is not null && reconcile.GetParameters().Length == 2,
    "reconciliation accepts only the original key header and cancellation, without client identity or payload selectors");
var length = key.GetCustomAttribute<StringLengthAttribute>()!;
var pattern = key.GetCustomAttribute<RegularExpressionAttribute>()!;
Check(length.MinimumLength == 8 && length.MaximumLength == 100 && pattern.IsValid("Aa09_.:-") &&
    !pattern.IsValid("key-0001\n") && !pattern.IsValid("bad key-01"), "HTTP key validation matches exact bounded submission-key syntax");
using var reconciled = JsonDocument.Parse(JsonSerializer.Serialize(new SiteReportReconciliationResponse("current_retained_report",
    new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Cosmix.Domain.Attendance.SiteReports.SiteReportKind.Arrival, null,
        DateTimeOffset.UtcNow, null, false, "received_pending_review", "received_unverified_evidence", true, 0),
        Guid.NewGuid(), Guid.NewGuid(), "Retained notes", null, null, null, "hr_review_pending", [], [])), options));
Check(reconciled.RootElement.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "resultKind", "detail" }.Order()) &&
    reconciled.RootElement.GetProperty("resultKind").GetString() == "current_retained_report" &&
    reconciled.RootElement.GetProperty("detail").GetProperty("responses").GetArrayLength() == 0,
    "serialized reconciliation identifies current detail with history rather than a frozen submission receipt");
Check(!reconciled.RootElement.GetRawText().Contains("submissionKey") && !reconciled.RootElement.GetRawText().Contains("storageKey") &&
    !reconciled.RootElement.GetRawText().Contains("privateEvidenceReference") && !reconciled.RootElement.GetRawText().Contains("content"),
    "reconciliation serialization exposes no original key, private references or attachment bytes");
Console.WriteLine($"PASS: {checks} B07 compiled queue/reconciliation contract checks; no host, SQL or authenticated HTTP.");
