#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

var count = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
void Reject(Action action, string name)
{
    try { action(); } catch (ArgumentException) { count++; return; }
    throw new Exception(name);
}
void RejectJson(Action action, string name)
{
    try { action(); } catch (JsonException) { count++; return; }
    throw new Exception(name);
}
var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
AttendancePayrollAttestationLine[] lines = [new("ordinary:2026-10-01", 125.25m, "PKR"), new("late:2026-10-02", 0m, "PKR")];
AttendancePayrollAttestationRules.RequireLines(lines, lines);
Reject(() => AttendancePayrollAttestationRules.RequireLines(lines, [lines[0]]), "Missing line must fail, including a zero line");
Reject(() => AttendancePayrollAttestationRules.RequireLines(lines, [lines[0], lines[0]]), "Duplicate keys must fail");
Reject(() => AttendancePayrollAttestationRules.RequireLines(lines, [lines[0] with { Amount = 125.26m }, lines[1]]), "One-cent mismatch must fail");
Reject(() => AttendancePayrollAttestationRules.RequireLines(lines, [lines[0] with { Currency = "USD" }, lines[1]]), "Currency mismatch must fail");
Reject(() => AttendancePayrollAttestationRules.RequireLines([], []), "Empty financial source cannot become a receipt");
Reject(() => AttendancePayrollAttestationRules.RequireLines(lines, [lines[0] with { Amount = -1m }, lines[1]]), "Negative amounts must fail");
Check(AttendancePayrollAttestationRules.LineFingerprint(lines) == AttendancePayrollAttestationRules.LineFingerprint([lines[1], lines[0] with { Amount = 125.250m }]), "Line order and decimal scale do not alter exact financial identity");
RejectJson(() => JsonSerializer.Deserialize<AttendancePayrollAttestationLine>("{\"Key\":\"x\",\"Currency\":\"PKR\"}"), "Missing JSON amount must fail");
RejectJson(() => JsonSerializer.Deserialize<AttendancePayrollAttestationLine>("{\"Key\":\"x\",\"Amount\":null,\"Currency\":\"PKR\"}"), "Null JSON amount must fail");
RejectJson(() => JsonSerializer.Deserialize<AttendancePayrollAttestationLine>("{\"Key\":\"x\",\"Amount\":\"0\",\"Currency\":\"PKR\"}"), "Quoted JSON amount must fail");
Reject(() => AttendancePayrollAttestationRules.RequireSourceTime(now.AddSeconds(-1), now), "Event cannot predate exact saved source");
Reject(() => AttendancePayrollAttestationRules.RequireReceipt("handoff", now, now, "PAYROLL", "EMP-1/OCT", "document locator", new string('A', 64), "explicit evidence", now), "Handoff cannot contain posting proof");
Reject(() => AttendancePayrollAttestationRules.RequireReceipt("posted", now, null, "PAYROLL", "EMP-1/OCT", "document locator", new string('A', 64), "explicit evidence", now), "Posting timestamp required");
Reject(() => AttendancePayrollAttestationRules.RequireReceipt("posted", now, now, "PAYROLL", "", "document locator", new string('A', 64), "explicit evidence", now), "External reference required");
Reject(() => AttendancePayrollAttestationRules.RequireReceipt("posted", now, now, "PAYROLL", "EMP-1/OCT", "", new string('A', 64), "explicit evidence", now), "Evidence required");
Reject(() => AttendancePayrollAttestationRules.RequireReceipt("posted", now.AddMinutes(1), now.AddMinutes(1), "PAYROLL", "EMP-1/OCT", "document locator", new string('A', 64), "explicit evidence", now), "Future posting cannot be claimed");
var employee = Guid.NewGuid(); var source = Guid.NewGuid(); var author = Guid.NewGuid();
AttendancePayrollAttestation Row(string status, Guid? previous, DateTimeOffset? posted) => new(employee, new(2026, 10, 1), source,
    1, new string('A', 64), previous, status, AttendancePayrollAttestationRules.LineFingerprint(lines),
    AttendancePayrollAttestationRules.ExternalReceiptKey("PAYROLL", "EMP-1/OCT"), now, posted, author, now,
    "private-reference", new string('B', 64));
var handoff = Row("handoff", null, null);
Check(!handoff.ConfirmsPosting && handoff.Step == 1, "Handoff is not posted proof");
Reject(() => AttendancePayrollAttestationRules.RequireTransition(handoff, handoff.Id, source, "handoff", now, null, handoff.ExternalReceiptKey), "Second idempotency key cannot repeat handoff");
AttendancePayrollAttestationRules.RequireTransition(handoff, handoff.Id, source, "posted", now, now,
    AttendancePayrollAttestationRules.ExternalReceiptKey("PAYROLL", "EMP-1/OCT"));
Reject(() => AttendancePayrollAttestationRules.RequireTransition(null, null, source, "posted", now, now, handoff.ExternalReceiptKey), "Posting cannot skip handoff");
Reject(() => AttendancePayrollAttestationRules.RequireTransition(handoff, Guid.NewGuid(), source, "posted", now, now, handoff.ExternalReceiptKey), "Stale expected predecessor must fail");
Reject(() => AttendancePayrollAttestationRules.RequireTransition(handoff, handoff.Id, Guid.NewGuid(), "posted", now, now, handoff.ExternalReceiptKey), "Replacement source cannot inherit handoff");
var posted = Row("posted", handoff.Id, now);
Check(posted.ConfirmsPosting && posted.Step == 2, "Only financial receipt confirms posting");
Reject(() => AttendancePayrollAttestationRules.RequireTransition(posted, posted.Id, source, "settled", now, now.AddSeconds(-1), posted.ExternalReceiptKey), "Settlement cannot change posting time");
Reject(() => AttendancePayrollAttestationRules.RequireTransition(posted, posted.Id, source, "settled", now, now, new string('C', 64)), "Settlement cannot change posting reference");
AttendancePayrollAttestationRules.RequireTransition(posted, posted.Id, source, "settled", now, now, posted.ExternalReceiptKey);
Reject(() => AttendancePayrollAttestationRules.RequireReceipt("settled", now, default(DateTimeOffset), "PAYROLL", "EMP-1/OCT", "document locator", new string('A', 64), "explicit evidence", now), "Default timestamp is not posting evidence");
Reject(() => AttendancePayrollAttestationRules.RequireLines(lines, [lines[0] with { Amount = 125.251m }, lines[1]]), "Sub-cent amount must fail");
Reject(() => AttendancePayrollAttestationRules.RequireLines(lines, Enumerable.Repeat(lines[0], 733).ToArray()), "Unbounded line count must fail");
Check(AttendancePayrollAttestationRules.ExternalReceiptKey(" payroll ", " EMP-1/OCT ") == handoff.ExternalReceiptKey, "Normalized references cannot bypass duplicate-receipt identity");
Check(AttendancePayrollAttestationRules.ExternalReceiptKey("PAYROLL", "emp-1/oct") != handoff.ExternalReceiptKey, "External references retain exact case-sensitive identity");
var settled = Row("settled", posted.Id, now);
Reject(() => AttendancePayrollAttestationRules.RequireTransition(settled, settled.Id, source, "posted", now, now, settled.ExternalReceiptKey), "Finalized evidence cannot be rewritten");

