#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Policies;

// Real service, operation, domain time/pay calculation; synthetic source-reader, authorization,
// target-lock verifier and in-memory IO. No SQL, providers, file storage or salary payment.
var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
async Task Denies(Func<Task> action, string code)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code, $"Expected {code}, got {e.Code}"); }
}
void Reject(Action action, string message)
{
    try { action(); throw new Exception(message); }
    catch (ArgumentException) { checks++; }
}
Check(AttendancePayrollFinancialRules.Difference(200, 100, []) == -100, "actual 200 corrected to 100 credits 100");
Check(AttendancePayrollFinancialRules.Difference(200, 150, [-100]) == 50, "later 150 after posted 100 credit recovers 50");
Check(AttendancePayrollFinancialRules.Difference(200, 0, [-100, 50]) == -150, "later full correction credits only net actual 150");
Reject(() => AttendancePayrollFinancialRules.Difference(200, 100, [-201]), "over-credit rejected");
Reject(() => AttendancePayrollFinancialRules.Difference(200.001m, 100, []), "subcent actual rejected");
var sourceMonth = new DateOnly(2026, 9, 1);
var target = CarryForwardRules.FirstAvailableMonth(sourceMonth,
    [new(Guid.NewGuid(), new(2026, 10, 15), new(2026, 11, 1)), new(Guid.NewGuid(), new(2026, 11, 1), new(2027, 1, 1))]);
Check(target.Start == new DateOnly(2027, 1, 1), "first eligible complete later month skips partial and full locks");

var f = new FinancialFixture();
var preview = await f.Preview();
Check(preview.ActuallyDeducted == 200 && preview.CorrectedDeduction == 100 && preview.SignedAmount == -100 && preview.Direction == "credit", "actual configured calculation 200 to 100");
Check(preview.TargetMonth == f.TargetMonth && preview.TargetRevisionId == f.Source.Target.Id && !preview.PaymentInstruction && !preview.PayrollReady, "target visible before approval; no payment/readiness");
var approvalRequest = f.Approval(preview);
var approved = await f.Approve(approvalRequest);
Check(approved.Status == "approved" && f.Adjustments.Count == 1 && f.Resolutions.Count == 0, "approval is not posting");
Check((await f.Approve(approvalRequest)).Id == approved.Id && f.Adjustments.Count == 1, "same-key approval retry one effect");
await Denies(() => f.Approve(approvalRequest, "another-approval"), "financial_carry_forward_pending_resolution");
await Denies(() => f.Approve(approvalRequest with { Reason = "changed payload" }), "idempotency_key_reused");
var frozen = await f.ReadMonth();
Check(frozen.Single().Status == "approved" && frozen.Single().SignedAmount == -100 && !string.IsNullOrWhiteSpace(frozen.Single().EvidenceReference), "protected approved source reaches consumer");
var posting = f.Posting(approved);
await Denies(() => f.Post(approved, posting with { SignedAmount = -99 }), "financial_carry_forward_posting_amount_conflict");
var posted = await f.Post(approved, posting);
Check(posted.Status == "posted" && f.Resolutions.Count == 1, "separate exact posting receipt");
Check((await f.Post(approved, posting)).ResolutionId == posted.ResolutionId, "posting retry one effect");
await Denies(() => f.Post(approved, posting, "second-posting-key"), "financial_carry_forward_already_resolved");
await Denies(() => f.Cancel(approved), "financial_carry_forward_already_resolved");
Check((await f.ReadMonth()).Single().Status == "posted", "consumer sees posted and can exclude new amount");
MonthLockFinancialAdjustments.RequireRetainedMatch(frozen, await f.ReadMonth()); checks++;
await Denies(() => f.Preview(), "financial_carry_forward_already_valued");
f.ChangeDuty(2);
var laterPreview = await f.Preview();
Check(laterPreview.SignedAmount == 50 && laterPreview.ConfirmedAdjustmentNet == -100 && laterPreview.CorrectedDeduction == 150, "later correction nets actual prior credit once");
var later = await f.Approve(f.Approval(laterPreview), "later-approval-key");
await Denies(() => f.Post(later, f.Posting(later) with { ExternalReference = posting.ExternalReference }, "reused-receipt-key"), "financial_carry_forward_receipt_reused");
Check((await f.ReadMonth()).Count == 2, "all target approvals retained, no latest-only collapse");

