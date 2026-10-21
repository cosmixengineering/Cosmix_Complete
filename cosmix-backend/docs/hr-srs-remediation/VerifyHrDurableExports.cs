using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Reports;
using Cosmix.Infrastructure.Files;
using UglyToad.PdfPig;

if (args.Length != 0)
{
    if (args.Length != 2 || args[0] != "--read-physical-export") throw new InvalidOperationException("Unknown verification mode.");
    await PhysicalExportFiles.ReadInChild(args[1]);
    return;
}

var checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
async Task Reject(Func<Task> action, string code)
{
    checks++;
    try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; }
    throw new Exception("Expected " + code);
}
{
    var failures = new List<string>();
    foreach (var permission in HrReportAuthorization.RequiredPermissions(HrReportTypes.SiteReporting, true))
    foreach (var atExpiry in new[] { false, true })
    {
        var f = new Fixture { RowCount = 1 };
        var expiry = f.Clock.Now.AddSeconds(1);
        var index = f.Grants.FindIndex(g => g.Permission == permission);
        f.Grants[index] = f.Grants[index] with { ValidUntilUtc = expiry };
        var request = await f.Request();
        f.NotificationStaging = () => f.ScopeQuery = () => f.Clock.Now = atExpiry ? expiry : expiry.AddTicks(-1);
        await f.Dispatch();
        checks++;
        if (atExpiry)
        {
            if (f.Messages.Single().LastFailureCode != "hr_report_scope_revoked" || f.Stored.Count != 0 ||
                f.Notifications.Count != 0 || f.JobRows[request.Id].State != ReportJobState.Pending)
                failures.Add(permission + ": expired grant published");
        }
        else if (f.JobRows[request.Id].State != ReportJobState.Completed || f.Stored.Count != 1 || f.Notifications.Count != 1)
            failures.Add(permission + ": unexpired grant denied");
    }
    Check(failures.Count == 0, "Final scope-query grant deadline: " + string.Join(", ", failures));
}
{
    var failures = new List<string>();
    foreach (var stage in new[] { "request", "replay", "status", "download" })
    foreach (var crossing in new[] { "scope-query", "audit-save" })
    {
        var f = new Fixture { RowCount = 1 };
        f.Clock.Now = new(2026, 10, 19, 18, 59, 59, TimeSpan.Zero);
        var midnight = f.Clock.Now.AddSeconds(1);
        var request = stage == "request" ? null : await f.Request();
        if (stage is "status" or "download") await f.Dispatch();
        var before = (f.JobRows.Count, f.Receipts.Count, f.Messages.Count, f.Audits.Count);
        var grants = f.Grants.ToArray();
        f.EmployeeScopeExpiresAt = midnight;
        if (crossing == "scope-query")
            f.Files.AfterWrite = purpose => { if (purpose.Contains(":audit:")) f.ScopeQuery = () => f.Clock.Now = midnight; };
        else f.AuditSave = () => f.Clock.Now = midnight;
        var denied = false;
        try
        {
            await (stage switch
            {
                "status" => (Task)f.Exports.StatusAsync(request!.Id, f.Context, default),
                "download" => f.Exports.DownloadAsync(request!.Id, f.Context, default),
                _ => f.Request()
            });
        }
        catch (AuthenticationFailure e) when (e.Code == "access_denied") { denied = true; }
        checks++;
        if (!denied) failures.Add(stage + ":" + crossing);
        Check(f.Clock.Now == midnight && f.Grants.SequenceEqual(grants), crossing + " changes business date with unchanged grants");
        if (stage == "download") Check(f.JobRows[request!.Id].DownloadExpiresAt > f.Clock.Now, "download expiry is not the rejection cause");
        if (denied) Check(before == (f.JobRows.Count, f.Receipts.Count, f.Messages.Count, f.Audits.Count), stage + ":" + crossing + " rolls back");
    }
    Check(failures.Count == 0, "Business-date crossing accepted: " + string.Join(", ", failures));
}
foreach (var utcBoundary in new[] { false, true })
{
    var f = new Fixture { RowCount = 1 };
    f.Clock.Now = new(2026, 10, 19, utcBoundary ? 23 : 18, 59, 59, TimeSpan.Zero);
    var request = await f.Request(); await f.Dispatch();
    var grants = f.Grants.ToArray();
    var afterCommit = f.Clock.Now.AddSeconds(1);
    f.TransactionCompleted = () => f.Clock.Now = afterCommit;
    if (utcBoundary)
        Check((await f.Exports.DownloadAsync(request.Id, f.Context, default)).Content.Length > 0, "UTC midnight within one Karachi business date remains valid");
    else
        await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "access_denied");
    Check(f.Clock.Now == afterCommit && f.Grants.SequenceEqual(grants) && f.JobRows[request.Id].DownloadExpiresAt > f.Clock.Now,
        "post-transaction date check uses unchanged grants and unexpired output");
}
{
    var f = new Fixture { RowCount = 1 };
    f.Clock.Now = new(2026, 10, 19, 18, 59, 59, TimeSpan.Zero);
    var request = await f.Request();
    var midnight = f.Clock.Now.AddSeconds(1);
    f.EmployeeScopeExpiresAt = midnight;
    f.NotificationStaging = () => f.ScopeQuery = () => f.Clock.Now = midnight;
    await f.Dispatch();
    Check(f.Messages.Single().LastFailureCode == "hr_report_scope_revoked" && f.Stored.Count == 0 && f.Notifications.Count == 0 &&
        f.JobRows[request.Id].State == ReportJobState.Pending, "publication rejects date crossed during final scope query");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request(); await f.Dispatch();
    var expiry = f.JobRows[request.Id].DownloadExpiresAt!.Value;
    f.Clock.Now = expiry.AddTicks(-1);
    f.Files.AfterWrite = purpose => { if (purpose.Contains(":audit:")) f.Clock.Now = expiry; };
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "hr_report_download_expired");
}
foreach (var stage in new[] { "request", "replay", "status", "download" })
{
    var f = new Fixture { RowCount = 1 };
    var request = stage == "request" ? null : await f.Request();
    if (stage is "status" or "download") await f.Dispatch();
    var before = (f.JobRows.Count, f.Receipts.Count, f.Messages.Count, f.Audits.Count);
    var expiry = f.Clock.Now.AddSeconds(1);
    f.EmployeeScopeExpiresAt = expiry;
    f.Files.AfterWrite = purpose => { if (purpose.Contains(":audit:")) f.Clock.Now = expiry; };
    await Reject(() => stage switch
    {
        "status" => f.Exports.StatusAsync(request!.Id, f.Context, default),
        "download" => f.Exports.DownloadAsync(request!.Id, f.Context, default),
        _ => f.Request()
    }, "access_denied");
    Check(before == (f.JobRows.Count, f.Receipts.Count, f.Messages.Count, f.Audits.Count),
        stage + " scheduled resource-scope expiry during audit IO rolls back publication");
}
{
    var f = new Fixture { RowCount = 1005 };
    var requested = await f.Request();
    var outputOutsideTransaction = false;
    f.Files.AfterWrite = purpose => { if (purpose.Contains(":csv:")) outputOutsideTransaction = !f.Inside; };
    f.BeforeCompletion = () => Check(f.SnapshotReads == 1 && f.WorkerLocks == 0 && f.Stored.Count == 0 &&
        f.Notifications.Count == 0 && f.JobRows[requested.Id].State == ReportJobState.Pending, "preparation is unpublished and never auth-locked");
    await f.Dispatch();
    Check(outputOutsideTransaction, "P1 output IO must occur outside source/publication transaction");
    Check(f.WorkerLocks == 1 && f.DataReads == 2, "one final auth lock after both source pages");
    Check(f.JobRows[requested.Id].State == ReportJobState.Completed, "P1 prepared output publishes");
}
foreach (var stage in new[] { "after-prepare", "notification-io" })
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request();
    if (stage == "after-prepare") f.BeforeCompletion = () => f.SessionCurrent = false;
    else f.NotificationStaging = () => f.SessionCurrent = false;
    await f.Dispatch();
    Check(f.Messages.Single().LastFailureCode == "hr_report_scope_revoked" && f.Stored.Count == 0 &&
        f.Notifications.Count == 0 && f.JobRows[request.Id].State == ReportJobState.Pending, stage + " revocation blocks atomic publication");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request();
    f.BeforeCompletion = () => f.Clock.Now = f.Clock.Now.AddMinutes(3);
    Check(!await f.Dispatch() && f.Stored.Count == 0 && f.Notifications.Count == 0 && f.WorkerLocks == 0,
        "expired preparation cannot enter publication");
    f.BeforeCompletion = null; f.Restart(); await f.Dispatch();
    Check(f.JobRows[request.Id].State == ReportJobState.Completed && f.Stored.Count == 1 && f.SnapshotReads == 2,
        "expired lease restart prepares again and publishes once");
}
{
    var f = new Fixture { RowCount = 1 }; await f.Request();
    f.BeforeCompletion = () => typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.CorrelationId))!.SetValue(f.Messages.Single(), "changed-binding");
    await f.Dispatch();
    Check(f.Messages.Single().LastFailureCode == "dispatch_preparation_mismatch" && f.Stored.Count == 0 && f.Notifications.Count == 0,
        "immutable message preparation mismatch blocks publication");
}
foreach (var change in new[] { "employee-status", "candidate-population" })
{
    var f = new Fixture(HrReportTypes.EmployeeMaster) { RowCount = 0 };
    var requested = await f.Request(filter: new(Status: "Active"));
    await f.Dispatch();
    f.MutableFilterChange = change;
    Check((await f.Exports.StatusAsync(requested.Id, f.Context, default)).State == "completed", change + " retained status remains authorized");
    Check((await f.Exports.DownloadAsync(requested.Id, f.Context, default)).Content.Length > 0, change + " retained download remains authorized");
    Check((await f.Request(filter: new(Status: "Active"))).Id == requested.Id, change + " retained replay remains authorized");
}
foreach (var stage in new[] { "publication", "download" })
{
    var f = new Fixture { RowCount = 1 }; var requested = await f.Request();
    f.GeneratedSite = f.OtherSite;
    void RevokeGeneratedSite()
    {
        foreach (var permission in HrReportAuthorization.RequiredPermissions(f.Type, true))
            f.Grants.Replace(permission, AccessScopeKind.Site, f.Site);
        f.GeneratedSite = null;
    }
    if (stage == "publication") f.BeforeCompletion = RevokeGeneratedSite;
    await f.Dispatch();
    if (stage == "publication")
        Check(f.Messages.Single().LastFailureCode == "hr_report_scope_revoked" && f.Stored.Count == 0 && f.Notifications.Count == 0,
            "newly generated historical source scope rechecked at publication");
    else
    {
        RevokeGeneratedSite();
        await Reject(() => f.Exports.DownloadAsync(requested.Id, f.Context, default), "access_denied");
        await Reject(() => f.Exports.StatusAsync(requested.Id, f.Context, default), "access_denied");
    }
}
foreach (var type in HrReportTypes.All)
foreach (var format in Enum.GetValues<ReportFormat>())
{
    var f = new Fixture(type) { RowCount = 0 };
    var requested = await f.Request(format);
    Check(requested.State == "pending" && requested.TotalCount is null, type + " pending with unknown count");
    Check(f.JobRows.Count == 1 && f.Messages.Count == 1 && f.Receipts.Count == 1, type + " job/outbox/receipt staged");
    Check(await f.Dispatch(), type + " existing dispatcher executes handler");
    var status = await f.Exports.StatusAsync(requested.Id, f.Context, default);
    Check(status.State == "completed" && status.TotalCount == 0 && status.ReturnedCount == 0, type + " exact empty result");
    var file = await f.Exports.DownloadAsync(requested.Id, f.Context, default);
    Check(file.Content.Length > 0 && file.ContentType.StartsWith(HrReportExportRules.ContentType(format)), type + " correct format");
    Check(f.Notifications.Count == 1 && f.Stored.Count == 1, type + " one private file and ready intent");
}

