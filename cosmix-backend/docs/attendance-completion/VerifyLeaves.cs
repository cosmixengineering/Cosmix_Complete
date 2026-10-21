#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Fails(Func<Task> call, string code)
{ try { await call(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; } throw new Exception("Expected " + code); }
var f = new LeaveFixture(); var input = f.Input();
var received = await f.Service.SubmitAsync(input, "leave-submit-001", f.Context, default);
Check(received.EmployeeId == f.Employee && received.RequestedMinutes == 480 && received.Treatment == LeavePayTreatment.PendingReview && received.PolicyVersionId is null, "Self intake computes exact minutes without invented policy");
Check(f.Requests.Single().Days.Single().ShiftVersionId == f.ShiftVersion, "Exact historical shift version retained");
Check((await f.Service.SubmitAsync(input, "leave-submit-001", f.Context, default)).Id == received.Id && f.Requests.Count == 1, "Retry replays only one submission");
await Fails(() => f.Service.SubmitAsync(input with { Reason = "Changed payload" }, "leave-submit-001", f.Context, default), "idempotency_key_reused");
Check((await f.Reader.ReadAsync(f.Employee, f.Date, default)).Status == LeaveContextStatus.Pending, "Pending leave is explicit context, not zero");
f.Actor = f.Actor with { EmployeeId = Guid.NewGuid() };
await Fails(() => f.Service.DetailAsync(received.Id, true, f.Context, default), "leave_not_found");
f.AsAdmin(); f.FixedAdmin = false;
await Fails(() => f.Service.QueueAsync(new(f.Date, f.Date), f.Context, default), "access_denied");
f.FixedAdmin = true; f.Actor = f.Actor with { MfaSatisfied = false };
await Fails(() => f.Service.QueueAsync(new(f.Date, f.Date), f.Context, default), "access_denied");
f.Actor = f.Actor with { MfaSatisfied = true }; f.SessionCurrent = false;
await Fails(() => f.Service.QueueAsync(new(f.Date, f.Date), f.Context, default), "invalid_authentication");
f.SessionCurrent = true; f.Actor = f.Actor with { EmployeeId = f.Employee };
await Fails(() => f.Service.DecideAsync(received.Id, new(ApprovalState.Returned, received.ApprovalVersion, "Need revised dates"), "leave-return-self", f.Context, default), "access_denied");
f.AsAdmin();
await Fails(() => f.Service.DecideAsync(received.Id, new(ApprovalState.Returned, Convert.ToBase64String(BitConverter.GetBytes(500L)), "Stale"), "leave-stale-001", f.Context, default), "leave_version_conflict");
var returned = await f.Service.DecideAsync(received.Id, new(ApprovalState.Returned, received.ApprovalVersion, "Please revise"), "leave-return-001", f.Context, default);
Check(returned.State == ApprovalState.Returned && f.Decisions.Count == 1, "Admin return preserves submitted evidence");
f.AsEmployee();
var revised = await f.Service.SubmitAsync(input with { PreviousRequestId = received.Id, Reason = "Revised response" }, "leave-revised-001", f.Context, default);
Check(f.Requests.Count == 2 && f.Requests[0].Reason == input.Reason && revised.PreviousRequestId == received.Id &&
    (await f.Service.DetailAsync(received.Id, true, f.Context, default)).SupersededByRequestId == revised.Id, "Resubmission is a new immutable request linked to the returned original");
await Fails(() => f.Service.SubmitAsync(input with { PreviousRequestId = received.Id }, "leave-revised-002", f.Context, default), "leave_not_awaiting_resubmission");
f.AsAdmin();
await Fails(() => f.Service.DecideAsync(revised.Id, new(ApprovalState.Approved, revised.ApprovalVersion, "Missing treatment"), "leave-no-policy-001", f.Context, default), "leave_treatment_requires_explicit_policy");
var approved = await f.Service.DecideAsync(revised.Id, new(ApprovalState.Approved, revised.ApprovalVersion, "Approved explicit paid leave", LeavePayTreatment.Paid, f.Policy.Id), "leave-approve-001", f.Context, default);
Check(approved.State == ApprovalState.Approved && f.Ledger.Single().MinutesDelta == -480, "Approval appends exact utilization");
Check((await f.Service.DecideAsync(revised.Id, new(ApprovalState.Approved, revised.ApprovalVersion, "Approved explicit paid leave", LeavePayTreatment.Paid, f.Policy.Id), "leave-approve-001", f.Context, default)).Id == approved.Id &&
    f.Ledger.Count == 1 && f.Decisions.Count == 2, "Exact Admin decision retry adds no decision or ledger debit");
var effect = await f.Reader.ReadAsync(f.Employee, f.Date, default);
Check(effect.Status == LeaveContextStatus.Complete && effect.Effects.Single().Minutes == 480 && effect.Effects[0].ShiftVersionId == f.ShiftVersion &&
    effect.Effects[0].PolicyVersionId == f.Policy.Id, "Reader verifies protected decision, policy, holidays and pinned exact interval");
var originalCategory = f.Category; f.Category = Guid.NewGuid();
Check((await f.Reader.ReadAsync(f.Employee, f.Date, default)).Status == LeaveContextStatus.Conflict, "Future category transfer invalidates old approval applicability even with unchanged shift");
f.Category = originalCategory;
f.FixedAdmin = false;
await Fails(() => f.Service.DecideAsync(revised.Id, new(ApprovalState.Approved, revised.ApprovalVersion, "Approved explicit paid leave", LeavePayTreatment.Paid, f.Policy.Id), "leave-approve-001", f.Context, default), "access_denied");
f.FixedAdmin = true; f.AsEmployee();
var cancellation = await f.Service.RequestCancellationAsync(revised.Id, new(approved.ApprovalVersion, "Plans changed"), "leave-cancel-001", f.Context, default);
Check((await f.Reader.ReadAsync(f.Employee, f.Date, default)).Status == LeaveContextStatus.Pending, "Pending cancellation does not erase existing evidence");
f.AsAdmin(); var c = cancellation.Cancellations.Single();
var cancelled = await f.Service.DecideCancellationAsync(revised.Id, c.Id, new(ApprovalState.Approved, c.ApprovalVersion, "Cancellation accepted"), "leave-cancel-approve", f.Context, default);
Check(cancelled.FinancialStatus == "cancelled_no_pay_effect" && f.Ledger.Count == 2 && f.Ledger.Sum(x => x.MinutesDelta) == 0 && f.Ledger[1].ReversesEntryId == f.Ledger[0].Id,
    "Cancellation appends reversal instead of deleting usage");
Check((await f.Reader.ReadAsync(f.Employee, f.Date, default)).Effects.Count == 0 && f.Requests.Count == 2 && f.Decisions.Count == 3, "Cancelled leave retains complete application and decision history");
Check(f.Audits.Any(x => x.ProtectedChangesReference is not null) && f.Receipts.Count == 6, "Operation boundary retains protected audit and exact retry receipts");
var noSchedule = new LeaveFixture { ScheduleAvailable = false };
await Fails(() => noSchedule.Service.SubmitAsync(noSchedule.Input(), "leave-no-schedule", noSchedule.Context, default), "leave_schedule_unavailable_or_changed");
var locked = new LeaveFixture { Locked = true };
await Fails(() => locked.Service.SubmitAsync(locked.Input(), "leave-locked-001", locked.Context, default), "leave_period_locked");
var invalidInterval = new LeaveFixture();
await Fails(() => invalidInterval.Service.SubmitAsync(invalidInterval.Input() with { Days = [new(invalidInterval.Date, invalidInterval.Start.AddHours(-1), invalidInterval.Start.AddHours(8))] }, "leave-bounds-001", invalidInterval.Context, default), "leave_schedule_unavailable_or_changed");
var scanner = new LeaveFixture { ScanState = DocumentScanStatus.Pending };
await Fails(() => scanner.Service.SubmitAsync(scanner.Input() with { Attachments = [new(LeaveEvidenceKind.Other, "synthetic.pdf", "application/pdf", "%PDF-synthetic"u8.ToArray())] }, "leave-scanner-001", scanner.Context, default), "leave_evidence_scanner_unavailable");
Check(scanner.Requests.Count == 0, "Unavailable scanner persists no leave business record");
var attachment = new LeaveAttachmentRequest(LeaveEvidenceKind.Other, "synthetic.pdf", "application/pdf", "%PDF-synthetic"u8.ToArray());
var docs = new LeaveFixture();
var withEvidence = await docs.Service.SubmitAsync(docs.Input() with { Attachments = [attachment] }, "leave-evidence-001", docs.Context, default);
Check((await docs.Service.EvidenceAsync(withEvidence.Id, LeaveEvidenceKind.Other, true, docs.Context, default)).Content.SequenceEqual(attachment.Content), "Scanned private attachment can be downloaded by its owner");
docs.Actor = docs.Actor with { EmployeeId = Guid.NewGuid() };
await Fails(() => docs.Service.EvidenceAsync(withEvidence.Id, LeaveEvidenceKind.Other, true, docs.Context, default), "leave_not_found");
docs.AsEmployee(); var contentKey = docs.Files.Keys.Single(k => docs.Files[k].Purpose.EndsWith(":Other", StringComparison.Ordinal));
docs.Files[contentKey].Bytes[0] ^= 1;
await Fails(() => docs.Service.EvidenceAsync(withEvidence.Id, LeaveEvidenceKind.Other, true, docs.Context, default), "leave_evidence_unavailable");
var badScan = new LeaveFixture { ScanState = DocumentScanStatus.Rejected };
await Fails(() => badScan.Service.SubmitAsync(badScan.Input() with { Attachments = [attachment] }, "leave-infected-001", badScan.Context, default), "leave_evidence_rejected");
async Task<LeaveResponse> Submit(LeaveFixture fixture, string key)
{ fixture.AsEmployee(); return await fixture.Service.SubmitAsync(fixture.Input(), key, fixture.Context, default); }
Task<LeaveResponse> Approve(LeaveFixture fixture, LeaveResponse row, string key)
{ fixture.AsAdmin(); return fixture.Service.DecideAsync(row.Id, new(ApprovalState.Approved, row.ApprovalVersion, "Synthetic paid approval", LeavePayTreatment.Paid, fixture.Policy.Id), key, fixture.Context, default); }
var probation = new LeaveFixture(); probation.JoinedOn = probation.Date.AddMonths(-4).AddDays(1); var pLeave = await Submit(probation, "leave-probation-001");
await Fails(() => Approve(probation, pLeave, "leave-probation-review"), "leave_service_eligibility_not_met");
var boundary = new LeaveFixture(); boundary.JoinedOn = boundary.Date.AddMonths(-4); var bLeave = await Submit(boundary, "leave-tenure-boundary");
Check((await Approve(boundary, bLeave, "leave-boundary-review")).State == ApprovalState.Approved, "Exact four-month service boundary qualifies");
var holiday = new LeaveFixture(); holiday.Holidays = [holiday.Date]; var hLeave = await Submit(holiday, "leave-holiday-001");
await Fails(() => Approve(holiday, hLeave, "leave-holiday-review"), "exclude_pinned_holidays_from_leave_request");
var exhausted = new LeaveFixture();
exhausted.Ledger.Add(new(exhausted.Employee, exhausted.Type.Id, new(2026, 7, 1), new(2027, 7, 1), LeaveLedgerEntryType.Utilization,
    -5760, Guid.NewGuid(), null, exhausted.Policy.Id, "Synthetic prior usage", "prior-usage-001", "synthetic-admin", new LeaveClock().GetUtcNow()));
var qLeave = await Submit(exhausted, "leave-quota-001");
await Fails(() => Approve(exhausted, qLeave, "leave-quota-review"), "leave_balance_insufficient_or_unavailable");
Check(exhausted.Ledger.Count == 1, "Quota rejection adds no usage");
var changedRoster = new LeaveFixture(); var rLeave = await Submit(changedRoster, "leave-roster-001"); changedRoster.ShiftVersion = Guid.NewGuid();
await Fails(() => Approve(changedRoster, rLeave, "leave-roster-review"), "leave_schedule_unavailable_or_changed");
var laterLock = new LeaveFixture();
Check((await laterLock.Service.PreviewAsync(new(laterLock.Type.Id, laterLock.Policy.Id, LeavePayTreatment.Paid, laterLock.Input().Days), laterLock.Context, default)).Status == "available",
    "Verified preview remains only current context before actual submission");
var lLeave = await Submit(laterLock, "leave-lock-later"); laterLock.Locked = true;
await Fails(() => Approve(laterLock, lLeave, "leave-lock-review"), "leave_period_locked");
var lockedAfterPreview = new LeaveFixture();
await lockedAfterPreview.Service.PreviewAsync(new(lockedAfterPreview.Type.Id, lockedAfterPreview.Policy.Id, LeavePayTreatment.Paid, lockedAfterPreview.Input().Days), lockedAfterPreview.Context, default);
lockedAfterPreview.Locked = true;
await Fails(() => Submit(lockedAfterPreview, "leave-submit-after-preview"), "leave_period_locked");
var cancelRejected = new LeaveFixture(); var crInput = await Submit(cancelRejected, "leave-cancel-reject"); var crApproved = await Approve(cancelRejected, crInput, "leave-cr-approved");
cancelRejected.AsEmployee(); var crRequest = await cancelRejected.Service.RequestCancellationAsync(crApproved.Id, new(crApproved.ApprovalVersion, "Cancel requested"), "leave-cr-request", cancelRejected.Context, default);
cancelRejected.AsAdmin(); var cr = crRequest.Cancellations.Single();
await cancelRejected.Service.DecideCancellationAsync(crApproved.Id, cr.Id, new(ApprovalState.Rejected, cr.ApprovalVersion, "Cancellation declined"), "leave-cr-rejected", cancelRejected.Context, default);
Check(cancelRejected.Ledger.Count == 1 && (await cancelRejected.Reader.ReadAsync(cancelRejected.Employee, cancelRejected.Date, default)).Effects.Count == 1, "Rejected cancellation retains approved paid leave and usage");
cancelRejected.AsEmployee(); var crSecond = await cancelRejected.Service.RequestCancellationAsync(crApproved.Id, new(crApproved.ApprovalVersion, "New cancellation reason"), "leave-cr-second", cancelRejected.Context, default);
cancelRejected.AsAdmin(); cancelRejected.Locked = true; var cr2 = crSecond.Cancellations.Last();
await Fails(() => cancelRejected.Service.DecideCancellationAsync(crApproved.Id, cr2.Id, new(ApprovalState.Approved, cr2.ApprovalVersion, "Now locked"), "leave-cr-locked", cancelRejected.Context, default), "leave_period_locked");
Check(cancelRejected.Ledger.Count == 1, "Locked cancellation appends no reversal");
var reviewPolicies = new LeaveFixture(); var rpLeave = await Submit(reviewPolicies, "leave-policy-selection");
reviewPolicies.AsAdmin(); reviewPolicies.Grants.RemoveAll(x => x.Permission == LeavePermissions.Configure);
var selectable = await reviewPolicies.Service.ReviewPoliciesAsync(rpLeave.Id, reviewPolicies.Context, default);
Check(selectable.Count == 1 && selectable[0].VersionId == reviewPolicies.Policy.Id && selectable[0].Settings.StandardDayMinutes == 480,
    "Review-only fixed Admin discovers explicit settings without configure permission");
Check(reviewPolicies.Requests.Count == 1 && reviewPolicies.Requests[0].PayTreatment == LeavePayTreatment.PendingReview && reviewPolicies.Ledger.Count == 0 && reviewPolicies.PublishedPolicies.Count == 0,
    "Reviewer policy discovery does not change requests, balances or settings");
await Fails(() => reviewPolicies.Settings.PublishAsync(new("NO-CONFIGURE", "No configure permission", reviewPolicies.Date, reviewPolicies.Date.AddDays(30), reviewPolicies.Rules, "Forbidden publish"),
    "leave-review-no-config", reviewPolicies.Context, default), "access_denied");
reviewPolicies.FixedAdmin = false;
await Fails(() => reviewPolicies.Service.ReviewPoliciesAsync(rpLeave.Id, reviewPolicies.Context, default), "access_denied");
reviewPolicies.FixedAdmin = true; reviewPolicies.Actor = reviewPolicies.Actor with { MfaSatisfied = false };
await Fails(() => reviewPolicies.Service.ReviewPoliciesAsync(rpLeave.Id, reviewPolicies.Context, default), "access_denied");
reviewPolicies.AsAdmin(); reviewPolicies.Grants.RemoveAll(x => x.Permission == LeavePermissions.Review);
await Fails(() => reviewPolicies.Service.ReviewPoliciesAsync(rpLeave.Id, reviewPolicies.Context, default), "access_denied");
var outsideCategory = new LeaveFixture(); var ocLeave = await Submit(outsideCategory, "leave-category-selection");
outsideCategory.AsAdmin(); outsideCategory.Category = Guid.NewGuid();
Check((await outsideCategory.Service.ReviewPoliciesAsync(ocLeave.Id, outsideCategory.Context, default)).Count == 0, "Policy selection excludes ineligible current historical category");
var outsideDates = new LeaveFixture(); var odLeave = await Submit(outsideDates, "leave-dates-selection");
outsideDates.AsAdmin(); typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.EffectiveFrom))!.SetValue(outsideDates.Policy, outsideDates.Date.AddDays(1));
Check((await outsideDates.Service.ReviewPoliciesAsync(odLeave.Id, outsideDates.Context, default)).Count == 0, "Policy selection excludes versions not covering requested dates");
var boundedPolicies = new LeaveFixture(); var bpLeave = await Submit(boundedPolicies, "leave-bounded-selection");
boundedPolicies.AsAdmin(); boundedPolicies.PublishedPolicies.AddRange(Enumerable.Repeat(boundedPolicies.Policy, 50));
await Fails(() => boundedPolicies.Service.ReviewPoliciesAsync(bpLeave.Id, boundedPolicies.Context, default), "leave_policy_selection_limit");
var config = new LeaveFixture(); config.AsAdmin(); config.Role = "ExplicitHR"; config.FixedAdmin = false;
await Fails(() => config.Settings.PublishAsync(new("LEAVE-EXPLICIT", "Explicit leave", config.Date, config.Date.AddDays(30), config.Rules, "Explicit settings"),
    "leave-policy-001", config.Context, default), "access_denied");
