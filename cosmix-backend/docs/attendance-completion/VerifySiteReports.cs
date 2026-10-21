#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeDocuments;

// Synthetic in-memory adapters; this does not authenticate a real account or execute SQL/scanner calls.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); checks++; }
async Task Fails(Func<Task> action, string code, int status)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, $"{code}/{status}, got {e.Code}/{e.StatusCode}"); }
}
var ct = CancellationToken.None;
var f = new Fixture();
var service = f.Service();
var input = f.Input();
Check(typeof(SiteReportService).GetMethod("QueueSitesAsync") is not null,
    "B07 readable site queues can be discovered through the existing report service");
Check(typeof(SiteReportService).GetMethod("ReconcileAsync") is not null,
    "B07 own immutable submission can be reconciled by its permanent key after receipt loss");
var report = await service.SubmitAsync(input, "report-0001", f.Context, ct);
Check(report.Report.EmployeeId == f.EmployeeId && report.RegisteredDeviceId == f.Device.Id, "server identity and persisted device");
Check(report.EmployeeAssignmentId == f.Assignment.Id, "historical assignment pin");
Check(report.Report.ReceivedAtUtc == f.Now, "server received time");
Check(report.Report.SubmissionStatus == "received_pending_review" && report.ReviewWorkflowStatus == "hr_review_pending", "received report awaits explicitly permissioned HR review");
Check(report.Report.EvidenceStatus == "received_unverified_evidence", "report is not attendance acceptance");
Check(f.Rows.Count == 1 && f.Receipts.Count == 1 && f.Audits.Count == 1, "business audit receipt staged together");
Check(f.Audits[0].EmployeeScopeId == f.EmployeeId, "audit self scope");
Check(f.Intents.Count == 1 && f.Intents[0].RecipientAccountId == f.Account.Id &&
    f.Intents[0].EventId == report.Report.Id && f.Intents[0].ResourceId == report.Report.Id &&
    f.Intents[0].TemplateCode == NotificationTemplates.SiteReport, "self confirmation staged for exact immutable report");
var retry = await service.SubmitAsync(input, "report-0001", f.Context, ct);
Check(retry.Report.Id == report.Report.Id && f.Rows.Count == 1, "exact retry uses retained receipt");
Check(f.Intents.Count == 1, "retry does not stage duplicate notification");
await Fails(() => service.SubmitAsync(input with { Notes = "changed" }, "report-0001", f.Context, ct), "idempotency_key_reused", 409);
await Fails(() => service.SubmitAsync(input, "short", f.Context, ct), "invalid_idempotency_key", 400);
var date = new DateOnly(2026, 10, 9);
Check(SiteReportRules.ValidateSubmission(input, f.Now) == date, "Pakistan date after UTC midnight boundary");
var mine = await service.MineAsync(new(date, date, PageSize: 1), f.Context, ct);
Check(mine.TotalCount == 1 && mine.Items.Count == 1, "bounded own list");
Check(f.QueriedEmployee == f.EmployeeId && f.QueriedSite is null, "server identity sent to SQL contract");
Check((await service.MineDetailAsync(report.Report.Id, f.Context, ct)).Report.Id == report.Report.Id, "own detail");
await Fails(() => service.MineDetailAsync(Guid.NewGuid(), f.Context, ct), "site_report_not_found", 404);
await Fails(() => service.MineEvidenceAsync(report.Report.Id, f.Context, ct), "site_report_not_found", 404);

f.Grants.Clear();
var fileReads = f.FileReads;
await Fails(() => service.SubmitAsync(input, "report-0001", f.Context, ct), "access_denied", 403);
Check(f.FileReads == fileReads, "revoked permission denied before reading replay evidence");
f.GrantSelf();
f.CurrentSession = false;
await Fails(() => service.MineAsync(new(date, date), f.Context, ct), "invalid_authentication", 401);
f.CurrentSession = true;
f.Identity = f.Identity with { Client = SessionClient.Web, MfaSatisfied = true };
await Fails(() => service.SubmitAsync(input, "report-0002", f.Context, ct), "access_denied", 403);
Check((await service.MineAsync(new(date, date), f.Context, ct)).Items.Count == 1, "web self read works without mobile writes");
f.Grants.Clear(); f.GrantRead(f.SiteId);
Check((await service.QueueAsync(f.SiteId, new(date, date), f.Context, ct)).Items.Count == 1, "authorized site queue");
Check(f.QueriedSite == f.SiteId && f.QueriedEmployee is null, "queue site constrained before paging");
await Fails(() => service.QueueAsync(Guid.NewGuid(), new(date, date), f.Context, ct), "access_denied", 403);
await Fails(() => service.DetailAsync(f.SiteId, Guid.NewGuid(), f.Context, ct), "site_report_not_found", 404);

// Discovery must not union an unrelated permission's All scope with report Read.
var secondSite = Guid.NewGuid(); var outsideSite = Guid.NewGuid();
f.QueueSites = [new(outsideSite, "A", "Outside grant", true), new(f.SiteId, "Z", "Granted site", true),
    new(secondSite, "B", "Historical inactive site", false)];
f.Grants.Add(new("hr.sites.read", false, Guid.NewGuid(), AccessScopeKind.All, null));
f.Grants.Add(new(SiteReportPermissions.Decide, false, Guid.NewGuid(), AccessScopeKind.All, null));
f.Grants.Add(new(SiteReportPermissions.Read, false, Guid.NewGuid(), AccessScopeKind.Department, Guid.NewGuid()));
f.GrantRead(f.SiteId);
var queues = await service.QueueSitesAsync(new(PageSize: 1), f.Context, ct);
Check(queues.TotalCount == 1 && queues.Items.Single().SiteId == f.SiteId && queues.PageSize == 1,
    "discovery filters exact report Read grants before totals and pagination");
Check(!f.QueriedAllSites && f.QueriedQueueSites.SequenceEqual(new[] { f.SiteId }), "duplicate grants collapse to exact server site scope");
Check(f.Audits.Last().EventType == "attendance.site-report.queue-sites", "discovery is audited through the existing operation");
f.GrantRead(secondSite);
queues = await service.QueueSitesAsync(new(PageSize: 1), f.Context, ct);
Check(queues.TotalCount == 2 && queues.Items.Single().SiteId == secondSite && !queues.Items.Single().IsActive,
    "inactive granted sites remain discoverable for historical review");
