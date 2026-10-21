using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.PayrollHandoff;

// Actual service/operation; explicit synthetic source, authorization transaction and storage.
// No SQL, real legal clearance, external posting verification or payment is claimed.
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
async Task Denies(Func<Task> action, string code) {
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code, $"Expected {code}; got {e.Code}"); }
}
var f = new PostingFixture();
var posted = await f.Post("posting-first-key");
Check(posted.Status == "posted" && !posted.PaymentInstruction && posted.VerificationMode == "manual_authorized_attestation", "explicit manual provenance");
Check(posted.Amount == 100 && f.Ledger.Single().Amount == 100 && posted.HandoffLineId == f.Line.Id, "exact line produces one ledger entry");
Check(posted.ActorUserId == f.Identity.AccountId && posted.PostedAtUtc == f.Request.PostedAtUtc && posted.PayrollMonth == f.Start, "exact actor/source month/posting time");
Check(f.Agreement.ApprovedPrincipal - f.Ledger.Sum(x => x.Amount) == 900, "principal reduced exactly once");
Check(f.Ledger.Single().SourceReference == $"payroll-line:{f.Line.Id:N}" && f.Receipts.Count == 1 && f.Audits.Count == 1, "durable line binding plus atomic receipt/audit");
Check(f.Files.Values.Any(x => x.Purpose.EndsWith(":audit") && System.Text.Encoding.UTF8.GetString(x.Data).Contains("SYNTHETIC-POSTED-RECEIPT")), "protected audit retains exact external evidence");
Check((await f.Post("posting-first-key")).LedgerEntryId == posted.LedgerEntryId && f.Ledger.Count == 1, "same-key retry has one effect");
f.Receipts.Clear();
Check((await f.Read()).LedgerEntryId == posted.LedgerEntryId, "retained posting read does not require a live HTTP retry receipt");
await Denies(() => f.Post("posting-first-key"), "payroll_posting_receipt_reused");
var retry = new PostingFixture(); await retry.Post("posting-first-key");
await Denies(() => retry.Post("posting-first-key", retry.Request with { Reason = "Changed reason" }), "idempotency_key_reused");
await Denies(() => f.Post("posting-second-key"), "payroll_posting_receipt_reused");
await Denies(() => f.Post("posting-third-key", f.Request with { ExternalLineReference = "different-line" }), "payroll_loan_line_already_posted");
foreach (var permission in new[] { AttendancePayrollAttestationPermissions.Attest, PayrollHandoffPermissions.Read, PayrollHandoffPermissions.Process, AdvanceLoanPermissions.Repay }) {
    var t = new PostingFixture(); await t.Post("posting-revoke-key");
    t.Grants.RemoveAll(x => x.Permission == permission);
    await Denies(() => t.Post("posting-revoke-key"), "access_denied");
}
var denied = new PostingFixture(); denied.SessionCurrent = false;
await Denies(() => denied.Post(), "invalid_authentication");
denied.SessionCurrent = true; denied.Identity = denied.Identity with { MfaSatisfied = false };
await Denies(() => denied.Post(), "access_denied");
denied.Identity = denied.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Denies(() => denied.Post(), "access_denied");
var self = new PostingFixture(); self.Identity = self.Identity with { EmployeeId = self.Request.EmployeeId };
await Denies(() => self.Post(), "payroll_loan_self_posting_forbidden");
var scope = new PostingFixture(); scope.Grants.RemoveAll(x => x.Permission == AdvanceLoanPermissions.Repay);
scope.Grants.Add(new(AdvanceLoanPermissions.Repay, true, Guid.NewGuid(), AccessScopeKind.Employee, scope.Request.EmployeeId));
await Denies(() => scope.Post(), "access_denied");
foreach (var change in new Func<ConfirmPayrollLoanPostingRequest, ConfirmPayrollLoanPostingRequest>[] {
    r => r with { EmployeeId = Guid.NewGuid() }, r => r with { ScheduleEntryId = Guid.NewGuid() },
    r => r with { SourceVersion = r.SourceVersion + 1 }, r => r with { PostedAmount = 50 }
}) { var t = new PostingFixture(); await Denies(() => t.Post(request: change(t.Request)), "payroll_loan_posting_source_conflict"); Check(t.Ledger.Count == 0, "mismatched source writes nothing"); }
var invalid = new PostingFixture();
await Denies(() => invalid.Post(request: invalid.Request with { EvidenceSha256 = "bad" }), "invalid_payroll_loan_posting");
await Denies(() => invalid.Post(request: invalid.Request with { PostedAtUtc = PostingClock.Now.AddMinutes(1) }), "payroll_loan_posting_batch_not_ready");
await Denies(() => invalid.Post(request: invalid.Request with { PostedAtUtc = PostingClock.Now.AddDays(-10) }), "payroll_loan_posting_batch_not_ready");
await Denies(() => invalid.Post(request: invalid.Request with { Currency = "USD" }), "invalid_payroll_loan_posting");
await Denies(() => invalid.Post(request: invalid.Request with { PostedAmount = 100.001m }), "invalid_payroll_loan_posting");
var changed = new PostingFixture(); changed.Ledger.Add(changed.Payment(20));
await Denies(() => changed.Post(), "payroll_loan_balance_changed");
var tooMuch = new PostingFixture(); tooMuch.Ledger.Add(tooMuch.Payment(950, null));
await Denies(() => tooMuch.Post(), "payroll_loan_balance_changed");
var nonprincipal = new PostingFixture(10);
await Denies(() => nonprincipal.Post(), "interest_free_loan_required");
var altered = new PostingFixture(); altered.CorruptLine = true;
await Denies(() => altered.Post(), "payroll_loan_posting_evidence_conflict");
var missing = new PostingFixture(); missing.BadLock = true;
await Denies(() => missing.Post(), "payroll_handoff_month_not_locked");
var wrongAgreement = new PostingFixture();
await Denies(() => wrongAgreement.Post(request: wrongAgreement.Request with { AgreementId = Guid.NewGuid() }), "agreement_not_found");
var lostFile = new PostingFixture(); lostFile.FailReads = true;
await Denies(() => lostFile.Post(), "private_storage_unavailable");
Check(lostFile.Ledger.Count == 0, "missing private source does not post");
var disk = new PostingFixture(); disk.FailWrites = true;
await Denies(() => disk.Post(), "private_storage_unavailable");
Check(disk.Ledger.Count == 0 && disk.Receipts.Count == 0 && disk.Audits.Count == 0, "storage failure rolls back synthetic transaction");
var audit = new PostingFixture(); audit.FailSave = true;
await Denies(() => audit.Post(), "synthetic_audit_failure");
Check(audit.Ledger.Count == 0 && audit.Receipts.Count == 0 && audit.Audits.Count == 0, "journal failure rolls back synthetic transaction");
var race = new PostingFixture();
var same = await Task.WhenAll(race.Post("posting-racing-key"), race.Post("posting-racing-key"));
Check(same[0].LedgerEntryId == same[1].LedgerEntryId && race.Ledger.Count == 1, "serialized synthetic retry race has one effect; not SQL race acceptance");
var foreign = new PostingFixture(); foreign.Ledger.Add(f.Ledger[0]);
await Denies(() => foreign.Post(), "payroll_posting_receipt_reused");
var proof = new PostingFixture(); await proof.Post(); proof.Audits.Clear();
await Denies(() => proof.Read(), "payroll_loan_posting_evidence_missing");
var readScope = new PostingFixture(); await readScope.Post();
readScope.Grants.RemoveAll(x => x.Permission == AdvanceLoanPermissions.Review);
await Denies(() => readScope.Read(), "access_denied");
var notPosted = new PostingFixture(); await Denies(() => notPosted.Read(), "payroll_loan_posting_not_found");
var advice = new PostingFixture();
Check((await advice.Advice()).CsvContent.Contains("-100.00", StringComparison.Ordinal), "unpaid installment is included in advice");
advice.Ledger.Add(advice.Payment(100));
await Denies(() => advice.Advice(), "payroll_loan_balance_changed");
var partialAdvice = new PostingFixture(); partialAdvice.Ledger.Add(partialAdvice.Payment(20));
await Denies(() => partialAdvice.Advice(), "payroll_loan_balance_changed");
var exhaustedAdvice = new PostingFixture(); exhaustedAdvice.Ledger.Add(exhaustedAdvice.Payment(950, null));
await Denies(() => exhaustedAdvice.Advice(), "payroll_loan_balance_changed");
var postedAdvice = new PostingFixture(); await postedAdvice.Post();
await Denies(() => postedAdvice.Advice(), "payroll_loan_line_already_posted");
var readyLock = new PostingFixture(); readyLock.Unfreeze();
Check((await readyLock.Lock()).Batch.State == "locked", "unpaid principal can be locked");
var paidBeforeLock = new PostingFixture(); paidBeforeLock.Unfreeze(); paidBeforeLock.Ledger.Add(paidBeforeLock.Payment(100));
await Denies(() => paidBeforeLock.Lock(), "payroll_loan_balance_changed");
Check(paidBeforeLock.Batch.LockedAt is null, "repaid loan cannot be frozen into a deduction");
var partialBeforeLock = new PostingFixture(); partialBeforeLock.Unfreeze(); partialBeforeLock.Ledger.Add(partialBeforeLock.Payment(20));
await Denies(() => partialBeforeLock.Lock(), "payroll_loan_balance_changed");
var creditAdvice = new PostingFixture(withAdjustment: true);
Check((await creditAdvice.Advice()).CsvContent.Contains("100.00", StringComparison.Ordinal), "approved exact credit is retained in advice");
creditAdvice.CurrentAdjustments = [creditAdvice.CurrentAdjustments[0] with { Status = "posted",
    PostingResolutionId = Guid.NewGuid(), PostingEvidenceFingerprint = new string('B',64) }];