// Synthetic adapters supply IO only; both payroll services and AttendanceOperation execute unchanged.
Check(AccessPermissionCatalog.Find(AttendancePayrollAttestationPermissions.Attest) is not null &&
    AccessPermissionCatalog.Find(AttendancePayrollAttestationPermissions.Read) is not null,
    "Root must integrate real permission catalog definitions before running service checks");
var ct = CancellationToken.None;
async Task FailsService(AttestationHarness harness, Func<Task> action, string code, int status = 409)
{
    var rowCount = harness.Attestations.Rows.Count; var receiptCount = harness.Receipts.Count;
    try { await action(); throw new Exception("Expected service failure: " + code); }
    catch (AuthenticationFailure error)
    {
        Check(error.Code == code && error.StatusCode == status,
            $"Expected {code}/{status}, got {error.Code}/{error.StatusCode}");
    }
    Check(harness.Attestations.Rows.Count == rowCount && harness.Receipts.Count == receiptCount && harness.TransactionDepth == 0,
        "Rejected operation cannot append business/idempotency rows or leave a transaction open");
}
var h = new AttestationHarness();
var saved = await h.SaveSource("source-save-001");
Check(h.Sources.Count == 1 && h.Files.Objects.ContainsKey(JsonSerializer.Deserialize<PrivateFileReference>(h.Sources[0].ProtectedEvidenceReference)!.StorageKey),
    "Source is saved through real input service with retained protected calculation evidence");
Check(!saved.Inputs.PayrollReady && !saved.Inputs.PaymentInstruction && h.Attestations.Rows.Count == 0,
    "Saved source is never posting evidence");
IAttendanceConfirmedPayrollPostingReader confirmed = h.Service;
Check(await confirmed.ReadConfirmedPostingAsync(h.Employee, h.Month, saved.Revision.Id, h.Context, ct) is null,
    "Confirmed posting reader returns no proof for a saved source alone");
var handoffRequest = h.Request(saved, "handoff", null);
var calculationsBefore = h.Calculations;
h.Calculation = h.Calculation with { InputFingerprint = new string('B', 64),
    Lines = [h.Calculation.Lines[0] with { Amount = 999m, CandidateAmount = 999m }, h.Calculation.Lines[1]] };
var serviceHandoff = await h.Create(saved, handoffRequest, "handoff-key-001");
Check(serviceHandoff.SourceRevisionId == saved.Revision.Id && serviceHandoff.SourceFingerprint == saved.Revision.InputFingerprint &&
    serviceHandoff.Lines[0].Amount == 125.25m && serviceHandoff.Lines[1].Amount == 0m && h.Calculations == calculationsBefore,
    "Handoff uses exact saved protected source, never current producer output");
Check(!serviceHandoff.ConfirmsPosting && serviceHandoff.PostedAtUtc is null && !serviceHandoff.PayrollReady && !serviceHandoff.PaymentInstruction,
    "Actual service handoff has no posting or payment authority");
Check(await confirmed.ReadConfirmedPostingAsync(h.Employee, h.Month, saved.Revision.Id, h.Context, ct) is null,
    "Confirmed adapter cannot promote handoff to posting");
var repeated = await h.Create(saved, handoffRequest, "handoff-key-001");
Check(repeated.Id == serviceHandoff.Id && h.Attestations.Rows.Count == 1 && h.AttestationReceipts.Count == 1,
    "Same-key retry returns one exact protected attestation without duplicate stage");
Check(h.Audits.Any(x => x.EventType == "attendance.payroll-attestations.create.replay" && x.TargetId == serviceHandoff.Id.ToString()),
    "Replay is audited through existing operation");
await FailsService(h, () => h.Create(saved, handoffRequest with { Statement = "Changed statement under same key" }, "handoff-key-001"), "idempotency_key_reused");
await FailsService(h, () => h.Create(saved, handoffRequest with { Lines = [handoffRequest.Lines[0] with { Amount = 0m }, handoffRequest.Lines[1]] }, "handoff-key-001"), "idempotency_key_reused");
await FailsService(h, () => h.Create(saved, handoffRequest, "handoff-second-key"), "payroll_attestation_transition_conflict");
await FailsService(h, () => h.Create(saved, handoffRequest with { ExpectedSourceFingerprint = new string('C', 64) }, "stale-source-hash"), "payroll_attestation_source_conflict");
await FailsService(h, () => h.Create(saved, handoffRequest with { ExpectedSourceRevision = 2 }, "stale-source-number"), "payroll_attestation_source_conflict");
var postedRequest = h.Request(saved, "posted", serviceHandoff.Id);
await FailsService(h, () => h.Create(saved, postedRequest with { ExpectedPreviousAttestationId = Guid.NewGuid() }, "stale-predecessor"), "payroll_attestation_transition_conflict");
await FailsService(h, () => h.Create(saved, postedRequest with { Lines = [postedRequest.Lines[0]] }, "missing-zero-line"), "invalid_payroll_attestation_receipt", 400);
await FailsService(h, () => h.Create(saved, postedRequest with { Lines = [postedRequest.Lines[0] with { Amount = 0m }, postedRequest.Lines[1]] }, "invent-zero-amount"), "invalid_payroll_attestation_receipt", 400);
await FailsService(h, () => h.Create(saved, postedRequest with { Lines = [] }, "empty-line-source"), "invalid_payroll_attestation_receipt", 400);
await FailsService(h, () => h.Create(saved, postedRequest with { Lines = null! }, "null-line-source"), "invalid_payroll_attestation_receipt", 400);
await FailsService(h, () => h.Create(saved, postedRequest with { PostedAtUtc = null }, "no-posting-time"), "invalid_payroll_attestation_receipt", 400);
await FailsService(h, () => h.Create(saved, postedRequest with { EvidenceReference = "" }, "no-posting-evidence"), "invalid_payroll_attestation_receipt", 400);
await FailsService(h, () => h.Create(saved, postedRequest with { ExternalReference = "" }, "no-posting-reference"), "invalid_payroll_attestation_receipt", 400);
var servicePosted = await h.Create(saved, postedRequest, "posting-key-001");
Check(servicePosted.ConfirmsPosting && servicePosted.Status == "posted" && servicePosted.PostedAtUtc == h.Now &&
    servicePosted.PreviousAttestationId == serviceHandoff.Id && servicePosted.ActorUserId == h.Identity.AccountId,
    "Actual service posting requires an explicit receipt, exact predecessor and server-derived author");
Check(!servicePosted.PayrollReady && !servicePosted.PaymentInstruction && servicePosted.VerificationMode == "manual_authorized_attestation",
    "Manual posted receipt does not assert integration, readiness or payment release");
var confirmedPosting = await confirmed.ReadConfirmedPostingAsync(h.Employee, h.Month, saved.Revision.Id, h.Context, ct);
Check(confirmedPosting is { } verifiedPosting && verifiedPosting.Id == servicePosted.Id &&
    verifiedPosting.SourceFingerprint == saved.Revision.InputFingerprint && verifiedPosting.Lines.SequenceEqual(postedRequest.Lines),
    "Confirmed adapter retains exact validated posting provenance");