foreach (var format in Enum.GetValues<ReportFormat>())
{
    var f = new Fixture() { RowCount = 1005 };
    var request = await f.Request(format, filter: new(Page: 87, PageSize: 2));
    Check(f.DataReads == 0, "request queues without loading report pages");
    var replay = await f.Request(format, filter: new(Page: 1, PageSize: 100));
    Check(replay.Id == request.Id && f.JobRows.Count == 1, "pagination ignored and idempotent request");
    await Reject(() => f.Request(format, reason: "Changed reason"), "idempotency_key_reused");
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "hr_report_not_ready");
    await f.Dispatch();
    Check(f.DataReads == 2, "full 1005 rows traverses two repository pages");
    var status = await f.Exports.StatusAsync(request.Id, f.Context, default);
    Check(status.TotalCount == 1005 && status.ReturnedCount == 1005, "counts reflect complete data");
    var file = await f.Exports.DownloadAsync(request.Id, f.Context, default);
    if (format == ReportFormat.Csv)
    {
        var text = Encoding.UTF8.GetString(file.Content);
        Check(text.Contains("row-1005") && text.Contains("'" + "=SYNTHETIC"), "last row and formula protection");
    }
    if (format == ReportFormat.Xlsx)
    {
        using var zip = new ZipArchive(new MemoryStream(file.Content));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet2.xml")!.Open());
        var text = reader.ReadToEnd();
        Check(text.Contains("row-1005") && !text.Contains("<f>"), "XLSX last row and no formulas");
    }
    if (format == ReportFormat.Pdf)
    {
        using var pdf = PdfDocument.Open(file.Content);
        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        Check(pdf.NumberOfPages > 1 && text.Contains("row-1005") && text.Contains("\\u062D"), "PDF pagination/last row/Unicode retained");
        Check(pdf.GetPages().All(p => p.Letters.All(l => l.BoundingBox.Left >= 39 && l.BoundingBox.Right < 557 &&
            l.BoundingBox.Bottom > 25 && l.BoundingBox.Top < 820)), "PDF glyphs fit page without clipping");
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "synthetic-hr-report.pdf"), file.Content);
    }
    f.Clock.Now = f.Clock.Now.AddHours(2);
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "hr_report_download_expired");
}

