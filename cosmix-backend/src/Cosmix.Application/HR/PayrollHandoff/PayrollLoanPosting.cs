using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Application.HR.PayrollHandoff;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record ConfirmPayrollLoanPostingRequest(
    [property: JsonRequired] Guid EmployeeId,
    [property: JsonRequired] Guid AgreementId,
    [property: JsonRequired] Guid ScheduleEntryId,
    [property: JsonRequired, Range(1, int.MaxValue)] int SourceVersion,
    [property: JsonRequired, Range(typeof(decimal), "0.01", "10000000")] decimal PostedAmount,
    [property: JsonRequired, Required] string Currency,
    [property: JsonRequired] DateTimeOffset PostedAtUtc,
    [property: JsonRequired, Required, StringLength(100)] string ExternalSystem,
    [property: JsonRequired, Required, StringLength(200)] string ExternalReference,
    [property: JsonRequired, Required, StringLength(200)] string ExternalLineReference,
    [property: JsonRequired, Required, StringLength(500)] string EvidenceReference,
    [property: JsonRequired, Required, StringLength(64, MinimumLength = 64)] string EvidenceSha256,
    [property: JsonRequired, Required, StringLength(4000)] string Statement,
    [property: JsonRequired, Required, StringLength(1000)] string Reason);

public sealed record PayrollLoanPostingResponse(Guid LedgerEntryId, Guid BatchId, Guid HandoffLineId,
    Guid EmployeeId, Guid AgreementId, Guid ScheduleEntryId, int SourceVersion,
    DateOnly PayrollMonth, decimal Amount, string Currency, DateTimeOffset PostedAtUtc,
    string ExternalSystem, string ExternalReference, string ExternalLineReference,
    string EvidenceReference, string EvidenceSha256, Guid ActorUserId, DateTimeOffset RecordedAtUtc,
    string Status = "posted", string VerificationMode = "manual_authorized_attestation",
    bool PaymentInstruction = false);