queues = await service.QueueSitesAsync(new(Page: 2, PageSize: 1), f.Context, ct);
Check(queues.TotalCount == 2 && queues.Page == 2 && queues.Items.Single().SiteId == f.SiteId, "discovery retains stable page metadata");
queues = await service.QueueSitesAsync(new(Page: 1000000, PageSize: 100), f.Context, ct);
Check(queues.TotalCount == 2 && queues.Items.Count == 0, "bounded high page returns empty without losing scoped total");
f.Grants.RemoveAll(x => x.Permission == SiteReportPermissions.Read && x.TargetId == secondSite);
Check((await service.QueueSitesAsync(new(), f.Context, ct)).TotalCount == 1, "discovery immediately rechecks changed grants");
f.Grants.Add(new(SiteReportPermissions.Read, false, f.RoleId, AccessScopeKind.All, null));
queues = await service.QueueSitesAsync(new(), f.Context, ct);
Check(queues.TotalCount == 3 && queues.Items.Select(x => x.Code).SequenceEqual(new[] { "A", "B", "Z" }), "only report Read All discovers all site queues");
Check(f.QueriedAllSites && f.QueriedQueueSites.Count == 0, "All uses an explicit server-authorized repository scope");
foreach (var invalid in new SiteReportQueueListRequest?[] { null, new(0), new(1000001), new(PageSize: 0), new(PageSize: 101) })
    await Fails(() => service.QueueSitesAsync(invalid!, f.Context, ct), "invalid_site_report_queue_page", 400);
var discoveryCalls = f.DiscoveryCalls;
f.Grants.Clear(); f.GrantSelf();
await Fails(() => service.QueueSitesAsync(new(), f.Context, ct), "access_denied", 403);
Check(f.DiscoveryCalls == discoveryCalls, "self-only callers cannot query queue metadata");
foreach (var kind in new[] { AccessScopeKind.Self, AccessScopeKind.Employee, AccessScopeKind.Department, AccessScopeKind.Team, AccessScopeKind.Project })
{
    f.Grants.Clear(); f.Grants.Add(new(SiteReportPermissions.Read, false, f.RoleId, kind, f.EmployeeId));
    await Fails(() => service.QueueSitesAsync(new(), f.Context, ct), "access_denied", 403);
}
foreach (var grant in new[] {
    new PermissionGrant(SiteReportPermissions.Read, false, f.RoleId, AccessScopeKind.Site, Guid.Empty),
    new PermissionGrant(SiteReportPermissions.Read, false, f.RoleId, AccessScopeKind.All, f.SiteId) })
{
    f.Grants.Clear(); f.Grants.Add(grant);
    await Fails(() => service.QueueSitesAsync(new(), f.Context, ct), "access_denied", 403);
}
f.Grants.Clear(); f.GrantRead(f.SiteId);
f.LeakQueueSites = true;
await Fails(() => service.QueueSitesAsync(new(), f.Context, ct), "access_denied", 403);
f.LeakQueueSites = false;
f.CurrentSession = false;
await Fails(() => service.QueueSitesAsync(new(), f.Context, ct), "invalid_authentication", 401);
f.CurrentSession = true;
f.Identity = f.Identity with { MfaSatisfied = false };
await Fails(() => service.QueueAsync(f.SiteId, new(date, date), f.Context, ct), "access_denied", 403);
await Fails(() => service.QueueSitesAsync(new(), f.Context, ct), "access_denied", 403);
f.Identity = f.Identity with { Client = SessionClient.Mobile, MfaSatisfied = false };
await Fails(() => service.QueueAsync(f.SiteId, new(date, date), f.Context, ct), "access_denied", 403);
await Fails(() => service.QueueSitesAsync(new(), f.Context, ct), "access_denied", 403);
f.Grants.Clear(); f.GrantSelf();

foreach (var kind in Enum.GetValues<SiteReportKind>())
{
    var result = await service.SubmitAsync(input with { Kind = kind }, "report-kind-" + (int)kind, f.Context, ct);
    Check(result.Report.Kind == kind, "report kind " + kind);
}
var invalidInputs = new[] {
    input with { SiteId = Guid.Empty }, input with { Kind = (SiteReportKind)999 }, input with { Notes = " " },
    input with { Notes = "bad\0notes" }, input with { TaskReference = "bad\nreference" }, input with { Notes = new string('x',4001) }
};
foreach(var invalid in invalidInputs)
    await Fails(() => service.SubmitAsync(invalid, "invalid-case", f.Context, ct), "invalid_site_report_request", 400);
foreach(var invalid in new[] { input with { Latitude = 91 }, input with { Longitude = -181 },
    input with { Latitude = 24, Longitude = 67 }, input with { Latitude = 24, Longitude = 67, AccuracyMeters = -1 },
    input with { Latitude = 24.12345678m, Longitude = 67, AccuracyMeters = 1 },
    input with { Latitude = 24, Longitude = 67, AccuracyMeters = 0.001m } })
    await Fails(() => service.SubmitAsync(invalid, "invalid-location", f.Context, ct), "invalid_report_location", 400);
await Fails(() => service.SubmitAsync(input with { IsOffline = true }, "invalid-offline", f.Context, ct), "invalid_report_capture_time", 400);
await Fails(() => service.SubmitAsync(input with { DeviceCapturedAtUtc = f.Now.AddSeconds(1) }, "future-device", f.Context, ct), "invalid_report_capture_time", 400);
await Fails(() => service.SubmitAsync(input with { IsOffline = true, DeviceCapturedAtUtc = f.Now.AddDays(-40) }, "old-device01", f.Context, ct), "report_capture_precedes_registered_device", 400);
var offline = await service.SubmitAsync(input with { IsOffline = true, DeviceCapturedAtUtc = f.Now.AddDays(-2), Latitude = 24, Longitude = 67, AccuracyMeters = 5 }, "offline-report", f.Context, ct);
Check(offline.Report.EvidenceStatus == "unverified_offline_evidence" && offline.Report.DeviceCapturedAtUtc == f.Now.AddDays(-2), "offline original evidence and pending state");
Check(f.AssignmentDate == date.AddDays(-2), "offline historical site resolved from claimed date only as unverified context");
f.Assignments.Clear();
await Fails(() => service.SubmitAsync(input, "missing-site", f.Context, ct), "report_site_assignment_unavailable", 409);
f.Assignments.Add(new(f.Assignment, true, null, null));
f.Assignments.Add(new(f.Assignment, true, null, null));
await Fails(() => service.SubmitAsync(input, "overlap-site", f.Context, ct), "report_site_assignment_conflict", 409);
f.Assignments.RemoveAt(1);
await Fails(() => service.SubmitAsync(input with { SiteId = Guid.NewGuid() }, "wrong-site01", f.Context, ct), "report_site_assignment_unavailable", 409);
f.Assignments[0] = new(f.Assignment, false, null, null);
await Fails(() => service.SubmitAsync(input, "inactive-site", f.Context, ct), "report_site_unavailable", 409);
Check((await service.SubmitAsync(input with { IsOffline = true, DeviceCapturedAtUtc = f.Now.AddDays(-1) }, "historical-site", f.Context, ct)).Report.IsOffline, "inactive site retains historical report context");
f.Assignments[0] = new(f.Assignment, true, null, null);
foreach (var invalid in new[] { new SiteReportListRequest(default,date), new(date,date.AddDays(-1)), new(date,date.AddDays(366)), new(date,date,PageSize:101) })
{
    try { await service.MineAsync(invalid, f.Context, ct); throw new Exception("Expected invalid list"); }
    catch(AuthenticationFailure e) { Check(e.StatusCode == 400, "bounded list validation"); }
}