Check(config.PublishedPolicies.Count == 0, "Delegated configure permission alone cannot publish Admin leave settings");
config.AsAdmin(); config.FixedAdmin = true;
var published = await config.Settings.PublishAsync(new("LEAVE-EXPLICIT", "Explicit HR leave", config.Date, config.Date.AddDays(30), config.Rules, "Explicit settings"), "leave-policy-001", config.Context, default);
Check(published.Authority == "DirectHr" && config.PublishedPolicies.Single().ApprovalRequestId is null, "Authorized fixed Admin publishes directly without a fabricated second approval");
await Fails(() => config.Settings.PublishAsync(new("BAD", "Incomplete", config.Date, config.Date.AddDays(30), config.Rules with { AnnualDays = 0 }, "Invalid settings"), "leave-policy-bad", config.Context, default), "invalid_leave_settings");
Check(config.PublishedPolicies.Count == 1, "Invalid leave settings preserve prior publication");
foreach (var invalid in new[] { JsonSerializer.Serialize(config.Rules).Replace("\"AnnualDays\":18", "\"AnnualDays\":\"18\""),
    JsonSerializer.Serialize(config.Rules).Replace("\"AnnualDays\":18", "\"AnnualDays\":18,\"AnnualDays\":18"),
    JsonSerializer.Serialize(config.Rules).Replace("\"AnnualDays\":18", "\"annualDays\":18") })
    await Fails(() => { LeavePolicySettings.Parse(invalid); return Task.CompletedTask; }, "leave_policy_unavailable");
