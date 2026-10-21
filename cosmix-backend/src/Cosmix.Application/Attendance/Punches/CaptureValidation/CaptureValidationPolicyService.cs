using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Common.Files;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;

namespace Cosmix.Application.Attendance.Punches.CaptureValidation;

public sealed class CaptureValidationPolicyService(AttendanceOperation operation, ICaptureValidationPolicyRepository repository,
    IPrivateFileStore files) : ICaptureValidationPolicyReader
{
    public const string EvidencePurpose = "attendance:capture-validation:global-policy:v1";

    public async Task<CaptureValidationPolicyEntry?> EffectiveAsync(DateTimeOffset receivedAtUtc, CancellationToken ct)
    {
        Utc(receivedAtUtc);
        return CaptureValidationPolicyRules.Effective(await Timeline(ct), receivedAtUtc);
    }
    public Task<CaptureValidationPolicyPreview> PreviewAsync(DateTimeOffset asOfUtc, AuthRequestContext context, CancellationToken ct)
    {
        Utc(asOfUtc);
        return operation.ReadAsync(CaptureValidationPermissions.Read, "attendance.capture.validation.preview", null, context,
            async (_, token) =>
            {
                var rows = await Timeline(token); var effective = CaptureValidationPolicyRules.Effective(rows, asOfUtc);
                return new CaptureValidationPolicyPreview(asOfUtc, rows.LastOrDefault()?.Revision ?? 0,
                    CaptureValidationPolicyRules.TimelineFingerprint(rows), effective is null ? "not_configured" : "approved_configured",
                    effective is null ? null : Map(effective));
            }, ct);
    }
    public Task<PagedResult<CaptureValidationPolicyResponse>> HistoryAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct)
    {
        if (page is < 1 or > 1000 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("capture_policy_invalid_page");
        return operation.ReadAsync(CaptureValidationPermissions.Read, "attendance.capture.validation.history", null, context,
            async (_, token) =>
            {
                var rows = await Timeline(token);
                return new PagedResult<CaptureValidationPolicyResponse>(rows.OrderByDescending(x => x.Revision)
                    .Skip((page - 1) * pageSize).Take(pageSize).Select(Map).ToArray(), rows.Count, page, pageSize);
            }, ct);
    }
    public Task<CaptureValidationPolicyResponse> PublishAsync(PublishCaptureValidationPolicyRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.ExpectedRevision < 0 || !CaptureValidationPolicyRules.Hash(request.ExpectedTimelineFingerprint) ||
            !CaptureValidationPolicyRules.Text(request.ApprovalReference, 200) ||
            !CaptureValidationPolicyRules.Text(request.Reason, 1000))
            throw AttendanceOperation.Invalid("capture_policy_invalid_publication");
        Utc(request.EffectiveFromUtc);
        // Freeze caller-owned collections before authorization and fingerprinting.
        request = request with { Settings = CaptureValidationPolicyRules.Freeze(request.Settings) };
        return operation.WriteAsync(CaptureValidationPermissions.Manage, "attendance.capture.validation.publish", null,
            key, request, request.Reason, context, async (actor, token) =>
            {
                if (request.EffectiveFromUtc <= actor.Now) throw AttendanceOperation.Conflict("capture_policy_future_effective_required");
                var rows = await Timeline(token); var previous = rows.LastOrDefault();
                if (rows.Count >= CaptureValidationPolicyRules.MaximumTimelineRows)
                    throw AttendanceOperation.Conflict("capture_policy_history_limit");
                if (request.ExpectedRevision != (previous?.Revision ?? 0) ||
                    request.ExpectedTimelineFingerprint.ToUpperInvariant() != CaptureValidationPolicyRules.TimelineFingerprint(rows))
                    throw AttendanceOperation.Conflict("capture_policy_version_conflict");
                if (previous is not null && request.EffectiveFromUtc <= previous.EffectiveFromUtc)
                    throw AttendanceOperation.Conflict("capture_policy_effective_order_conflict");
                var entry = new CaptureValidationPolicyEntry(Guid.NewGuid(), checked(request.ExpectedRevision + 1), previous?.Id,
                    request.EffectiveFromUtc, actor.Now, actor.Identity.AccountId.ToString("D"), request.ApprovalReference,
                    request.Settings, request.Reason, "", "");
                entry = entry with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(entry) };
                var proof = JsonSerializer.SerializeToUtf8Bytes(new CaptureValidationPolicyProof(1, entry));
                var reference = await files.WriteAsync(proof, EvidencePurpose, token);
                entry = entry with { ProtectedEvidenceReference = AttendanceOperation.Reference(reference) };
                repository.Add(entry);
                // AttendanceOperation commits policy, protected audit and replay receipt together.
                return new AttendanceChange<CaptureValidationPolicyResponse>(entry.Id, previous is null ? null : Map(previous), Map(entry));
            }, ct, requireSystemRole: true);
    }
    private async Task<IReadOnlyList<CaptureValidationPolicyEntry>> Timeline(CancellationToken ct)
    {
        var rows = await repository.TimelineAsync(ct);
        CaptureValidationPolicyRules.ValidateTimeline(rows);
        foreach (var row in rows)
        {
            if (!CaptureValidationPolicyRules.Text(row.ProtectedEvidenceReference, 500))
                throw AttendanceOperation.Conflict("capture_policy_evidence_unavailable");
            var proof = await operation.ReadPrivateAsync<CaptureValidationPolicyProof>(row.ProtectedEvidenceReference, EvidencePurpose, ct);
            if (proof.SchemaVersion != 1 || proof.Entry is null || proof.Entry.ProtectedEvidenceReference != "")
                throw AttendanceOperation.Conflict("capture_policy_evidence_unavailable");
            CaptureValidationPolicyRules.ValidateEntry(proof.Entry);
            if (proof.Entry.SourceFingerprint != row.SourceFingerprint || proof.Entry.Id != row.Id)
                throw AttendanceOperation.Conflict("capture_policy_evidence_unavailable");
        }
        return rows.Select(x => x with { Settings = CaptureValidationPolicyRules.Freeze(x.Settings) }).ToArray();
    }
    private static void Utc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero || value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue)
            throw AttendanceOperation.Invalid("capture_policy_utc_time_required");
    }
    private static CaptureValidationPolicyResponse Map(CaptureValidationPolicyEntry x) => new(x.Id, x.Revision, x.SupersedesVersionId,
        x.EffectiveFromUtc, x.PublishedAtUtc, x.PublishedBy, x.ApprovalReference, x.Settings, x.Reason, x.SourceFingerprint);
}