var postedRow = h.Attestations.Rows.Single(x => x.Id == servicePosted.Id);
var receipt = h.AttestationReceipts.Single(x => x.Key == "posting-key-001");
var resultEnvelope = h.Files.Json(receipt.ProtectedResultReference!);
Check(receipt.CompletedAt == h.Now && receipt.ActorUserId == h.Identity.AccountId.ToString() && receipt.ResultCode == "attendance-operation-v2" &&
    resultEnvelope["ResourceId"]!.GetValue<Guid>() == servicePosted.Id &&
    resultEnvelope["Result"]!["PostedAtUtc"]!.GetValue<DateTimeOffset>() == h.Now && resultEnvelope["ResourceScopes"]!.AsArray().Count != 0,
    "Existing operation persists protected scoped replay evidence with the posted receipt");
var audit = h.Audits.Single(x => x.EventType == "attendance.payroll-attestations.create" && x.TargetId == servicePosted.Id.ToString());
var auditEnvelope = h.Files.Json(audit.ProtectedChangesReference!);
Check(audit.ActorUserId == h.Identity.AccountId.ToString() && audit.EmployeeScopeId == h.Employee &&
    audit.CorrelationId == h.Context.CorrelationId && auditEnvelope["After"]!["SourceRevisionId"]!.GetValue<Guid>() == saved.Revision.Id &&
    auditEnvelope["Before"]!["Id"]!.GetValue<Guid>() == serviceHandoff.Id,
    "Audit contains protected exact before/after evidence and current actor/correlation metadata");
var privatePosting = h.Files.Json(postedRow.ProtectedEvidenceReference);
Check(privatePosting["SourceProtectedEvidenceReference"]!.GetValue<string>() == h.Sources[0].ProtectedEvidenceReference &&
    privatePosting["SourceFingerprint"]!.GetValue<string>() == saved.Revision.InputFingerprint,
    "Manual receipt retains exact protected saved-source reference as well as its fingerprint");
var originalPostedBytes = h.Files.Bytes(postedRow.ProtectedEvidenceReference);
h.Files.Corrupt(postedRow.ProtectedEvidenceReference, json => json["Lines"]![0]!["Amount"] = 0m);
await FailsService(h, () => confirmed.ReadConfirmedPostingAsync(h.Employee, h.Month, saved.Revision.Id, h.Context, ct), "payroll_attestation_evidence_invalid");
h.Files.Restore(postedRow.ProtectedEvidenceReference, originalPostedBytes);
var settlementRequest = h.Request(saved, "settled", servicePosted.Id);
await FailsService(h, () => h.Create(saved, settlementRequest with { ExternalReference = "Replacement reference" }, "change-post-reference"), "payroll_attestation_transition_conflict");
var serviceSettled = await h.Create(saved, settlementRequest, "settlement-key-001");
var immutableBytes = h.Files.Bytes(h.Attestations.Rows.Single(x => x.Id == serviceSettled.Id).ProtectedEvidenceReference);
await FailsService(h, () => h.Create(saved, h.Request(saved, "posted", serviceSettled.Id), "rewrite-final-post"), "payroll_attestation_transition_conflict");
Check(h.Files.Bytes(h.Attestations.Rows.Single(x => x.Id == serviceSettled.Id).ProtectedEvidenceReference).SequenceEqual(immutableBytes) &&
    (await h.Service.HistoryAsync(h.Employee, h.Month, saved.Revision.Id, h.Context, ct)).Select(x => x.Id)
        .SequenceEqual(new[] { serviceHandoff.Id, servicePosted.Id, serviceSettled.Id }),
    "Final-stage attempts preserve all exact immutable service receipts");
Check((await confirmed.ReadConfirmedPostingAsync(h.Employee, h.Month, saved.Revision.Id, h.Context, ct))?.Id == serviceSettled.Id,
    "Confirmed adapter returns retained settled receipt with same posting binding");
Check(h.MaximumTransactionDepth == 1 && h.TransactionDepth == 0, "Source detail and attestation never nest AttendanceOperation transactions");

var corrupt = new AttestationHarness(); var corruptSaved = await corrupt.SaveSource("corrupt-source-save");
var originalSourceBytes = corrupt.Files.Bytes(corrupt.Sources[0].ProtectedEvidenceReference);
corrupt.Files.Corrupt(corrupt.Sources[0].ProtectedEvidenceReference, json => json["Calculation"]!["Lines"]![0]!["Amount"] = 0m);
await FailsService(corrupt, () => corrupt.Create(corruptSaved, corrupt.Request(corruptSaved, "handoff", null), "corrupt-source-first"), "payroll_input_evidence_invalid");
corrupt.Files.Restore(corrupt.Sources[0].ProtectedEvidenceReference, originalSourceBytes);
corrupt.AfterCommit = () => corrupt.Files.Corrupt(corrupt.Sources[0].ProtectedEvidenceReference,
    json => json["Calculation"]!["Lines"]![0]!["Amount"] = 0m);
await FailsService(corrupt, () => corrupt.Create(corruptSaved, corrupt.Request(corruptSaved, "handoff", null), "corrupt-source-between"), "payroll_attestation_source_evidence_invalid");

var latest = new AttestationHarness(); var oldSaved = await latest.SaveSource("old-source-save");
latest.Calculation = latest.Calculation with { InputFingerprint = new string('B', 64) };
var newSaved = await latest.SaveSource("new-source-save");
await FailsService(latest, () => latest.Create(oldSaved, latest.Request(oldSaved, "handoff", null), "superseded-source"), "payroll_attestation_source_superseded");
var newHandoff = await latest.Create(newSaved, latest.Request(newSaved, "handoff", null), "latest-handoff-key");
await FailsService(latest, () => latest.Create(oldSaved, latest.Request(oldSaved, "posted", newHandoff.Id), "wrong-source-chain"), "payroll_attestation_chain_source_conflict");
latest.Calculation = latest.Calculation with { InputFingerprint = new string('C', 64) };
var replacementSaved = await latest.SaveSource("replacement-after-handoff");
var staleHandoffReadiness = await latest.InRead((actor, token) => latest.FinancialReader().InspectAsync(
    latest.Month, latest.Month.AddMonths(1), [latest.Employee], actor, token));
Check(staleHandoffReadiness.Issues.Any(x => x.EmployeeId == latest.Employee && x.Code == "payroll_handoff_source_reconciliation_required") &&
    staleHandoffReadiness.PayrollInputs.Single().SourceRevisionId == replacementSaved.Revision.Id &&
    staleHandoffReadiness.PayrollInputs.Single().HandoffId is null,
    "Unposted stale handoff is a visible not-ready dependency on the current revision, not a failed global preview or reused approval");
var pinnedPosting = await latest.Create(newSaved, latest.Request(newSaved, "posted", newHandoff.Id), "post-pinned-source");
Check(pinnedPosting.SourceRevisionId == newSaved.Revision.Id && pinnedPosting.SourceRevisionId != replacementSaved.Revision.Id,
    "New Attendance revision cannot silently change a financial chain's exact source");
var postedStaleReadiness = await latest.InRead((actor, token) => latest.FinancialReader().InspectAsync(
    latest.Month, latest.Month.AddMonths(1), [latest.Employee], actor, token));