public sealed partial class PayrollHandoffService
{
    public Task<PayrollLoanPostingResponse> LoanPostingAsync(Guid batchId, Guid lineId,
        AuthRequestContext context, CancellationToken ct)
    {
        if (batchId == Guid.Empty || lineId == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(AttendancePayrollAttestationPermissions.Read,
            "attendance.payroll-handoff.loan-posting.read", null, context, async (actor, token) =>
            {
                var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token);
                if (!AccessPermissionEvaluator.HasAllScope(actor.Identity, grants, PayrollHandoffPermissions.Read) ||
                    !AccessPermissionEvaluator.HasAllScope(actor.Identity, grants, AdvanceLoanPermissions.Review))
                    throw AttendanceOperation.Denied();
                var loanRepository = loans ?? throw AttendanceOperation.Conflict("payroll_loan_posting_unavailable");
                var batch = await repository.FindBatchAsync(batchId, false, token)
                    ?? throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                var line = batch.Lines.SingleOrDefault(x => x.Id == lineId)
                    ?? throw AttendanceOperation.Missing("payroll_handoff_line_not_found");
                if (batch.Id != batchId || line.Component != PayrollComponentKind.LoanRepayment || line.SourceType != LoanSourceType)
                    throw AttendanceOperation.Conflict("payroll_loan_posting_source_conflict");
                var agreementId = await repository.FindAgreementIdForScheduleAsync(line.SourceId, token)
                    ?? throw AttendanceOperation.Conflict("payroll_loan_posting_agreement_conflict");
                var rows = (await loanRepository.ListLedgerEntriesAsync([agreementId], token))
                    .Where(x => x.SourceReference == $"payroll-line:{lineId:N}").ToArray();
                if (rows.Length == 0) throw AttendanceOperation.Missing("payroll_loan_posting_not_found");
                if (rows.Length != 1) throw AttendanceOperation.Conflict("payroll_loan_posting_ledger_conflict");
                var row = rows[0];
                var audit = await repository.LoanPostingAuditAsync(row.Id, token)
                    ?? throw AttendanceOperation.Conflict("payroll_loan_posting_evidence_missing");
                if (audit.EventType != "attendance.payroll-handoff.loan-posting" || audit.TargetType != "Attendance" ||
                    audit.TargetId != row.Id.ToString() || audit.ActorUserId != row.RecordedByUserId ||
                    !Guid.TryParse(audit.ActorUserId, out var originalActor) || audit.ProtectedChangesReference is null)
                    throw AttendanceOperation.Conflict("payroll_loan_posting_audit_conflict");
                var saved = await operation.ReadPrivateAsync<LoanPostingAudit>(audit.ProtectedChangesReference,
                    $"attendance:operation:{originalActor:N}:attendance.payroll-handoff.loan-posting:audit", token);
                var result = saved.After;
                if (saved.ResourceId != row.Id || result is null || result.LedgerEntryId != row.Id || result.BatchId != batchId ||
                    result.HandoffLineId != lineId || result.AgreementId != agreementId || row.AdvanceLoanAgreementId != agreementId ||
                    result.ScheduleEntryId != row.RepaymentScheduleEntryId || result.Amount != row.Amount || result.Currency != row.Currency ||
                    result.ActorUserId != originalActor || result.RecordedAtUtc != row.RecordedAt || result.PayrollMonth != batch.PeriodStart ||
                    row.Kind != RepaymentEntryKind.PayrollDeduction || result.Status != "posted" || result.PaymentInstruction ||
                    result.VerificationMode != "manual_authorized_attestation" ||
                    result.PostedAtUtc.Offset != TimeSpan.Zero || result.PostedAtUtc == default ||
                    row.ValueDate != DateOnly.FromDateTime(result.PostedAtUtc.ToOffset(PakistanOffset).DateTime) ||
                    !AttendancePayrollAttestationRules.Hash(result.EvidenceSha256) ||
                    !AttendancePayrollAttestationRules.Text(result.EvidenceReference, 500) ||
                    !AttendancePayrollAttestationRules.Text(result.ExternalSystem, 100) ||
                    !AttendancePayrollAttestationRules.Text(result.ExternalReference, 200) ||
                    !AttendancePayrollAttestationRules.Text(result.ExternalLineReference, 200) ||
                    row.IdempotencyKey != PostingReceiptKey(result.ExternalSystem, result.ExternalReference, result.ExternalLineReference))
                    throw AttendanceOperation.Conflict("payroll_loan_posting_evidence_conflict");
                var request = new ConfirmPayrollLoanPostingRequest(result.EmployeeId, result.AgreementId, result.ScheduleEntryId,
                    result.SourceVersion, result.Amount, result.Currency, result.PostedAtUtc, result.ExternalSystem,
                    result.ExternalReference, result.ExternalLineReference, result.EvidenceReference, result.EvidenceSha256,
                    "Retained posting read", "Retained posting read");
                _ = await BindLoanPostingAsync(batchId, lineId, request, actor, token, writing: false);
                return result;
            }, ct);
    }

