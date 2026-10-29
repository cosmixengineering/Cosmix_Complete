using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed partial class PayrollAccountingService
{
    public Task<SalaryPaymentResponse> RequestPaymentReversalAsync(Guid id, RequestSalaryPaymentReversalRequest request, string key,
        AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.payment", "payroll.payment.reversal.request", id.ToString(), key, request, request.Reason, context,
            (actor, token) => RequestPaymentReversalCoreAsync(id, request, actor.AccountId.ToString(), token), ct);

    public Task<SalaryPaymentResponse> ApprovePaymentReversalAsync(Guid id, ApproveSalaryPaymentReversalRequest request, string key,
        AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.payment.confirm", "payroll.payment.reversal.approve", id.ToString(), key, request, request.Reason, context,
            (actor, token) => ApprovePaymentReversalCoreAsync(id, request, actor.AccountId.ToString(), token), ct);

    internal async Task<SalaryPaymentResponse> ConfirmPaymentWithEvidenceCoreAsync(Guid id, ConfirmSalaryPaymentRequest request, string actor, CancellationToken ct)
    {
        var (payment, obligation, journal) = await RequirePaymentAsync(id, request.ExpectedVersion, ct);
        if (payment.State != SalaryPaymentState.Posted || payment.ReversalRequestedAt.HasValue || SameActor(actor, payment.CreatedByUserId))
            throw new PayrollFinanceConflictException("Independent confirmation of an unreversed posted payment is required.");
        await financeSetupService!.RequirePeriodAsync(journal.PostingDate!.Value, obligation.Currency, ct);
        var at = clock.GetUtcNow();
        var proof = await RetainPaymentProofAsync(payment, obligation, "confirmation", actor, at, request.Reason, request.Evidence, ct);
        PayrollFinanceErrors.Run(() => payment.Confirm(actor, at, proof));
        await repository.SaveChangesAsync(ct);
        return MapSalaryPayment(payment);
    }

    internal async Task<SalaryPaymentResponse> RequestPaymentReversalCoreAsync(Guid id, RequestSalaryPaymentReversalRequest request, string actor, CancellationToken ct)
    {
        var (payment, obligation, _) = await RequirePaymentAsync(id, request.ExpectedVersion, ct);
        if (payment.State is not (SalaryPaymentState.Posted or SalaryPaymentState.Confirmed) || payment.ReversalRequestedAt.HasValue ||
            (request.PaymentFailed && payment.State == SalaryPaymentState.Confirmed))
            throw new PayrollFinanceConflictException("This payment cannot request the selected outcome.");
        if (payment.State == SalaryPaymentState.Confirmed)
            await ValidatePaymentProofAsync(payment, obligation, "confirmation", payment.ConfirmationProofReference,
                payment.ConfirmedByUserId!, payment.ConfirmedAt!.Value, ct);
        var at = clock.GetUtcNow();
        var proof = await RetainPaymentProofAsync(payment, obligation, request.PaymentFailed ? "failure-request" : "reversal-request",
            actor, at, request.Reason, request.Evidence, ct);
        PayrollFinanceErrors.Run(() => payment.RequestReversal(actor, at, request.Reason, proof, request.PaymentFailed));
        await repository.SaveChangesAsync(ct);
        return MapSalaryPayment(payment);
    }

    internal async Task<SalaryPaymentResponse> ApprovePaymentReversalCoreAsync(Guid id, ApproveSalaryPaymentReversalRequest request, string actor, CancellationToken ct)
    {
        var (payment, obligation, original) = await RequirePaymentAsync(id, request.ExpectedVersion, ct);
        if (payment.State is not (SalaryPaymentState.Posted or SalaryPaymentState.Confirmed) || !payment.ReversalRequestedAt.HasValue ||
            payment.ReversalJournalEntryId.HasValue || SameActor(actor, payment.CreatedByUserId) || SameActor(actor, payment.ReversalRequestedByUserId))
            throw new PayrollFinanceConflictException("An independent reviewer and outstanding reversal request are required.");
        await ValidatePaymentProofAsync(payment, obligation, payment.ReversalIsFailure ? "failure-request" : "reversal-request",
            payment.ReversalRequestProofReference, payment.ReversalRequestedByUserId!, payment.ReversalRequestedAt.Value, ct);
        if (payment.ConfirmedAt.HasValue)
            await ValidatePaymentProofAsync(payment, obligation, "confirmation", payment.ConfirmationProofReference,
                payment.ConfirmedByUserId!, payment.ConfirmedAt.Value, ct);
        if (request.PostingDate < original.PostingDate) throw new PayrollFinanceConflictException("A reversal cannot precede the original posting date.");
        var period = await financeSetupService!.RequirePeriodAsync(request.PostingDate, obligation.Currency, ct);
        var sourceKey = $"PAYMENT-REVERSAL-{payment.Id:N}";
        if ((await repository.GetJournalEntriesForObligationAsync(obligation.Id, ct)).Any(x => x.SourceKey == sourceKey))
            throw new PayrollFinanceConflictException("A reversal journal already exists for this payment.");
        var at = clock.GetUtcNow();
        // Reverse the exact original accounts and amount, preserving the historical posting forever.
        var reversal = new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryPaymentReversal,
            original.CreditAccountCode, original.DebitAccountCode, original.Amount, original.Currency,
            $"Salary payment reversal {payment.Id:D}", actor, at);
        reversal.BindPosting(request.PostingDate, period.Id, original.PostingProfileId, sourceKey);
        var proof = await RetainPaymentProofAsync(payment, obligation, "reversal-approval", actor, at, request.Reason, request.Evidence, ct,
            reversal.Id, request.PostingDate);
        PayrollFinanceErrors.Run(() => payment.ApproveReversal(actor, at, reversal.Id, proof));
        await repository.AddJournalEntryAsync(reversal, ct);
        await repository.SaveChangesAsync(ct);
        return MapSalaryPayment(payment);
    }

    private async Task<(SalaryPayment Payment, PayrollObligation Obligation, PayrollJournalEntry Journal)> RequirePaymentAsync(Guid id, int expectedVersion, CancellationToken ct)
    {
        var payment = await repository.GetSalaryPaymentByIdAsync(id, ct) ?? throw new PayrollFinanceConflictException("Salary payment not found.");
        await repository.AcquirePaymentLockAsync(payment.ObligationId, payment.EmployeeId ?? Guid.Empty, ct);
        await repository.RefreshSalaryPaymentAsync(payment, ct);
        if (expectedVersion <= 0 || payment.Version != expectedVersion) throw new PayrollFinanceConflictException("Payment version conflict.");
        var obligation = await repository.GetObligationByIdAsync(payment.ObligationId, ct) ?? throw new PayrollFinanceConflictException("Obligation not found.");
        var source = await RequireFrozenAsync(obligation.BatchId, ct);
        ValidateObligationSource(obligation, source);
        var journal = payment.PaymentJournalEntryId.HasValue ? await repository.GetJournalEntryByIdAsync(payment.PaymentJournalEntryId.Value, ct) : null;
        var accrual = await repository.GetJournalEntryByIdAsync(payment.AccrualJournalEntryId, ct);
        if (payment.EmployeeId is null || !source.Lines.Any(x => x.EmployeeId == payment.EmployeeId) || journal is null || accrual is null ||
            accrual.ObligationId != obligation.Id || accrual.Kind != JournalEntryKind.SalaryAccrual || accrual.SourceKey != $"ACCRUAL-SALARY-{obligation.Id:N}" ||
            accrual.Amount != obligation.TotalNetAmount || accrual.Currency != obligation.Currency ||
            journal.Kind != JournalEntryKind.SalaryPayment || journal.ObligationId != obligation.Id || journal.Amount != payment.Amount ||
            journal.Currency != obligation.Currency || journal.DebitAccountCode != accrual.CreditAccountCode || journal.PostingDate is null ||
            !SameActor(journal.PostedByUserId, payment.CreatedByUserId) || journal.PostedAt != payment.CreatedAt ||
            journal.SourceKey != $"PAYMENT-{obligation.Id:N}-{payment.Id:N}")
            throw new PayrollFinanceConflictException("Payment journal does not match its frozen employee obligation.");
        return (payment, obligation, journal);
    }

    private async Task<string> RetainPaymentProofAsync(SalaryPayment payment, PayrollObligation obligation, string action, string actor,
        DateTimeOffset at, string reason, string? evidence, CancellationToken ct, Guid? reversalId = null, DateOnly? postingDate = null)
    {
        ValidateEvidence(reason, evidence);
        if (files is null) throw new InvalidOperationException("Private payment evidence storage is required.");
        var proof = new PaymentProof(1, payment.Id, obligation.Id, payment.EmployeeId!.Value, payment.Amount, obligation.Currency,
            obligation.SourceFingerprint!, payment.PaymentJournalEntryId!.Value, action, actor, at, reason.Trim(), evidence!.Trim(), reversalId, postingDate);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(proof);
        try
        {
            var reference = await files.WriteAsync(bytes, PaymentPurpose(payment.Id, action), ct);
            var retained = await files.ReadAsync(reference, PaymentPurpose(payment.Id, action), ct);
            if (!bytes.AsSpan().SequenceEqual(retained)) throw new PayrollFinanceConflictException("Payment proof storage verification failed.");
            return JsonSerializer.Serialize(reference);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task ValidatePaymentProofAsync(SalaryPayment payment, PayrollObligation obligation, string action, string? encoded,
        string actor, DateTimeOffset at, CancellationToken ct, Guid? reversalJournalId = null, DateOnly? postingDate = null)
    {
        if (files is null || string.IsNullOrWhiteSpace(encoded)) throw new PayrollFinanceConflictException("Payment evidence is unavailable.");
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(encoded) ?? throw new PayrollFinanceConflictException("Payment evidence is unavailable.");
        var proof = JsonSerializer.Deserialize<PaymentProof>(await files.ReadAsync(reference, PaymentPurpose(payment.Id, action), ct));
        if (proof is null || proof.Schema != 1 || proof.PaymentId != payment.Id || proof.ObligationId != obligation.Id || proof.EmployeeId != payment.EmployeeId ||
            proof.Amount != payment.Amount || proof.Currency != obligation.Currency || proof.SourceFingerprint != obligation.SourceFingerprint ||
            proof.PaymentJournalId != payment.PaymentJournalEntryId || proof.Action != action || proof.Actor != actor || proof.At != at ||
            proof.ReversalJournalId != reversalJournalId || proof.PostingDate != postingDate ||
            (action.EndsWith("-request", StringComparison.Ordinal) && proof.Reason != payment.ReversalReason))
            throw new PayrollFinanceConflictException("Payment proof does not match its retained financial source.");
        ValidateEvidence(proof.Reason, proof.Evidence);
    }

    internal async Task ValidatePaymentProofsForReadAsync(SalaryPayment payment, PayrollObligation obligation, CancellationToken ct)
    {
        if (payment.ObligationId != obligation.Id || payment.EmployeeId is null) throw new PayrollFinanceConflictException("Payment employee obligation mismatch.");
        if (payment.PaymentJournalEntryId.HasValue)
        {
            var posted = await repository.GetJournalEntryByIdAsync(payment.PaymentJournalEntryId.Value, ct);
            if (posted is null || !SameActor(posted.PostedByUserId, payment.CreatedByUserId) || posted.PostedAt != payment.CreatedAt ||
                posted.ObligationId != obligation.Id || posted.Amount != payment.Amount || posted.Currency != obligation.Currency ||
                posted.Kind != JournalEntryKind.SalaryPayment || posted.SourceKey != $"PAYMENT-{obligation.Id:N}-{payment.Id:N}")
                throw new PayrollFinanceConflictException("Payment maker, time or amount does not match its append-only journal.");
        }
        if (payment.State == SalaryPaymentState.Confirmed && !payment.ConfirmedAt.HasValue)
            throw new PayrollFinanceConflictException("Confirmed payment evidence is missing.");
        if (payment.ConfirmedAt.HasValue)
        {
            if (payment.State is not (SalaryPaymentState.Confirmed or SalaryPaymentState.Reversed))
                throw new PayrollFinanceConflictException("A retained confirmation cannot be rewritten as an unconfirmed payment.");
            if (string.IsNullOrWhiteSpace(payment.ConfirmedByUserId) || SameActor(payment.ConfirmedByUserId, payment.CreatedByUserId) || payment.ConfirmedAt < payment.CreatedAt)
                throw new PayrollFinanceConflictException("Payment confirmation actor or time is inconsistent.");
            await ValidatePaymentProofAsync(payment, obligation, "confirmation", payment.ConfirmationProofReference, payment.ConfirmedByUserId, payment.ConfirmedAt.Value, ct);
        }
        if (payment.ReversalRequestedAt.HasValue)
        {
            if (string.IsNullOrWhiteSpace(payment.ReversalRequestedByUserId)) throw new PayrollFinanceConflictException("Reversal requester is missing.");
            await ValidatePaymentProofAsync(payment, obligation, payment.ReversalIsFailure ? "failure-request" : "reversal-request",
                payment.ReversalRequestProofReference, payment.ReversalRequestedByUserId, payment.ReversalRequestedAt.Value, ct);
        }
        if (payment.State == SalaryPaymentState.Reversed || (payment.State == SalaryPaymentState.Failed && payment.PaymentJournalEntryId.HasValue) || payment.ReversedAt.HasValue)
        {
            if (!payment.ReversalRequestedAt.HasValue || !payment.ReversedAt.HasValue || !payment.ReversalJournalEntryId.HasValue || string.IsNullOrWhiteSpace(payment.ReversedByUserId) ||
                SameActor(payment.ReversedByUserId, payment.CreatedByUserId) || SameActor(payment.ReversedByUserId, payment.ReversalRequestedByUserId) ||
                payment.ReversedAt < payment.ReversalRequestedAt || payment.State != (payment.ReversalIsFailure ? SalaryPaymentState.Failed : SalaryPaymentState.Reversed) ||
                (payment.ReversalIsFailure && (payment.ConfirmedAt.HasValue || payment.FailureReason != payment.ReversalReason)))
                throw new PayrollFinanceConflictException("Independent retained reversal evidence is missing or inconsistent.");
            var original = payment.PaymentJournalEntryId.HasValue ? await repository.GetJournalEntryByIdAsync(payment.PaymentJournalEntryId.Value, ct) : null;
            var reversal = await repository.GetJournalEntryByIdAsync(payment.ReversalJournalEntryId.Value, ct);
            if (original is null || reversal is null || reversal.Kind != JournalEntryKind.SalaryPaymentReversal || reversal.ObligationId != obligation.Id ||
                reversal.Amount != payment.Amount || reversal.Amount != original.Amount || reversal.Currency != obligation.Currency ||
                reversal.DebitAccountCode != original.CreditAccountCode || reversal.CreditAccountCode != original.DebitAccountCode ||
                reversal.SourceKey != $"PAYMENT-REVERSAL-{payment.Id:N}" || reversal.PostedByUserId != payment.ReversedByUserId ||
                reversal.PostedAt != payment.ReversedAt || reversal.PostingDate is null || reversal.PostingDate < original.PostingDate)
                throw new PayrollFinanceConflictException("Reversal journal is not an exact compensation of the original payment.");
            await ValidatePaymentProofAsync(payment, obligation, "reversal-approval", payment.ReversalApprovalProofReference,
                payment.ReversedByUserId, payment.ReversedAt.Value, ct, reversal.Id, reversal.PostingDate);
        }
    }

    internal static void ValidateEvidence(string reason, string? evidence)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || reason.Any(char.IsControl) || string.IsNullOrWhiteSpace(evidence) ||
            evidence.Length > 8000 || evidence.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new PayrollFinanceConflictException("A reason and retained receipt or statement evidence (up to 8000 characters) are required.");
    }
    private static bool SameActor(string actor, string? other) => string.Equals(actor, other, StringComparison.OrdinalIgnoreCase);
    private static string PaymentPurpose(Guid paymentId, string action) => $"payroll-payment/{paymentId:N}/{action}";
    private sealed record PaymentProof(int Schema, Guid PaymentId, Guid ObligationId, Guid EmployeeId, decimal Amount, string Currency,
        string SourceFingerprint, Guid PaymentJournalId, string Action, string Actor, DateTimeOffset At, string Reason, string Evidence,
        Guid? ReversalJournalId, DateOnly? PostingDate);
}