// Generation failures are exposed through the existing outbox state; never complete a partial file.
foreach (var scenario in new[] { "missing-page", "changed-count", "no-count", "too-many", "oversized-text" })
{
    var f = new Fixture { RowCount = 1005, Scenario = scenario };
    var request = await f.Request(); await f.Dispatch();
    var status = await f.Exports.StatusAsync(request.Id, f.Context, default);
    Check(status.State == "failed" && status.TotalCount is null, scenario + " terminal failure without fabricated count");
    Check(f.Stored.Count == 0 && f.Notifications.Count == 0 && f.JobRows[request.Id].State == ReportJobState.Pending,
        scenario + " transaction rolled back");
}

foreach (var stage in new[] { "request", "build", "download", "status", "replay" })
foreach (var permission in HrReportAuthorization.RequiredPermissions(HrReportTypes.SiteReporting, true))
{
    var f = new Fixture { RowCount = 1 };
    HrReportExportResponse? request = stage == "request" ? null : await f.Request();
    if (stage is "download" or "status") await f.Dispatch();
    f.Grants.RemoveAll(g => g.Permission == permission);
    if (stage == "build")
    {
        await f.Dispatch();
        Check(f.Messages.Single().State == DispatchState.Failed && f.Messages.Single().LastFailureCode == "hr_report_scope_revoked",
            "worker rejects revoked " + permission);
    }
    else await Reject(() => stage switch
    {
        "download" => f.Exports.DownloadAsync(request!.Id, f.Context, default),
        "status" => f.Exports.StatusAsync(request!.Id, f.Context, default),
        _ => f.Request()
    }, "access_denied");
    Check(f.Stored.Count == (stage is "download" or "status" ? 1 : 0), stage + " denied before output generation");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request();
    f.Actor = f.Actor with { AccountId = Guid.NewGuid() };
    await Reject(() => f.Exports.StatusAsync(request.Id, f.Context, default), "hr_report_not_found");
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "hr_report_not_found");
}
{
    var f = new Fixture(); f.Options.Enabled = false;
    await Reject(() => f.Request(), "hr_report_export_worker_not_enabled");
    Check(f.JobRows.Count == 0, "disabled worker stages nothing");
}
{
    var f = new Fixture(); f.Actor = f.Actor with { Client = SessionClient.Mobile };
    await Reject(() => f.Request(), "access_denied");
    f.Actor = f.Actor with { Client = SessionClient.Web, MfaSatisfied = false };
    await Reject(() => f.Request(), "access_denied");
}
{
    var f = new Fixture { RowCount = 1 }; await f.Request(); f.SessionCurrent = false;
    await f.Dispatch(); Check(f.Messages.Single().LastFailureCode == "hr_report_scope_revoked", "revoked original session blocks worker");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request();
    f.RemoveEmployee = true; await f.Dispatch();
    Check(f.Messages.Single().State == DispatchState.Failed, "lost current employee scope fails whole job");
    await Reject(() => f.Exports.StatusAsync(request.Id, f.Context, default), "access_denied");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request();
    foreach (var p in HrReportAuthorization.RequiredPermissions(f.Type, true))
        f.Grants.Replace(p, AccessScopeKind.Site, f.OtherSite);
    // Repository can currently authorize another site; original retained site must still deny.
    f.CurrentSite = f.OtherSite;
    await f.Dispatch(); Check(f.Messages.Single().LastFailureCode == "hr_report_scope_revoked", "original resource scope rechecked");
    await Reject(() => f.Exports.StatusAsync(request.Id, f.Context, default), "access_denied");
}
foreach (var phase in new[] { "filters", "receipt", "audit" })
{
    var f = new Fixture { RowCount = 1 }; f.Files.FailPurpose = ":" + phase + ":";
    await Reject(() => f.Request(), "private_storage_unavailable");
    Check(f.JobRows.Count == 0 && f.Receipts.Count == 0 && f.Messages.Count == 0, phase + " request rollback");
}
foreach (var phase in new[] { "csv", "manifest" })
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request();
    f.Files.FailPurpose = ":" + phase + ":";
    await f.Dispatch();
    Check(f.Messages.Single().State == DispatchState.Pending && f.Messages.Single().Attempts == 1 && f.Stored.Count == 0,
        phase + " private storage retry and atomic rollback");
    f.Files.FailPurpose = null; f.Clock.Now = f.Clock.Now.AddSeconds(10);
    f.Restart(); await f.Dispatch();
    Check((await f.Exports.StatusAsync(request.Id, f.Context, default)).State == "completed" &&
        f.JobRows.Count == 1 && f.Stored.Count == 1 && f.Notifications.Count == 1, phase + " restart retry completes once");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request(); f.Options.MaximumAttempts = 2;
    f.Files.FailPurpose = ":csv:"; await f.Dispatch(); f.Clock.Now = f.Clock.Now.AddSeconds(10); await f.Dispatch();
    var status = await f.Exports.StatusAsync(request.Id, f.Context, default);
    Check(status.State == "failed" && status.FailureCode == "private_storage_unavailable", "retry exhaustion visible");
}
foreach (var phase in new[] { "csv", "manifest" })
{
    var f = new Fixture { RowCount = 1 }; await f.Request();
    f.Files.AfterWrite = purpose => { if (purpose.Contains(":" + phase + ":")) f.SessionCurrent = false; };
    await f.Dispatch();
    Check(f.Messages.Single().LastFailureCode == "hr_report_scope_revoked" && f.Stored.Count == 0 && f.Notifications.Count == 0,
        "revocation during " + phase + " IO blocks publish");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request(); await f.Dispatch();
    f.Files.AfterRead = purpose => { if (purpose.Contains(":csv:")) f.SessionCurrent = false; };
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "invalid_authentication");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request(); await f.Dispatch();
    f.Files.CorruptReads = true;
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "hr_report_evidence_conflict");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request(); await f.Dispatch();
    f.Files.MissingReads = true;
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "private_storage_unavailable");
}
{
    var f = new Fixture { RowCount = 1 }; var request = await f.Request();
    using var cancel = new CancellationTokenSource();
    f.Files.AfterWrite = purpose => { if (purpose.Contains(":csv:")) { cancel.Cancel(); cancel.Token.ThrowIfCancellationRequested(); } };
    try { await f.Dispatch(cancel.Token); throw new Exception("cancellation accepted"); } catch (OperationCanceledException) { checks++; }
    Check(f.JobRows[request.Id].State == ReportJobState.Pending && f.Messages.Single().State == DispatchState.Processing,
        "interruption rolls back business state but retains recoverable lease");
    f.Files.AfterWrite = null; f.Clock.Now = f.Clock.Now.AddMinutes(3); f.Restart(); await f.Dispatch();
    Check(f.JobRows[request.Id].State == ReportJobState.Completed && f.Notifications.Count == 1, "expired lease restart completes once");
}