Check(postedStaleReadiness.Issues.Any(x => x.Code == "payroll_handoff_source_reconciliation_required") &&
    postedStaleReadiness.Fingerprint != staleHandoffReadiness.Fingerprint && postedStaleReadiness.PayrollInputs.Single().HandoffId is null,
    "Changed posting evidence changes the pending fingerprint without attaching old posting to a new calculation");
await FailsService(latest, () => latest.Create(replacementSaved, latest.Request(replacementSaved, "handoff", null), "replace-bound-chain"), "payroll_attestation_chain_source_conflict");

var boundary = new AttestationHarness(); var boundarySaved = await boundary.SaveSource("boundary-source-save");
boundary.AfterCommit = () => boundary.Sources.Clear();
await FailsService(boundary, () => boundary.Create(boundarySaved, boundary.Request(boundarySaved, "handoff", null), "source-removed-between"), "payroll_attestation_source_conflict");

var verifiedEmpty = new AttestationHarness(); verifiedEmpty.Calculation = verifiedEmpty.Calculation with { Lines = [] };
var emptyPreview = await verifiedEmpty.Inputs.PreviewAsync(verifiedEmpty.Employee, verifiedEmpty.Month, verifiedEmpty.Context, ct);
Check(emptyPreview.CanSave && emptyPreview.State == "ready" && emptyPreview.Lines.Count == 0 && emptyPreview.DeductionTotal == 0m,
    "Actual input service accepts a verified complete empty deduction set without inventing a zero line");
var emptySaved = await verifiedEmpty.SaveSource("verified-empty-source");
var emptyRequest = verifiedEmpty.Request(emptySaved, "handoff", null);
var emptyHandoff = await verifiedEmpty.Create(emptySaved, emptyRequest, "verified-empty-handoff");
Check(emptyHandoff.Status == "handoff" && emptyHandoff.Lines.Count == 0 && emptyHandoff.SourceRevisionId == emptySaved.Revision.Id &&
    emptyHandoff.SourceFingerprint == emptySaved.Revision.InputFingerprint && !emptyHandoff.ConfirmsPosting &&
    emptyHandoff.PostedAtUtc is null && !emptyHandoff.PayrollReady && !emptyHandoff.PaymentInstruction,
    "Exact verified empty source becomes a reviewable handoff, never a posting/payment/ready claim");
var emptyReplay = await verifiedEmpty.Create(emptySaved, emptyRequest, "verified-empty-handoff");
Check(emptyReplay.Id == emptyHandoff.Id && verifiedEmpty.Attestations.Rows.Count == 1 && verifiedEmpty.AttestationReceipts.Count == 1,
    "Verified no-deduction retry has one immutable business row and operation receipt");
Check(verifiedEmpty.Audits.Any(x => x.EventType == "attendance.payroll-attestations.create" && x.TargetId == emptyHandoff.Id.ToString()),
    "No-deduction handoff uses existing protected financial audit");
Check((await verifiedEmpty.Service.HistoryAsync(verifiedEmpty.Employee, verifiedEmpty.Month, emptySaved.Revision.Id, verifiedEmpty.Context, ct)).Single().Id == emptyHandoff.Id,
    "Accounts/Finance can read its exact retained no-deduction review record");
Check(await verifiedEmpty.Service.ReadConfirmedPostingAsync(verifiedEmpty.Employee, verifiedEmpty.Month, emptySaved.Revision.Id, verifiedEmpty.Context, ct) is null,
    "No-deduction handoff never becomes confirmed posting or a refund basis");
await FailsService(verifiedEmpty, () => verifiedEmpty.Create(emptySaved, emptyRequest with { Statement = "Changed empty handoff statement" }, "verified-empty-handoff"), "idempotency_key_reused");
await FailsService(verifiedEmpty, () => verifiedEmpty.Create(emptySaved, emptyRequest, "duplicate-empty-handoff"), "payroll_attestation_transition_conflict");
await FailsService(verifiedEmpty, () => verifiedEmpty.Create(emptySaved, emptyRequest with { Lines = null! }, "missing-empty-collection"), "invalid_payroll_attestation_receipt", 400);
await FailsService(verifiedEmpty, () => verifiedEmpty.Create(emptySaved, emptyRequest with { Lines = [new("fabricated-zero", 0m, "PKR")] }, "invent-empty-zero-line"), "invalid_payroll_attestation_receipt", 400);
await FailsService(verifiedEmpty, () => verifiedEmpty.Create(emptySaved, emptyRequest with { PostedAtUtc = verifiedEmpty.Now }, "empty-handoff-posting-proof"), "invalid_payroll_attestation_receipt", 400);
await FailsService(verifiedEmpty, () => verifiedEmpty.Create(emptySaved, emptyRequest with { ExpectedSourceFingerprint = new string('C', 64) }, "empty-stale-source"), "payroll_attestation_source_conflict");
foreach (var emptyStatus in new[] { "posted", "settled" })
    await FailsService(verifiedEmpty, () => verifiedEmpty.Create(emptySaved, verifiedEmpty.Request(emptySaved, emptyStatus, emptyHandoff.Id), "verified-empty-" + emptyStatus),
        "payroll_attestation_no_deduction_lines");
Check(verifiedEmpty.Sources.Count == 1 && emptySaved.Inputs.Lines.Count == 0 && verifiedEmpty.Attestations.Rows.Count == 1,
    "Valid empty source retains only a reviewable handoff, with no fake monetary line or payment receipt");
var emptyFinancialReader = verifiedEmpty.FinancialReader();
var inspectedEmpty = await verifiedEmpty.InRead((actor, token) => emptyFinancialReader.InspectAsync(verifiedEmpty.Month,
    verifiedEmpty.Month.AddMonths(1), [verifiedEmpty.Employee], actor, token));
Check(inspectedEmpty.PayrollInputs.Single().HandoffId == emptyHandoff.Id &&
    !inspectedEmpty.Issues.Any(x => x.Code == "payroll_no_charge_handoff_required") &&
    inspectedEmpty.Issues.Any(x => x.Code == "legal_payroll_activation_required"),
    "Month inspection binds exact no-charge handoff while preserving legal activation blocker");
await FailsService(verifiedEmpty, () => verifiedEmpty.InRead((actor, token) => emptyFinancialReader.VerifyRetainedAsync(inspectedEmpty.PayrollInputs, actor, token)),
    "month_lock_legal_activation_unavailable");
await FailsService(verifiedEmpty, () => verifiedEmpty.Service.CreateAsync(verifiedEmpty.Employee, verifiedEmpty.Month, Guid.NewGuid(),
    verifiedEmpty.Request(emptySaved, "handoff", null), "missing-saved-source", verifiedEmpty.Context, ct), "attendance_resource_not_found", 404);
verifiedEmpty.Calculation = verifiedEmpty.Calculation with { InputFingerprint = new string('D', 64) };
var changedEmpty = await verifiedEmpty.SaveSource("changed-empty-after-handoff");
var changedEmptyInspection = await verifiedEmpty.InRead((actor, token) => emptyFinancialReader.InspectAsync(
    verifiedEmpty.Month, verifiedEmpty.Month.AddMonths(1), [verifiedEmpty.Employee], actor, token));