var upload = input with { Attachment = new("photo.png", "image/png", [137,80,78,71,13,10,26,10,1,2,3]) };
f.Scan = new(DocumentScanStatus.Failed,"scanner/offline");
var beforeFiles = f.Stored.Count;
var beforeRows = f.Rows.Count;
await Fails(() => service.SubmitAsync(upload, "scan-failed1", f.Context, ct), "report_attachment_scanner_unavailable", 503);
Check(f.Stored.Count == beforeFiles && f.Rows.Count == beforeRows, "scanner failure has no report/file success");
f.Scan = new(DocumentScanStatus.Rejected,"synthetic/scanner");
await Fails(() => service.SubmitAsync(upload, "scan-reject1", f.Context, ct), "report_attachment_rejected", 400);
f.Scan = new(DocumentScanStatus.Clean,"synthetic/scanner");
var attached = await service.SubmitAsync(upload, "attachment01", f.Context, ct);
Check(attached.Report.HasAttachment, "scanned evidence attached");
var content = await service.MineEvidenceAsync(attached.Report.Id, f.Context, ct);
Check(content.Content.SequenceEqual(upload.Attachment.Content) && content.FileName == "photo.png", "purpose-bound private download");
var scanCalls = f.ScanCalls;
await service.SubmitAsync(upload, "attachment01", f.Context, ct);
Check(f.ScanCalls == scanCalls, "exact replay does not rescan or duplicate attachment");
Check(!JsonSerializer.Serialize(attached).Contains("StorageKey"), "private storage references not exposed");
await Fails(() => service.SubmitAsync(upload with { Attachment = upload.Attachment with { FileName = "../photo.png" } }, "invalid-file", f.Context, ct), "invalid_report_attachment", 400);
await Fails(() => service.SubmitAsync(upload with { Attachment = upload.Attachment with { ContentType = "text/html" } }, "invalid-type", f.Context, ct), "unsupported_report_attachment", 400);
var others = new Fixture();
var otherReport = await others.Service().SubmitAsync(others.Input(),"other-report", others.Context,ct);
f.Rows.Add(others.Rows.Single());
await Fails(() => service.MineDetailAsync(otherReport.Report.Id, f.Context,ct),"site_report_not_found",404);
f.Identity = f.Identity with { Client = SessionClient.Web, MfaSatisfied = true }; f.Grants.Clear(); f.GrantRead(f.SiteId);
await Fails(() => service.DetailAsync(f.SiteId,otherReport.Report.Id,f.Context,ct),"site_report_not_found",404);
f.Identity = f.Identity with { Client = SessionClient.Mobile, MfaSatisfied = false }; f.Grants.Clear(); f.GrantSelf();
f.Receipts.RemoveAll(x => x.Key == "report-0001");
await Fails(() => service.SubmitAsync(input,"report-0001",f.Context,ct),"site_report_already_received",409);
f.FailSave = true;
beforeRows = f.Rows.Count;
var beforeIntents = f.Intents.Count;
try { await service.SubmitAsync(input,"rollback-001",f.Context,ct); throw new Exception("Expected save failure"); }
catch(IOException) { Check(f.Rows.Count == beforeRows, "business staging rolled back by transaction contract"); }
Check(f.Intents.Count == beforeIntents, "notification intent staging rolls back with failed report");
f.FailSave = false;
try { JsonSerializer.Deserialize<SubmitSiteReportRequest>("{\"EmployeeId\":\"00000000-0000-0000-0000-000000000001\"}"); throw new Exception("Expected unknown identity rejection"); }
catch(JsonException) { Check(true,"client identity fields rejected by JSON contract"); }
var flow = new Fixture();
var submitted = await flow.Service().SubmitAsync(flow.Input(), "review-report-001", flow.Context, ct);
var original = flow.Rows.Single();
flow.BecomeReviewer();
var reviewed = await flow.ReviewService().DecideAsync(flow.SiteId, original.Id,
    new(0, 0, SiteReportReviewOutcome.Returned, "Please explain the incident."), "return-report-001", flow.Context, ct);
Check(reviewed.Report.SubmissionStatus == "returned" && reviewed.ReviewWorkflowStatus == "employee_response_required", "HR return requires an employee response");
Check(reviewed.Reviews.Single().Id == flow.Reviews.Single().Id, "returned review identity is available for client resubmission binding");
flow.GrantRead(flow.SiteId);
Check((await flow.Service().QueueAsync(flow.SiteId, new(date, date), flow.Context, ct)).Items.Single().SubmissionStatus == "returned",
    "site queue reflects an unanswered Return");
flow.BecomeEmployee();
var answered = await flow.ReviewService().RespondAsync(original.Id,
    new(1, 0, flow.Reviews.Single().Id, "The entrance was blocked; supervisor was informed."), "respond-report-001", flow.Context, ct);
Check(answered.Report.SubmissionStatus == "received_pending_review" && answered.Report.ResponseSequence == 1 && answered.Responses!.Single().WrittenResponse.Contains("entrance"),
    "employee response preserves original and returns report to HR");
flow.BecomeReviewer();
flow.GrantRead(flow.SiteId);
var answeredQueue = (await flow.Service().QueueAsync(flow.SiteId, new(date, date), flow.Context, ct)).Items.Single();
Check(answeredQueue.SubmissionStatus == "received_pending_review" && answeredQueue.ReviewSequence == 1 && answeredQueue.ResponseSequence == 1,
    "site queue reflects a returned answer as pending review with both current sequences");
var acceptedReport = await flow.ReviewService().DecideAsync(flow.SiteId, original.Id,
    new(1, 1, SiteReportReviewOutcome.Accepted, "Reviewed the written explanation."), "accept-report-001", flow.Context, ct);
Check(acceptedReport.Report.SubmissionStatus == "accepted" && acceptedReport.ReviewWorkflowStatus == "review_complete" &&
    acceptedReport.Report.EvidenceStatus == "received_unverified_evidence", "accepted operational report never becomes verified attendance");
Check(flow.Rows.Single().Notes == original.Notes && flow.Reviews.Count == 2 && flow.Responses.Count == 1, "original report and complete review/response trail remain immutable");
Check((await flow.Service().QueueAsync(flow.SiteId, new(date, date), flow.Context, ct)).Items.Single().SubmissionStatus == "accepted",
    "site queue keeps a later acceptance terminal after an employee answer");