// Review regression: an earlier exact zero must remain immutable after its successor.
var zero = new FinancialFixture(); zero.ChangeDuty(1, outsideShift: true);
var zeroApproval = await zero.Approve();
Check(zeroApproval.Status == "approved" && zeroApproval.Input.SignedAmount == 0 && zero.Resolutions.Count == 0, "verified zero has financial coverage without fake posting");
await Denies(() => zero.Post(zeroApproval, zero.Posting(zeroApproval)), "financial_carry_forward_posting_amount_conflict");
zero.ChangeDuty(4);
var successor = await zero.Approve(key: "zero-successor-key");
var zeroMembership = await zero.ReadMonth();
Check(zeroMembership.Count == 2 && zeroMembership.Single(x => x.AdjustmentId == zeroApproval.Id).SignedAmount == 0 &&
    zeroMembership.Single(x => x.AdjustmentId == successor.Id).SignedAmount == -100, "zero A then B reads both immutable approvals without stale A failure");
await Denies(() => zero.Cancel(zeroApproval), "financial_carry_forward_cancellation_superseded");
Check((await zero.History()).Count == 2 && (await zero.ReadMonth()).Count == 2, "denied predecessor cancellation cannot corrupt chain");

var cancel = new FinancialFixture(); var abandoned = await cancel.Approve();
await cancel.Cancel(abandoned);
Check((await cancel.ReadMonth()).Count == 0 && (await cancel.History()).Single().Status == "cancelled", "cancelled remains audited but has no financial coverage");
Check((await cancel.Approve(key: "after-cancellation")).Input.SignedAmount == -100, "cancelled unposted credit is not actual money");

var locked = new FinancialFixture(); var lockedApproval = await locked.Approve(); var lockEntries = await locked.ReadMonth();
locked.Locked = true; locked.FailSourceReads = true;
Check((await locked.ReadMonth()).Single().AdjustmentId == lockedApproval.Id, "locked read does not ask live source calculator");
await Denies(() => locked.Cancel(lockedApproval), "financial_carry_forward_target_locked");
locked.LockMembership = false;
await Denies(() => locked.Post(lockedApproval, locked.Posting(lockedApproval)), "financial_carry_forward_target_lock_membership_required");
locked.LockMembership = true;
await locked.Post(lockedApproval, locked.Posting(lockedApproval));
MonthLockFinancialAdjustments.RequireRetainedMatch(lockEntries, await locked.ReadMonth()); checks++;
Check(locked.MaximumDepth == 1, "posting after lock avoids nested authorization transactions");

var omission = new FinancialFixture(); await omission.Approve();
await Denies(() => omission.ReadMonth([]), "financial_carry_forward_target_population_incomplete");
var changedTarget = new FinancialFixture(); var old = await changedTarget.Preview();
changedTarget.Source = changedTarget.Source with { Target = changedTarget.Source.Target with { Id = Guid.NewGuid(), InputFingerprint = new('B', 64) } };
await Denies(() => changedTarget.Approve(changedTarget.Approval(old)), "financial_carry_forward_preview_stale");
var lockRace = new FinancialFixture(); var expected = await lockRace.Preview();
lockRace.AfterPrivateWrite = () => lockRace.Locked = true;
await Denies(() => lockRace.Approve(lockRace.Approval(expected)), "financial_carry_forward_preview_stale");
Check(lockRace.Adjustments.Count == 0 && lockRace.Receipts.Count == 0, "lock changed during private IO commits nothing");
var sourceRace = new FinancialFixture(); var before = await sourceRace.Preview();
sourceRace.AfterPrivateWrite = () => sourceRace.ChangeDuty(2);
await Denies(() => sourceRace.Approve(sourceRace.Approval(before)), "financial_carry_forward_preview_stale");
Check(sourceRace.Adjustments.Count == 0, "source changed during IO commits nothing");