Check(LeavePolicySettings.Year(new(2026, 6, 30)).To == new DateOnly(2026, 7, 1) && LeavePolicySettings.Year(new(2026, 7, 1)).From == new DateOnly(2026, 7, 1), "Fiscal year boundaries are exclusive July-June");
Check(LeaveTreatmentRules.ExceedsDays(new(5760, 5760, [new(5760, 480, LeaveKind.Casual)]), LeaveKind.Casual, 1, 540, 18, 12), "480-to-540 day change cannot grant a thirteenth casual day");
Check(LeaveTreatmentRules.ExceedsDays(new(6480, 6480, [new(6480, 540, LeaveKind.Casual)]), LeaveKind.Casual, 1, 480, 18, 12), "540-to-480 day change preserves twelve-day limit");
Check(!LeaveTreatmentRules.ExceedsDays(new(5280, 5280, [new(5760, 480, LeaveKind.Casual), new(-480, 480, LeaveKind.Casual)]), LeaveKind.Casual, 540, 540, 18, 12), "Cancellation restores exactly one original day across policy duration changes");
Check(LeaveTreatmentRules.ExceedsDays(new(2, 2, [new(1, 3, LeaveKind.Casual), new(1, 3, LeaveKind.Casual)]), LeaveKind.Casual, 2, 3, 18, 1), "Exact rational arithmetic rejects 4/3 days without decimal rounding");
Console.WriteLine($"PASS: {checks} leave workflow/authority/evidence/settings checks; synthetic adapters only, no database/network writes.");