var acceptedRetry = await flow.ReviewService().DecideAsync(flow.SiteId, original.Id,
    new(1, 1, SiteReportReviewOutcome.Accepted, "Reviewed the written explanation."), "accept-report-001", flow.Context, ct);
Check(acceptedRetry.Report == acceptedReport.Report && flow.Reviews.Count == 2, "identical decision retry retains original receipt without duplicate review");
Check(flow.Intents.Count == 4 && flow.Intents.All(x => x.RecipientAccountId == flow.Account.Id), "intake/return/response/accept notify only report owner and retries do not duplicate");
await Fails(() => flow.ReviewService().DecideAsync(flow.SiteId, original.Id,
    new(1, 1, SiteReportReviewOutcome.Rejected, "Changed outcome."), "accept-report-001", flow.Context, ct), "idempotency_key_reused", 409);
await Fails(() => flow.ReviewService().DecideAsync(flow.SiteId, original.Id,
    new(2, 1, SiteReportReviewOutcome.Rejected, "Try another decision."), "after-accepted-001", flow.Context, ct), "site_report_not_pending_review", 409);
await Fails(() => flow.ReviewService().DecideAsync(flow.SiteId, original.Id,
    new(1, 0, SiteReportReviewOutcome.Accepted, "Outdated tab."), "stale-decision-001", flow.Context, ct), "site_report_version_conflict", 409);
flow.Grants.Clear();
await Fails(() => flow.ReviewService().DecideAsync(flow.SiteId, original.Id,
    new(1, 1, SiteReportReviewOutcome.Accepted, "Reviewed the written explanation."), "accept-report-001", flow.Context, ct), "access_denied", 403);
flow.BecomeEmployee();
var ownAnswered = await flow.Service().MineDetailAsync(original.Id, flow.Context, ct);
Check(ownAnswered.Reviews.Count == 2 && ownAnswered.Responses!.Single().WrittenResponse == answered.Responses!.Single().WrittenResponse,
    "own detail reads complete purpose-bound response and decision history");
var reconciledHistory = await flow.Service().ReconcileAsync("review-report-001", flow.Context, ct);
Check(reconciledHistory.ResultKind == "current_retained_report" && reconciledHistory.Detail.Report.SubmissionStatus == "accepted" &&
    reconciledHistory.Detail.Reviews.Count == 2 && reconciledHistory.Detail.Responses!.Single().WrittenResponse == "The entrance was blocked; supervisor was informed.",
    "reconciliation returns current accepted history and protected written answer");
var frozenSubmission = await flow.Service().SubmitAsync(flow.Input(), "review-report-001", flow.Context, ct);
Check(frozenSubmission.Report.SubmissionStatus == "received_pending_review" && frozenSubmission.Reviews.Count == 0,
    "submission retry still returns its frozen receipt independently of current reconciliation");
Check(!JsonSerializer.Serialize(ownAnswered).Contains("PrivateEvidenceReference") && !JsonSerializer.Serialize(ownAnswered).Contains("StorageKey"), "response detail hides protected references");
await Fails(() => flow.ReviewService().RespondAsync(original.Id,
    new(2, 1, flow.Reviews.Last().Id, "Try to reopen accepted report."), "after-accepted-response", flow.Context, ct), "site_report_not_awaiting_response", 409);
await Fails(() => flow.ReviewService().OwnResponseEvidenceAsync(original.Id, flow.Responses.Single().Id, flow.Context, ct), "site_report_not_found", 404);

var recovery = new Fixture();
var recoveryService = recovery.Service();
var recoveryInput = recovery.Input() with { Attachment = upload.Attachment };
const string recoveryKey = "Recovery-Key_001";
var recoveryReport = await recoveryService.SubmitAsync(recoveryInput, recoveryKey, recovery.Context, ct);
typeof(IdempotencyReceipt).GetProperty(nameof(IdempotencyReceipt.ExpiresAt))!.SetValue(recovery.Receipts.Single(), recovery.Now);
await Fails(() => recoveryService.SubmitAsync(recoveryInput, recoveryKey, recovery.Context, ct), "idempotency_key_expired", 409);
var recoveryState = (recovery.Rows.Count, recovery.Reviews.Count, recovery.Responses.Count, recovery.Receipts.Count,
    recovery.Intents.Count, recovery.Stored.Count, recovery.ScanCalls);
var recoveryAudits = recovery.Audits.Count;
var recovered = await recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct);
Check(recovered.ResultKind == "current_retained_report" && recovered.Detail.Report.Id == recoveryReport.Report.Id &&
    recovered.Detail.Report.HasAttachment && recovered.Detail.Notes == "Arrived at assigned site",
    "expired submission receipt recovers the immutable report through the original key");
Check(recoveryState == (recovery.Rows.Count, recovery.Reviews.Count, recovery.Responses.Count, recovery.Receipts.Count,
    recovery.Intents.Count, recovery.Stored.Count, recovery.ScanCalls), "reconciliation stages no business row, receipt, file, scan or notification");
Check(recovery.Audits.Count == recoveryAudits + 1 && recovery.Audits.Last().EventType == "attendance.site-report.reconciled" &&
    recovery.Audits.Last().EmployeeScopeId == recovery.EmployeeId, "reconciliation uses the existing audited own-read transaction");
Check(recovery.QueriedSubmissionEmployee == recovery.EmployeeId && recovery.QueriedSubmissionAccount == recovery.Account.Id &&
    recovery.QueriedSubmissionKey == recoveryKey, "reconciliation derives exact employee/account/key repository scope from the current session");
var recoveredJson = JsonSerializer.Serialize(recovered);
Check(!recoveredJson.Contains("IdempotencyKey") && !recoveredJson.Contains(recoveryKey) && !recoveredJson.Contains("StorageKey") &&
    !recoveredJson.Contains("PrivateEvidenceReference") && !recoveredJson.Contains("Content") && !recoveredJson.Contains(Convert.ToBase64String(upload.Attachment.Content)),
    "reconciliation exposes no key, private path, envelope or attachment bytes");
var retainedReplacementDevice = new RegisteredDevice(recovery.Account.Id, "retained-replacement-device", "android", "synthetic", recovery.Now.AddMinutes(-5));
retainedReplacementDevice.MarkVerified(recovery.Now.AddMinutes(-5)); recovery.UseDevice(retainedReplacementDevice);
Check((await recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct)).Detail.RegisteredDeviceId == recovery.Device.Id,
    "retained-receipt recovery keeps original device provenance on a replacement session device");
