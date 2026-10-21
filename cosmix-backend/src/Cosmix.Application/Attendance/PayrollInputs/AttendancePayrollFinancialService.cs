using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.PayrollInputs;

public sealed class AttendancePayrollFinancialService(AttendanceOperation operation,
    IAttendancePayrollFinancialSourceReader sources, IAttendancePayrollFinancialRepository repository,
    IAttendancePayrollInputLedger ledger, IAttendancePayrollInputProducer producer, IPrivateFileStore files,
    IAccountAccessReader access, ICurrentAuthentication current, ISchedulingJournalRepository journal,
    TimeProvider clock, IAttendancePayrollFinancialTargetLockReader? targetLocks = null)
    : IAttendancePayrollFinancialMonthReader
{
    public Task<AttendancePayrollFinancialPreview> PreviewAsync(Guid employeeId, DateOnly sourceMonth,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, sourceMonth);
        return operation.ReadAsync(AttendancePayrollAttestationPermissions.Read,
            "attendance.payroll-financial.preview", employeeId, context, async (actor, token) =>
            {
                var history = await History(employeeId, sourceMonth, actor, false, token);
                var source = await sources.ReadAsync(employeeId, sourceMonth, actor, token);
                await Require(source, actor, false, token);
                return Preview(source, history);
            }, ct, resourceScopes: (_, token) => producer.ResourceScopesAsync(employeeId, sourceMonth, token));
    }

    public Task<AttendancePayrollFinancialResponse> ApproveAsync(Guid employeeId, DateOnly sourceMonth,
        ApproveAttendancePayrollFinancialRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, sourceMonth);
        if (request is null || !AttendancePayrollAttestationRules.Hash(request.ExpectedPreviewFingerprint) ||
            request.ExpectedPreviousAdjustmentId == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.WriteAsync(AttendancePayrollAttestationPermissions.Attest,
            "attendance.payroll-financial.approve", employeeId, key, new { sourceMonth, request }, request.Reason,
            context, async (actor, token) =>
            {
                if (actor.Identity.EmployeeId == employeeId) throw AttendanceOperation.Denied();
                var history = await History(employeeId, sourceMonth, actor, true, token);
                var source = await sources.ReadAsync(employeeId, sourceMonth, actor, token);
                await Require(source, actor, true, token);
                var preview = Preview(source, history);
                if (request.ExpectedPreviewFingerprint != preview.PreviewFingerprint ||
                    request.ExpectedTargetMonth != preview.TargetMonth || request.ExpectedPreviousAdjustmentId != preview.PreviousAdjustmentId)
                    throw AttendanceOperation.Conflict("financial_carry_forward_preview_stale");
                if (await ledger.IsLockedAsync(preview.TargetMonth, token))
                    throw AttendanceOperation.Conflict("financial_carry_forward_target_locked");
                var evidence = new AttendancePayrollFinancialEvidence(1, preview, source,
                    actor.Identity.AccountId, actor.Now, request.Reason.Trim());
                var bytes = Bytes(evidence);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(bytes, Purpose(employeeId, sourceMonth), token));
                // Private IO must not allow a changed source, target, lock or competing approval through.
                var reloaded = await sources.ReadAsync(employeeId, sourceMonth, actor, token);
                await Require(reloaded, actor, true, token);
                var again = Preview(reloaded, await History(employeeId, sourceMonth, actor, true, token));
                if (again.PreviewFingerprint != preview.PreviewFingerprint || await ledger.IsLockedAsync(preview.TargetMonth, token))
                    throw AttendanceOperation.Conflict("financial_carry_forward_preview_stale");
                var row = new AttendancePayrollFinancialAdjustment(employeeId, sourceMonth, preview.SourceRevisionId,
                    preview.ConfirmedPostingId, checked(preview.ExpectedRevision + 1), preview.PreviousAdjustmentId,
                    preview.TargetMonth, preview.TargetRevisionId, preview.PreviewFingerprint, actor.Identity.AccountId,
                    actor.Now, reference, Hash(bytes));
                repository.Add(row);
                return new AttendanceChange<AttendancePayrollFinancialResponse>(row.Id, null, Response(new(row, evidence, null, null)));
            }, ct, resourceScopes: (_, token) => producer.ResourceScopesAsync(employeeId, sourceMonth, token),
            beforeReplay: (actor, token) => RequireReplay(employeeId, sourceMonth, key, "approve", null,
                request.ExpectedPreviewFingerprint, request.ExpectedTargetMonth, request.ExpectedPreviousAdjustmentId, actor, token));
    }

    public Task<IReadOnlyList<AttendancePayrollFinancialResponse>> HistoryAsync(Guid employeeId, DateOnly sourceMonth,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, sourceMonth);
        return operation.ReadAsync<IReadOnlyList<AttendancePayrollFinancialResponse>>(AttendancePayrollAttestationPermissions.Read,
            "attendance.payroll-financial.history", employeeId, context, async (actor, token) =>
                (await History(employeeId, sourceMonth, actor, false, token)).Select(Response).ToArray(), ct,
            resourceScopes: (_, token) => producer.ResourceScopesAsync(employeeId, sourceMonth, token));
    }

    public Task<AttendancePayrollFinancialResponse> PostAsync(Guid employeeId, DateOnly sourceMonth, Guid adjustmentId,
        PostAttendancePayrollFinancialRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, sourceMonth);
        if (adjustmentId == Guid.Empty || request is null || !AttendancePayrollAttestationRules.Hash(request.ExpectedPreviewFingerprint))
            throw AttendanceOperation.Invalid();
        return Resolve(employeeId, sourceMonth, adjustmentId, request, null, key, context, ct);
    }

    public Task<AttendancePayrollFinancialResponse> CancelAsync(Guid employeeId, DateOnly sourceMonth, Guid adjustmentId,
        CancelAttendancePayrollFinancialRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, sourceMonth);
        if (adjustmentId == Guid.Empty || request is null || !AttendancePayrollAttestationRules.Hash(request.ExpectedPreviewFingerprint))
            throw AttendanceOperation.Invalid();
        return Resolve(employeeId, sourceMonth, adjustmentId, null, request, key, context, ct);
    }

    private Task<AttendancePayrollFinancialResponse> Resolve(Guid employee, DateOnly month, Guid id,
        PostAttendancePayrollFinancialRequest? posting, CancelAttendancePayrollFinancialRequest? cancellation,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        var status = posting is null ? "cancelled" : "posted";
        var reason = posting?.Reason ?? cancellation!.Reason;
        return operation.WriteAsync(AttendancePayrollAttestationPermissions.Attest,
            "attendance.payroll-financial." + status, employee, key, new { month, id, posting, cancellation }, reason,
            context, async (actor, token) =>
            {
                if (actor.Identity.EmployeeId == employee) throw AttendanceOperation.Denied();
                var history = await History(employee, month, actor, true, token);
                var retained = history.SingleOrDefault(x => x.Row.Id == id) ?? throw AttendanceOperation.Missing();
                if (retained.Resolution is not null) throw AttendanceOperation.Conflict("financial_carry_forward_already_resolved");
                if (posting is null && history.Last().Row.Id != id)
                    throw AttendanceOperation.Conflict("financial_carry_forward_cancellation_superseded");
                if ((posting?.ExpectedPreviewFingerprint ?? cancellation!.ExpectedPreviewFingerprint) != retained.Row.PreviewFingerprint)
                    throw AttendanceOperation.Conflict("financial_carry_forward_preview_stale");
                string? receiptKey = null;
                if (posting is not null)
                {
                    ValidatePosting(posting, retained, actor.Now);
                    receiptKey = AttendancePayrollAttestationRules.ExternalReceiptKey(posting.ExternalSystem, posting.ExternalReference);
                    if (await repository.ReceiptExistsAsync(receiptKey, token))
                        throw AttendanceOperation.Conflict("financial_carry_forward_receipt_reused");
                }
                await CheckResolutionTarget(retained, history, actor, posting is not null, token);
                var proof = new AttendancePayrollFinancialResolutionEvidence(1, id, retained.Row.EvidenceFingerprint,
                    status, actor.Identity.AccountId, actor.Now, posting, reason.Trim());
                var bytes = Bytes(proof);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(bytes, ResolutionPurpose(id), token));
                var again = await History(employee, month, actor, true, token);
                var currentRow = again.Single(x => x.Row.Id == id);
                if (currentRow.Resolution is not null || currentRow.Row.EvidenceFingerprint != retained.Row.EvidenceFingerprint ||
                    receiptKey is not null && await repository.ReceiptExistsAsync(receiptKey, token))
                    throw AttendanceOperation.Conflict("financial_carry_forward_resolution_conflict");
                await CheckResolutionTarget(currentRow, again, actor, posting is not null, token);
                var resolution = new AttendancePayrollFinancialResolution(id, status, receiptKey,
                    posting?.PostedAtUtc ?? actor.Now, actor.Identity.AccountId, actor.Now, reference, Hash(bytes));
                repository.Add(resolution);
                return new AttendanceChange<AttendancePayrollFinancialResponse>(resolution.Id, Response(retained),
                    Response(retained with { Resolution = resolution, ResolutionEvidence = proof }));
            }, ct, resourceScopes: (_, token) => producer.ResourceScopesAsync(employee, month, token),
            beforeReplay: (actor, token) => RequireReplay(employee, month, key, status, id,
                posting?.ExpectedPreviewFingerprint ?? cancellation!.ExpectedPreviewFingerprint, null, null, actor, token));
    }

    private async Task RequireReplay(Guid employee, DateOnly month, string key, string kind, Guid? adjustmentId,
        string expectedFingerprint, DateOnly? expectedTarget, Guid? expectedPrevious, AttendanceActor actor, CancellationToken ct)
    {
        if (actor.Identity.EmployeeId == employee) throw AttendanceOperation.Denied();
        var history = await History(employee, month, actor, true, ct);
        var operationName = "attendance.payroll-financial." + kind;
        var receipt = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), operationName, key, ct);
        if (receipt is null) return;
        if (receipt.CompletedAt is null || receipt.ProtectedResultReference is null)
            throw AttendanceOperation.Conflict("financial_carry_forward_replay_unavailable");
        var saved = await operation.ReadPrivateAsync<ReplayEvidence>(receipt.ProtectedResultReference,
            $"attendance:operation:{actor.Identity.AccountId:N}:{operationName}:result", ct);
        var row = history.SingleOrDefault(x => x.Row.Id == (adjustmentId ?? saved.Result?.Id));
        if (row is null || saved.Result is null || row.Row.PreviewFingerprint != expectedFingerprint)
            throw AttendanceOperation.Conflict("financial_carry_forward_replay_unavailable");
        AttendancePayrollFinancialResponse expected;
        if (kind == "approve")
        {
            if (saved.ResourceId != row.Row.Id || row.Row.TargetMonth != expectedTarget || row.Row.PreviousAdjustmentId != expectedPrevious)
                throw AttendanceOperation.Conflict("financial_carry_forward_replay_unavailable");
            expected = Response(row with { Resolution = null, ResolutionEvidence = null });
        }
        else
        {
            if (row.Resolution is null || row.ResolutionEvidence is null || row.Resolution.Status != kind || saved.ResourceId != row.Resolution.Id)
                throw AttendanceOperation.Conflict("financial_carry_forward_replay_unavailable");
            expected = Response(row);
        }
        if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(saved.Result))
            throw AttendanceOperation.Conflict("financial_carry_forward_replay_unavailable");
    }

    private async Task CheckResolutionTarget(Retained entry, IReadOnlyList<Retained> history,
        AttendanceActor actor, bool posting, CancellationToken ct)
    {
        if (await ledger.IsLockedAsync(entry.Row.TargetMonth, ct))
        {
            if (!posting) throw AttendanceOperation.Conflict("financial_carry_forward_target_locked");
            if (targetLocks is null || !await targetLocks.ContainsAsync(Entry(entry), actor, ct))
                throw AttendanceOperation.Conflict("financial_carry_forward_target_lock_membership_required");
            return;
        }
        if (!posting) return;
        var live = await sources.ReadAsync(entry.Row.EmployeeId, entry.Row.SourceMonth, actor, ct);
        await Require(live, actor, true, ct);
        var preview = Preview(live, history.Where(x => x.Row.Revision < entry.Row.Revision).ToArray());
        if (preview.PreviewFingerprint != entry.Row.PreviewFingerprint)
            throw AttendanceOperation.Conflict("financial_carry_forward_preview_stale");
    }

    public async Task<IReadOnlyList<AttendancePayrollFinancialMonthEntry>> ReadAsync(DateOnly targetMonth,
        IReadOnlyList<Guid> employeeIds, AttendanceActor actor, CancellationToken ct)
    {
        ValidateMonth(targetMonth);
        if (employeeIds is null || employeeIds.Count > MonthLockCoverage.MaximumEmployees || employeeIds.Any(x => x == Guid.Empty) ||
            employeeIds.Distinct().Count() != employeeIds.Count) throw AttendanceOperation.Invalid();
        var identity = current.Require();
        if (identity != actor.Identity || identity.Client != SessionClient.Web || !identity.MfaSatisfied)
            throw AttendanceOperation.Denied();
        if (!await journal.IsCurrentSessionAsync(identity, ct)) throw new AuthenticationFailure("invalid_authentication", 401);
        var candidates = await repository.TargetAsync(targetMonth, ct);
        if (candidates.Count > 4096 || candidates.Any(x => x.Adjustment.TargetMonth != targetMonth) ||
            candidates.Select(x => x.Adjustment.Id).Distinct().Count() != candidates.Count)
            throw AttendanceOperation.Conflict("financial_carry_forward_target_membership_invalid");
        if (candidates.Any(x => x.Resolution?.Status != "cancelled" && !employeeIds.Contains(x.Adjustment.EmployeeId)))
            throw AttendanceOperation.Conflict("financial_carry_forward_target_population_incomplete");
        var locked = await ledger.IsLockedAsync(targetMonth, ct);
        var result = new List<AttendancePayrollFinancialMonthEntry>();
        foreach (var group in candidates.GroupBy(x => (x.Adjustment.EmployeeId, x.Adjustment.SourceMonth)))
        {
            var history = await History(group.Key.EmployeeId, group.Key.SourceMonth, actor, false, ct);
            if (!employeeIds.Contains(group.Key.EmployeeId))
            {
                if (history.Any(x => x.Row.TargetMonth == targetMonth && x.Resolution?.Status != "cancelled"))
                    throw AttendanceOperation.Conflict("financial_carry_forward_target_population_incomplete");
                continue;
            }
            foreach (var candidate in group)
            {
                var retained = history.SingleOrDefault(x => x.Row.Id == candidate.Adjustment.Id)
                    ?? throw AttendanceOperation.Conflict("financial_carry_forward_target_membership_invalid");
                if (retained.Row.EvidenceFingerprint != candidate.Adjustment.EvidenceFingerprint ||
                    retained.Resolution?.Id != candidate.Resolution?.Id)
                    throw AttendanceOperation.Conflict("financial_carry_forward_target_membership_invalid");
                if (retained.Resolution?.Status == "cancelled") continue;
                if (!locked && retained.Resolution is null && retained.Row.Id == history.Last().Row.Id)
                {
                    var live = await sources.ReadOpenTargetAsync(group.Key.EmployeeId, group.Key.SourceMonth, targetMonth, actor, ct);
                    await Require(live, actor, false, ct);
                    var preview = Preview(live, history.Where(x => x.Row.Revision < retained.Row.Revision).ToArray());
                    if (preview.PreviewFingerprint != retained.Row.PreviewFingerprint)
                        throw AttendanceOperation.Conflict("financial_carry_forward_preview_stale");
                }
                result.Add(Entry(retained));
            }
        }
        return result.OrderBy(x => x.EmployeeId).ThenBy(x => x.SourceMonth).ThenBy(x => x.Revision).ToArray();
    }

    private async Task<IReadOnlyList<Retained>> History(Guid employee, DateOnly month, AttendanceActor actor, bool writing, CancellationToken ct)
    {
        var rows = await repository.HistoryAsync(employee, month, ct);
        if (rows.Count > AttendancePayrollFinancialRules.MaximumAdjustments || rows.Select(x => x.Adjustment.Id).Distinct().Count() != rows.Count)
            throw AttendanceOperation.Conflict("financial_carry_forward_history_limit");
        var result = new List<Retained>();
        foreach (var pair in rows.OrderBy(x => x.Adjustment.Revision))
        {
            var row = pair.Adjustment;
            var proof = await operation.ReadPrivateAsync<AttendancePayrollFinancialEvidence>(row.ProtectedEvidenceReference, Purpose(employee, month), ct);
            if (row.EmployeeId != employee || row.SourceMonth != month || row.Revision != result.Count + 1 ||
                row.PreviousAdjustmentId != result.LastOrDefault()?.Row.Id || proof.SchemaVersion != 1 ||
                Hash(Bytes(proof)) != row.EvidenceFingerprint || proof.ActorUserId != row.ActorUserId || proof.CreatedAtUtc != row.CreatedAtUtc ||
                proof.Preview.SourceRevisionId != row.SourceRevisionId || proof.Preview.ConfirmedPostingId != row.ConfirmedPostingId ||
                proof.Preview.TargetRevisionId != row.TargetRevisionId || proof.Preview.TargetMonth != row.TargetMonth ||
                proof.Preview.PreviewFingerprint != row.PreviewFingerprint || !AttendancePayrollAttestationRules.Text(proof.Reason, 1000) ||
                result.Any(x => x.Row.CreatedAtUtc > row.CreatedAtUtc || x.Resolution?.CreatedAtUtc > row.CreatedAtUtc) ||
                JsonSerializer.Serialize(Preview(proof.Sources, result)) != JsonSerializer.Serialize(proof.Preview))
                throw AttendanceOperation.Conflict("financial_carry_forward_evidence_invalid");
            await Require(proof.Sources, actor, writing, ct);
            AttendancePayrollFinancialResolutionEvidence? resolutionProof = null;
            var retained = new Retained(row, proof, pair.Resolution, null);
            if (pair.Resolution is { } resolution)
            {
                resolutionProof = await operation.ReadPrivateAsync<AttendancePayrollFinancialResolutionEvidence>(
                    resolution.ProtectedEvidenceReference, ResolutionPurpose(row.Id), ct);
                if (resolution.AdjustmentId != row.Id || resolution.CreatedAtUtc < row.CreatedAtUtc || resolution.OccurredAtUtc < row.CreatedAtUtc ||
                    resolutionProof.SchemaVersion != 1 || Hash(Bytes(resolutionProof)) != resolution.EvidenceFingerprint ||
                    resolutionProof.AdjustmentId != row.Id || resolutionProof.AdjustmentEvidenceFingerprint != row.EvidenceFingerprint ||
                    resolutionProof.Status != resolution.Status || resolutionProof.ActorUserId != resolution.ActorUserId ||
                    resolutionProof.CreatedAtUtc != resolution.CreatedAtUtc || !AttendancePayrollAttestationRules.Text(resolutionProof.Reason, 1000))
                    throw AttendanceOperation.Conflict("financial_carry_forward_resolution_evidence_invalid");
                if (resolution.Status == "posted" && resolutionProof.Posting is { } posting)
                {
                    ValidatePosting(posting, retained, resolution.CreatedAtUtc);
                    if (posting.PostedAtUtc != resolution.OccurredAtUtc || resolution.ExternalReceiptKey !=
                        AttendancePayrollAttestationRules.ExternalReceiptKey(posting.ExternalSystem, posting.ExternalReference))
                        throw AttendanceOperation.Conflict("financial_carry_forward_resolution_evidence_invalid");
                }
                else if (resolution.Status != "cancelled" || resolutionProof.Posting is not null || resolution.ExternalReceiptKey is not null ||
                    resolution.OccurredAtUtc != resolution.CreatedAtUtc)
                    throw AttendanceOperation.Conflict("financial_carry_forward_resolution_evidence_invalid");
            }
            result.Add(retained with { ResolutionEvidence = resolutionProof });
        }
        return result;
    }

    private async Task Require(AttendancePayrollFinancialSources source, AttendanceActor actor, bool writing, CancellationToken ct)
    {
        var scopes = source.OriginalScopes.Concat(await producer.ResourceScopesAsync(source.Source.EmployeeId, source.Source.PeriodStart, ct))
            .Concat(await producer.ResourceScopesAsync(source.Source.EmployeeId, source.Target.PeriodStart, ct)).ToArray();
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != source.Source.EmployeeId))
            throw AttendanceOperation.Conflict("financial_carry_forward_scope_invalid");
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        foreach (var permission in writing ? new[] { AttendancePayrollInputPermissions.Read, AttendancePayrollAttestationPermissions.Read,
                     AttendancePayrollAttestationPermissions.Attest } : new[] { AttendancePayrollInputPermissions.Read, AttendancePayrollAttestationPermissions.Read })
            if (!AttendanceScopeEvaluation.Allows(actor.Identity, grants, permission, scopes)) throw AttendanceOperation.Denied();
    }

    private static AttendancePayrollFinancialPreview Preview(AttendancePayrollFinancialSources source, IReadOnlyList<Retained> history)
    {
        if (history.Any(x => x.Resolution is null && x.Proof.Preview.SignedAmount != 0))
            throw AttendanceOperation.Conflict("financial_carry_forward_pending_resolution");
        if (history.Count >= AttendancePayrollFinancialRules.MaximumAdjustments)
            throw AttendanceOperation.Conflict("financial_carry_forward_history_limit");
        var calculation = source.OriginalCalculation; var posting = source.Posting;
        if (source.Source.EmployeeId != calculation.EmployeeId || source.Source.PeriodStart != calculation.MonthStart ||
            posting.EmployeeId != source.Source.EmployeeId || posting.MonthStart != source.Source.PeriodStart ||
            posting.SourceRevisionId != source.Source.Id || posting.SourceRevision != source.Source.Revision ||
            posting.SourceFingerprint != source.Source.InputFingerprint || posting.Status != "posted" || !posting.ConfirmsPosting ||
            posting.PostedAtUtc is null || !AttendancePayrollAttestationRules.Hash(source.PostingEvidenceFingerprint) ||
            source.Target.EmployeeId != source.Source.EmployeeId || source.Target.PeriodStart <= source.Source.PeriodStart ||
            history.Any(x => x.Row.SourceRevisionId != source.Source.Id || x.Row.ConfirmedPostingId != posting.Id))
            throw AttendanceOperation.Conflict("financial_carry_forward_source_conflict");
        try { AttendancePayrollAttestationRules.RequireLines(calculation.Lines.Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency)).ToArray(), posting.Lines); }
        catch (ArgumentException) { throw AttendanceOperation.Conflict("financial_carry_forward_confirmed_posting_required"); }
        var ids = source.Corrections.SelectMany(x => new[] { x.AdjustmentId }.Concat(x.PredecessorAdjustmentIds ?? [])).Order().ToArray();
        if (ids.Length is < 1 or > 31 || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw AttendanceOperation.Conflict("financial_carry_forward_correction_history_limit");
        if (history.Any(x => x.Resolution?.Status != "cancelled" && x.Proof.Preview.FactualAdjustmentIds.Order().SequenceEqual(ids)))
            throw AttendanceOperation.Conflict("financial_carry_forward_already_valued");
        var corrected = AttendancePayrollFinancialCalculation.Calculate(calculation, source.Corrections);
        var actual = posting.Lines.Sum(x => x.Amount);
        var effects = history.Where(x => x.Resolution?.Status == "posted").Select(x => x.Proof.Preview.SignedAmount).ToArray();
        var total = corrected.Lines.Sum(x => x.Amount);
        decimal delta;
        try { delta = AttendancePayrollFinancialRules.Difference(actual, total, effects); }
        catch (ArgumentException) { throw AttendanceOperation.Conflict("financial_carry_forward_balance_invalid"); }
        var preview = new AttendancePayrollFinancialPreview(source.Source.EmployeeId, source.Source.PeriodStart,
            source.Source.Id, posting.Id, source.Target.PeriodStart, source.Target.Id, history.Count, history.LastOrDefault()?.Row.Id,
            actual, effects.Sum(), total, delta, "PKR", delta < 0 ? "credit" : delta > 0 ? "recovery" : "no_change", "", true, ids, corrected.Lines);
        return preview with { PreviewFingerprint = Hash(Bytes(new { source, preview,
            history = history.Select(x => new { x.Row.Id, x.Row.EvidenceFingerprint, ResolutionId = x.Resolution?.Id,
                ResolutionFingerprint = x.Resolution?.EvidenceFingerprint }).ToArray() })) };
    }

    private static void ValidatePosting(PostAttendancePayrollFinancialRequest posting, Retained retained, DateTimeOffset now)
    {
        if (posting.SignedAmount == 0 || posting.SignedAmount != retained.Proof.Preview.SignedAmount || posting.Currency != "PKR" ||
            posting.ExpectedPreviewFingerprint != retained.Row.PreviewFingerprint || !AttendancePayrollAttestationRules.Text(posting.Reason, 1000))
            throw AttendanceOperation.Conflict("financial_carry_forward_posting_amount_conflict");
        try
        {
            AttendancePayrollAttestationRules.RequireSourceTime(posting.PostedAtUtc, retained.Row.CreatedAtUtc);
            AttendancePayrollAttestationRules.RequireReceipt("posted", posting.PostedAtUtc, posting.PostedAtUtc,
                posting.ExternalSystem, posting.ExternalReference, posting.EvidenceReference, posting.EvidenceSha256, posting.Statement, now);
        }
        catch (ArgumentException) { throw AttendanceOperation.Invalid("invalid_financial_carry_forward_posting"); }
    }
    private static AttendancePayrollFinancialResponse Response(Retained x) => new(x.Row.Id, x.Row.Revision, x.Proof.Preview,
        x.Resolution?.Status ?? "approved", x.Row.ActorUserId,
        x.Row.CreatedAtUtc, x.Resolution?.Id, x.ResolutionEvidence?.Posting?.PostedAtUtc, x.ResolutionEvidence?.Posting?.ExternalReference);
    private static AttendancePayrollFinancialMonthEntry Entry(Retained x) => new(x.Row.Id, x.Row.EmployeeId,
        x.Row.SourceMonth, x.Row.TargetMonth, x.Row.Revision, x.Row.SourceRevisionId, x.Row.ConfirmedPostingId,
        x.Row.TargetRevisionId, x.Proof.Sources.Target.InputFingerprint, x.Row.PreviewFingerprint, x.Row.EvidenceFingerprint,
        x.Row.ProtectedEvidenceReference, x.Proof.Preview.SignedAmount, "PKR", Response(x).Status,
        x.Resolution?.Status == "posted" ? x.Resolution.Id : null, x.Resolution?.Status == "posted" ? x.Resolution.EvidenceFingerprint : null,
        x.Proof.Preview.FactualAdjustmentIds);
    private static string Purpose(Guid employee, DateOnly month) => $"attendance:financial-carry-forward:{employee:N}:{month:yyyy-MM}";
    private static string ResolutionPurpose(Guid id) => $"attendance:financial-carry-forward-resolution:{id:N}";
    private static byte[] Bytes<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length > 16 * 1024 * 1024) throw AttendanceOperation.Conflict("financial_carry_forward_evidence_limit");
        return bytes;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Validate(Guid employee, DateOnly month)
    {
        if (employee == Guid.Empty) throw AttendanceOperation.Invalid();
        ValidateMonth(month);
    }
    private static void ValidateMonth(DateOnly month)
    {
        try { AttendancePayrollFinancialRules.RequireMonth(month); }
        catch (ArgumentException) { throw AttendanceOperation.Invalid("invalid_financial_carry_forward_month"); }
    }
    private sealed record Retained(AttendancePayrollFinancialAdjustment Row, AttendancePayrollFinancialEvidence Proof,
        AttendancePayrollFinancialResolution? Resolution, AttendancePayrollFinancialResolutionEvidence? ResolutionEvidence);
    private sealed record ReplayEvidence(Guid ResourceId, AttendancePayrollFinancialResponse? Result);
}
