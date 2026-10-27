using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.PayrollInputs;

namespace Cosmix.Application.Attendance.PayrollInputs;

public sealed class AttendancePayrollAttestationService(AttendanceOperation operation,
    AttendancePayrollInputService inputs, IAttendancePayrollInputLedger ledger,
    IAttendancePayrollInputProducer producer, IAttendancePayrollAttestationRepository repository,
    IPrivateFileStore files, IAccountAccessReader access) : IAttendanceConfirmedPayrollPostingReader
{
    public async Task<AttendancePayrollAttestationResponse> CreateAsync(Guid employeeId, DateOnly monthStart,
        Guid sourceRevisionId, CreateAttendancePayrollAttestationRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, monthStart, sourceRevisionId);
        if (request is null || request.ExpectedSourceRevision < 1 ||
            !AttendancePayrollAttestationRules.Hash(request.ExpectedSourceFingerprint) ||
            !AttendancePayrollAttestationRules.Text(request.Reason, 1000))
            throw AttendanceOperation.Invalid("invalid_payroll_attestation_request");
        // This audited read owns its own transaction. Never nest DetailAsync inside WriteAsync.
        var detail = await inputs.DetailAsync(employeeId, monthStart, sourceRevisionId, context, ct);
        BoundSource? bound = null;
        return await operation.WriteAsync(AttendancePayrollAttestationPermissions.Attest,
            "attendance.payroll-attestations.create", employeeId, key, new { monthStart, sourceRevisionId, request },
            request.Reason, context, async (actor, token) =>
            {
                var source = bound ?? throw AttendanceOperation.Conflict("payroll_attestation_source_unavailable");
                if (source.Row.Revision != request.ExpectedSourceRevision || source.Row.InputFingerprint != request.ExpectedSourceFingerprint)
                    throw AttendanceOperation.Conflict("payroll_attestation_source_conflict");
                ValidateRequest(request, source.Lines, source.Row.CreatedAtUtc, actor.Now);
                var history = await ReadHistory(source, actor, AttendancePayrollAttestationPermissions.Attest, token);
                var previous = history.LastOrDefault()?.Row;
                var receiptKey = AttendancePayrollAttestationRules.ExternalReceiptKey(request.ExternalSystem, request.ExternalReference);
                try
                {
                    AttendancePayrollAttestationRules.RequireTransition(previous, request.ExpectedPreviousAttestationId,
                        sourceRevisionId, request.Status, request.OccurredAtUtc, request.PostedAtUtc, receiptKey);
                }
                catch (ArgumentException) { throw AttendanceOperation.Conflict("payroll_attestation_transition_conflict"); }
                // Only the first handoff chooses a source; all later evidence remains pinned to it.
                if (previous is null && (await ledger.LatestAsync(employeeId, monthStart, token))?.Id != sourceRevisionId)
                    throw AttendanceOperation.Conflict("payroll_attestation_source_superseded");
                var step = AttendancePayrollAttestationRules.Step(request.Status);
                if (await repository.ReceiptExistsAsync(receiptKey, step, token))
                    throw AttendanceOperation.Conflict("payroll_attestation_receipt_reused");
                var evidence = new AttendancePayrollAttestationEvidence(1, employeeId, monthStart,
                    source.Row.Id, source.Row.Revision, source.Row.InputFingerprint, source.Row.ProtectedEvidenceReference,
                    previous?.Id, request.Status, request.OccurredAtUtc, request.PostedAtUtc,
                    source.Lines, request.ExternalSystem.Trim().ToUpperInvariant(), request.ExternalReference.Trim(),
                    request.EvidenceReference.Trim(), request.EvidenceSha256, request.Statement.Trim(), request.Reason.Trim(),
                    actor.Identity.AccountId, actor.Now, source.Evidence.OriginalScopes);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
                if (bytes.Length > 1024 * 1024) throw AttendanceOperation.Conflict("payroll_attestation_evidence_limit");
                var reference = AttendanceOperation.Reference(await files.WriteAsync(bytes,
                    Purpose(source.Row.Id, step), token));
                var row = new AttendancePayrollAttestation(employeeId, monthStart, sourceRevisionId, source.Row.Revision,
                    source.Row.InputFingerprint, previous?.Id, request.Status,
                    AttendancePayrollAttestationRules.HandoffLineFingerprint(source.Lines), receiptKey,
                    request.OccurredAtUtc, request.PostedAtUtc, actor.Identity.AccountId, actor.Now, reference, Hash(bytes));
                repository.Add(row);
                return new AttendanceChange<AttendancePayrollAttestationResponse>(row.Id,
                    history.LastOrDefault()?.Response, Map(row, evidence));
            }, ct, resourceScopes: async (actor, token) =>
            {
                bound = await BindSource(detail, actor, token);
                return await Scopes(bound, actor, token);
            });
    }

    public async Task<IReadOnlyList<AttendancePayrollAttestationResponse>> HistoryAsync(Guid employeeId,
        DateOnly monthStart, Guid sourceRevisionId, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, monthStart, sourceRevisionId);
        var detail = await inputs.DetailAsync(employeeId, monthStart, sourceRevisionId, context, ct);
        BoundSource? bound = null;
        return await operation.ReadAsync<IReadOnlyList<AttendancePayrollAttestationResponse>>(
            AttendancePayrollAttestationPermissions.Read, "attendance.payroll-attestations.history", employeeId,
            context, async (actor, token) => (await ReadHistory(bound!, actor,
                AttendancePayrollAttestationPermissions.Read, token)).Select(x => x.Response).ToArray(), ct,
            resourceScopes: async (actor, token) =>
            {
                bound = await BindSource(detail, actor, token);
                return await Scopes(bound, actor, token);
            });
    }

    public async Task<AttendancePayrollAttestationResponse?> ReadConfirmedPostingAsync(Guid employeeId,
        DateOnly monthStart, Guid sourceRevisionId, AuthRequestContext context, CancellationToken ct) =>
        (await HistoryAsync(employeeId, monthStart, sourceRevisionId, context, ct)).LastOrDefault(x => x.ConfirmsPosting);

    internal async Task<IReadOnlyList<AttendancePayrollAttestationResponse>> ReadForMonthLockAsync(
        AttendancePayrollInputRevision row, AttendanceActor actor, CancellationToken ct)
    {
        var saved = await inputs.ReadRetainedForMonthLockAsync(row.EmployeeId, row.Id, actor, ct);
        var lines = saved.Calculation.Lines.Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency))
            .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        var source = new BoundSource(row, saved, lines);
        var scopes = await Scopes(source, actor, ct);
        await RequireScope(actor, AttendancePayrollAttestationPermissions.Read, scopes, ct);
        return (await ReadHistory(source, actor, AttendancePayrollAttestationPermissions.Read, ct)).Select(x => x.Response).ToArray();
    }

    private async Task<BoundSource> BindSource(AttendancePayrollInputDetail detail, AttendanceActor actor, CancellationToken ct)
    {
        var revision = detail.Revision;
        var row = await ledger.FindAsync(revision.EmployeeId, revision.Id, ct);
        if (row is null || row.PeriodStart != revision.MonthStart || row.Revision != revision.Revision ||
            row.InputFingerprint != revision.InputFingerprint || row.CalculationVersion != revision.CalculationVersion ||
            row.ActorUserId != revision.ActorUserId || row.CreatedAtUtc != revision.CreatedAtUtc ||
            row.SupersedesRevisionId != revision.SupersedesRevisionId)
            throw AttendanceOperation.Conflict("payroll_attestation_source_conflict");
        var evidence = await operation.ReadPrivateAsync<AttendancePayrollInputEvidence>(row.ProtectedEvidenceReference,
            $"attendance:payroll-inputs:{row.EmployeeId:N}:{row.PeriodStart:yyyy-MM}:{row.Revision}", ct);
        // Revalidate the authoritative protected source while holding the write/read transaction.
        if (evidence.SchemaVersion != 1 || evidence.EmployeeId != row.EmployeeId || evidence.MonthStart != row.PeriodStart ||
            evidence.Revision != row.Revision || evidence.ActorUserId != row.ActorUserId || evidence.CreatedAtUtc != row.CreatedAtUtc ||
            !AttendancePayrollInputRules.IsSupportedVersion(row.CalculationVersion) || row.CalculationVersion!=evidence.Calculation.SourceManifest.CalculationVersion || evidence.PreviewFingerprint != row.InputFingerprint ||
            Hash(JsonSerializer.SerializeToUtf8Bytes(new { Calculation = evidence.Calculation, Jurisdictions = evidence.Jurisdictions })) != row.InputFingerprint ||
            evidence.Calculation.EmployeeId != row.EmployeeId || evidence.Calculation.MonthStart != row.PeriodStart ||
            evidence.Calculation.MonthEndExclusive != row.PeriodStart.AddMonths(1) ||
            evidence.Calculation.SourceManifest.EmployeeId != row.EmployeeId || evidence.Calculation.SourceManifest.MonthStart != row.PeriodStart ||
            evidence.Calculation.SourceManifest.CalculationVersion != AttendancePayrollInputRules.VersionFor(evidence.Calculation.SourceManifest.Days.Select(x=>x.Compensation)) ||
            !AttendanceScopeEvaluation.IsValid(evidence.OriginalScopes) ||
            evidence.OriginalScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != row.EmployeeId) ||
            JsonSerializer.Serialize(evidence.OriginalScopes) != JsonSerializer.Serialize(evidence.Calculation.SourceManifest.OriginalScopes))
            throw AttendanceOperation.Conflict("payroll_attestation_source_evidence_invalid");
        if (evidence.Calculation.State != "ready" || evidence.Calculation.Issues.Count != 0)
            throw AttendanceOperation.Conflict("payroll_attestation_source_unavailable");
        var lines = evidence.Calculation.Lines.Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency))
            .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        try
        {
            AttendancePayrollAttestationRules.RequireHandoffLines(lines,
                detail.Inputs.Lines.Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency)).ToArray());
        }
        catch (ArgumentException) { throw AttendanceOperation.Conflict("payroll_attestation_source_lines_invalid"); }
        await RequireScope(actor, AttendancePayrollInputPermissions.Read, evidence.OriginalScopes, ct);
        return new(row, evidence, lines);
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> Scopes(BoundSource source, AttendanceActor actor, CancellationToken ct)
    {
        var current = await producer.ResourceScopesAsync(source.Row.EmployeeId, source.Row.PeriodStart, ct);
        await RequireScope(actor, AttendancePayrollInputPermissions.Read, current, ct);
        return source.Evidence.OriginalScopes.Concat(current).ToArray();
    }
    private Task RequireScope(AttendanceActor actor, string permission,
        IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct) =>
        operation.RequireScopedPermissionAsync(actor, permission, scopes, ct);

    private Task<IReadOnlyList<Retained>> ReadHistory(BoundSource source, AttendanceActor actor, string permission, CancellationToken ct) =>
        ReadHistoryCore(source, actor, permission, operation, repository, access, ct);

    internal static async Task<IReadOnlyList<AttendancePayrollAttestationResponse>> ReadValidatedHistoryAsync(
        AttendancePayrollInputRevision row, AttendancePayrollInputEvidence evidence, AttendanceActor actor,
        AttendanceOperation operation, IAttendancePayrollAttestationRepository repository, IAccountAccessReader access, CancellationToken ct)
    {
        if (evidence.Calculation.State != "ready" || evidence.Calculation.Issues.Count != 0)
            throw AttendanceOperation.Conflict("payroll_attestation_source_unavailable");
        var lines = evidence.Calculation.Lines.Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency))
            .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        await operation.RequireScopedPermissionAsync(actor, AttendancePayrollAttestationPermissions.Read, evidence.OriginalScopes, ct);
        return (await ReadHistoryCore(new(row, evidence, lines), actor, AttendancePayrollAttestationPermissions.Read,
            operation, repository, access, ct)).Select(x => x.Response).ToArray();
    }

    private static async Task<IReadOnlyList<Retained>> ReadHistoryCore(BoundSource source, AttendanceActor actor, string permission,
        AttendanceOperation operation, IAttendancePayrollAttestationRepository repository, IAccountAccessReader access, CancellationToken ct)
    {
        var rows = await repository.HistoryAsync(source.Row.EmployeeId, source.Row.PeriodStart, ct);
        if (rows.Count > 3 || rows.Any(x => x.SourceRevisionId != source.Row.Id))
            throw AttendanceOperation.Conflict("payroll_attestation_chain_source_conflict");
        var result = new List<Retained>();
        AttendancePayrollAttestation? previous = null;
        foreach (var row in rows)
        {
            var evidence = await operation.ReadPrivateAsync<AttendancePayrollAttestationEvidence>(row.ProtectedEvidenceReference,
                Purpose(row.SourceRevisionId, row.Step), ct);
            if (evidence.SchemaVersion != 1 || Hash(JsonSerializer.SerializeToUtf8Bytes(evidence)) != row.EvidenceFingerprint ||
                evidence.EmployeeId != row.EmployeeId || row.EmployeeId != source.Row.EmployeeId ||
                evidence.MonthStart != row.PeriodStart || row.PeriodStart != source.Row.PeriodStart ||
                evidence.SourceRevisionId != row.SourceRevisionId || evidence.SourceRevision != row.SourceRevision ||
                row.SourceRevision != source.Row.Revision || evidence.SourceFingerprint != row.SourceFingerprint ||
                row.SourceFingerprint != source.Row.InputFingerprint || evidence.SourceProtectedEvidenceReference != source.Row.ProtectedEvidenceReference ||
                evidence.PreviousAttestationId != row.PreviousAttestationId || evidence.Status != row.Status ||
                evidence.OccurredAtUtc != row.OccurredAtUtc || evidence.PostedAtUtc != row.PostedAtUtc ||
                evidence.ActorUserId != row.ActorUserId || evidence.CreatedAtUtc != row.CreatedAtUtc ||
                JsonSerializer.Serialize(evidence.OriginalScopes) != JsonSerializer.Serialize(source.Evidence.OriginalScopes) ||
                !AttendancePayrollAttestationRules.Text(evidence.Reason, 1000))
                throw AttendanceOperation.Conflict("payroll_attestation_evidence_invalid");
            try
            {
                AttendancePayrollAttestationRules.RequireHandoffLines(source.Lines, evidence.Lines);
                if (source.Lines.Count == 0 && row.Status != "handoff")
                    throw new ArgumentException("A no-deduction handoff cannot contain posting or settlement proof.");
                AttendancePayrollAttestationRules.RequireSourceTime(row.OccurredAtUtc, source.Row.CreatedAtUtc);
                AttendancePayrollAttestationRules.RequireReceipt(evidence.Status, evidence.OccurredAtUtc, evidence.PostedAtUtc,
                    evidence.ExternalSystem, evidence.ExternalReference, evidence.EvidenceReference, evidence.EvidenceSha256,
                    evidence.Statement, row.CreatedAtUtc);
                if (row.Step != AttendancePayrollAttestationRules.Step(row.Status) ||
                    row.ExternalReceiptKey != AttendancePayrollAttestationRules.ExternalReceiptKey(evidence.ExternalSystem, evidence.ExternalReference) ||
                    row.LineFingerprint != AttendancePayrollAttestationRules.HandoffLineFingerprint(evidence.Lines))
                    throw new ArgumentException("Receipt header differs from protected evidence.");
                AttendancePayrollAttestationRules.RequireTransition(previous, row.PreviousAttestationId, source.Row.Id,
                    row.Status, row.OccurredAtUtc, row.PostedAtUtc, row.ExternalReceiptKey);
            }
            catch (ArgumentException) { throw AttendanceOperation.Conflict("payroll_attestation_evidence_invalid"); }
            await operation.RequireScopedPermissionAsync(actor, permission, evidence.OriginalScopes, ct);
            result.Add(new(row, Map(row, evidence))); previous = row;
        }
        return result;
    }

    private static void ValidateRequest(CreateAttendancePayrollAttestationRequest request,
        IReadOnlyList<AttendancePayrollAttestationLine> source, DateTimeOffset sourceCreatedAtUtc, DateTimeOffset now)
    {
        try
        {
            AttendancePayrollAttestationRules.RequireSourceTime(request.OccurredAtUtc, sourceCreatedAtUtc);
            AttendancePayrollAttestationRules.RequireReceipt(request.Status, request.OccurredAtUtc, request.PostedAtUtc,
                request.ExternalSystem, request.ExternalReference, request.EvidenceReference, request.EvidenceSha256, request.Statement, now);
            if (source.Count == 0)
            {
                if (request.Status != "handoff") throw AttendanceOperation.Conflict("payroll_attestation_no_deduction_lines");
                AttendancePayrollAttestationRules.RequireHandoffLines(source, request.Lines);
            }
            else AttendancePayrollAttestationRules.RequireLines(source, request.Lines);
        }
        catch (ArgumentException) { throw AttendanceOperation.Invalid("invalid_payroll_attestation_receipt"); }
    }
    private static AttendancePayrollAttestationResponse Map(AttendancePayrollAttestation row, AttendancePayrollAttestationEvidence evidence) =>
        new(row.Id, row.EmployeeId, row.PeriodStart, row.SourceRevisionId, row.SourceRevision, row.SourceFingerprint,
            row.PreviousAttestationId, row.Status, row.ConfirmsPosting, row.OccurredAtUtc, row.PostedAtUtc, row.ActorUserId,
            row.CreatedAtUtc, evidence.Lines, evidence.ExternalSystem, evidence.ExternalReference, evidence.EvidenceReference,
            evidence.EvidenceSha256, evidence.Statement, evidence.Reason);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Purpose(Guid sourceRevisionId, int step) => $"attendance:payroll-attestation:{sourceRevisionId:N}:{step}";
    private static void Validate(Guid employeeId, DateOnly monthStart, Guid sourceRevisionId)
    {
        if (employeeId == Guid.Empty || sourceRevisionId == Guid.Empty || monthStart.Day != 1 || monthStart.Year is < 1900 or > 9998)
            throw AttendanceOperation.Invalid("invalid_payroll_attestation_period");
    }
    private sealed record BoundSource(AttendancePayrollInputRevision Row, AttendancePayrollInputEvidence Evidence,
        IReadOnlyList<AttendancePayrollAttestationLine> Lines);
    private sealed record Retained(AttendancePayrollAttestation Row, AttendancePayrollAttestationResponse Response);
}