foreach (var permission in FinancialFixture.Permissions)
{
    var denied = new FinancialFixture(); var saved = await denied.Approve();
    denied.Grants.RemoveAll(x => x.Permission == permission);
    await Denies(() => denied.Post(saved, denied.Posting(saved)), "access_denied");
    await Denies(() => denied.Approve(denied.Approval(saved.Input)), "access_denied");
}
var scopeLoss = new FinancialFixture(); var scopeApproval = await scopeLoss.Approve();
scopeLoss.Source = scopeLoss.Source with { OriginalScopes = [new([new(scopeLoss.Employee, SiteId: Guid.NewGuid())])] };
await Denies(() => scopeLoss.Preview(), "access_denied");
var identity = new FinancialFixture(); identity.SessionCurrent = false;
await Denies(() => identity.Preview(), "invalid_authentication");
identity.SessionCurrent = true; identity.Identity = identity.Identity with { MfaSatisfied = false };
await Denies(() => identity.Preview(), "access_denied");
identity.Identity = identity.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Denies(() => identity.Preview(), "access_denied");
var self = new FinancialFixture(); self.Identity = self.Identity with { EmployeeId = self.Employee };
await Denies(() => self.Approve(), "access_denied");
var handoff = new FinancialFixture(); handoff.Source = handoff.Source with { Posting = handoff.Source.Posting with { Status = "handoff", ConfirmsPosting = false, PostedAtUtc = null } };
await Denies(() => handoff.Preview(), "financial_carry_forward_source_conflict");
var auditFailure = new FinancialFixture(); auditFailure.FailSave = true;
await Denies(async () => await auditFailure.Approve(auditFailure.Approval(await auditFailure.PreviewBeforeFailure())), "synthetic_commit_failure");
Check(auditFailure.Adjustments.Count == 0 && auditFailure.Receipts.Count == 0, "failed journal commit rolls back business and receipt");
var storage = new FinancialFixture(); var storagePreview = await storage.Preview(); storage.FailWrites = true;
await Denies(() => storage.Approve(storage.Approval(storagePreview)), "private_storage_unavailable");
Check(storage.Adjustments.Count == 0, "failed evidence write commits nothing");
var corrupt = new FinancialFixture(); await corrupt.Approve();
corrupt.CorruptApproval();
await Denies(() => corrupt.History(), "financial_carry_forward_evidence_invalid");
var lostApproval = new FinancialFixture(); var approvedBeforeLoss = await lostApproval.Approve();
lostApproval.Adjustments.Clear();
await Denies(() => lostApproval.Approve(lostApproval.Approval(approvedBeforeLoss.Input)), "financial_carry_forward_replay_unavailable");
Check(lostApproval.Adjustments.Count == 0, "surviving approval retry receipt cannot replace missing durable approval");
var lostPosting = new FinancialFixture(); var postingApproval = await lostPosting.Approve();
var postedRequest = lostPosting.Posting(postingApproval); await lostPosting.Post(postingApproval, postedRequest);
lostPosting.Resolutions.Clear();
await Denies(() => lostPosting.Post(postingApproval, postedRequest), "financial_carry_forward_replay_unavailable");
var lostCancellation = new FinancialFixture(); var cancelledApproval = await lostCancellation.Approve();
await lostCancellation.Cancel(cancelledApproval); lostCancellation.Resolutions.Clear();
await Denies(() => lostCancellation.Cancel(cancelledApproval), "financial_carry_forward_replay_unavailable");
var replaySelf = new FinancialFixture(); var formerlyAuthorized = await replaySelf.Approve();
replaySelf.Identity = replaySelf.Identity with { EmployeeId = replaySelf.Employee };
await Denies(() => replaySelf.Approve(replaySelf.Approval(formerlyAuthorized.Input)), "access_denied");
var replayPostingSelf = new FinancialFixture(); var beforeSelf = await replayPostingSelf.Approve(); var beforeSelfPost = replayPostingSelf.Posting(beforeSelf);
await replayPostingSelf.Post(beforeSelf, beforeSelfPost); replayPostingSelf.Identity = replayPostingSelf.Identity with { EmployeeId = replayPostingSelf.Employee };
await Denies(() => replayPostingSelf.Post(beforeSelf, beforeSelfPost), "access_denied");
var roundTrip = JsonSerializer.Deserialize<AttendancePayrollFinancialSources>(JsonSerializer.Serialize(f.Source))!;
Check(roundTrip.Source.Id == f.Source.Source.Id && roundTrip.Target.Id == f.Source.Target.Id &&
    roundTrip.Source.ProtectedEvidenceReference == f.Source.Source.ProtectedEvidenceReference, "protected source-header DTO round-trip preserves exact identities");