Check(changedEmptyInspection.PayrollInputs.Single().SourceRevisionId == changedEmpty.Revision.Id &&
    changedEmptyInspection.PayrollInputs.Single().HandoffId is null &&
    changedEmptyInspection.Issues.Any(x => x.Code == "payroll_handoff_source_reconciliation_required") &&
    changedEmptyInspection.Issues.Any(x => x.Code == "payroll_no_charge_handoff_required") &&
    changedEmptyInspection.Issues.Any(x => x.Code == "legal_payroll_activation_required"),
    "A changed no-charge calculation still needs its own handoff and legal readiness, never inherited approval");
verifiedEmpty.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Read);
await FailsService(verifiedEmpty, () => verifiedEmpty.InRead((actor, token) => emptyFinancialReader.InspectAsync(
    verifiedEmpty.Month, verifiedEmpty.Month.AddMonths(1), [verifiedEmpty.Employee], actor, token)), "access_denied", 403);
var unavailable = new AttestationHarness(); unavailable.Calculation = unavailable.Calculation with
    { State = "unavailable", Lines = [], Issues = [new("source_missing")] };
var unavailablePreview = await unavailable.Inputs.PreviewAsync(unavailable.Employee, unavailable.Month, unavailable.Context, ct);
Check(!unavailablePreview.CanSave && unavailablePreview.DeductionTotal is null && unavailablePreview.State == "unavailable",
    "Unavailable/missing financial source remains unknown, distinct from verified empty");
await FailsService(unavailable, () => unavailable.Inputs.SaveAsync(unavailable.Employee, unavailable.Month,
    new(unavailablePreview.PreviewFingerprint, 0, "Cannot save missing financial evidence"), "missing-evidence-save", unavailable.Context, ct), "payroll_input_sources_unavailable");
Check(unavailable.Sources.Count == 0 && unavailable.Attestations.Rows.Count == 0, "Missing source never produces a saved zero deduction or posted receipt");
foreach (var state in new[] { "ready", "failed", "unverified" })
{
    var incomplete = new AttestationHarness();
    incomplete.Calculation = incomplete.Calculation with
        { State = state, Lines = [], Issues = [new("calculation_incomplete")] };
    await FailsService(incomplete, () => incomplete.Inputs.PreviewAsync(incomplete.Employee, incomplete.Month, incomplete.Context, ct),
        "payroll_input_contract_invalid");
    Check(incomplete.Sources.Count == 0 && incomplete.Attestations.Rows.Count == 0,
        "Incomplete, failed or unverified calculations cannot create no-charge financial evidence");
}

var emptyAuthority = new AttestationHarness(); emptyAuthority.Calculation = emptyAuthority.Calculation with { Lines = [] };
var emptyAuthoritySaved = await emptyAuthority.SaveSource("empty-authority-source");
var emptyAuthorityRequest = emptyAuthority.Request(emptyAuthoritySaved, "handoff", null);
emptyAuthority.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Attest);
await FailsService(emptyAuthority, () => emptyAuthority.Create(emptyAuthoritySaved, emptyAuthorityRequest, "empty-unassigned-finance"), "access_denied", 403);
emptyAuthority.Grant(AttendancePayrollAttestationPermissions.Attest, emptyAuthority.Site);
emptyAuthority.Identity = emptyAuthority.Identity with { MfaSatisfied = false };
await FailsService(emptyAuthority, () => emptyAuthority.Create(emptyAuthoritySaved, emptyAuthorityRequest, "empty-no-mfa"), "access_denied", 403);
emptyAuthority.Identity = emptyAuthority.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await FailsService(emptyAuthority, () => emptyAuthority.Create(emptyAuthoritySaved, emptyAuthorityRequest, "empty-mobile"), "access_denied", 403);
emptyAuthority.Identity = emptyAuthority.Identity with { Client = SessionClient.Web };
var emptyAuthorized = await emptyAuthority.Create(emptyAuthoritySaved, emptyAuthorityRequest, "empty-authorized");
emptyAuthority.SessionCurrent = false;
await FailsService(emptyAuthority, () => emptyAuthority.Create(emptyAuthoritySaved, emptyAuthorityRequest, "empty-authorized"), "invalid_authentication", 401);
emptyAuthority.SessionCurrent = true;
emptyAuthority.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Attest);
await FailsService(emptyAuthority, () => emptyAuthority.Create(emptyAuthoritySaved, emptyAuthorityRequest, "empty-authorized"), "access_denied", 403);
Check(emptyAuthority.Attestations.Rows.Single().Id == emptyAuthorized.Id, "Denied no-charge replay preserves exact reviewable record");

var emptyCorrupt = new AttestationHarness(); emptyCorrupt.Calculation = emptyCorrupt.Calculation with { Lines = [] };
var emptyCorruptSaved = await emptyCorrupt.SaveSource("empty-corrupt-source");
emptyCorrupt.Files.Corrupt(emptyCorrupt.Sources[0].ProtectedEvidenceReference, json => json["Calculation"]!["State"] = "unavailable");
await FailsService(emptyCorrupt, () => emptyCorrupt.Create(emptyCorruptSaved, emptyCorrupt.Request(emptyCorruptSaved, "handoff", null), "empty-corrupt-handoff"), "payroll_input_evidence_invalid");

var receiptReuse = new AttestationHarness(); receiptReuse.Calculation = receiptReuse.Calculation with { Lines = [] };
var receiptReuseSaved = await receiptReuse.SaveSource("receipt-reuse-empty-source");
var receiptReuseRequest = receiptReuse.Request(receiptReuseSaved, "handoff", null);
// Global receipt lookup includes a different employee's actual service-produced handoff.
receiptReuse.Attestations.Rows.Add(h.Attestations.Rows.Single(x => x.Id == serviceHandoff.Id));
var privateObjectsBeforeReuse = receiptReuse.Files.Objects.Count;
await FailsService(receiptReuse, () => receiptReuse.Create(receiptReuseSaved, receiptReuseRequest with
    { ExternalSystem = " " + serviceHandoff.ExternalSystem.ToLowerInvariant() + " ",
      ExternalReference = " " + serviceHandoff.ExternalReference + " " }, "cross-employee-receipt"),
    "payroll_attestation_receipt_reused");
Check(receiptReuse.Files.Objects.Count == privateObjectsBeforeReuse &&
    !receiptReuse.Audits.Any(x => x.EventType == "attendance.payroll-attestations.create"),
    "Denied cross-employee receipt reuse cannot create private financial evidence or a success audit");
var uniqueReceiptHandoff = await receiptReuse.Create(receiptReuseSaved, receiptReuseRequest, "cross-employee-receipt");
Check(uniqueReceiptHandoff.EmployeeId == receiptReuse.Employee && uniqueReceiptHandoff.SourceRevisionId == receiptReuseSaved.Revision.Id &&
    uniqueReceiptHandoff.Lines.Count == 0 && !uniqueReceiptHandoff.ConfirmsPosting && !uniqueReceiptHandoff.PayrollReady &&
    !uniqueReceiptHandoff.PaymentInstruction && receiptReuse.AttestationReceipts.Count == 1 &&
    (await receiptReuse.Service.HistoryAsync(receiptReuse.Employee, receiptReuse.Month, receiptReuseSaved.Revision.Id, receiptReuse.Context, ct))
        .Single().Id == uniqueReceiptHandoff.Id,
    "A unique employee receipt can use the denied key without inheriting another employee's financial chain");