await Fails(() => recoveryService.SubmitAsync(recoveryInput, recoveryKey, recovery.Context, ct), "idempotency_key_reused", 409);
recovery.UseDevice(recovery.Device);
await Fails(() => recoveryService.SubmitAsync(recoveryInput with { Notes = "changed" }, recoveryKey, recovery.Context, ct), "idempotency_key_reused", 409);
recovery.Receipts.Clear();
await Fails(() => recoveryService.SubmitAsync(recoveryInput, recoveryKey, recovery.Context, ct), "site_report_already_received", 409);
Check((await recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct)).Detail.Report.Id == recoveryReport.Report.Id,
    "missing receipt also recovers through the retained permanent business key");
await Fails(() => recoveryService.ReconcileAsync("recovery-key_001", recovery.Context, ct), "site_report_not_found", 404);
await Fails(() => recoveryService.ReconcileAsync("unknown-key-001", recovery.Context, ct), "site_report_not_found", 404);
foreach (var invalidKey in new string?[] { null, "", "1234567", new('a', 101), "bad key-001", "key-0001\n", "key-0001/", "key-0001?", "key-0001\u00e9" })
    await Fails(() => recoveryService.ReconcileAsync(invalidKey!, recovery.Context, ct), "invalid_idempotency_key", 400);
foreach (var validKey in new[] { "12345678", new string('a', 100), "Aa09_.:-" })
    await Fails(() => recoveryService.ReconcileAsync(validKey, recovery.Context, ct), "site_report_not_found", 404);
recovery.Grants.Clear();
var recoveryQueries = recovery.SubmissionQueries; var recoveryReads = recovery.FileReads;
await Fails(() => recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct), "access_denied", 403);
Check(recovery.SubmissionQueries == recoveryQueries && recovery.FileReads == recoveryReads, "revoked Self denies before lookup or private history reads");
recovery.GrantSelf(); recovery.CurrentSession = false;
await Fails(() => recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct), "invalid_authentication", 401);
recovery.CurrentSession = true;
recovery.Identity = recovery.Identity with { AccountId = Guid.NewGuid() };
await Fails(() => recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct), "site_report_not_found", 404);
recovery.LeakSubmission = true;
await Fails(() => recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct), "site_report_not_found", 404);
recovery.BecomeEmployee(); recovery.Identity = recovery.Identity with { EmployeeId = Guid.NewGuid() };
recovery.Grants.Clear(); recovery.Grants.Add(new(SiteReportPermissions.Self, false, recovery.RoleId, AccessScopeKind.Self, recovery.Identity.EmployeeId));
await Fails(() => recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct), "site_report_not_found", 404);
recovery.BecomeEmployee();
await Fails(() => recoveryService.ReconcileAsync("wrong-key-001", recovery.Context, ct), "site_report_not_found", 404);
recovery.LeakSubmission = false;
recovery.Rows.Add(others.Rows.Single());
await Fails(() => recoveryService.ReconcileAsync("other-report", recovery.Context, ct), "site_report_not_found", 404);
recovery.Identity = recovery.Identity with { Client = SessionClient.Web, MfaSatisfied = true };
Check((await recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct)).Detail.Report.Id == recoveryReport.Report.Id,
    "current web own-history authority permits read-only recovery");
recovery.BecomeEmployee();
var replacementDevice = new RegisteredDevice(recovery.Account.Id, "replacement-device", "android", "synthetic", recovery.Now.AddMinutes(-5));
replacementDevice.MarkVerified(recovery.Now.AddMinutes(-5)); recovery.UseDevice(replacementDevice);
Check((await recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct)).Detail.RegisteredDeviceId == recovery.Device.Id,
    "new current device can read original provenance without changing the original submission");
await Fails(() => recoveryService.SubmitAsync(recoveryInput with { Notes = "changed on replacement device" }, recoveryKey, recovery.Context, ct), "site_report_already_received", 409);
Check(recovery.Rows.Count == 2 && recovery.Intents.Count == 1 && recovery.Receipts.Count == 0,
    "missing-receipt recovery does not authorize changed-payload or new-device resubmission");
for (var i = 0; i < 257; i++) recovery.Reviews.Add(new(recoveryReport.Report.Id, i + 1, SiteReportReviewOutcome.Returned, "synthetic", "HR", "History bound", recovery.Now));
await Fails(() => recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct), "site_report_review_history_limit", 409);
recovery.Reviews.Clear();
var boundedReturn = new SiteReportReview(recoveryReport.Report.Id, 1, SiteReportReviewOutcome.Returned, "synthetic", "HR", "History bound", recovery.Now);
recovery.Reviews.Add(boundedReturn);
for (var i = 0; i < 257; i++) recovery.Responses.Add(new(Guid.NewGuid(), recoveryReport.Report.Id, i + 1, boundedReturn.Id,
    recovery.EmployeeId, recovery.Account.Id, recovery.Now, "unread-private-reference"));
await Fails(() => recoveryService.ReconcileAsync(recoveryKey, recovery.Context, ct), "site_report_response_history_limit", 409);

var cycle = new Fixture();
var cycleReport = await cycle.Service().SubmitAsync(cycle.Input(), "cycle-report-001", cycle.Context, ct);
cycle.BecomeReviewer();
await cycle.ReviewService().DecideAsync(cycle.SiteId, cycleReport.Report.Id, new(0, 0, SiteReportReviewOutcome.Returned, "First question"), "cycle-return-001", cycle.Context, ct);
cycle.BecomeEmployee();
await cycle.ReviewService().RespondAsync(cycleReport.Report.Id, new(1, 0, cycle.Reviews.Single().Id, "First answer"), "cycle-answer-001", cycle.Context, ct);
cycle.BecomeReviewer();
await cycle.ReviewService().DecideAsync(cycle.SiteId, cycleReport.Report.Id, new(1, 1, SiteReportReviewOutcome.Returned, "Second question"), "cycle-return-002", cycle.Context, ct);
cycle.GrantRead(cycle.SiteId);
var secondReturnQueue = (await cycle.Service().QueueAsync(cycle.SiteId, new(date, date), cycle.Context, ct)).Items.Single();
Check(secondReturnQueue.SubmissionStatus == "returned" && secondReturnQueue.ReviewSequence == 2 && secondReturnQueue.ResponseSequence == 1,
    "an answer to an earlier Return cannot satisfy the site's latest Return");
cycle.BecomeEmployee();
await Fails(() => cycle.ReviewService().RespondAsync(cycleReport.Report.Id, new(2, 1, cycle.Reviews.First().Id, "Wrong review answer"), "cycle-answer-wrong", cycle.Context, ct), "site_report_not_awaiting_response", 409);
await cycle.ReviewService().RespondAsync(cycleReport.Report.Id, new(2, 1, cycle.Reviews.Last().Id, "Second answer"), "cycle-answer-002", cycle.Context, ct);
cycle.Receipts.RemoveAll(x => x.Key == "cycle-report-001");
var cycleRecovered = await cycle.Service().ReconcileAsync("cycle-report-001", cycle.Context, ct);
Check(cycleRecovered.Detail.Report.SubmissionStatus == "received_pending_review" && cycleRecovered.Detail.Reviews.Count == 2 &&
    cycleRecovered.Detail.Responses!.Select(x => x.WrittenResponse).SequenceEqual(new[] { "First answer", "Second answer" }),
    "missing original receipt recovers successive Return/answer history without rewriting the original report");