await Denies(() => creditAdvice.Advice(), "payroll_handoff_financial_adjustment_source_changed");
var creditLock = new PostingFixture(withAdjustment: true); creditLock.Unfreeze();
Check((await creditLock.Lock()).Batch.Totals.AdjustmentTotal == 100m, "credit is included in frozen batch totals separately from loan principal");
var postedCredit = new PostingFixture(withAdjustment: true); postedCredit.Unfreeze();
postedCredit.CurrentAdjustments = [postedCredit.CurrentAdjustments[0] with { Status = "posted",
    PostingResolutionId = Guid.NewGuid(), PostingEvidenceFingerprint = new string('B',64) }];
await Denies(() => postedCredit.Lock(), "payroll_handoff_financial_adjustment_source_changed");
Check(postedCredit.Batch.LockedAt is null, "already-posted financial adjustment cannot be frozen for duplicate credit");
var changedCredit = new PostingFixture(withAdjustment: true);
changedCredit.CurrentAdjustments = [changedCredit.CurrentAdjustments[0] with { SignedAmount = -200m }];
await Denies(() => changedCredit.Advice(), "month_lock_financial_adjustments_changed");
var missingCredit = new PostingFixture(withAdjustment: true); missingCredit.CurrentAdjustments = [];
await Denies(() => missingCredit.Advice(), "month_lock_financial_adjustments_changed");
var corruptCredit = new PostingFixture(withAdjustment: true) { CorruptLine = true };
await Denies(() => corruptCredit.Advice(), "payroll_handoff_financial_adjustment_evidence_invalid");
Console.WriteLine($"PASS: {checks} confirmed loan posting checks; actual services with synthetic IO, NOT authenticated SQL acceptance.");
return 0;