public class LeaveProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? m, object?[]? a) => Handler(m!, a ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> h) where T : class
    { var proxy = Create<T, LeaveProxy>(); ((LeaveProxy)(object)proxy).Handler = h; return proxy; }
}
public sealed class LeaveClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 12, 5, 0, 0, TimeSpan.Zero); }
public sealed class LeaveFixture
{
    public Guid Employee = Guid.NewGuid(), Requester = Guid.NewGuid(), ShiftVersion = Guid.NewGuid(), Category = Guid.NewGuid();
    public DateOnly Date = new(2026, 10, 14);
    public DateOnly? JoinedOn = new DateOnly(2025, 1, 1);
    public IReadOnlyList<DateOnly> Holidays = [];
    public DateTimeOffset Start => new(2026, 10, 14, 4, 0, 0, TimeSpan.Zero);
    public AuthenticatedAccount Actor = null!;
    public string Role = "Employee";
    public bool FixedAdmin = true, SessionCurrent = true, Locked, ScheduleAvailable = true;
    public DocumentScanStatus ScanState = DocumentScanStatus.Clean;
    public List<LeaveRequest> Requests = []; public List<ApprovalRequest> Approvals = []; public List<ApprovalDecision> Decisions = [];
    public List<LeaveCancellationRequest> Cancellations = []; public List<LeaveLedgerEntry> Ledger = [];
    public List<PolicyVersion> PublishedPolicies = []; public List<IdempotencyReceipt> Receipts = []; public List<AuditEvent> Audits = [];
    public List<PermissionGrant> Grants = []; public Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public LeaveType Type = new("CASUAL", "Casual", false, true, true, LeaveKind.Casual);
    public LeavePolicySettings Rules; public PolicyVersion Policy;
    public LeaveService Service; public LeaveSettingsService Settings; public LeaveAttendanceContextReader Reader;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-leave");
    public LeaveFixture()
    {
        AsEmployee();
        Rules = new(18, 6, 12, 4, 480, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Casual]);
        var p = new Policy("SYNTHETIC-LEAVE", "Synthetic leave", PolicyCategory.Leave);
        Policy = new(p.Id, 1, new(2026, 7, 1), new(2027, 7, 1), LeavePolicySettings.SchemaVersion, JsonSerializer.Serialize(Rules), null, null,
            Guid.NewGuid().ToString(), new LeaveClock().GetUtcNow(), PolicyAuthority.DirectHr);
        typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.Policy))!.SetValue(Policy, p);
        foreach (var permission in LeavePermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = LeaveProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = LeaveProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = LeaveProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(Role),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin && Role is "ADMIN" or "SUPERADMIN"), _ => throw new Exception(m.Name) });
        var journal = LeaveProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent), "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)a[0]!), "AddAudit" => AddAudit((AuditEvent)a[0]!), "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var files = LeaveProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") { var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = (bytes, (string)a[1]!);
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
            var file = Files[((PrivateFileReference)a[0]!).StorageKey]; if (file.Purpose != (string)a[1]!) throw new Exception("Private purpose mismatch"); return Task.FromResult(file.Bytes);
        });
        var operation = new AttendanceOperation(current, access, authorization, journal,
            LeaveProxy.Make<IAuthenticationRepository>((m, _) => throw new Exception("Unexpected authentication IO " + m.Name)), new LeaveClock(), files);
        var repository = LeaveProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "TypeAsync" => Task.FromResult<LeaveType?>((Guid)a[0]! == Type.Id ? Type : null), "TypesAsync" => Task.FromResult<IReadOnlyList<LeaveType>>([Type]),
            "EmployeeExistsAsync" => Task.FromResult(true), "SubmissionExistsAsync" => Task.FromResult(Requests.Any(x => x.EmployeeId == (Guid)a[0]! && x.SubmissionKey == (string)a[1]!)),
            "FindAsync" => Task.FromResult(Find((Guid)a[0]!, (Guid?)a[1])), "ForDateAsync" => Task.FromResult<IReadOnlyList<LeaveRow>>(Rows((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly)a[1]!)),
            "OverlappingAsync" => Task.FromResult<IReadOnlyList<LeaveRow>>(Rows((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly)a[2]!)),
            "AnyLockedAsync" => Task.FromResult(Locked), "ExpectVersion" => null, "SaveChangesAsync" => Save(), "Add" => Add(a),
            "PolicyAsync" => Task.FromResult<LeavePolicyEvidence?>(new(Policy, Category, JoinedOn, true, Holidays, new(-Ledger.Sum(x => x.MinutesDelta), -Ledger.Sum(x => x.MinutesDelta),
                Ledger.Select(x => new LeaveUsageUnit(-x.MinutesDelta, Rules.StandardDayMinutes, Type.Kind)).ToArray()))),
            "PolicyVersionAsync" => Task.FromResult<PolicyVersion?>((Guid)a[0]! == Policy.Id ? Policy : null), "HolidaysAsync" => Task.FromResult<IReadOnlyList<DateOnly>>([]),
            "LedgerAsync" => Task.FromResult<IReadOnlyList<LeaveLedgerEntry>>(Ledger.Where(x => x.LeaveRequestId == (Guid)a[0]!).ToArray()),
            "SettingsReferencesValidAsync" => Task.FromResult(true), "PolicyCodeExistsAsync" => Task.FromResult(false), "TypeCodeExistsAsync" => Task.FromResult(false),
            "PolicyVersionsAsync" => Task.FromResult<IReadOnlyList<PolicyVersion>>([Policy, ..PublishedPolicies]), _ => throw new Exception("Unexpected repository " + m.Name) });
        var scanner = LeaveProxy.Make<IEmployeeFileScanner>((_, _) => Task.FromResult(new FileScanResult(ScanState, ScanState == DocumentScanStatus.Clean ? "synthetic-scanner" : "")));
        var rosters = LeaveProxy.Make<IIndividualRosterResolver>((_, a) => Task.FromResult(new EffectiveRosterResponse((Guid)a[0]!, (DateOnly)a[1]!, ScheduleAvailable ? "scheduled" : "unassigned", "Asia/Karachi",
            ShiftVersionId: ScheduleAvailable ? ShiftVersion : null, StartsAt: ScheduleAvailable ? Start : null, EndsAt: ScheduleAvailable ? Start.AddHours(8) : null)));
        var evidence = new LeaveEvidence(operation, files, scanner); var provenance = new LeaveProvenance(operation, files);
        Service = new(operation, repository, evidence, provenance, new(access, authorization), rosters, files);
        Settings = new(operation, repository); Reader = new(repository, provenance, operation);
    }
    public void AsEmployee() { Actor = new(Requester, Employee, Guid.NewGuid(), 1, SessionClient.Mobile, true); Role = "Employee"; }
    public void AsAdmin() { Actor = new(Guid.Parse("d38e9b9a-53cd-4cb8-a971-c41d6d71b943"), Guid.Parse("a6a8ff3f-b2c2-447f-aafe-a1ef402d1229"), Guid.NewGuid(), 1, SessionClient.Web, true); Role = Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Actor.AccountId, new LeaveClock().GetUtcNow()).Code; }
    public SubmitLeaveRequest Input() => new(Type.Id, [new(Date, Start, Start.AddHours(8))], "Synthetic personal leave");
    private LeaveRow? Find(Guid id, Guid? employee) { var r = Requests.SingleOrDefault(x => x.Id == id && (!employee.HasValue || x.EmployeeId == employee)); return r is null ? null : Row(r); }
    private LeaveRow Row(LeaveRequest r) => new(r, Type, r.ApprovalRequest, Decisions.Where(x => x.ApprovalRequestId == r.ApprovalRequestId).ToArray(),
        Cancellations.Where(c => c.LeaveRequestId == r.Id).Select(c => new LeaveCancellationRow(c, c.ApprovalRequest, Decisions.Where(x => x.ApprovalRequestId == c.ApprovalRequestId).ToArray())).ToArray(),
        Requests.SingleOrDefault(x => x.PreviousRequestId == r.Id)?.Id);
    private LeaveRow[] Rows(Guid employee, DateOnly from, DateOnly to) => Requests.Where(x => x.EmployeeId == employee && x.StartDate <= to && x.EndDate >= from).Select(Row).ToArray();
    private object? Add(object?[] a) { switch (a[0]) {
        case LeaveRequest r: Requests.Add(r); Approvals.Add((ApprovalRequest)a[2]!); break;
        case LeaveCancellationRequest c: Cancellations.Add(c); Approvals.Add((ApprovalRequest)a[1]!); break;
        case ApprovalDecision d: Decisions.Add(d); break; case LeaveLedgerEntry e: Ledger.Add(e); break;
        case Policy p: var v = (PolicyVersion)a[1]!; typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.Policy))!.SetValue(v, p); PublishedPolicies.Add(v); break;
        case LeaveType: break; default: throw new Exception("Unexpected added row"); } return null; }
    private Task Save() { foreach (var a in Approvals) typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.RowVersion))!.SetValue(a, BitConverter.GetBytes(1L + Decisions.Count(x => x.ApprovalRequestId == a.Id))); return Task.CompletedTask; }
    private object? AddReceipt(IdempotencyReceipt r) { Receipts.Add(r); return null; }
    private object? AddAudit(AuditEvent a) { Audits.Add(a); return null; }
}