var authority = new AttestationHarness(); var authoritySaved = await authority.SaveSource("authority-source-save");
authority.Identity = authority.Identity with { MfaSatisfied = false };
await FailsService(authority, () => authority.Create(authoritySaved, authority.Request(authoritySaved, "handoff", null), "mfa-denied-handoff"), "access_denied", 403);
authority.Identity = authority.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await FailsService(authority, () => authority.Create(authoritySaved, authority.Request(authoritySaved, "handoff", null), "mobile-denied-handoff"), "access_denied", 403);
authority.Identity = authority.Identity with { Client = SessionClient.Web }; authority.SessionCurrent = false;
await FailsService(authority, () => authority.Create(authoritySaved, authority.Request(authoritySaved, "handoff", null), "revoked-session-handoff"), "invalid_authentication", 401);
authority.SessionCurrent = true;
authority.AfterCommit = () => authority.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Attest);
await FailsService(authority, () => authority.Create(authoritySaved, authority.Request(authoritySaved, "handoff", null), "permission-revoked-between"), "access_denied", 403);

var scopes = new AttestationHarness(); var scopeSaved = await scopes.SaveSource("scoped-source-save");
var currentSite = Guid.NewGuid(); scopes.TransferTo(currentSite);
var scopeRequest = scopes.Request(scopeSaved, "handoff", null);
var scopedHandoff = await scopes.Create(scopeSaved, scopeRequest, "scoped-handoff-key");
scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Attest && x.TargetId == scopes.Site);
Check(AttendanceScopeEvaluation.Allows(scopes.Identity, scopes.Grants, AttendancePayrollAttestationPermissions.Attest, scopes.CurrentScopes),
    "Original-scope denial occurs while current scopes remain allowed");
await FailsService(scopes, () => scopes.Create(scopeSaved, scopeRequest, "scoped-handoff-key"), "access_denied", 403);
scopes.Grant(AttendancePayrollAttestationPermissions.Attest, scopes.Site);
scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Attest && x.TargetId == currentSite);
await FailsService(scopes, () => scopes.Create(scopeSaved, scopeRequest, "scoped-handoff-key"), "access_denied", 403);
scopes.Grant(AttendancePayrollAttestationPermissions.Attest, currentSite);
scopes.AfterCommit = () => scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Attest && x.TargetId == scopes.Site);
await FailsService(scopes, () => scopes.Create(scopeSaved, scopes.Request(scopeSaved, "posted", scopedHandoff.Id), "original-revoked-between"), "access_denied", 403);
scopes.Grant(AttendancePayrollAttestationPermissions.Attest, scopes.Site);
scopes.AfterCommit = () => scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Attest && x.TargetId == currentSite);
await FailsService(scopes, () => scopes.Create(scopeSaved, scopes.Request(scopeSaved, "posted", scopedHandoff.Id), "current-revoked-between"), "access_denied", 403);
scopes.Grant(AttendancePayrollAttestationPermissions.Attest, currentSite);
scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollInputPermissions.Read && x.TargetId == scopes.Site);
await FailsService(scopes, () => scopes.Service.HistoryAsync(scopes.Employee, scopes.Month, scopeSaved.Revision.Id, scopes.Context, ct), "access_denied", 403);
scopes.Grant(AttendancePayrollInputPermissions.Read, scopes.Site);
scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollInputPermissions.Read && x.TargetId == currentSite);
await FailsService(scopes, () => scopes.Service.HistoryAsync(scopes.Employee, scopes.Month, scopeSaved.Revision.Id, scopes.Context, ct), "access_denied", 403);
scopes.Grant(AttendancePayrollInputPermissions.Read, currentSite);
scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Read && x.TargetId == scopes.Site);
await FailsService(scopes, () => scopes.Service.ReadConfirmedPostingAsync(scopes.Employee, scopes.Month, scopeSaved.Revision.Id, scopes.Context, ct), "access_denied", 403);
scopes.Grant(AttendancePayrollAttestationPermissions.Read, scopes.Site);
scopes.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Read && x.TargetId == currentSite);
await FailsService(scopes, () => scopes.Service.ReadConfirmedPostingAsync(scopes.Employee, scopes.Month, scopeSaved.Revision.Id, scopes.Context, ct), "access_denied", 403);

var atomic = new AttestationHarness(); var atomicSaved = await atomic.SaveSource("atomic-source-save");
atomic.AfterCommit = () => atomic.FailNextSave = true;
await FailsService(atomic, () => atomic.Create(atomicSaved, atomic.Request(atomicSaved, "handoff", null), "commit-fails-handoff"), "synthetic_commit_failure", 503);
Check(!atomic.Audits.Any(x => x.EventType == "attendance.payroll-attestations.create"), "Failed commit cannot retain financial audit/receipt/business rows");
await atomic.Create(atomicSaved, atomic.Request(atomicSaved, "handoff", null), "commit-fails-handoff");
Check(atomic.Attestations.Rows.Count == 1 && atomic.AttestationReceipts.Count == 1, "Same key can succeed after rolled-back synthetic commit");

var guard = new NoAttestationConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=attestation.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var model = db.GetService<IDesignTimeModel>().Model;
var entity = model.FindEntityType(typeof(AttendancePayrollAttestation))!;
Check(entity.GetTableName() == "AttendancePayrollAttestations" && entity.GetSchema() == "attendance", "Dedicated attendance table");
foreach (var names in new[] { new[] { "EmployeeId", "PeriodStart", "Step" }, new[] { "ExternalReceiptKey", "Step" } })
    Check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(names)), "Uniqueness prevents duplicate stage/receipt");
Check(entity.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "No cascade evidence deletion");
Check(entity.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(AttendancePayrollInputRevision) && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "PeriodStart", "SourceRevisionId" })), "Source FK binds employee/month/revision");
Check(entity.FindProperty("Amount") is null && entity.FindProperty("ExternalReference") is null, "Financial receipt body stays private");
var serviceFinalRow = h.Attestations.Rows.Single(x => x.Id == serviceSettled.Id);
db.Attach(serviceFinalRow); db.Remove(serviceFinalRow);
try { db.SaveChanges(); throw new Exception("Deletion accepted"); }
catch (InvalidOperationException e) when (e.Message.Contains("append-only", StringComparison.Ordinal)) { count++; }
Check(guard.Attempts == 0, "Deletion rejected before any SQL connection");
db.ChangeTracker.Clear(); db.Attach(serviceFinalRow);
db.Entry(serviceFinalRow).Property(x => x.ProtectedEvidenceReference).CurrentValue = "replacement";
try { db.SaveChanges(); throw new Exception("Evidence replacement accepted"); }
catch (InvalidOperationException e) when (e.Message.Contains("append-only", StringComparison.Ordinal)) { count++; }
Check(guard.Attempts == 0, "Update rejected before any SQL connection");
Console.WriteLine($"PASS: {count} payroll attestation rules, real-service, operation/authorization/replay/evidence and offline-model checks. Synthetic IO only; no SQL/network or live identity acceptance.");

