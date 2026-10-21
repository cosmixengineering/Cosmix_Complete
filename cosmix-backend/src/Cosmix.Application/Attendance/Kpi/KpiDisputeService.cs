using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.Kpi;

/// <summary>Retained disputes only. Score preparation and independent score review remain in AttendanceKpiService.</summary>
public sealed class KpiDisputeService(AttendanceOperation operation, IKpiDisputeRepository repository,
    IFinalizedKpiReader finalized, IKpiSourceReader sources, IAttendanceSummaryRepository summaries,
    KpiDisputeEvidenceReader evidence, KpiDisputeAuthority authority, IPrivateFileStore files,
    INotificationOutbox notifications, TimeProvider clock)
{
    public Task<MyFinalizedKpiView> MyScoreAsync(DateOnly yearFrom, AuthRequestContext context, CancellationToken ct)
    {
        KpiDisputeRules.Year(yearFrom);
        return operation.ReadAsync(KpiDisputePermissionDefs.Self, "attendance.kpi.self.read", null, context,
            async (actor, token) => {
                await authority.SelfAsync(actor, token);
                var score = await Final(actor.Identity.EmployeeId, yearFrom, token);
                await authority.SelfAsync(actor, token);
                return KpiDisputeRules.SelfScore(score);
            }, ct, self: true, requireDevice: true);
    }

    public Task<KpiDisputeView> SubmitAsync(CreateKpiDisputeRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        KpiDisputeRules.Create(request);
        return operation.WriteAsync(KpiDisputePermissionDefs.Self, "attendance.kpi.dispute.submit", null, key, request, request.Reason, context,
            async (actor, token) => {
                var employee = actor.Identity.EmployeeId;
                var final = await Final(employee, request.YearFrom, token);
                if (final.Id != request.ExpectedFinalizationId || final.Revision != request.ExpectedFinalRevision ||
                    final.SourceFingerprint != request.ExpectedFinalSourceFingerprint)
                    throw AttendanceOperation.Conflict("kpi_dispute_final_version_changed");
                var source = await Source(final, token);
                var rows = await repository.YearAsync(employee, request.YearFrom, null, null, token);
                if (rows.Count >= KpiDisputeRules.YearLimit) throw AttendanceOperation.Conflict("kpi_dispute_year_limit");
                var prior = new List<KpiDisputeEvidence>();
                foreach (var row in rows) {
                    var old = await evidence.ReadAsync(employee, row.Id, token);
                    if (old.Draft.FinalScore.Id == final.Id) prior.Add(old);
                }
                ValidateResubmission(request, actor, prior);
                var documents = await evidence.DocumentsAsync(employee, request.EvidenceDocumentIds ?? [], clock.GetUtcNow(), token);
                await authority.SelfAsync(actor, token);
                var at = clock.GetUtcNow();
                var draft = new KpiDisputeDraft(1, Guid.NewGuid(), actor.Identity, actor.DeviceId!.Value,
                    actor.RoleCode, key, at, final, source, request.ReasonKind, request.Statement.Trim(), request.Reason.Trim(), documents, request.ResubmissionOf);
                var reference = await Store(draft, KpiDisputeRules.DraftPurpose(employee), token);
                await CheckSources(final, source, documents, token);
                await authority.SelfAsync(actor, token);
                var approval = new ApprovalRequest(KpiDisputeRules.RequestType(final), employee,
                    actor.Identity.AccountId.ToString(), reference, 1, at);
                repository.Add(approval);
                repository.Add(Audit(KpiDisputeRules.SubmittedEvent, employee, approval.Id, actor, at, reference, context, actor.DeviceId.Value));
                await repository.SaveChangesAsync(token);
                var saved = await evidence.ReadAsync(employee, approval.Id, token);
                return new AttendanceChange<KpiDisputeView>(approval.Id, null, KpiDisputeRules.View(saved));
            }, ct, self: true, requireDevice: true, beforeReplay: (actor, token) => authority.SelfAsync(actor, token));
    }

    public Task<KpiDisputeView> MyDetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(KpiDisputePermissionDefs.Self, "attendance.kpi.dispute.self.read", null, context,
            async (actor, token) => {
                var retained = await evidence.ReadAsync(actor.Identity.EmployeeId, id, token);
                Own(actor, retained);
                await authority.SelfAsync(actor, token);
                return KpiDisputeRules.View(retained);
            }, ct, self: true, requireDevice: true);

    public Task<PagedResult<KpiDisputeView>> MyListAsync(KpiDisputeListRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        KpiDisputeRules.List(request);
        return operation.ReadAsync<PagedResult<KpiDisputeView>>(KpiDisputePermissionDefs.Self, "attendance.kpi.dispute.self.list", null, context,
            async (actor, token) => {
                var page = await repository.PageAsync(actor.Identity.EmployeeId, request, actor.Identity.AccountId, null, token);
                var views = new List<KpiDisputeView>();
                foreach (var row in page.Items) {
                    var retained = await evidence.ReadAsync(actor.Identity.EmployeeId, row.Id, token);
                    Own(actor, retained);
                    views.Add(KpiDisputeRules.View(retained));
                }
                await authority.SelfAsync(actor, token);
                return new(views, page.TotalCount, page.Page, page.PageSize);
            }, ct, self: true, requireDevice: true);
    }

    public Task<KpiDisputeReviewView> ReviewDetailAsync(Guid employee, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<KpiDisputeReviewView>(KpiDisputePermissionDefs.Decide, "attendance.kpi.dispute.review.read", employee, context,
            async (actor, token) => {
                var retained = await evidence.ReadAsync(employee, id, token);
                await ReviewAuthority(actor, retained, token);
                return new(KpiDisputeRules.View(retained), retained.Draft.FinalScore, retained.Draft.SourceAtSubmission,
                    retained.Draft.Documents, retained.Response, retained.Amendment);
            }, ct, resourceScopes: (actor, token) => ReviewScopes(actor, employee, id, token));

    public Task<PagedResult<KpiDisputeView>> ReviewListAsync(Guid employee, KpiDisputeListRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        KpiDisputeRules.List(request);
        return operation.ReadAsync<PagedResult<KpiDisputeView>>(KpiDisputePermissionDefs.Decide, "attendance.kpi.dispute.review.list", employee, context,
            async (actor, token) => {
                // All retained annual scopes were authorized before SQL count/pagination.
                var page = await repository.PageAsync(employee, request, null, actor.Identity.AccountId, token);
                var views = new List<KpiDisputeView>();
                foreach (var row in page.Items) {
                    var retained = await evidence.ReadAsync(employee, row.Id, token);
                    await ReviewAuthority(actor, retained, token);
                    views.Add(KpiDisputeRules.View(retained));
                }
                await authority.ReviewAsync(actor, employee, null, await YearScopes(actor, employee, request.YearFrom, token), token);
                return new(views, page.TotalCount, page.Page, page.PageSize);
            }, ct, resourceScopes: (actor, token) => YearScopes(actor, employee, request.YearFrom, token));
    }

    public Task<KpiDisputeView> RespondAsync(Guid employee, Guid id, RespondKpiDisputeRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        KpiDisputeRules.Text(request.Reason, 1000, "invalid_kpi_dispute_reason");
        KpiDisputeRules.State(request.Outcome);
        KpiDisputeRules.Version(request.ExpectedVersion);
        return operation.WriteAsync(KpiDisputePermissionDefs.Decide, "attendance.kpi.dispute.respond", employee, key,
            new { id, request }, request.Reason, context, async (actor, token) => {
                var retained = await evidence.ReadAsync(employee, id, token);
                if (retained.Response is not null) throw AttendanceOperation.Conflict("kpi_dispute_already_responded");
                KpiDisputeRules.ExpectVersion(repository, retained.Row.Request, request.ExpectedVersion);
                var draft = retained.Draft;
                var observedFinal = await Final(employee, draft.FinalScore.From, token);
                var source = await Source(observedFinal, token);
                var expectedFinal = request.Outcome == KpiDisputeOutcome.Returned ? observedFinal : draft.FinalScore;
                var expectedSource = request.Outcome == KpiDisputeOutcome.Returned ? source : draft.SourceAtSubmission;
                await CheckSources(expectedFinal, expectedSource, draft.Documents, token);
                if (request.Outcome == KpiDisputeOutcome.ReviewedNoChange && !KpiDisputeRules.MatchesFinal(source, draft.FinalScore))
                    throw AttendanceOperation.Conflict("kpi_dispute_correction_review_required");
                var proof = await ResponseAuthority(actor, retained, observedFinal, source, token);
                var response = new KpiDisputeResponseSnapshot(1, id, employee, draft.FinalScore.Id,
                    KpiDisputeRules.Hash(draft), request.Outcome, request.Reason.Trim(), proof.VerifiedAtUtc, observedFinal, source, draft.Documents, proof);
                var reference = await Store(response, KpiDisputeRules.ResponsePurpose(employee, id), token);
                await CheckSources(expectedFinal, expectedSource, draft.Documents, token);
                SameAuthority(proof, await ResponseAuthority(actor, retained, observedFinal, source, token));
                var before = KpiDisputeRules.View(retained);
                var decision = retained.Row.Request.Decide(KpiDisputeRules.State(request.Outcome), actor.Identity.AccountId.ToString(),
                    actor.RoleCode, response.Reason, response.RespondedAtUtc, reference);
                repository.Add(decision);
                await notifications.StageAsync(new(decision.Id, draft.Requester.AccountId, NotificationTemplates.Decision, id), context, token);
                await CheckSources(expectedFinal, expectedSource, draft.Documents, token);
                SameAuthority(proof, await ResponseAuthority(actor, retained, observedFinal, source, token));
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<KpiDisputeView>(id, before, KpiDisputeRules.View(await evidence.ReadAsync(employee, id, token)));
            }, ct, resourceScopes: (actor, token) => ReviewScopes(actor, employee, id, token),
            beforeReplay: async (actor, token) => { await ReviewAuthority(actor, await evidence.ReadAsync(employee, id, token), token); });
    }

    public Task<KpiDisputeView> LinkAmendmentAsync(Guid employee, Guid id, LinkKpiDisputeAmendmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        KpiDisputeRules.Text(request.Reason, 1000, "invalid_kpi_dispute_reason");
        KpiDisputeRules.Version(request.ExpectedVersion);
        if (request.ExpectedAmendedFinalizationId == Guid.Empty) throw AttendanceOperation.Invalid("invalid_kpi_dispute_amendment");
        return operation.WriteAsync(KpiDisputePermissionDefs.Decide, "attendance.kpi.dispute.amendment.link", employee, key,
            new { id, request }, request.Reason, context, async (actor, token) => {
                var retained = await evidence.ReadAsync(employee, id, token);
                if (retained.Response?.Outcome != KpiDisputeOutcome.CorrectionRequired || retained.Amendment is not null)
                    throw AttendanceOperation.Conflict("kpi_dispute_amendment_not_pending");
                KpiDisputeRules.ExpectVersion(repository, retained.Row.Request, request.ExpectedVersion);
                var old = retained.Draft.FinalScore;
                var amended = await Final(employee, old.From, token);
                if (amended.Id != request.ExpectedAmendedFinalizationId || amended.SupersedesId != old.Id ||
                    amended.Revision != old.Revision + 1 || amended.RequestId == old.RequestId || amended.DecisionId == old.DecisionId ||
                    amended.FinalizedAtUtc < retained.Response.RespondedAtUtc)
                    throw AttendanceOperation.Conflict("kpi_dispute_verified_successor_required");
                var source = await Source(amended, token);
                if (!KpiDisputeRules.MatchesFinal(source, amended)) throw AttendanceOperation.Conflict("kpi_dispute_amended_source_changed");
                var scopes = KpiDisputeRules.Merge(employee, await Scopes(retained, token), amended.OriginalScopes, source.Scopes);
                var proof = await authority.ReviewAsync(actor, employee, retained.Row.Request.RequesterUserId, scopes, token);
                var amendment = new KpiDisputeAmendmentSnapshot(1, id, employee, old.Id, amended, source.Fingerprint,
                    request.Reason.Trim(), proof.VerifiedAtUtc, proof);
                var reference = await Store(amendment, KpiDisputeRules.AmendmentPurpose(employee, id), token);
                await CheckSources(amended, source, [], token);
                var checkedProof = await authority.ReviewAsync(actor, employee, retained.Row.Request.RequesterUserId,
                    KpiDisputeRules.Merge(employee, await Scopes(retained, token), amended.OriginalScopes, source.Scopes), token);
                SameAuthority(proof, checkedProof);
                repository.Add(Audit(KpiDisputeRules.AmendmentEvent, employee, id, actor, amendment.LinkedAtUtc, reference, context, actor.Identity.SessionId));
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<KpiDisputeView>(id, KpiDisputeRules.View(retained), KpiDisputeRules.View(await evidence.ReadAsync(employee, id, token)));
            }, ct, resourceScopes: async (actor, token) => {
                var retained = await evidence.ReadAsync(employee, id, token);
                KpiDisputeRules.Independent(actor.Identity, employee, retained.Row.Request.RequesterUserId);
                var latest = await Final(employee, retained.Draft.FinalScore.From, token);
                return KpiDisputeRules.Merge(employee, await Scopes(retained, token), latest.OriginalScopes);
            }, beforeReplay: async (actor, token) => { await ReviewAuthority(actor, await evidence.ReadAsync(employee, id, token), token); });
    }

    private async Task<FinalizedKpiScore> Final(Guid employee, DateOnly from, CancellationToken ct)
    {
        repository.RequireTransaction();
        var score = await finalized.ReadAsync(employee, from, ct) ?? throw AttendanceOperation.Missing("kpi_finalized_score_unavailable");
        KpiDisputeRules.Final(score, employee, from);
        if (score.FinalizedAtUtc > clock.GetUtcNow()) throw AttendanceOperation.Conflict("kpi_dispute_final_evidence_conflict");
        return score;
    }
    private async Task<KpiSource> Source(FinalizedKpiScore final, CancellationToken ct)
    {
        var source = await sources.ReadAsync(final.EmployeeId, final.ToExclusive.AddDays(-1), ct);
        KpiDisputeRules.Source(source, final);
        return source;
    }
    private async Task CheckSources(FinalizedKpiScore final, KpiSource source,
        IReadOnlyList<KpiDisputeDocument> documents, CancellationToken ct)
    {
        if (KpiDisputeRules.Hash(await Final(final.EmployeeId, final.From, ct)) != KpiDisputeRules.Hash(final))
            throw AttendanceOperation.Conflict("kpi_dispute_final_version_changed");
        if (KpiDisputeRules.Hash(await Source(final, ct)) != KpiDisputeRules.Hash(source))
            throw AttendanceOperation.Conflict("kpi_dispute_source_changed");
        var actual = await evidence.DocumentsAsync(final.EmployeeId, documents.Select(x => x.Id).ToArray(), clock.GetUtcNow(), ct);
        if (KpiDisputeRules.Hash(actual) != KpiDisputeRules.Hash(documents))
            throw AttendanceOperation.Conflict("kpi_dispute_document_version_changed");
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> Scopes(KpiDisputeEvidence retained, CancellationToken ct)
    {
        var final = retained.Draft.FinalScore;
        var current = await summaries.ResourceScopesAsync(final.EmployeeId, final.From, final.ToExclusive.AddDays(-1), ct);
        var sourceScopes = await sources.ScopesAsync(final.EmployeeId, final.ToExclusive.AddDays(-1), ct);
        var sets = new List<IReadOnlyList<AttendanceResourceScope>> { current, sourceScopes, final.OriginalScopes, retained.Draft.SourceAtSubmission.Scopes };
        if (retained.Response is not null) sets.Add(retained.Response.Authority.Scopes);
        if (retained.Amendment is not null) sets.Add(retained.Amendment.Authority.Scopes);
        return KpiDisputeRules.Merge(final.EmployeeId, sets.ToArray());
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> ReviewScopes(AttendanceActor actor, Guid employee, Guid id, CancellationToken ct)
    {
        KpiDisputeRules.Independent(actor.Identity, employee, null);
        var retained = await evidence.ReadAsync(employee, id, ct);
        KpiDisputeRules.Independent(actor.Identity, employee, retained.Row.Request.RequesterUserId);
        var observedFinal = await Final(employee, retained.Draft.FinalScore.From, ct);
        return KpiDisputeRules.Merge(employee, await Scopes(retained, ct), observedFinal.OriginalScopes);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> YearScopes(AttendanceActor actor, Guid employee, DateOnly from, CancellationToken ct)
    {
        KpiDisputeRules.Independent(actor.Identity, employee, null);
        var sets = new List<IReadOnlyList<AttendanceResourceScope>> {
            await summaries.ResourceScopesAsync(employee, from, from.AddYears(1).AddDays(-1), ct),
            await sources.ScopesAsync(employee, from.AddYears(1).AddDays(-1), ct)
        };
        var rows = await repository.YearAsync(employee, from, null, actor.Identity.AccountId, ct);
        foreach (var row in rows) {
            var retained = await evidence.ReadAsync(employee, row.Id, ct);
            KpiDisputeRules.Independent(actor.Identity, employee, retained.Row.Request.RequesterUserId);
            sets.Add(await Scopes(retained, ct));
        }
        return KpiDisputeRules.Merge(employee, sets.ToArray());
    }
    private async Task<KpiDisputeReviewAuthority> ReviewAuthority(AttendanceActor actor, KpiDisputeEvidence retained, CancellationToken ct) =>
        await authority.ReviewAsync(actor, retained.Row.Request.SubjectId, retained.Row.Request.RequesterUserId, await Scopes(retained, ct), ct);
    private async Task<KpiDisputeReviewAuthority> ResponseAuthority(AttendanceActor actor, KpiDisputeEvidence retained,
        FinalizedKpiScore observedFinal, KpiSource source, CancellationToken ct) =>
        await authority.ReviewAsync(actor, retained.Row.Request.SubjectId, retained.Row.Request.RequesterUserId,
            KpiDisputeRules.Merge(retained.Row.Request.SubjectId, await Scopes(retained, ct), observedFinal.OriginalScopes, source.Scopes), ct);
    private static void SameAuthority(KpiDisputeReviewAuthority expected, KpiDisputeReviewAuthority current)
    {
        if (KpiDisputeRules.Hash(expected.Permissions) != KpiDisputeRules.Hash(current.Permissions) ||
            KpiDisputeRules.Hash(expected.Scopes) != KpiDisputeRules.Hash(current.Scopes))
            throw AttendanceOperation.Conflict("kpi_dispute_review_authority_changed");
    }
    private void ValidateResubmission(CreateKpiDisputeRequest request, AttendanceActor actor, IReadOnlyList<KpiDisputeEvidence> prior)
    {
        if (prior.Any(x => x.Response?.Outcome != KpiDisputeOutcome.Returned)) throw AttendanceOperation.Conflict("kpi_dispute_duplicate_final_version");
        if (prior.Count == 0) {
            if (request.ResubmissionOf is not null) throw AttendanceOperation.Conflict("kpi_dispute_returned_request_required");
            return;
        }
        var tips = prior.Where(x => !prior.Any(n => n.Draft.ResubmissionOf == x.Row.Request.Id)).ToArray();
        if (tips.Length != 1 || request.ResubmissionOf != tips[0].Row.Request.Id ||
            tips[0].Row.Request.RequesterUserId != actor.Identity.AccountId.ToString())
            throw AttendanceOperation.Conflict("kpi_dispute_returned_request_required");
        KpiDisputeRules.ExpectVersion(repository, tips[0].Row.Request, request.ExpectedReturnedVersion!);
    }
    private static void Own(AttendanceActor actor, KpiDisputeEvidence retained)
    {
        if (retained.Row.Request.SubjectId != actor.Identity.EmployeeId || retained.Row.Request.RequesterUserId != actor.Identity.AccountId.ToString())
            throw AttendanceOperation.Denied();
    }
    private async Task<string> Store<T>(T payload, string purpose, CancellationToken ct) =>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(payload), purpose, ct));
    private static AuditEvent Audit(string eventType, Guid employee, Guid id, AttendanceActor actor, DateTimeOffset at,
        string reference, AuthRequestContext context, Guid device) => new(eventType, actor.Identity.AccountId.ToString(), actor.RoleCode,
            KpiDisputeRules.TargetType, id.ToString(), at, context.CorrelationId, employee, reference, context.IpAddress, device.ToString());
}