    public Task<PayrollLoanPostingResponse> ConfirmLoanPostingAsync(Guid batchId, Guid lineId,
        ConfirmPayrollLoanPostingRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (batchId == Guid.Empty || lineId == Guid.Empty || request is null ||
            request.EmployeeId == Guid.Empty || request.AgreementId == Guid.Empty || request.ScheduleEntryId == Guid.Empty ||
            request.SourceVersion < 1 || request.PostedAmount is <= 0 or > 10_000_000 ||
            decimal.Round(request.PostedAmount, 2) != request.PostedAmount || request.Currency != Currency ||
            request.PostedAtUtc == default || request.PostedAtUtc.Offset != TimeSpan.Zero ||
            !AttendancePayrollAttestationRules.Text(request.ExternalSystem, 100) ||
            !AttendancePayrollAttestationRules.Text(request.ExternalReference, 200) ||
            !AttendancePayrollAttestationRules.Text(request.ExternalLineReference, 200) ||
            !AttendancePayrollAttestationRules.Text(request.EvidenceReference, 500) ||
            !AttendancePayrollAttestationRules.Hash(request.EvidenceSha256) ||
            !AttendancePayrollAttestationRules.Text(request.Statement, 4000))
            throw AttendanceOperation.Invalid("invalid_payroll_loan_posting");
        BoundLoanPosting? bound = null;
        return operation.WriteAsync(AttendancePayrollAttestationPermissions.Attest,
            "attendance.payroll-handoff.loan-posting", null, key, new { batchId, lineId, request },
            request.Reason, context, async (actor, token) =>
            {
                var source = bound ?? throw AttendanceOperation.Conflict("payroll_loan_posting_source_unavailable");
                var loanRepository = loans ?? throw AttendanceOperation.Conflict("payroll_loan_posting_unavailable");
                var receiptKey = PostingReceiptKey(request.ExternalSystem, request.ExternalReference, request.ExternalLineReference);
                if (await loanRepository.FindLedgerEntryByIdempotencyKeyAsync(receiptKey, token) is not null)
                    throw AttendanceOperation.Conflict("payroll_posting_receipt_reused");
                var entries = await loanRepository.ListLedgerEntriesAsync([source.Agreement.Id], token);
                var sourceReference = $"payroll-line:{lineId:N}";
                if (entries.Any(x => x.SourceReference == sourceReference ||
                    x.RepaymentScheduleEntryId == source.Schedule.Id && x.Kind == RepaymentEntryKind.PayrollDeduction))
                    throw AttendanceOperation.Conflict("payroll_loan_line_already_posted");
                // A partial/manual repayment after preparation needs a revised Accounts decision,
                // never a silent smaller amount or a second charge for the frozen installment.
                if (entries.Sum(x => x.Amount) < 0 ||
                    entries.Where(x => x.RepaymentScheduleEntryId == source.Schedule.Id).Sum(x => x.Amount) != 0 ||
                    entries.Any(x => x.AdvanceLoanAgreementId != source.Agreement.Id || x.Currency != Currency) ||
                    source.Agreement.ApprovedPrincipal - entries.Sum(x => x.Amount) < request.PostedAmount)
                    throw AttendanceOperation.Conflict("payroll_loan_balance_changed");
                var entry = new RepaymentLedgerEntry(source.Agreement.Id, source.Schedule.Id,
                    RepaymentEntryKind.PayrollDeduction, request.PostedAmount, Currency,
                    DateOnly.FromDateTime(request.PostedAtUtc.ToOffset(PakistanOffset).DateTime),
                    sourceReference, receiptKey, null, actor.Identity.AccountId.ToString(), actor.Now);
                loanRepository.AddLedgerEntry(entry);
                // The shared operation commits this row, protected posting evidence, audit and
                // HTTP retry receipt in one existing serializable authorization transaction.
                return new AttendanceChange<PayrollLoanPostingResponse>(entry.Id, null,
                    new(entry.Id, batchId, lineId, source.Line.EmployeeId, source.Agreement.Id, source.Schedule.Id,
                        source.Line.SourceVersion, source.Batch.PeriodStart, entry.Amount, entry.Currency,
                        request.PostedAtUtc, request.ExternalSystem.Trim().ToUpperInvariant(), request.ExternalReference.Trim(),
                        request.ExternalLineReference.Trim(), request.EvidenceReference.Trim(), request.EvidenceSha256,
                        actor.Identity.AccountId, actor.Now));
            }, ct, beforeReplay: async (actor, token) =>
            {
                // Revalidate every extra authority and exact frozen source even on receipt replay.
                bound = await BindLoanPostingAsync(batchId, lineId, request, actor, token);
            });
    }