public sealed class NoAttestationConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("Database connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database connections forbidden"); }
}

public sealed class AttestationHarness
{
    public Guid Employee { get; } = Guid.NewGuid();
    public Guid Site { get; } = Guid.NewGuid();
    public DateOnly Month { get; } = new(2026, 9, 1);
    public DateTimeOffset Now { get; } = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public AuthRequestContext Context { get; } = new("127.0.0.1", "synthetic-attestation-regression");
    public bool SessionCurrent = true, FailNextSave;
    public int TransactionDepth, MaximumTransactionDepth, Calculations;
    public Action? AfterCommit;
    public List<PermissionGrant> Grants { get; } = [];
    public List<AttendancePayrollInputRevision> Sources { get; } = [];
    public List<AuditEvent> Audits { get; } = [];
    public List<IdempotencyReceipt> Receipts { get; } = [];
    public List<IdempotencyReceipt> AttestationReceipts => Receipts.Where(x => x.Operation == "attendance.payroll-attestations.create").ToList();
    public AttestationMemoryFiles Files { get; } = new();
    public AttestationMemoryRepository Attestations { get; }
    public AttendancePayrollInputCalculation Calculation;
    public IReadOnlyList<AttendanceResourceScope> CurrentScopes;
    public AttendancePayrollInputService Inputs { get; }
    public AttendancePayrollAttestationService Service { get; }
    public AttendanceOperation Operation { get; }
    public IAttendancePayrollInputLedger Ledger { get; }
    public static IReadOnlyList<string> Permissions { get; } = [AttendancePayrollInputPermissions.Read,
        AttendancePayrollInputPermissions.Calculate, AttendancePayrollAttestationPermissions.Read, AttendancePayrollAttestationPermissions.Attest];

    public AttestationHarness()
    {
        foreach (var permission in Permissions) Grant(permission, Site);
        CurrentScopes = [new([new(Employee, SiteId: Site)])];
        var line = new AttendancePayrollInputLine("ordinary:2026-09-01", "ordinary_shortfall", Month, [Month],
            [Guid.NewGuid()], [Guid.NewGuid()], [new string('C', 64)], Guid.NewGuid(), new string('D', 64),
            AttendanceDeductionSalaryBasis.BaseSalary, 31000m, 30, 0.12525m, 100, 1000,
            "501", "4", 125.25m, "PKR", AttendanceDeductionRounding.TwoDecimalPlacesHalfUp, CandidateAmount: 125.25m);
        Calculation = new(Employee, Month, Month.AddMonths(1), Month, Month.AddMonths(1).AddDays(-1), "ready", new string('A', 64),
            [line, line with { Key = "zero:2026-09-02", Amount = 0m, CandidateAmount = 0m, ExactAmountNumerator = "0", ExactAmountDenominator = "1" }], [],
            new(1, Employee, Month, Month.AddMonths(1), Month, Month.AddMonths(1).AddDays(-1), AttendancePayrollInputRules.Version,
                new string('E', 64), CurrentScopes, []));
        var current = AttestationProxy.Make<ICurrentAuthentication>((method, _) => method.Name == "Require" ? Identity : throw Unexpected(method));
        var access = AttestationProxy.Make<IAccountAccessReader>((method, args) =>
        {
            Inside();
            if (method.Name != "GetGrantsAsync" || (Guid)args[0]! != Identity.AccountId) throw Unexpected(method);
            return Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray());
        });
        var authorization = AttestationProxy.Make<IAccessManagementRepository>((method, args) => method.Name switch
        {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(this, args),
            "GetAssignmentRoleCodeAsync" => Role((Guid)args[0]!),
            _ => throw Unexpected(method)
        });
        var operation = new AttendanceOperation(current, access, authorization, new AttestationMemoryJournal(this),
            AttestationProxy.Make<IAuthenticationRepository>((method, _) => throw Unexpected(method)), new AttestationClock(Now), Files);
        Operation = operation;
        var producer = AttestationProxy.Make<IAttendancePayrollInputProducer>((method, args) =>
        {
            Inside(); Match((Guid)args[0]!, (DateOnly)args[1]!);
            if (method.Name == "ResourceScopesAsync") return Task.FromResult(CurrentScopes);
            if (method.Name == "CalculateAsync") { Calculations++; return Task.FromResult(Calculation); }
            throw Unexpected(method);
        });
        var ledger = new AttestationMemorySourceLedger(this);
        Ledger = ledger;
        var jurisdictions = AttestationProxy.Make<IAttendancePayrollJurisdictionReader>((method, args) =>
        {
            Inside();
            if (method.Name != "ReadAsync" || ((IReadOnlyList<AttendancePayrollJurisdictionDay>)args[0]!).Count != 0) throw Unexpected(method);
            return Task.FromResult<IReadOnlyList<AttendancePayrollJurisdictionContext>>([]);
        });
        Inputs = new(operation, producer, ledger, jurisdictions, Files, access);
        Attestations = new(this);
        Service = new(operation, Inputs, ledger, producer, Attestations, Files, access);
    }
    public void Grant(string permission, Guid site) => Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.Site, site));
    public AttendanceMonthLockFinancialReader FinancialReader() => new(Inputs, Service, Ledger, Attestations);
    public Task<T> InRead<T>(Func<AttendanceActor, CancellationToken, Task<T>> action) => Operation.ReadAsync(
        AttendancePayrollInputPermissions.Read, "synthetic.financial-dependency.inspect", Employee, Context, action,
        CancellationToken.None, resourceScopes: (_, _) => Task.FromResult(CurrentScopes));
    public Task InRead(Func<AttendanceActor, CancellationToken, Task> action) => InRead(async (actor, token) =>
    { await action(actor, token); return true; });
    public void TransferTo(Guid site)
    {
        CurrentScopes = [new([new(Employee, SiteId: site)])];
        foreach (var permission in Permissions) Grant(permission, site);
    }
    public async Task<AttendancePayrollInputDetail> SaveSource(string key)
    {
        var preview = await Inputs.PreviewAsync(Employee, Month, Context, CancellationToken.None);
        return await Inputs.SaveAsync(Employee, Month, new(preview.PreviewFingerprint, preview.ExpectedRevision,
            "Synthetic source saved through actual payroll input service"), key, Context, CancellationToken.None);
    }
    public CreateAttendancePayrollAttestationRequest Request(AttendancePayrollInputDetail saved, string status, Guid? previous) =>
        new(saved.Revision.Revision, saved.Revision.InputFingerprint, previous, status, Now, status == "handoff" ? null : Now,
            saved.Inputs.Lines.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency)).ToArray(),
            "SYNTHETIC-PAYROLL", $"employee/{Employee:N}/month/{Month:yyyy-MM}", "synthetic-document-identifier",
            new string('F', 64), "Explicit synthetic manual assertion; no external integration", "Synthetic regression evidence");
    public Task<AttendancePayrollAttestationResponse> Create(AttendancePayrollInputDetail saved,
        CreateAttendancePayrollAttestationRequest request, string key) =>
        Service.CreateAsync(Employee, Month, saved.Revision.Id, request, key, Context, CancellationToken.None);
    public void Inside() { if (TransactionDepth != 1) throw new Exception("Repository authority must execute inside exactly one transaction"); }
    public void Match(Guid employee, DateOnly month)
    { if (employee != Employee || month != Month) throw new Exception("Repository call escaped exact employee/month"); }
    private Task<string?> Role(Guid assignment)
    {
        Inside();
        return Task.FromResult<string?>(Grants.Any(x => x.RoleAssignmentId == assignment) ? "SyntheticOwnerAssignedFinance" : null);
    }
    private static Exception Unexpected(MethodInfo method) => new("Unexpected adapter method: " + method.Name);
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (TransactionDepth != 0) throw new Exception("Nested AttendanceOperation transaction forbidden");
        var sourceBefore = Sources.ToArray(); var rowBefore = Attestations.Rows.ToArray();
        var auditBefore = Audits.ToArray(); var receiptBefore = Receipts.ToArray();
        TransactionDepth++; MaximumTransactionDepth = Math.Max(MaximumTransactionDepth, TransactionDepth);
        T result;
        try { result = await action(ct); }
        catch
        {
            Restore(Sources, sourceBefore); Restore(Attestations.Rows, rowBefore);
            Restore(Audits, auditBefore); Restore(Receipts, receiptBefore); throw;
        }
        finally { TransactionDepth--; }
        var callback = AfterCommit; AfterCommit = null; callback?.Invoke();
        return result;
    }
    private static void Restore<T>(List<T> target, T[] values) { target.Clear(); target.AddRange(values); }
}