foreach (var format in Enum.GetValues<ReportFormat>())
{
    var disk = new PhysicalExportFiles();
    var f = new Fixture(files: disk.Store()) { RowCount = 2 };
    var request = await f.Request(format);
    await f.Dispatch();
    Check(f.JobRows[request.Id].State == ReportJobState.Completed, format + " publishes with real encrypted store");
    var download = await f.Exports.DownloadAsync(request.Id, f.Context, default);
    var stored = f.Stored.Single();
    var path = disk.Object(stored.StorageKey);
    var ciphertext = await File.ReadAllBytesAsync(path);
    Check(ciphertext.Length > download.Content.Length && !ciphertext.SequenceEqual(download.Content), format + " disk object is not plaintext");
    Check((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0, format + " persisted object is immutable by default");
    Check(Directory.GetFiles(disk.Objects).Length >= 5, format + " scope, receipt, audit and export evidence reach disk");
    f.Files.ReplaceStore(disk.Store()); f.Restart();
    Check((await f.Exports.DownloadAsync(request.Id, f.Context, default)).Content.SequenceEqual(download.Content),
        format + " rebuilt store and services read retained encrypted result");
    Check((await f.Request(format)).Id == request.Id && f.Stored.Count == 1 && f.Notifications.Count == 1,
        format + " retry after reconstruction retains one published result");
    await disk.VerifySeparateReader(f.Files.References[stored.StorageKey], request.Id, format, download.Content);
    Check(true, format + " separate dotnet process decrypts original bytes");

    // Faults affect only this run's freshly created synthetic files; retain evidence, never touch application storage.
    var damaged = ciphertext.ToArray(); damaged[^1] ^= 1;
    disk.ReplaceOwnedObject(stored.StorageKey, damaged);
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "private_file_storage_unavailable");
    disk.ReplaceOwnedObject(stored.StorageKey, ciphertext);
    var missing = path + ".held";
    File.Move(path, missing);
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "private_file_storage_unavailable");
    File.Move(missing, path);
    File.Move(disk.Key, disk.Key + ".held");
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "private_file_storage_unavailable");
    File.Move(disk.Key + ".held", disk.Key);
    Check((await f.Exports.DownloadAsync(request.Id, f.Context, default)).Content.SequenceEqual(download.Content),
        format + " restored synthetic objects remain readable");
    f.SessionCurrent = false;
    await Reject(() => f.Exports.DownloadAsync(request.Id, f.Context, default), "invalid_authentication");
    Console.WriteLine($"Physical encrypted {format} evidence retained: {disk.Root}");
}