    private async Task<BoundLoanPosting> BindLoanPostingAsync(Guid batchId, Guid lineId,
        ConfirmPayrollLoanPostingRequest request, AttendanceActor actor, CancellationToken ct, bool writing = true)
    {
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct);
        var permissions = writing
            ? new[] { PayrollHandoffPermissions.Read, PayrollHandoffPermissions.Process, AdvanceLoanPermissions.Repay }
            : new[] { PayrollHandoffPermissions.Read, AdvanceLoanPermissions.Review };
        foreach (var permission in permissions)
            if (!AccessPermissionEvaluator.HasAllScope(actor.Identity, grants, permission)) throw AttendanceOperation.Denied();
        if (loans is null) throw AttendanceOperation.Conflict("payroll_loan_posting_unavailable");
        var batch = await repository.FindBatchAsync(batchId, false, ct)
            ?? throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
        if (batch.Id != batchId || batch.LockedAt is null || batch.ApprovalRequestId is null || batch.Currency != Currency ||
            request.PostedAtUtc < batch.LockedAt || request.PostedAtUtc > actor.Now)
            throw AttendanceOperation.Conflict("payroll_loan_posting_batch_not_ready");
        var line = batch.Lines.SingleOrDefault(x => x.Id == lineId)
            ?? throw AttendanceOperation.Missing("payroll_handoff_line_not_found");
        if (line.PayrollHandoffBatchId != batchId || line.Component != PayrollComponentKind.LoanRepayment ||
            line.SourceType != LoanSourceType || line.EmployeeId != request.EmployeeId ||
            line.SourceId != request.ScheduleEntryId || line.SourceVersion != request.SourceVersion ||
            line.Amount >= 0 || -line.Amount != request.PostedAmount || line.Currency != request.Currency)
            throw AttendanceOperation.Conflict("payroll_loan_posting_source_conflict");
        if (writing && line.EmployeeId == actor.Identity.EmployeeId)
            throw new AuthenticationFailure("payroll_loan_self_posting_forbidden", 403);
        var (month, _) = await ReadLockedMonthAsync(batch.PeriodStart, batch.PeriodEnd, actor, ct);
        if (month.Event?.Id != line.AttendancePeriodLockEventId || !month.Summaries.Any(x => x.EmployeeId == line.EmployeeId))
            throw AttendanceOperation.Conflict("payroll_loan_posting_lock_conflict");
        var evidence = await operation.ReadPrivateAsync<PayrollHandoffLineEvidence>(line.CalculationPrivateReference, LinePurpose(batchId), ct);
        if (evidence.SchemaVersion != 1 || evidence.BatchId != batchId || evidence.EmployeeId != line.EmployeeId ||
            evidence.Component != line.Component.ToString() || evidence.SourceType != line.SourceType ||
            evidence.SourceId != line.SourceId || evidence.SourceVersion != line.SourceVersion ||
            evidence.AttendancePeriodLockEventId != line.AttendancePeriodLockEventId || evidence.Amount != line.Amount ||
            evidence.Currency != line.Currency || evidence.PolicyVersionId != line.PolicyVersionId ||
            evidence.ApprovalRequestId != line.ApprovalRequestId || evidence.PreparedAtUtc != batch.PreparedAt ||
            evidence.ActorAccountId.ToString() != batch.PreparedByUserId)
            throw AttendanceOperation.Conflict("payroll_loan_posting_evidence_conflict");
        var agreement = await loans.FindAgreementWithDetailsAsync(request.AgreementId, ct)
            ?? throw AttendanceOperation.Missing("agreement_not_found");
        var schedule = agreement.Schedule.SingleOrDefault(x => x.Id == line.SourceId);
        var approval = agreement.AdvanceLoanRequest?.ApprovalRequest;
        if (agreement.Id != request.AgreementId || agreement.AdvanceLoanRequest?.EmployeeId != line.EmployeeId ||
            agreement.AdvanceLoanRequestId != agreement.AdvanceLoanRequest.Id ||
            approval?.Id != agreement.ApprovalRequestId || approval?.Id != line.ApprovalRequestId ||
            approval?.State != ApprovalState.Approved || approval.RequestType != AdvanceLoanService.RequestType ||
            approval.SubjectId != agreement.AdvanceLoanRequestId || approval.ClosedAt is null || approval.ClosedAt > batch.PreparedAt ||
            schedule is null || schedule.AdvanceLoanAgreementId != agreement.Id || schedule.Sequence != line.SourceVersion ||
            schedule.DueDate < batch.PeriodStart || schedule.DueDate >= batch.PeriodEnd ||
            schedule.Currency != Currency || agreement.Currency != Currency ||
            schedule.PrincipalAmount + schedule.OtherApprovedAmount != request.PostedAmount ||
            agreement.ApprovedTermsPrivateReference != evidence.SourceEvidenceReference)
            throw AttendanceOperation.Conflict("payroll_loan_posting_agreement_conflict");
        if (schedule.OtherApprovedAmount != 0)
            throw AttendanceOperation.Conflict("interest_free_loan_required");
        return new(batch, line, agreement, schedule);
    }

    private sealed record BoundLoanPosting(PayrollHandoffBatch Batch, PayrollHandoffLine Line,
        AdvanceLoanAgreement Agreement, RepaymentScheduleEntry Schedule);
    private sealed record LoanDebit(Guid EmployeeId, Guid ScheduleId, int Sequence, decimal Amount,
        string Currency, Guid? ApprovalId);

    private async Task ValidateFrozenLoanBalancesAsync(PayrollHandoffBatch batch, CancellationToken ct)
    {
        var lines = batch.Lines.Where(x => x.Component == PayrollComponentKind.LoanRepayment).ToArray();
        if (lines.Any(x => x.SourceType != LoanSourceType || x.PayrollHandoffBatchId != batch.Id))
            throw AttendanceOperation.Conflict("payroll_loan_posting_source_conflict");
        var paid = await ValidateLoanDebitsAsync(lines.Select(x => new LoanDebit(x.EmployeeId,
            x.SourceId, x.SourceVersion, -x.Amount, x.Currency, x.ApprovalRequestId)).ToArray(),
            batch.PeriodStart, batch.PeriodEnd, ct);
        // Frozen instructions are never silently resized after a manual repayment.
        if (paid.Count != 0) throw AttendanceOperation.Conflict("payroll_loan_balance_changed");
    }

    private async Task<HashSet<Guid>> ValidateLoanDebitsAsync(IReadOnlyList<LoanDebit> debits,
        DateOnly start, DateOnly end, CancellationToken ct)
    {
        var paid = new HashSet<Guid>();
        if (debits.Count == 0) return paid;
        var loanRepository = loans ?? throw AttendanceOperation.Conflict("payroll_loan_posting_unavailable");
        if (debits.Select(x => x.ScheduleId).Distinct().Count() != debits.Count)
            throw AttendanceOperation.Conflict("payroll_loan_posting_source_conflict");
        var reserved = new Dictionary<Guid, decimal>();
        foreach (var debit in debits)
        {
            var agreementId = await repository.FindAgreementIdForScheduleAsync(debit.ScheduleId, ct)
                ?? throw AttendanceOperation.Conflict("payroll_loan_posting_agreement_conflict");
            var agreement = await loanRepository.FindAgreementWithDetailsAsync(agreementId, ct)
                ?? throw AttendanceOperation.Conflict("payroll_loan_posting_agreement_conflict");
            var schedule = agreement.Schedule.SingleOrDefault(x => x.Id == debit.ScheduleId);
            var request = agreement.AdvanceLoanRequest;
            var approval = request?.ApprovalRequest;
            if (schedule is not null && schedule.OtherApprovedAmount != 0)
                throw AttendanceOperation.Conflict("interest_free_loan_required");
            if (agreement.Id != agreementId || request?.EmployeeId != debit.EmployeeId ||
                request.Id != agreement.AdvanceLoanRequestId || approval?.State != ApprovalState.Approved ||
                approval.Id != agreement.ApprovalRequestId || approval.Id != debit.ApprovalId ||
                approval.SubjectId != request.Id || approval.RequestType != AdvanceLoanService.RequestType ||
                approval.ClosedAt is null || schedule is null || schedule.AdvanceLoanAgreementId != agreement.Id ||
                schedule.Sequence != debit.Sequence || schedule.DueDate < start || schedule.DueDate >= end ||
                debit.Currency != Currency || agreement.Currency != Currency || schedule.Currency != Currency ||
                debit.Amount <= 0 || debit.Amount != schedule.PrincipalAmount)
                throw AttendanceOperation.Conflict("payroll_loan_posting_agreement_conflict");
            var entries = await loanRepository.ListLedgerEntriesAsync([agreement.Id], ct);
            var total = entries.Sum(x => x.Amount);
            if (entries.Any(x => x.AdvanceLoanAgreementId != agreement.Id || x.Currency != Currency) ||
                total < 0 || total > agreement.ApprovedPrincipal)
                throw AttendanceOperation.Conflict("payroll_loan_balance_changed");
            if (entries.Any(x => x.RepaymentScheduleEntryId == schedule.Id && x.Kind == RepaymentEntryKind.PayrollDeduction))
                throw AttendanceOperation.Conflict("payroll_loan_line_already_posted");
            var schedulePaid = entries.Where(x => x.RepaymentScheduleEntryId == schedule.Id).Sum(x => x.Amount);
            if (schedulePaid == schedule.PrincipalAmount)
            {
                paid.Add(schedule.Id);
                continue;
            }
            var amount = reserved.GetValueOrDefault(agreement.Id) + debit.Amount;
            if (schedulePaid != 0 || amount > agreement.ApprovedPrincipal - total)
                throw AttendanceOperation.Conflict("payroll_loan_balance_changed");
            reserved[agreement.Id] = amount;
        }
        return paid;
    }

    private sealed record LoanPostingAudit(Guid ResourceId, PayrollLoanPostingResponse After);
    private static string PostingReceiptKey(string system, string reference, string line) =>
        "payroll-posted:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            System = system.Trim().ToUpperInvariant(), Reference = reference.Trim(), Line = line.Trim()
        })));
}