public sealed class AttestationMemorySourceLedger(AttestationHarness h) : IAttendancePayrollInputLedger
{
    public Task<bool> IsLockedAsync(DateOnly monthStart, CancellationToken ct) { h.Inside(); h.Match(h.Employee, monthStart); return Task.FromResult(false); }
    public Task<AttendancePayrollInputRevision?> LatestAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct)
    { h.Inside(); h.Match(employeeId, monthStart); return Task.FromResult(h.Sources.OrderByDescending(x => x.Revision).FirstOrDefault()); }
    public Task<AttendancePayrollInputRevision?> FindAsync(Guid employeeId, Guid id, CancellationToken ct)
    { h.Inside(); return Task.FromResult(h.Sources.SingleOrDefault(x => x.EmployeeId == employeeId && x.Id == id)); }
    public Task<PagedResult<AttendancePayrollInputRevision>> HistoryAsync(Guid employeeId, DateOnly monthStart, int page, int pageSize, CancellationToken ct)
    {
        h.Inside(); h.Match(employeeId, monthStart);
        return Task.FromResult(new PagedResult<AttendancePayrollInputRevision>(h.Sources.OrderByDescending(x => x.Revision)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArray(), h.Sources.Count, page, pageSize));
    }
    public void Add(AttendancePayrollInputRevision revision)
    {
        h.Inside(); h.Match(revision.EmployeeId, revision.PeriodStart);
        if (h.Sources.Any(x => x.Revision == revision.Revision)) throw new Exception("Duplicate source revision");
        h.Sources.Add(revision);
    }
}

public sealed class AttestationMemoryRepository(AttestationHarness h) : IAttendancePayrollAttestationRepository
{
    public List<AttendancePayrollAttestation> Rows { get; } = [];
    public Task<IReadOnlyList<AttendancePayrollAttestation>> HistoryAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct)
    {
        h.Inside(); h.Match(employeeId, monthStart);
        return Task.FromResult<IReadOnlyList<AttendancePayrollAttestation>>(Rows.Where(x => x.EmployeeId == employeeId && x.PeriodStart == monthStart)
            .OrderBy(x => x.Step).Take(4).ToArray());
    }
    public Task<bool> ReceiptExistsAsync(string externalReceiptKey, int step, CancellationToken ct)
    { h.Inside(); return Task.FromResult(Rows.Any(x => x.ExternalReceiptKey == externalReceiptKey && x.Step == step)); }
    public void Add(AttendancePayrollAttestation row)
    {
        h.Inside(); h.Match(row.EmployeeId, row.PeriodStart);
        if (Rows.Any(x => x.Step == row.Step && (x.EmployeeId == row.EmployeeId && x.PeriodStart == row.PeriodStart || x.ExternalReceiptKey == row.ExternalReceiptKey)))
            throw new AuthenticationFailure("access_write_conflict", 409);
        Rows.Add(row);
    }
}

public sealed class AttestationMemoryJournal(AttestationHarness h) : ISchedulingJournalRepository
{
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor, CancellationToken ct)
    { h.Inside(); return Task.FromResult(h.SessionCurrent && actor == h.Identity); }
    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct)
    { h.Inside(); return Task.FromResult(h.Receipts.SingleOrDefault(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key)); }
    public void AddAudit(AuditEvent audit) { h.Inside(); h.Audits.Add(audit); }
    public void AddReceipt(IdempotencyReceipt receipt)
    {
        h.Inside();
        if (h.Receipts.Any(x => x.ActorUserId == receipt.ActorUserId && x.Operation == receipt.Operation && x.Key == receipt.Key))
            throw new Exception("Duplicate idempotency receipt");
        h.Receipts.Add(receipt);
    }
    public Task SaveChangesAsync(CancellationToken ct)
    {
        h.Inside();
        if (h.FailNextSave) { h.FailNextSave = false; throw new AuthenticationFailure("synthetic_commit_failure", 503); }
        return Task.CompletedTask;
    }
}

public sealed class AttestationMemoryFiles : IPrivateFileStore
{
    public Dictionary<string, (string Purpose, byte[] Bytes)> Objects { get; } = [];
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N"); Objects.Add(key, (purpose, content.ToArray()));
        return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(content)), "synthetic-memory", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        if (!Objects.TryGetValue(reference.StorageKey, out var value) || value.Purpose != purpose)
            throw new AuthenticationFailure("private_storage_unavailable", 503);
        // Deliberate corruption injection reaches the services' own source/receipt integrity checks.
        return Task.FromResult(value.Bytes.ToArray());
    }
    public byte[] Bytes(string reference) => Objects[JsonSerializer.Deserialize<PrivateFileReference>(reference)!.StorageKey].Bytes.ToArray();
    public JsonNode Json(string reference) => JsonNode.Parse(Bytes(reference))!;
    public void Corrupt(string reference, Action<JsonNode> mutate)
    { var json = Json(reference); mutate(json); Restore(reference, JsonSerializer.SerializeToUtf8Bytes(json)); }
    public void Restore(string reference, byte[] bytes)
    {
        var key = JsonSerializer.Deserialize<PrivateFileReference>(reference)!.StorageKey;
        Objects[key] = (Objects[key].Purpose, bytes.ToArray());
    }
}

public class AttestationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var instance = Create<T, AttestationProxy>(); ((AttestationProxy)(object)instance).Handler = handler; return instance; }
}
public sealed class AttestationClock(DateTimeOffset now) : TimeProvider
{ public override DateTimeOffset GetUtcNow() => now; }