var guard = new Fixture(); var guardReport = await guard.Service().SubmitAsync(guard.Input(), "guard-report-001", guard.Context, ct);
guard.Identity = guard.Identity with { Client = SessionClient.Web, MfaSatisfied = true };
guard.Grants.Clear(); guard.Grants.Add(new(SiteReportPermissions.Decide, false, guard.RoleId, AccessScopeKind.Site, guard.SiteId));
await Fails(() => guard.ReviewService().DecideAsync(guard.SiteId, guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Accepted, "Own review."), "own-decision-001", guard.Context, ct), "access_denied", 403);
guard.Identity = guard.Identity with { AccountId = Guid.NewGuid() };
await Fails(() => guard.ReviewService().DecideAsync(guard.SiteId, guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Accepted, "Same employee through another account."), "same-employee-review", guard.Context, ct), "access_denied", 403);
guard.BecomeReviewer(); guard.Identity = guard.Identity with { MfaSatisfied = false };
await Fails(() => guard.ReviewService().DecideAsync(guard.SiteId, guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Accepted, "No MFA."), "missing-mfa-review", guard.Context, ct), "access_denied", 403);
guard.Identity = guard.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Fails(() => guard.ReviewService().DecideAsync(guard.SiteId, guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Accepted, "Mobile review."), "mobile-review-001", guard.Context, ct), "access_denied", 403);
guard.BecomeReviewer();
await Fails(() => guard.ReviewService().DecideAsync(Guid.NewGuid(), guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Accepted, "Wrong site."), "wrong-site-review", guard.Context, ct), "access_denied", 403);
guard.Grants.Clear(); guard.GrantRead(guard.SiteId);
await Fails(() => guard.ReviewService().DecideAsync(guard.SiteId, guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Accepted, "Read-only user."), "read-only-review", guard.Context, ct), "access_denied", 403);
guard.BecomeReviewer();
await Fails(() => guard.ReviewService().DecideAsync(guard.SiteId, guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Escalated, "Unapproved escalation."), "escalated-review", guard.Context, ct), "invalid_site_report_decision", 400);
var rejectedReport = await guard.ReviewService().DecideAsync(guard.SiteId, guardReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Rejected, "Insufficient operational evidence."), "reject-report-001", guard.Context, ct);
Check(rejectedReport.Report.SubmissionStatus == "rejected" && rejectedReport.ReviewWorkflowStatus == "review_complete", "HR rejection completes review without attendance effect");
guard.BecomeEmployee();
await Fails(() => guard.ReviewService().RespondAsync(guardReport.Report.Id,
    new(1, 0, guard.Reviews.Single().Id, "Try rejected report."), "rejected-response", guard.Context, ct), "site_report_not_awaiting_response", 409);

var returned = new Fixture(); var returnedReport = await returned.Service().SubmitAsync(returned.Input(), "returned-report-001", returned.Context, ct);
returned.BecomeReviewer();
await returned.ReviewService().DecideAsync(returned.SiteId, returnedReport.Report.Id,
    new(0, 0, SiteReportReviewOutcome.Returned, "Please supply photo and explanation."), "returned-decision-001", returned.Context, ct);
await Fails(() => returned.ReviewService().DecideAsync(returned.SiteId, returnedReport.Report.Id,
    new(1, 0, SiteReportReviewOutcome.Accepted, "Accept without response."), "premature-review", returned.Context, ct), "site_report_not_pending_review", 409);
returned.BecomeEmployee();
var respond = new RespondToSiteReportRequest(1, 0, returned.Reviews.Single().Id, "Additional operational evidence.", upload.Attachment);
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond with { ExpectedReviewSequence = 2 }, "stale-response-001", returned.Context, ct), "site_report_version_conflict", 409);
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond with { ReturnedReviewId = Guid.NewGuid() }, "wrong-returned-review", returned.Context, ct), "site_report_not_awaiting_response", 409);
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond with { WrittenResponse = " " }, "empty-response-001", returned.Context, ct), "invalid_site_report_response", 400);
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond with { WrittenResponse = new string('a', 4001) }, "long-response-001", returned.Context, ct), "invalid_site_report_response", 400);
returned.Scan = new(DocumentScanStatus.Failed, "unavailable");
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond, "scanner-response-001", returned.Context, ct), "report_attachment_scanner_unavailable", 503);
Check(returned.Responses.Count == 0 && returned.Reviews.Count == 1, "scanner failure preserves returned state");
returned.Scan = new(DocumentScanStatus.Clean, "synthetic/scanner");
var responded = await returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond, "response-with-file", returned.Context, ct);
var responseRow = returned.Responses.Single();
Check(responded.Responses!.Single().HasAttachment && responded.Report.ResponseSequence == 1, "scanned response attachment and immutable sequence retained");
var responseFile = await returned.ReviewService().OwnResponseEvidenceAsync(returnedReport.Report.Id, responseRow.Id, returned.Context, ct);
Check(responseFile.Content.SequenceEqual(upload.Attachment.Content), "purpose-bound response attachment roundtrips privately");
var beforeRescan = returned.ScanCalls;
var responseRetry = await returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond, "response-with-file", returned.Context, ct);
Check(responseRetry.Report == responded.Report && returned.Responses.Count == 1 && returned.ScanCalls == beforeRescan, "response exact retry does not duplicate or rescan evidence");
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond with { WrittenResponse = "changed" }, "response-with-file", returned.Context, ct), "idempotency_key_reused", 409);
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond, "duplicate-response-newkey", returned.Context, ct), "site_report_version_conflict", 409);
returned.Identity = returned.Identity with { AccountId = Guid.NewGuid() };
await Fails(() => returned.ReviewService().RespondAsync(returnedReport.Report.Id, respond, "different-account-response", returned.Context, ct), "site_report_not_found", 404);
returned.BecomeReviewer(); returned.Grants.Add(new(SiteReportPermissions.Read, false, returned.RoleId, AccessScopeKind.Site, returned.SiteId));
Check((await returned.Service().DetailAsync(returned.SiteId, returnedReport.Report.Id, returned.Context, ct)).Responses!.Single().Id == responseRow.Id,
    "scoped HR detail includes decrypted employee answer");
Check((await returned.ReviewService().ResponseEvidenceAsync(returned.SiteId, returnedReport.Report.Id, responseRow.Id, returned.Context, ct)).Content.SequenceEqual(upload.Attachment.Content),
    "scoped reviewer attachment route reads only bound report response");