Console.WriteLine($"PASS {checks} durable HR export checks; actual services/dispatcher/renderers and physical encrypted export round trips. Transactions/identity remain synthetic; no SQL/HTTP/provider acceptance.");

sealed class Fixture
{
    public readonly string Type;
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public readonly Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), OtherSite = Guid.NewGuid();
    public Guid CurrentSite;
    public readonly Clock Clock = new();
    public readonly List<PermissionGrant> Grants = [];
    public readonly MemoryFiles Files;
    public readonly Dictionary<Guid, ReportExportJob> JobRows = [];
    public readonly List<StoredFile> Stored = [];
    public readonly List<OutboxMessage> Messages = [];
    public readonly List<IdempotencyReceipt> Receipts = [];
    public readonly List<AuditEvent> Audits = [];
    public readonly List<NotificationIntent> Notifications = [];
    public readonly AttendanceJobOptions Options = new() { Enabled = true };
    public readonly AuthRequestContext Context = new("", "hr-export-synthetic");
    public HrReportExportService Exports = null!;
    public DurableJobProcessor Processor = null!;
    public int RowCount = 1005, DataReads;
    public bool SessionCurrent = true, Inside, RemoveEmployee, AuthLocked, InSnapshot;
    public int SnapshotReads, WorkerLocks;
    public Action? BeforeCompletion, NotificationStaging;
    public Action? ScopeQuery, AuditSave, TransactionCompleted;
    public string? MutableFilterChange;
    public Guid? GeneratedSite;
    public DateTimeOffset? EmployeeScopeExpiresAt;
    public string? Scenario;

    public Fixture(string type = HrReportTypes.SiteReporting, IPrivateFileStore? files = null)
    {
        Files = new(files);
        Type = type; CurrentSite = Site;
        foreach (var p in HrReportAuthorization.RequiredPermissions(type, true))
            Grants.Add(new(p, false, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddDays(-1) });
        Restart();
    }
    public void Restart()
    {
        var current = Proxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var auth = Proxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("REVIEWER"),
            _ => throw new Exception(m.Name)
        });
        var journal = Proxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch
        {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(r => r.ActorUserId == (string)a[0]! && r.Operation == (string)a[1]! && r.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!),
            "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => SaveAudit(),
            _ => throw new Exception(m.Name)
        });
        var data = Proxy.Make<IHrReportRepository>((m, a) =>
        {
            if (!Inside) throw new Exception("report source read outside transaction");
            if (m.Name is "SelectScopeAsync" or "SelectRetainedScopeAsync")
            {
                var allowed = (HrReportAccess)a[m.Name == "SelectScopeAsync" ? 2 : 1]!;
                ScopeQuery?.Invoke();
                if (EmployeeScopeExpiresAt is { } expiry && allowed.Now >= expiry)
                    return Task.FromResult(new HrReportSelection([], false));
                if (m.Name == "SelectScopeAsync" && MutableFilterChange == "candidate-population") throw AttendanceOperation.Conflict("hr_report_candidate_limit");
                if (m.Name == "SelectScopeAsync" && MutableFilterChange == "employee-status") return Task.FromResult(new HrReportSelection([], false));
                return Task.FromResult(new HrReportSelection(RemoveEmployee ? [] : [Employee], false)
                { ReadRoleAssignmentIds = Grants.Where(g => g.Permission == HrReportPermissions.Read).Select(g => g.RoleAssignmentId).ToArray(),
                    ScopeGroups = RemoveEmployee ? [] : m.Name == "SelectRetainedScopeAsync" && (bool)a[2]! && GeneratedSite.HasValue
                        ? [new([new(EmployeeId: Employee, SiteId: CurrentSite)]), new([new(EmployeeId: Employee, SiteId: GeneratedSite)])]
                        : [new([new(EmployeeId: Employee, SiteId: CurrentSite)])] });
            }
            if (!InSnapshot || AuthLocked) throw new Exception("worker pages must be in source snapshot without auth lock");
            DataReads++;
            var filter = (HrReportFilter)a[0]!;
            if (m.Name == "GetSiteReportingAsync")
            {
                var total = Scenario == "too-many" ? HrReportExportRules.MaximumRows + 1 : RowCount;
                if (Scenario == "changed-count" && filter.Page > 1) total++;
                var start = (filter.Page - 1) * filter.PageSize;
                var take = Scenario == "missing-page" && filter.Page > 1 ? 0 : Math.Max(0, Math.Min(filter.PageSize, RowCount - start));
                var rows = Enumerable.Range(start, take).Select(i => new SiteReportingReportRow(GuidFromInt(i + 1), Clock.Now, i + 1,
                    "row-" + (i + 1), "Synthetic site", "Observation", "=SYNTHETIC",
                    Scenario == "oversized-text" ? new string('x', 32768) : "Synthetic \u062D\u0645\u0627 note", false, "Pending", null, null, null)).ToArray();
                return Task.FromResult<IReadOnlyList<SiteReportingReportRow>>(Scenario == "no-count" ? rows : new HrReportRows<SiteReportingReportRow>(rows, total));
            }
            var list = m.ReturnType.GetGenericArguments()[0];
            var row = list.GetGenericArguments()[0];
            var result = Activator.CreateInstance(typeof(HrReportRows<>).MakeGenericType(row), Array.CreateInstance(row, 0), 0);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(list).Invoke(null, [result]);
        });
        var exports = Proxy.Make<IHrReportExportRepository>((m, a) => m.Name switch
        {
            "FindAsync" => Task.FromResult(JobRows.GetValueOrDefault((Guid)a[1]!) is { } j && j.RequestedByUserId == ((Guid)a[0]!).ToString() ? j : null),
            "FindWorkerAsync" => Task.FromResult(JobRows.GetValueOrDefault((Guid)a[0]!)),
            "FindFileAsync" => Task.FromResult(Stored.SingleOrDefault(s => s.Id == (Guid)a[0]!)),
            "LockAuthorizationAsync" => LockAuthorization(),
            "ReadSnapshotAsync" => GetType().GetMethod(nameof(Snapshot))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "Add" => a[0] is ReportExportJob job ? AddJob(job) : Add(Stored, (StoredFile)a[0]!),
            _ => throw new Exception(m.Name)
        });
        var jobs = Proxy.Make<IDurableJobRepository>((m, a) => m.Name switch
        {
            "FindByIdentityAsync" => Task.FromResult(Messages.SingleOrDefault(j => j.MessageType == (string)a[0]! && j.DeduplicationKey == (string)a[1]!)),
            "Stage" => Inside ? Add(Messages, (OutboxMessage)a[0]!) : throw new Exception("stage outside transaction"),
            "ClaimNextAsync" => Claim((IReadOnlyCollection<string>)a[0]!, (DateTimeOffset)a[1]!, (TimeSpan)a[2]!),
            "CompleteAsync" => Complete((DurableJobClaim)a[0]!, (Func<OutboxMessage, CancellationToken, Task>)a[1]!, (CancellationToken)a[2]!),
            "FailAsync" => Fail((DurableJobClaim)a[0]!, (string)a[1]!, (DateTimeOffset?)a[2]),
            "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            _ => throw new Exception(m.Name)
        });
        var notifications = Proxy.Make<INotificationOutbox>((_, a) =>
        {
            if (!Inside || AuthLocked || InSnapshot) throw new Exception("notification IO must precede auth lock within publication");
            NotificationStaging?.Invoke();
            Add(Notifications, (NotificationIntent)a[0]!); return Task.FromResult(true);
        });
        var authn = Proxy.Make<IAuthenticationRepository>((m, _) => throw new Exception(m.Name));
        var operation = new AttendanceOperation(current, access, auth, journal, authn, Clock, Files);
        var authorization = new HrReportAuthorization(current, access, auth, journal, data, Files, Clock);
        Exports = new(new(data, authorization, Clock), authorization, exports, current, journal, jobs, Files, operation, notifications, Options, Clock);
        Processor = new(jobs, [new HrReportExportHandler(Exports)], Options, Clock);
    }
    public Task<HrReportExportResponse> Request(ReportFormat format = ReportFormat.Csv, HrReportFilter? filter = null, string reason = "Synthetic export") =>
        Exports.RequestAsync(Type, new(format, reason, filter), "synthetic-key-0001", Context, default);
    public Task<bool> Dispatch(CancellationToken ct = default) => Processor.DispatchOneAsync(ct);
    public async Task<T> Snapshot<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (Inside || AuthLocked) throw new Exception("snapshot must start outside producer/auth transaction");
        var before = (JobRows.Count, Stored.Count, Receipts.Count, Messages.Count, Audits.Count, Notifications.Count);
        InSnapshot = true; SnapshotReads++;
        try
        {
            var result = await Transaction(action, ct);
            if (before != (JobRows.Count, Stored.Count, Receipts.Count, Messages.Count, Audits.Count, Notifications.Count) ||
                JobRows.Values.Any(j => j.State != ReportJobState.Pending)) throw new Exception("snapshot changed business state");
            return result;
        }
        finally { InSnapshot = false; }
    }
    private Task LockAuthorization()
    {
        if (!Inside || InSnapshot) throw new Exception("worker lock outside publication transaction");
        AuthLocked = true; WorkerLocks++; return Task.CompletedTask;
    }
    private Task SaveAudit()
    {
        if (!Inside) throw new Exception("save outside transaction");
        AuditSave?.Invoke();
        return Task.CompletedTask;
    }
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (Inside) throw new Exception("nested transaction"); Inside = true;
        var jobs = JobRows.ToArray();
        var states = jobs.ToDictionary(j => j.Key, j => j.Value.GetType().GetProperties().Where(p => p.SetMethod is not null).ToDictionary(p => p, p => p.GetValue(j.Value)));
        var stored = Stored.Count; var receipts = Receipts.Count; var messages = Messages.Count; var audits = Audits.Count; var notices = Notifications.Count;
        try
        {
            var result = await action(ct);
            TransactionCompleted?.Invoke();
            return result;
        }
        catch
        {
            JobRows.Clear(); foreach (var job in jobs) { JobRows.Add(job.Key, job.Value); foreach (var p in states[job.Key]) p.Key.SetValue(job.Value, p.Value); }
            Stored.RemoveRange(stored, Stored.Count - stored); Receipts.RemoveRange(receipts, Receipts.Count - receipts);
            Messages.RemoveRange(messages, Messages.Count - messages); Audits.RemoveRange(audits, Audits.Count - audits);
            Notifications.RemoveRange(notices, Notifications.Count - notices); throw;
        }
        finally { Inside = false; AuthLocked = false; }
    }
    private Task<DurableJobClaim?> Claim(IReadOnlyCollection<string> allowed, DateTimeOffset now, TimeSpan lease)
    {
        var job = Messages.FirstOrDefault(j => allowed.Contains(j.MessageType) && j.State is DispatchState.Pending or DispatchState.Processing &&
            j.AvailableAt <= now && (j.LeaseExpiresAt is null || j.LeaseExpiresAt <= now));
        return Task.FromResult(job is null ? null : new DurableJobClaim(job.Id, job.Claim(now, now.Add(lease)), now.Add(lease), job.Attempts)
            { Preparation = new(job.MessageType, job.EncryptedPayloadReference, job.CorrelationId, job.DeduplicationKey) });
    }
    private Task<bool> Complete(DurableJobClaim claim, Func<OutboxMessage, CancellationToken, Task> action, CancellationToken ct)
    {
        BeforeCompletion?.Invoke();
        return Transaction(async token =>
        {
            var message = Messages.Single(m => m.Id == claim.MessageId);
            if (message.LeaseToken != claim.LeaseToken || message.LeaseExpiresAt <= Clock.Now || message.State != DispatchState.Processing) return false;
            await action(message, token); message.Complete(claim.LeaseToken, Clock.Now); return true;
        }, ct);
    }
    private Task<bool> Fail(DurableJobClaim claim, string code, DateTimeOffset? retry)
    {
        var message = Messages.Single(m => m.Id == claim.MessageId);
        if (message.LeaseToken != claim.LeaseToken || message.LeaseExpiresAt <= Clock.Now || message.State != DispatchState.Processing) return Task.FromResult(false);
        message.RecordFailure(claim.LeaseToken, Clock.Now, code, retry); return Task.FromResult(true);
    }
    private object? AddJob(ReportExportJob job) { JobRows.Add(job.Id, job); return null; }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private static Guid GuidFromInt(int i) { var bytes = new byte[16]; BitConverter.GetBytes(i).CopyTo(bytes, 0); return new(bytes); }
}

