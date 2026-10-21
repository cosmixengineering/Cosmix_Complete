using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class AttendanceKpiService(AttendanceOperation operation, IKpiRepository repository,
    IKpiSourceReader sources, IAttendanceSummaryRepository summaries, IFinalizedKpiReader finalized,
    IPrivateFileStore files, TimeProvider clock, INotificationOutbox notifications,
    IWorkTreatmentDisciplinaryOutcomeReader? disciplinary = null)
{
    public const string RequestType = "AttendanceKpiScore", FinalizedEvent = "attendance.kpi.finalized", TargetType = "AttendanceKpiPeriod";
    public static string Target(Guid employee, DateOnly from) => $"{employee:N}:{from:yyyyMMdd}";
    public static string RequestPurpose(Guid employee) => $"attendance:kpi:request:{employee:N}";
    public static string FinalPurpose(Guid employee, DateOnly from) => $"attendance:kpi:final:{Target(employee, from)}";

    public Task<KpiSource> PreviewAsync(Guid employee, DateOnly asOf, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(KpiPermissions.Read, "attendance.kpi.preview", employee, context,
            (_, token) => sources.ReadAsync(employee, asOf, token), ct,
            resourceScopes: (_, token) => sources.ScopesAsync(employee, asOf, token));

    public Task<KpiRequestView> PrepareAsync(Guid employee, PrepareKpiRequest request, string key,
        AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(KpiPermissions.Prepare, "attendance.kpi.prepare", employee, key, request, request.Reason, context,
            async (actor, token) => {
                var source = await sources.ReadAsync(employee, request.AsOf, token);
                PreparationReady(source);
                if (!Digest(request.ExpectedSourceFingerprint) || request.ExpectedSourceFingerprint != source.Fingerprint)
                    throw AttendanceOperation.Conflict("kpi_source_changed");
                var previous = await finalized.ReadAsync(employee, source.From, token);
                if (previous?.Id != request.ExpectedPreviousFinalizationId)
                    throw AttendanceOperation.Conflict("kpi_final_revision_changed");
                if (previous?.SourceFingerprint == source.Fingerprint) throw AttendanceOperation.Conflict("kpi_source_already_finalized");
                var pending = await repository.PendingAsync(employee, token);
                if (pending.Count > 128) throw AttendanceOperation.Conflict("kpi_pending_limit");
                foreach (var row in pending) {
                    var old = await Snapshot(row, token);
                    if (old.Source.From == source.From) throw AttendanceOperation.Conflict("kpi_period_review_pending");
                }
                var snapshot = new KpiRequestSnapshot(1, employee, actor.Identity.AccountId, request.AsOf, source, request.ExpectedPreviousFinalizationId);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), RequestPurpose(employee), token));
                if ((await sources.ReadAsync(employee, request.AsOf, token)).Fingerprint != source.Fingerprint)
                    throw AttendanceOperation.Conflict("kpi_source_changed");
                var approval = new ApprovalRequest(RequestType, employee, actor.Identity.AccountId.ToString(), reference, 1, clock.GetUtcNow());
                repository.Add(approval);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<KpiRequestView>(approval.Id, previous, View(approval, snapshot, null, null));
            }, ct, resourceScopes: (_, token) => sources.ScopesAsync(employee, request.AsOf, token));

    public Task<KpiRequestView> GetAsync(Guid employee, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(KpiPermissions.Read, "attendance.kpi.request.read", employee, context,
            async (_, token) => {
                var row = await Find(employee, id, token);
                var snapshot = await Snapshot(row.Request, token);
                var latest = await finalized.ReadAsync(employee, snapshot.Source.From, token);
                return View(row.Request, snapshot, row.Decisions.SingleOrDefault(), latest?.RequestId == id ? latest : null);
            }, ct, resourceScopes: (_, token) => Scopes(employee, id, token));

    public Task<KpiRequestView> DecideAsync(Guid employee, Guid id, DecideKpiRequest request, string key,
        AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(KpiPermissions.Decide, "attendance.kpi.decide", employee, key, new { id, request }, request.Reason, context,
            async (actor, token) => {
                var row = await Find(employee, id, token);
                var snapshot = await Snapshot(row.Request, token);
                if (row.Request.State != ApprovalState.Pending || row.Decisions.Count != 0)
                    throw AttendanceOperation.Conflict("kpi_request_already_decided");
                if (request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
                    throw AttendanceOperation.Invalid("invalid_kpi_decision");
                repository.ExpectVersion(row.Request, Version(request.ExpectedVersion));
                var before = View(row.Request, snapshot, null, null);
                FinalizedKpiScore? score = null;
                var at = clock.GetUtcNow();
                if (request.Decision == ApprovalState.Approved)
                {
                    var current = await sources.ReadAsync(employee, snapshot.AsOf, token);
                    PreparationReady(current);
                    if (current.Fingerprint != snapshot.Source.Fingerprint) throw AttendanceOperation.Conflict("kpi_source_changed");
                    if (current.Calculation.RequiresDisciplinaryReview)
                    {
                        if (current.SourceManifest!.Input.Rules.Kpi!.Beyond120Treatment == Cosmix.Domain.HR.Policies.KpiBeyondTierTreatment.ReviewRequired)
                            throw AttendanceOperation.Conflict("kpi_beyond_120_valuation_rule_required");
                        var reviewed = disciplinary is null ? null : await disciplinary.ReadAsync(employee, id, current.Fingerprint, token);
                        if (reviewed is null || reviewed.EmployeeId != employee || reviewed.AnnualLateSourceId != id ||
                            reviewed.AnnualLateSourceFingerprint != current.Fingerprint || reviewed.ClosedCaseId == Guid.Empty ||
                            reviewed.Outcome == WorkCaseOutcome.ReferredForFurtherReview)
                            throw AttendanceOperation.Conflict("kpi_disciplinary_review_required");
                    }
                    var previous = await finalized.ReadAsync(employee, current.From, token);
                    if (previous?.Id != snapshot.ExpectedPreviousFinalizationId) throw AttendanceOperation.Conflict("kpi_final_revision_changed");
                    var decision = row.Request.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                        request.Reason, at, row.Request.SnapshotPrivateReference);
                    score = new(1, Guid.NewGuid(), employee, current.From, current.ToExclusive, checked((previous?.Revision ?? 0) + 1),
                        previous?.Id, id, decision.Id, current.PolicyVersionId!.Value, current.Calculation.ExactScore!.Value,
                        current.Calculation.DisplayScore!.Value, current.Calculation.Fingerprint, current.Fingerprint,
                        actor.Identity.AccountId, decision.DecidedAt, current.Scopes);
                    var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(score), FinalPurpose(employee, current.From), token));
                    if ((await sources.ReadAsync(employee, snapshot.AsOf, token)).Fingerprint != current.Fingerprint)
                        throw AttendanceOperation.Conflict("kpi_source_changed");
                    repository.Add(decision);
                    repository.Add(new AuditEvent(FinalizedEvent, actor.Identity.AccountId.ToString(), actor.RoleCode, TargetType,
                        Target(employee, current.From), decision.DecidedAt, context.CorrelationId, employee, reference,
                        context.IpAddress, actor.Identity.SessionId.ToString()));
                }
                else repository.Add(row.Request.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    request.Reason, at, row.Request.SnapshotPrivateReference));
                await repository.SaveChangesAsync(token);
                var decided = await Find(employee, id, token);
                await notifications.StageAsync(new(decided.Decisions.Single().Id, snapshot.RequesterAccountId,
                    NotificationTemplates.Decision, id), context, token);
                return new AttendanceChange<KpiRequestView>(id, before,
                    View(decided.Request, snapshot, decided.Decisions.Single(), score));
            }, ct, resourceScopes: (_, token) => Scopes(employee, id, token),
            beforeReplay: async (actor, token) => {
                var row = await Find(employee, id, token);
                if (row.Request.RequesterUserId == actor.Identity.AccountId.ToString() || actor.Identity.EmployeeId == employee)
                    throw AttendanceOperation.Denied();
            });

    private async Task<IReadOnlyList<AttendanceResourceScope>> Scopes(Guid employee, Guid id, CancellationToken ct)
    {
        var row = await Find(employee, id, ct);
        var snapshot = await Snapshot(row.Request, ct);
        var current = await summaries.ResourceScopesAsync(employee, snapshot.Source.From, snapshot.Source.ToExclusive.AddDays(-1), ct);
        var reviewed = disciplinary is null || !snapshot.Source.Calculation.RequiresDisciplinaryReview ? null :
            await disciplinary.ReadAsync(employee, id, snapshot.Source.Fingerprint, ct);
        return current.Concat(snapshot.Source.Scopes).Concat(reviewed?.OriginalScopes ?? []).DistinctBy(x => JsonSerializer.Serialize(x)).ToArray();
    }
    private async Task<KpiRequestRow> Find(Guid employee, Guid id, CancellationToken ct)
    {
        if (employee == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        var row = await repository.FindAsync(employee, id, ct) ?? throw AttendanceOperation.Missing();
        if (row.Request.RequestType != RequestType || row.Request.SubjectId != employee || row.Decisions.Count > 1)
            throw AttendanceOperation.Conflict("kpi_request_source_conflict");
        return row;
    }
    private async Task<KpiRequestSnapshot> Snapshot(ApprovalRequest row, CancellationToken ct)
    {
        var s = await operation.ReadPrivateAsync<KpiRequestSnapshot>(row.SnapshotPrivateReference, RequestPurpose(row.SubjectId), ct);
        if (s.SchemaVersion != 1 || s.EmployeeId != row.SubjectId || s.Source.EmployeeId != row.SubjectId ||
            s.RequesterAccountId.ToString() != row.RequesterUserId || s.AsOf.AddDays(1) != s.Source.ToExclusive ||
            !Digest(s.Source.Fingerprint) || !AttendanceScopeEvaluation.IsValid(s.Source.Scopes) ||
            !s.Source.Scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId == row.SubjectId))
            throw AttendanceOperation.Conflict("kpi_request_evidence_conflict");
        return s;
    }
    public static void Ready(KpiSource source)
    {
        PreparationReady(source);
        if (source.Calculation.RequiresDisciplinaryReview) throw AttendanceOperation.Conflict("kpi_disciplinary_review_required");
    }
    private static void PreparationReady(KpiSource source)
    {
        if (source.Calculation.State is not ("ready_for_review" or "review_required") ||
            source.Calculation.Issues.Any(x => x != "kpi_beyond_120_review_required") ||
            source.Calculation.State == "review_required" && !source.Calculation.RequiresDisciplinaryReview ||
            source.PolicyVersionId is null || source.PolicyVersionId == Guid.Empty || source.Calculation.ExactScore is null or < 0 or > 100 ||
            source.Calculation.DisplayScore is null || source.SourceManifest is null)
            throw AttendanceOperation.Conflict("kpi_verified_calculation_required");
        VerifyManifest(source);
        if (source.ToExclusive != source.From.AddYears(1)) throw AttendanceOperation.Conflict("kpi_annual_period_incomplete");
    }
    internal static void VerifyManifest(KpiSource source)
    {
        var manifest = source.SourceManifest;
        if (manifest is null || manifest.CalculationVersion != AttendanceKpiCalculation.Version ||
            manifest.Input.From != source.From || manifest.Input.ToExclusive != source.ToExclusive ||
            manifest.SavedDays.Count != manifest.Input.Days.Count || manifest.SavedDays.Any(x =>
                !manifest.Input.Days.Any(d => d.Date == x.BusinessDate && d.SummaryId == x.SummaryId && d.SourceFingerprint == x.InputFingerprint) ||
                x.Revision < 1 || x.AttendancePolicyVersionId is null || x.KpiPolicyVersionId == Guid.Empty || x.KpiAssignmentIds.Count == 0) ||
            manifest.SavedDays.Select(x => x.BusinessDate).Distinct().Count() != manifest.SavedDays.Count ||
            JsonSerializer.Serialize(AttendanceKpiCalculation.Calculate(manifest.Input)) != JsonSerializer.Serialize(source.Calculation))
            throw AttendanceOperation.Conflict("kpi_calculation_manifest_conflict");
    }
    private static KpiRequestView View(ApprovalRequest row, KpiRequestSnapshot s, ApprovalDecision? decision, FinalizedKpiScore? score) =>
        new(row.Id, row.SubjectId, s.Source.From, s.Source.ToExclusive, row.State.ToString(), Convert.ToBase64String(row.RowVersion),
            s.Source.Fingerprint, s.Source.Calculation, decision?.Id, score?.Id, score?.Revision);
    private static bool Digest(string? value) => value?.Length == 64 && value.All(char.IsAsciiHexDigit);
    private static byte[] Version(string? value) { try { var b = Convert.FromBase64String(value ?? ""); return b.Length == 8 ? b : throw AttendanceOperation.Invalid("invalid_kpi_version"); }
        catch (FormatException) { throw AttendanceOperation.Invalid("invalid_kpi_version"); } }
}