await Fails(() => returned.ReviewService().ResponseEvidenceAsync(Guid.NewGuid(), returnedReport.Report.Id, responseRow.Id, returned.Context, ct), "access_denied", 403);
await Fails(() => returned.ReviewService().ResponseEvidenceAsync(returned.SiteId, returnedReport.Report.Id, Guid.NewGuid(), returned.Context, ct), "site_report_not_found", 404);
returned.FailSave = true;
try { await returned.ReviewService().DecideAsync(returned.SiteId, returnedReport.Report.Id, new(1,1,SiteReportReviewOutcome.Accepted,"Synthetic rollback."),"rollback-review-001",returned.Context,ct); throw new Exception("Expected rollback"); }
catch(IOException) { Check(returned.Reviews.Count == 1 && returned.Responses.Count == 1 && returned.Intents.Count == 3, "failed save rolls back review, notification, audit and receipt staging"); }
returned.FailSave = false;
// Tamper a protected response binding without changing the database row; reads must fail closed.
var responseReference = JsonSerializer.Deserialize<PrivateFileReference>(responseRow.PrivateEvidenceReference)!;
var retainedResponse = returned.Stored[responseReference.StorageKey];
var responseJson = System.Text.Json.Nodes.JsonNode.Parse(retainedResponse.Bytes)!;
responseJson["ReportId"] = Guid.NewGuid().ToString();
returned.Stored[responseReference.StorageKey] = (retainedResponse.Purpose, JsonSerializer.SerializeToUtf8Bytes(responseJson));
await Fails(() => returned.Service().DetailAsync(returned.SiteId, returnedReport.Report.Id, returned.Context, ct), "report_response_evidence_unavailable", 409);
returned.BecomeEmployee();
await Fails(() => returned.Service().ReconcileAsync("returned-report-001", returned.Context, ct), "report_response_evidence_unavailable", 409);
Check(SiteReportPermissions.Definitions.Single(x => x.Code == SiteReportPermissions.Decide).Management,
    "HR decision is a management permission with no fixed role-name gate");
Console.WriteLine($"PASS: {checks} site-report service/rule checks (synthetic memory; no SQL, genuine login or scanner acceptance).");