public class PostingProxy : DispatchProxy {
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class {
        var result = Create<T, PostingProxy>(); ((PostingProxy)(object)result).Handler = handler; return result;
    }
}
public sealed class PostingClock : TimeProvider {
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class PostingFixture {
    public DateOnly Start = new(2026, 9, 1);
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public List<PermissionGrant> Grants = [];
    public List<RepaymentLedgerEntry> Ledger = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<string, (string Purpose, byte[] Data)> Files = [];
    public bool SessionCurrent = true, CorruptLine, BadLock, FailReads, FailWrites, FailSave;
    public AdvanceLoanAgreement Agreement;
    public RepaymentScheduleEntry Schedule;
    public PayrollHandoffBatch Batch;
    public PayrollHandoffLine Line;
    public ConfirmPayrollLoanPostingRequest Request;
    public PayrollHandoffService Service;
    public IReadOnlyList<AttendancePayrollFinancialMonthEntry> CurrentAdjustments = [];
    private readonly SemaphoreSlim mutex = new(1);
    public PostingFixture(decimal extra = 0, bool withAdjustment = false) {
        var now = PostingClock.Now; var employee = Guid.NewGuid(); var preparer = Guid.NewGuid();
        foreach (var permission in new[] { AttendancePayrollAttestationPermissions.Attest, AttendancePayrollAttestationPermissions.Read, PayrollHandoffPermissions.Read, PayrollHandoffPermissions.Lock, PayrollHandoffPermissions.Process, AdvanceLoanPermissions.Repay, AdvanceLoanPermissions.Review })
            Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.All, null));
        var loan = new AdvanceLoanRequest(employee, AdvanceLoanKind.Loan, 1000, "PKR", "Synthetic loan", Start, Guid.NewGuid().ToString(), now.AddDays(-6));
        var approval = new ApprovalRequest(AdvanceLoanService.RequestType, loan.Id, loan.RequestedByUserId, "synthetic-terms", 1, now.AddDays(-6));
        loan.Submit(approval); approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Synthetic Finance", "Synthetic decision", now.AddDays(-5));
        Set(loan, "ApprovalRequest", approval);
        Agreement = new(loan.Id, 1000, "PKR", Start, "Synthetic approved terms", approval.Id, Guid.NewGuid().ToString(), now.AddDays(-5));
        Set(Agreement, "AdvanceLoanRequest", loan);
        Schedule = new(Agreement.Id, 1, Start, 100, 0, "PKR");
        // Simulate a retained legacy row; new domain records cannot contain charges.
        typeof(RepaymentScheduleEntry).GetProperty(nameof(RepaymentScheduleEntry.OtherApprovedAmount))!.SetValue(Schedule, extra);
        Agreement.Schedule.Add(Schedule);
        var period = new AttendancePeriodLock(Start, Start.AddMonths(1));
        var lockEvent = period.RecordDirectLock(Guid.NewGuid(), Guid.NewGuid().ToString(), "Synthetic lock", now.AddDays(-3), "synthetic-lock-reference");
        var hash = new string('A', 64);
        var summary = new DailyAttendanceSummary(employee, Start, 1, AttendanceDayStatus.Present, Guid.NewGuid(), Guid.NewGuid(), "synthetic-calculation", hash, 540, 540, 540, 0, 0, 0, now.AddDays(-4));
        var month = new MonthLockExisting(period, lockEvent, [summary], false);
        var snapshot = new MonthLockSnapshot(3, lockEvent.Id, period.Id, period.Revision, Start, Start.AddMonths(1), lockEvent.Actor, lockEvent.Reason, lockEvent.AtUtc, hash,
            [new(employee, Start, summary.Id, hash, summary.PolicyVersionId!.Value, summary.CalculationVersion)], [],
            [new(employee, Start, Guid.NewGuid(), 1, hash, "synthetic", hash, Guid.NewGuid(), hash, hash)], []);
        Batch = new("SYNTHETIC-LOAN-POSTING", Start, Start.AddMonths(1), "PKR", preparer.ToString(), now.AddDays(-2));
        var evidence = new { SchemaVersion = 1, BatchId = Batch.Id, EmployeeId = employee, Component = PayrollComponentKind.LoanRepayment.ToString(),
            SourceType = "hr.advance-loan.schedule", SourceId = Schedule.Id, SourceVersion = 1, AttendancePeriodLockEventId = lockEvent.Id,
            Amount = -(100 + extra), Currency = "PKR", PolicyVersionId = (Guid?)null, ApprovalRequestId = (Guid?)approval.Id,
            SourceEvidenceReference = Agreement.ApprovedTermsPrivateReference, ActorAccountId = preparer, PreparedAtUtc = Batch.PreparedAt };
        var reference = Store(JsonSerializer.SerializeToUtf8Bytes(evidence), $"hr:payroll-handoff:{Batch.Id:N}:line");
        Line = new(Batch.Id, employee, PayrollComponentKind.LoanRepayment, "hr.advance-loan.schedule", Schedule.Id, 1, lockEvent.Id,
            -(100 + extra), "PKR", null, approval.Id, AttendanceOperation.Reference(reference), "synthetic-trace");
        Batch.Lines.Add(Line); Batch.Lock(Guid.NewGuid(), Guid.NewGuid().ToString(), now.AddDays(-1));
        if (withAdjustment)
        {
            var member = snapshot.PayrollInputs![0];
            var credit = new AttendancePayrollFinancialMonthEntry(Guid.NewGuid(), employee, Start.AddMonths(-1), Start, 1,
                Guid.NewGuid(), Guid.NewGuid(), member.SourceRevisionId, member.InputFingerprint, hash, hash,
                "synthetic-credit-evidence", -100m, "PKR", "approved", null, null, [Guid.NewGuid()]);
            CurrentAdjustments = [credit];
            snapshot = snapshot with { SchemaVersion = 4, FinancialAdjustments = [credit] };
            var creditEvidence = evidence with { Component = PayrollComponentKind.OtherApprovedAdjustment.ToString(),
                SourceType = "attendance.payroll-financial-adjustment", SourceId = credit.AdjustmentId,
                SourceVersion = credit.Revision, Amount = 100m, PolicyVersionId = (Guid?)null, ApprovalRequestId = (Guid?)null,
                SourceEvidenceReference = credit.EvidenceReference };
            var creditReference = Store(JsonSerializer.SerializeToUtf8Bytes(creditEvidence), $"hr:payroll-handoff:{Batch.Id:N}:line");
            Batch.Lines.Add(new(Batch.Id, employee, PayrollComponentKind.OtherApprovedAdjustment,
                "attendance.payroll-financial-adjustment", credit.AdjustmentId, credit.Revision, lockEvent.Id,
                100m, "PKR", null, null, AttendanceOperation.Reference(creditReference), "synthetic-credit-trace"));
        }
        Request = new(employee, Agreement.Id, Schedule.Id, 1, 100 + extra, "PKR", now.AddMinutes(-5), "SYNTHETIC", "SYNTHETIC-POSTED-RECEIPT", "line-1", "synthetic:signed-receipt", hash, "Synthetic external deduction confirmation", "Record synthetic posting");
        var store = PostingProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") { if (FailWrites) throw new AuthenticationFailure("private_storage_unavailable", 503); return Task.FromResult(Store((byte[])a[0]!, (string)a[1]!)); }
            if (FailReads) throw new AuthenticationFailure("private_storage_unavailable", 503);
            var input = (PrivateFileReference)a[0]!; var file = Files[input.StorageKey];
            if (file.Purpose != (string)a[1]! || input.Sha256 != Convert.ToHexString(SHA256.HashData(file.Data))) throw new Exception("Protected source mismatch");
            if (CorruptLine && file.Purpose.EndsWith(":line")) return Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(evidence with { Amount = -101m }));
            return Task.FromResult(file.Data);
        });
        var access = PostingProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var auth = PostingProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => typeof(PostingFixture).GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("Synthetic Finance"), _ => throw new Exception(m.Name) });
        var journal = PostingProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => FailSave ? throw new AuthenticationFailure("synthetic_audit_failure", 503) : Task.CompletedTask,
            _ => throw new Exception(m.Name) });
        var operation = new AttendanceOperation(PostingProxy.Make<ICurrentAuthentication>((_, _) => Identity), access, auth, journal,
            PostingProxy.Make<IAuthenticationRepository>((m, _) => throw new Exception(m.Name)), new PostingClock(), store);
        var loans = PostingProxy.Make<IAdvanceLoanRepository>((m, a) => m.Name switch {
            "FindAgreementWithDetailsAsync" => Task.FromResult((Guid)a[0]! == Agreement.Id ? Agreement : null),
            "FindLedgerEntryByIdempotencyKeyAsync" => Task.FromResult(Ledger.SingleOrDefault(x => x.IdempotencyKey == (string)a[0]!)),
            "ListLedgerEntriesAsync" => Task.FromResult<IReadOnlyList<RepaymentLedgerEntry>>(Ledger.Where(x => ((IReadOnlyCollection<Guid>)a[0]!).Contains(x.AdvanceLoanAgreementId)).ToArray()),
            "AddLedgerEntry" => Add(Ledger, (RepaymentLedgerEntry)a[0]!), _ => throw new Exception(m.Name) });
        Service = new(operation, PostingProxy.Make<IPayrollHandoffRepository>((m, a) => m.Name switch {
            "FindBatchAsync" => Task.FromResult<PayrollHandoffBatch?>(Batch),
            "FindAgreementIdForScheduleAsync" => Task.FromResult<Guid?>(Schedule.Id == (Guid)a[0]! ? Agreement.Id : null),
            "LoanPostingAuditAsync" => Task.FromResult(Audits.SingleOrDefault(x => x.TargetId == ((Guid)a[0]!).ToString() && x.EventType == "attendance.payroll-handoff.loan-posting")),
            "ProcessingAsync" => Task.FromResult<IReadOnlyList<PayrollHandoffProcessingRecord>>([]),
            "EmployeeNamesAsync" => Task.FromResult<IReadOnlyDictionary<Guid,string>>(new Dictionary<Guid,string>{{employee,"SYNTHETIC EMPLOYEE"}}),
            "ExpectVersion" => null,
            "Add" => null,
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception(m.Name) }),
            PostingProxy.Make<IMonthLockRepository>((m, _) => m.Name == "FindAsync" ? Task.FromResult(BadLock ? null : month) : throw new Exception(m.Name)),
            PostingProxy.Make<IAttendancePayrollInputLedger>((m, _) => throw new Exception(m.Name)),
            PostingProxy.Make<IAllowanceRepository>((m, _) => throw new Exception(m.Name)),
            PostingProxy.Make<IAllowanceSettlementStore>((m, _) => throw new Exception(m.Name)), access, store,
            PostingProxy.Make<IMonthLockSnapshotReader>((_, _) => Task.FromResult(snapshot)), loans,
            PostingProxy.Make<IAttendancePayrollFinancialMonthReader>((_, _) => Task.FromResult(CurrentAdjustments)));
    }
    public Task<PayrollLoanPostingResponse> Post(string key = "synthetic-posting-key", ConfirmPayrollLoanPostingRequest? request = null) =>
        Service.ConfirmLoanPostingAsync(Batch.Id, Line.Id, request ?? Request, key, new("127.0.0.1", "synthetic-posting"), default);
    public Task<PayrollLoanPostingResponse> Read() => Service.LoanPostingAsync(Batch.Id, Line.Id, new("127.0.0.1", "synthetic-posting-read"), default);
    public Task<PayrollHandoffBankAdvice> Advice() => Service.BankAdviceAsync(Batch.Id, new("127.0.0.1", "synthetic-bank-advice"), default);
    public Task<PayrollHandoffBatchDetail> Lock() => Service.LockAsync(Batch.Id, new(Convert.ToBase64String(new byte[8]), "Synthetic loan lock"), "synthetic-lock-key", new("127.0.0.1", "synthetic-loan-lock"), default);
    public void Unfreeze() { Set(Batch, "LockedAt", null); Set(Batch, "LockedByUserId", null); Set(Batch, "ApprovalRequestId", null); }
    public RepaymentLedgerEntry Payment(decimal amount) => Payment(amount, Schedule.Id);
    public RepaymentLedgerEntry Payment(decimal amount, Guid? schedule) => new(Agreement.Id, schedule, RepaymentEntryKind.ReceivedPayment, amount, "PKR", Start, "synthetic-payment", Guid.NewGuid().ToString(), null, Identity.AccountId.ToString(), PostingClock.Now);
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) {
        await mutex.WaitAsync(ct); var ledger = Ledger.Count; var receipts = Receipts.Count; var audit = Audits.Count;
        try { return await action(ct); }
        catch { Ledger.RemoveRange(ledger, Ledger.Count - ledger); Receipts.RemoveRange(receipts, Receipts.Count - receipts); Audits.RemoveRange(audit, Audits.Count - audit); throw; }
        finally { mutex.Release(); }
    }
    private PrivateFileReference Store(byte[] bytes, string purpose) {
        var id = Guid.NewGuid().ToString("N"); Files[id] = (purpose, bytes);
        return new(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length);
    }
    private static object? Add<T>(List<T> rows, T value) { rows.Add(value); return null; }
    private static void Set(object row, string name, object? value) => row.GetType().GetProperty(name)!.SetValue(row, value);
}