sealed class Clock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, Proxy>(); ((Proxy)(object)proxy).Handler = handler; return proxy; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
}
sealed class MemoryFiles(IPrivateFileStore? store = null) : IPrivateFileStore
{
    public readonly Dictionary<string, PrivateFileReference> References = [];
    public readonly Dictionary<string, (byte[] Bytes, string Purpose)> Data = [];
    public string? FailPurpose;
    public bool CorruptReads, MissingReads;
    public Action<string>? AfterWrite, AfterRead;
    public void ReplaceStore(IPrivateFileStore replacement) => store = replacement;
    public async Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
    {
        if (FailPurpose is not null && purpose.Contains(FailPurpose)) throw new AuthenticationFailure("private_storage_unavailable", 503);
        if (store is not null)
        {
            var reference = await store.WriteAsync(bytes, purpose, ct);
            References.Add(reference.StorageKey, reference);
            AfterWrite?.Invoke(purpose); return reference;
        }
        var id = Guid.NewGuid().ToString("N"); Data.Add(id, (bytes.ToArray(), purpose)); AfterWrite?.Invoke(purpose);
        return new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length);
    }
    public async Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        if (MissingReads) throw new AuthenticationFailure("private_storage_unavailable", 503);
        byte[] bytes;
        if (store is not null) bytes = await store.ReadAsync(reference, purpose, ct);
        else
        {
            var value = Data[reference.StorageKey];
            if (purpose != value.Purpose) throw new Exception("Private purpose mismatch");
            bytes = value.Bytes.ToArray();
        }
        if (CorruptReads && purpose.Contains(":csv:")) bytes[0] ^= 1;
        AfterRead?.Invoke(purpose); return bytes;
    }
}
sealed class PhysicalExportFiles
{
    private const string Parent = @"D:\CosmixVerification\hr-backend-20261019";
    public string Root { get; }
    public string Objects => Path.Combine(Root, "objects");
    public string Key => Path.Combine(Root, "keys", "active.bin");
    private PrivateFileStorageOptions Options => new()
    { Directory = Objects, KeyFile = Key, KeyReference = "synthetic-export-only", MaxSizeBytes = 8_000_000 };
    public EncryptedPrivateFileStore Store() => new(Options);
    public PhysicalExportFiles()
    {
        Root = Path.Combine(Parent, "export-storage-" + Guid.NewGuid().ToString("N"));
        CheckRoot(Root);
        if (Directory.Exists(Root) || File.Exists(Root)) throw new InvalidOperationException("Fresh synthetic directory required.");
        using var identity = WindowsIdentity.GetCurrent();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(identity.User!);
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(Root).Create(security);
        Directory.CreateDirectory(Objects); Directory.CreateDirectory(Path.GetDirectoryName(Key)!);
        var key = RandomNumberGenerator.GetBytes(32);
        try { using var stream = new FileStream(Key, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public string Object(string name)
    {
        if (Path.GetFileName(name) != name || !name.EndsWith(".enc", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid owned object.");
        return Path.Combine(Objects, name);
    }
    public void ReplaceOwnedObject(string name, byte[] bytes)
    {
        CheckRoot(Root);
        var path = Object(name);
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Fixture object is a link.");
        try
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            using var file = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None);
            file.Write(bytes); file.Flush(flushToDisk: true);
        }
        finally { File.SetAttributes(path, attributes); }
    }
    public async Task VerifySeparateReader(PrivateFileReference reference, Guid id, ReportFormat format, byte[] plaintext)
    {
        var manifest = Path.Combine(Root, "reader.json");
        using (var output = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(output, new ReaderInput(reference, id, format,
                Convert.ToHexString(SHA256.HashData(plaintext)), plaintext.Length, Environment.ProcessId));
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--read-physical-export"); start.ArgumentList.Add(manifest);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Reader process did not start.");
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            child.Kill(entireProcessTree: true); await child.WaitForExitAsync();
            throw new InvalidOperationException("Owned synthetic reader timed out.");
        }
        if (child.ExitCode != 0 || await stderr != "" || !(await stdout).Contains("PASS independent encrypted export reader", StringComparison.Ordinal))
            throw new InvalidOperationException("Independent encrypted reader failed: " + await stderr);
    }
    public static async Task ReadInChild(string manifest)
    {
        var root = Path.GetDirectoryName(manifest) ?? throw new InvalidOperationException("Missing synthetic root.");
        CheckRoot(root);
        if (Path.GetFileName(manifest) != "reader.json" || (File.GetAttributes(manifest) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Invalid owned manifest.");
        var input = JsonSerializer.Deserialize<ReaderInput>(await File.ReadAllTextAsync(manifest))
            ?? throw new InvalidOperationException("Invalid reader metadata.");
        if (input.WriterPid == Environment.ProcessId || !Enum.IsDefined(input.Format)) throw new InvalidOperationException("Reader must be independent.");
        var store = new EncryptedPrivateFileStore(new()
        { Directory = Path.Combine(root, "objects"), KeyFile = Path.Combine(root, "keys", "active.bin"),
            KeyReference = "synthetic-export-only", MaxSizeBytes = 8_000_000 });
        var bytes = await store.ReadAsync(input.Reference, HrReportExportRules.FilePurpose(input.JobId, input.Format), default);
        if (bytes.Length != input.Length || Convert.ToHexString(SHA256.HashData(bytes)) != input.Sha256)
            throw new InvalidOperationException("Independent reader bytes differ.");
        Console.WriteLine("PASS independent encrypted export reader");
    }
    private sealed record ReaderInput(PrivateFileReference Reference, Guid JobId, ReportFormat Format, string Sha256, int Length, int WriterPid);
    private static void CheckRoot(string root)
    {
        if (Path.GetDirectoryName(root) != Parent || !Path.GetFileName(root).StartsWith("export-storage-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(Path.GetFileName(root)[15..], "N", out _)) throw new InvalidOperationException("Invalid synthetic root.");
        for (string? part = root; part is not null; part = Path.GetDirectoryName(part))
            if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Synthetic path contains a reparse point.");
    }
}
static class GrantChanges
{
    public static void Replace(this List<PermissionGrant> grants, string permission, AccessScopeKind kind, Guid target)
    {
        var at = grants.FindIndex(g => g.Permission == permission);
        grants[at] = grants[at] with { ScopeKind = kind, TargetId = target };
    }
}