sealed class Fixture : ICurrentAuthentication, IAccountAccessReader, ISchedulingJournalRepository,
    ISiteReportRepository, ISiteReportReviewRepository, IPrivateFileStore, IEmployeeFileScanner, INotificationOutbox
{
    public readonly Guid EmployeeId = Guid.NewGuid(), SiteId = Guid.NewGuid(), RoleId = Guid.NewGuid();
    public readonly DateTimeOffset Now = new(2026,10,8,21,30,0,TimeSpan.Zero);
    public readonly AuthRequestContext Context = new("127.0.0.1","synthetic-site-report-check");
    public readonly UserAccount Account;
    public readonly RegisteredDevice Device;
    public RegisteredDevice SessionDevice;
    public readonly UserSession Session;
    public readonly EmployeeAssignment Assignment;
    public AuthenticatedAccount Identity;
    public bool CurrentSession = true, FailSave, QueriedAllSites, LeakQueueSites, LeakSubmission;
    public Guid? QueriedEmployee, QueriedSite;
    public Guid? QueriedSubmissionEmployee, QueriedSubmissionAccount;
    public string? QueriedSubmissionKey;
    public DateOnly AssignmentDate;
    public int FileReads, ScanCalls, DiscoveryCalls, SubmissionQueries;
    public IReadOnlyList<Guid> QueriedQueueSites = [];
    public List<SiteReportQueueSiteResponse> QueueSites = [];
    public FileScanResult Scan = new(DocumentScanStatus.Clean,"synthetic/scanner");
    public List<PermissionGrant> Grants = [];
    public List<SiteReport> Rows = [];
    public List<SiteReportReview> Reviews = [];
    public List<SiteReportResponse> Responses = [];
    public List<SiteReportAssignment> Assignments = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public List<NotificationIntent> Intents = [];
    public Dictionary<string,(string Purpose, byte[] Bytes)> Stored = [];
    public Fixture()
    {
        Account = (UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!;
        Set(Account,"Id",Guid.NewGuid()); Set(Account,"EmployeeId",EmployeeId);
        Device = new(Account.Id,"synthetic-device-hash","android","synthetic",Now.AddDays(-30)); Device.MarkVerified(Now.AddDays(-30));
        SessionDevice = Device;
        Session = (UserSession)Activator.CreateInstance(typeof(UserSession),true)!;
        Set(Session,"Id",Guid.NewGuid()); Set(Session,"UserAccountId",Account.Id); Set(Session,"Client",SessionClient.Mobile);
        Set(Session,"RegisteredDeviceId",(Guid?)Device.Id); Set(Session,"SecurityVersion",1); Set(Session,"CreatedAt",Now.AddMinutes(-10)); Set(Session,"ExpiresAt",Now.AddHours(1));
        Identity = new(Account.Id, EmployeeId, Session.Id, 1, SessionClient.Mobile,false);
        Assignment = new(EmployeeId,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),null,SiteId,null,
            new(2026,9,1),null,"synthetic",Now.AddDays(-30));
        Assignments.Add(new(Assignment,true,null,null)); GrantSelf();
    }
    public AttendanceOperation Operation()
    {
        var authz = InterfaceProxy.Make<IAccessManagementRepository>((m,a) => m.Name switch {
            "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()).Invoke(this,a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SYNTHETIC_ROLE"),
            _ => throw new NotSupportedException(m.Name)
        });
        var auth = InterfaceProxy.Make<IAuthenticationRepository>((m,a) => m.Name switch {
            "GetSessionAsync" => Task.FromResult<UserSession?>(Session),
            "GetDeviceAsync" => Task.FromResult<RegisteredDevice?>(SessionDevice),
            _ => throw new NotSupportedException(m.Name)
        });
        return new AttendanceOperation(this,this,authz,this,auth,new FixedClock(Now),this);
    }
    public SiteReportService Service() => new(Operation(),this,this,this,this,new(Operation(),this,this),this);
    public SiteReportReviewService ReviewService() => new(Operation(),this,this,new(Operation(),this,this),this);
    public void BecomeReviewer()
    { Identity=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true); Grants.Clear(); Grants.Add(new(SiteReportPermissions.Decide,false,RoleId,AccessScopeKind.Site,SiteId)); }
    public void BecomeEmployee()
    { Identity=new(Account.Id,EmployeeId,Session.Id,1,SessionClient.Mobile,false); Grants.Clear(); GrantSelf(); }
    public void UseDevice(RegisteredDevice device)
    { SessionDevice = device; Set(Session, "RegisteredDeviceId", (Guid?)device.Id); }
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {
        var oldRows = Rows.ToArray(); var oldReceipts=Receipts.ToArray(); var oldAudits=Audits.ToArray(); var oldIntents=Intents.ToArray();
        var oldReviews=Reviews.ToArray(); var oldResponses=Responses.ToArray();
        try { return await action(ct); }
        catch { Rows=[..oldRows]; Receipts=[..oldReceipts]; Audits=[..oldAudits]; Intents=[..oldIntents]; Reviews=[..oldReviews]; Responses=[..oldResponses]; throw; }
    }
    public SubmitSiteReportRequest Input() => new(SiteId,SiteReportKind.Arrival,"Arrived at assigned site");
    public void GrantSelf() => Grants.Add(new(SiteReportPermissions.Self,false,RoleId,AccessScopeKind.Self,EmployeeId));
    public void GrantRead(Guid siteId) => Grants.Add(new(SiteReportPermissions.Read,false,RoleId,AccessScopeKind.Site,siteId));
    public AuthenticatedAccount Require() => Identity;
    public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid id,DateTimeOffset now,CancellationToken ct) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants);
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor,CancellationToken ct) => Task.FromResult(CurrentSession);
    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor,string operation,string key,CancellationToken ct) => Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==actor&&x.Operation==operation&&x.Key==key));
    public void AddAudit(AuditEvent row) => Audits.Add(row);
    public void AddReceipt(IdempotencyReceipt row) => Receipts.Add(row);
    public Task SaveChangesAsync(CancellationToken ct) => FailSave ? Task.FromException(new IOException("synthetic save failure")) : Task.CompletedTask;
    public Task<SiteReportAuthor?> FindAuthorAsync(Guid a,Guid d,CancellationToken ct) => Task.FromResult<SiteReportAuthor?>(a==Account.Id&&d==SessionDevice.Id?new(Account,SessionDevice):null);
    public Task<IReadOnlyList<SiteReportAssignment>> AssignmentsAsync(Guid e,DateOnly date,CancellationToken ct) { AssignmentDate=date; return Task.FromResult<IReadOnlyList<SiteReportAssignment>>(Assignments); }
    public Task<SiteReportReadModel?> FindAsync(Guid id,Guid? e,Guid? site,CancellationToken ct) => Task.FromResult(Rows.Where(x=>x.Id==id&&(!e.HasValue||x.EmployeeId==e)&&(!site.HasValue||x.SiteId==site)).Select(x=>new SiteReportReadModel(x,Reviews.Where(r=>r.SiteReportId==x.Id).ToArray(),Responses.Where(r=>r.SiteReportId==x.Id).ToArray())).SingleOrDefault());
    public Task<SiteReportReadModel?> FindSubmissionAsync(Guid employeeId, Guid accountId, string key, CancellationToken ct)
    {
        SubmissionQueries++; QueriedSubmissionEmployee = employeeId; QueriedSubmissionAccount = accountId; QueriedSubmissionKey = key;
        var row = LeakSubmission ? Rows.FirstOrDefault() : Rows.SingleOrDefault(x => x.EmployeeId == employeeId && x.UserAccountId == accountId && x.IdempotencyKey == key);
        return Task.FromResult(row is null ? null : new SiteReportReadModel(row, Reviews.Where(x => x.SiteReportId == row.Id).ToArray(), Responses.Where(x => x.SiteReportId == row.Id).ToArray()));
    }
    public Task<PagedResult<SiteReportReadModel>> ListAsync(Guid? e,Guid? site,DateTimeOffset from,DateTimeOffset to,SiteReportListRequest request,CancellationToken ct)
    {
        QueriedEmployee=e; QueriedSite=site;
        var rows=Rows.Where(x=>(!e.HasValue||x.EmployeeId==e)&&(!site.HasValue||x.SiteId==site)&&x.ReceivedAtUtc>=from&&x.ReceivedAtUtc<to).OrderByDescending(x=>x.ReceivedAtUtc).ThenBy(x=>x.Id).ToArray();
        return Task.FromResult(new PagedResult<SiteReportReadModel>(rows.Skip((request.Page-1)*request.PageSize).Take(request.PageSize)
            .Select(x=>new SiteReportReadModel(x, Reviews.Where(r=>r.SiteReportId==x.Id).OrderByDescending(r=>r.Sequence).Take(1).ToArray(),
                Responses.Where(r=>r.SiteReportId==x.Id && r.EmployeeId==x.EmployeeId).OrderByDescending(r=>r.Sequence).Take(1).ToArray())).ToArray(),rows.Length,request.Page,request.PageSize));
    }
    public Task<bool> HasSubmissionKeyAsync(Guid e,string key,CancellationToken ct) => Task.FromResult(Rows.Any(x=>x.EmployeeId==e&&x.IdempotencyKey==key));
    public Task<PagedResult<SiteReportQueueSiteResponse>> QueueSitesAsync(bool allSites, IReadOnlyList<Guid> siteIds,
        SiteReportQueueListRequest request, CancellationToken ct)
    {
        DiscoveryCalls++; QueriedAllSites = allSites; QueriedQueueSites = siteIds;
        var sites = QueueSites.Where(x => LeakQueueSites || allSites || siteIds.Contains(x.SiteId)).OrderBy(x => x.Code).ThenBy(x => x.SiteId).ToArray();
        return Task.FromResult(new PagedResult<SiteReportQueueSiteResponse>(sites.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToArray(), sites.Length, request.Page, request.PageSize));
    }
    public void Add(SiteReport report) => Rows.Add(report);
    public void Add(SiteReportReview review) => Reviews.Add(review);
    public void Add(SiteReportResponse response) => Responses.Add(response);
    public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct)
    {
        var key=Guid.NewGuid().ToString("N"); Stored.Add(key,(purpose,bytes.ToArray()));
        return Task.FromResult(new PrivateFileReference(key,"v1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic-key",bytes.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct)
    {
        FileReads++; var value=Stored[reference.StorageKey];
        if(value.Purpose!=purpose) throw new IOException("purpose mismatch"); return Task.FromResult(value.Bytes.ToArray());
    }
    public Task<FileScanResult> ScanAsync(byte[] bytes,CancellationToken ct) { ScanCalls++; return Task.FromResult(Scan); }
    public Task<bool> StageAsync(NotificationIntent intent,AuthRequestContext context,CancellationToken ct) { Intents.Add(intent); return Task.FromResult(true); }
    private static void Set(object value,string name,object? data) => value.GetType().GetProperty(name)!.SetValue(value,data);
}
sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
public class InterfaceProxy : DispatchProxy
{
    public Func<MethodInfo,object?[]?,object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args) => Handler(method!,args);
    public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class
    { var proxy=Create<T,InterfaceProxy>(); ((InterfaceProxy)(object)proxy).Handler=handler; return proxy; }
}