Console.WriteLine($"Financial carry-forward: {checks} checks passed (synthetic IO; no SQL/provider acceptance).");

public sealed class FinancialFixture
{
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid();
    public DateOnly Month = new(2026, 9, 1), TargetMonth = new(2026, 10, 1);
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-financial-carry-forward");
    public bool SessionCurrent = true, Locked, LockMembership = true, FailSourceReads, FailWrites, FailSave;
    public int Depth, MaximumDepth;
    public Action? AfterPrivateWrite;
    public List<PermissionGrant> Grants = [];
    public List<AttendancePayrollFinancialAdjustment> Adjustments = [];
    public List<AttendancePayrollFinancialResolution> Resolutions = [];
    public List<AuditEvent> Audits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AttendancePayrollFinancialSources Source;
    public AttendancePayrollFinancialService Service;
    public AttendanceOperation Operation;
    public IReadOnlyList<AttendanceResourceScope> Scopes;
    public static readonly string[] Permissions = [AttendancePayrollInputPermissions.Read,
        AttendancePayrollAttestationPermissions.Read, AttendancePayrollAttestationPermissions.Attest];

    public FinancialFixture()
    {
        foreach (var permission in Permissions) Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.Site, Site));
        Scopes = [new([new(Employee, SiteId: Site)])];
        Source = MakeSource(); ChangeDuty(4);
        var current = FinancialProxy.Make<ICurrentAuthentication>((_, _) => Identity);
        var access = FinancialProxy.Make<IAccountAccessReader>((_, _) => { Inside(); return Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()); });
        var authorization = FinancialProxy.Make<IAccessManagementRepository>((method, args) => method.Name switch
        {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(this, args),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticAssignedFinance"),
            _ => throw new Exception(method.Name)
        });
        var journal = FinancialProxy.Make<ISchedulingJournalRepository>((method, args) =>
        {
            Inside();
            return method.Name switch
            {
                "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
                "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)args[0]! && x.Operation == (string)args[1]! && x.Key == (string)args[2]!)),
                "AddReceipt" => Add(Receipts, (IdempotencyReceipt)args[0]!), "AddAudit" => Add(Audits, (AuditEvent)args[0]!),
                "SaveChangesAsync" => FailSave ? throw new AuthenticationFailure("synthetic_commit_failure", 503) : Task.CompletedTask,
                _ => throw new Exception(method.Name)
            };
        });
        var files = FinancialProxy.Make<IPrivateFileStore>((method, args) =>
        {
            Inside();
            if (method.Name == "WriteAsync")
            {
                if (FailWrites) throw new AuthenticationFailure("private_storage_unavailable", 503);
                var data = ((byte[])args[0]!).ToArray(); var purpose = (string)args[1]!; var id = Guid.NewGuid().ToString("N");
                Files[id] = (purpose, data);
                var callback = AfterPrivateWrite; AfterPrivateWrite = null; callback?.Invoke();
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(data)), "synthetic", data.Length));
            }
            var reference = (PrivateFileReference)args[0]!;
            if (!Files.TryGetValue(reference.StorageKey, out var value) || value.Purpose != (string)args[1]!) throw new AuthenticationFailure("private_storage_unavailable", 503);
            return Task.FromResult(value.Bytes.ToArray());
        });
        var clock = new FinancialClock();
        Operation = new(current, access, authorization, journal,
            FinancialProxy.Make<IAuthenticationRepository>((m, _) => throw new Exception(m.Name)), clock, files);
        var producer = FinancialProxy.Make<IAttendancePayrollInputProducer>((m, _) =>
        { Inside(); return m.Name == "ResourceScopesAsync" ? Task.FromResult(Scopes) : throw new Exception(m.Name); });
        var ledger = FinancialProxy.Make<IAttendancePayrollInputLedger>((m, _) =>
        { Inside(); return m.Name == "IsLockedAsync" ? Task.FromResult(Locked) : throw new Exception(m.Name); });
        var source = FinancialProxy.Make<IAttendancePayrollFinancialSourceReader>((_, _) =>
        { Inside(); return FailSourceReads ? throw new Exception("Live sources must not be revalued") : Task.FromResult(Source); });
        var repository = FinancialProxy.Make<IAttendancePayrollFinancialRepository>((m, args) =>
        {
            Inside();
            if (m.Name == "HistoryAsync") return Task.FromResult<IReadOnlyList<AttendancePayrollFinancialHistoryRow>>(Rows().Where(x => x.Adjustment.EmployeeId == (Guid)args[0]! && x.Adjustment.SourceMonth == (DateOnly)args[1]!).ToArray());
            if (m.Name == "TargetAsync") return Task.FromResult<IReadOnlyList<AttendancePayrollFinancialHistoryRow>>(Rows().Where(x => x.Adjustment.TargetMonth == (DateOnly)args[0]!).ToArray());
            if (m.Name == "ReceiptExistsAsync") return Task.FromResult(Resolutions.Any(x => x.ExternalReceiptKey == (string)args[0]!));
            if (args[0] is AttendancePayrollFinancialAdjustment row) return Add(Adjustments, row);
            return Add(Resolutions, (AttendancePayrollFinancialResolution)args[0]!);
        });
        var locks = FinancialProxy.Make<IAttendancePayrollFinancialTargetLockReader>((_, _) => { Inside(); return Task.FromResult(LockMembership); });
        Service = new(Operation, source, repository, ledger, producer, files, access, current, journal, clock, locks);
    }

    public Task<AttendancePayrollFinancialPreview> Preview() => Service.PreviewAsync(Employee, Month, Context, default);
    public async Task<AttendancePayrollFinancialPreview> PreviewBeforeFailure()
    { FailSave = false; var result = await Preview(); FailSave = true; return result; }
    public ApproveAttendancePayrollFinancialRequest Approval(AttendancePayrollFinancialPreview p) => new(p.PreviewFingerprint, p.TargetMonth, p.PreviousAdjustmentId, "Synthetic approval");
    public async Task<AttendancePayrollFinancialResponse> Approve(ApproveAttendancePayrollFinancialRequest? request = null, string key = "first-approval-key") =>
        await Service.ApproveAsync(Employee, Month, request ?? Approval(await Preview()), key, Context, default);
    public PostAttendancePayrollFinancialRequest Posting(AttendancePayrollFinancialResponse approval) => new(approval.Input.PreviewFingerprint,
        approval.Input.SignedAmount, "PKR", Now, "SYNTHETIC", "receipt-" + approval.Id.ToString("N"), "synthetic-artifact", new('F', 64), "Synthetic exact posting assertion", "Synthetic posting");
    public Task<AttendancePayrollFinancialResponse> Post(AttendancePayrollFinancialResponse row, PostAttendancePayrollFinancialRequest p, string key = "first-posting-key") =>
        Service.PostAsync(Employee, Month, row.Id, p, key, Context, default);
    public Task<AttendancePayrollFinancialResponse> Cancel(AttendancePayrollFinancialResponse row) =>
        Service.CancelAsync(Employee, Month, row.Id, new(row.Input.PreviewFingerprint, "Synthetic cancellation"), "cancel-" + row.Id.ToString("N"), Context, default);
    public Task<IReadOnlyList<AttendancePayrollFinancialResponse>> History() => Service.HistoryAsync(Employee, Month, Context, default);
    public Task<IReadOnlyList<AttendancePayrollFinancialMonthEntry>> ReadMonth(IReadOnlyList<Guid>? employees = null) =>
        Operation.ReadAsync(AttendancePayrollAttestationPermissions.Read, "attendance.synthetic.financial-month", Employee, Context,
            (actor, ct) => Service.ReadAsync(TargetMonth, employees ?? [Employee], actor, ct), default,
            resourceScopes: (_, _) => Task.FromResult(Scopes));
    public void ChangeDuty(int hours, bool outsideShift = false)
    {
        var day = Source.OriginalCalculation.SourceManifest.Days.Single(); var snapshot = day.Snapshot!;
        var start = snapshot.TimeInput.ShiftStartsAtUtc!.Value.AddHours(outsideShift ? -hours : 0);
        var correctionId = Guid.NewGuid();
        IReadOnlyList<CarryForwardSession> sessions = [new(Guid.NewGuid(), null, start, start.AddHours(hours), Site,
            snapshot.Roster.ShiftVersionId!.Value, true, correctionId, null, null)];
        var prior = Source.Corrections.SingleOrDefault();
        var proof = new CarryForwardInterpretationSnapshot(1, Employee, day.BusinessDate, prior?.Evidence.SourceLockEventId ?? Guid.NewGuid(), prior?.Evidence.SourceSummaryId ?? Guid.NewGuid(),
            correctionId, Guid.NewGuid(), (prior?.Evidence.Revision ?? 0) + 1, prior?.AdjustmentId, TargetMonth, TargetMonth.AddMonths(1), prior?.Evidence.AfterWorkedTicks ?? 0, hours * TimeSpan.TicksPerHour,
            new('A', 64), CarryForwardEvidence.InterpretationFingerprint(sessions), Now.AddHours(-1), [], [], sessions, Scopes);
        var predecessors = prior is null ? Array.Empty<Guid>() : new[] { prior.AdjustmentId }.Concat(prior.PredecessorAdjustmentIds ?? []).ToArray();
        Source = Source with { Corrections = [new(Guid.NewGuid(), proof, predecessors)] };
    }
    public void CorruptApproval()
    {
        var key = JsonSerializer.Deserialize<PrivateFileReference>(Adjustments.Single().ProtectedEvidenceReference)!.StorageKey;
        var value = Files[key]; var json = JsonNode.Parse(value.Bytes)!;
        json["Preview"]!["SignedAmount"] = -199;
        Files[key] = (value.Purpose, JsonSerializer.SerializeToUtf8Bytes(json));
    }
    private AttendancePayrollFinancialSources MakeSource()
    {
        var rules = new AttendancePolicyRules(3, new(900, 3600, 9000),
            new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks, AttendanceEarlyExitTreatment.ActualWorkedTime,
                14400, AttendanceNonScheduledDayTreatment.NoCredit, AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
            new(3, AttendanceLateResetPeriod.CalendarMonth, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
                AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
                AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange, AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
                true, AttendanceLateDeductionKind.DayFraction, null, null, null, AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
            new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, .5m,
                AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
                AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));
        var day = Month.AddDays(1); var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
        var time = new AttendanceTimeInput(rules, start, start.AddHours(8), 480, 0, false, false, [], []);
        var result = AttendanceDailyTimeCalculation.Calculate(time);
        var policy = new AttendancePolicyResolution("effective", Guid.NewGuid(), [], rules, AttendancePolicyRulesCodec.Canonicalize(rules), new('A', 64));
        var roster = new EffectiveRosterResponse(Employee, day, "scheduled", RosterSchedule.TimeZoneId, Site,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, start, start.AddHours(8), 480, 0, 0);
        var snapshot = new AttendanceCalculationSnapshot(1, Employee, day, AttendanceDailyTimeCalculation.Version, new('B', 64), roster,
            new(Guid.NewGuid(), Guid.NewGuid(), "SITE", Site, Month, null), policy,
            new(LeaveContextStatus.Complete, [], new('C', 64), []), [], [], [], [], time, 0);
        var compensation = new AttendancePayrollCompensation(Guid.NewGuid(), 6000, 6000, "PKR", WageBasis.Monthly, Month, null, new('D', 64), []);
        var ids = new Guid?[] { policy.PolicyVersionId, roster.AssignmentId, roster.ShiftId, roster.ShiftVersionId, Site, snapshot.Employment.AssignmentId }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order().ToArray();
        var input = new AttendancePayrollInputDay(day, "available", policy.PolicyVersionId, rules, time, result, compensation, new('E', 64), ids);
        var money = AttendancePayrollInputRules.Calculate(Month, [input]);
        var manifest = new AttendancePayrollInputManifest(1, Employee, Month, TargetMonth, day, day, AttendancePayrollInputRules.Version,
            new('F', 64), Scopes, [new(day, "available", input.SourceFingerprint, snapshot, result, compensation, policy)]);
        var calculation = new AttendancePayrollInputCalculation(Employee, Month, TargetMonth, day, day, money.State, new('A', 64), money.Lines, money.Issues, manifest);
        var row = new AttendancePayrollFinancialRevision(Guid.NewGuid(), Employee, Month, 1, new('B', 64), "synthetic-source-reference", Now.AddDays(-10));
        var target = new AttendancePayrollFinancialRevision(Guid.NewGuid(), Employee, TargetMonth, 1, new('C', 64), "synthetic-target-reference", Now.AddDays(-1));
        var posting = new AttendancePayrollAttestationResponse(Guid.NewGuid(), Employee, Month, row.Id, 1, row.InputFingerprint,
            Guid.NewGuid(), "posted", true, Now.AddDays(-5), Now.AddDays(-5), Identity.AccountId, Now.AddDays(-5),
            money.Lines.Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency)).ToArray(),
            "SYNTHETIC", "original-deduction", "synthetic-source-receipt", new('D', 64), "Synthetic posting", "Synthetic reason");
        return new(row, calculation, posting, new('E', 64), target, [], Scopes);
    }
    private IReadOnlyList<AttendancePayrollFinancialHistoryRow> Rows() => Adjustments.OrderBy(x => x.Revision)
        .Select(x => new AttendancePayrollFinancialHistoryRow(x, Resolutions.SingleOrDefault(r => r.AdjustmentId == x.Id))).ToArray();
    private void Inside() { if (Depth != 1) throw new Exception("Expected one authorized transaction"); }
    private static object? Add<T>(List<T> rows, T row) { rows.Add(row); return null; }
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (Depth != 0) throw new Exception("Nested authorization transaction");
        var approvals = Adjustments.ToArray(); var resolutions = Resolutions.ToArray(); var receipts = Receipts.ToArray(); var audits = Audits.ToArray();
        Depth++; MaximumDepth = Math.Max(Depth, MaximumDepth);
        try { return await action(ct); }
        catch { Restore(Adjustments, approvals); Restore(Resolutions, resolutions); Restore(Receipts, receipts); Restore(Audits, audits); throw; }
        finally { Depth--; }
    }
    private static void Restore<T>(List<T> target, T[] rows) { target.Clear(); target.AddRange(rows); }
}
public class FinancialProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var instance = Create<T, FinancialProxy>(); ((FinancialProxy)(object)instance).Handler = handler; return instance; }
}
public sealed class FinancialClock : TimeProvider { public override DateTimeOffset GetUtcNow() => FinancialFixture.Now; }
