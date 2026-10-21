using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiMissingEvidenceService(AttendanceOperation operation, IKpiMissingEvidenceRepository repository,
    IKpiMissingEvidenceSourceReader sources, KpiMissingEvidenceEvidenceReader evidence, KpiMissingEvidenceAuthority authority, IPrivateFileStore files)
{
    public Task<KpiMissingEvidenceSource> SourceAsync(Guid employee, Guid leave, DateOnly date, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(KpiPermissions.Read, "attendance.kpi.missing-evidence.source", employee, context,
            (_, token) => sources.ReadAsync(employee, leave, date, token), ct,
            resourceScopes: (_, token) => sources.ScopesAsync(employee, date, KpiMissingEvidenceRules.End(date), token));

    public Task<KpiMissingEvidenceView> PrepareAsync(Guid employee, PrepareKpiMissingEvidenceRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(KpiPermissions.Prepare,
            "attendance.kpi.missing-evidence.prepare", employee, key, request, request.Reason, context, async (actor, token) => {
                repository.RequireTransaction();
                var source = await sources.ReadAsync(employee, request.LeaveRequestId, request.BusinessDate, token);
                KpiMissingEvidenceEvidenceReader.Ready(source);
                if (source.Fingerprint != request.ExpectedSourceFingerprint) throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
                KpiMissingEvidenceRules.Unique(await repository.TargetAsync(employee, request.BusinessDate, request.LeaveRequestId, token));
                var scopes = KpiMissingEvidenceRules.Scopes(employee, source.Scopes,
                    await sources.ScopesAsync(employee, request.BusinessDate, KpiMissingEvidenceRules.End(request.BusinessDate), token));
                await authority.CheckAsync(actor, KpiPermissions.Prepare, employee, [source.LeaveRequester], scopes, token);
                var snapshot = new KpiMissingEvidenceSnapshot(1, employee, actor.Identity.AccountId, source);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot),
                    KpiMissingEvidenceRules.RequestPurpose(employee), token));
                if ((await sources.ReadAsync(employee, request.LeaveRequestId, request.BusinessDate, token)).Fingerprint != source.Fingerprint)
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
                var verified = await authority.CheckAsync(actor, KpiPermissions.Prepare, employee, [source.LeaveRequester], scopes, token);
                var approval = new ApprovalRequest(KpiMissingEvidenceRules.RequestType(request.BusinessDate, request.LeaveRequestId), employee,
                    actor.Identity.AccountId.ToString(), reference, 1, verified.AtUtc);
                repository.Add(approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<KpiMissingEvidenceView>(approval.Id, null, View(approval, snapshot, null));
            }, ct, resourceScopes: (_, token) => sources.ScopesAsync(employee, request.BusinessDate, KpiMissingEvidenceRules.End(request.BusinessDate), token),
            beforeReplay: async (actor, token) => {
                var source = await sources.ReadAsync(employee, request.LeaveRequestId, request.BusinessDate, token);
                KpiMissingEvidenceEvidenceReader.Ready(source);
                if (source.Fingerprint != request.ExpectedSourceFingerprint) throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
                await authority.CheckAsync(actor, KpiPermissions.Prepare, employee, [source.LeaveRequester], source.Scopes, token);
            });

    public Task<KpiMissingEvidenceView> GetAsync(Guid employee, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(KpiPermissions.Read, "attendance.kpi.missing-evidence.request", employee, context, async (_, token) => {
            var row = await evidence.FindAsync(employee, id, token); var snapshot = await evidence.SnapshotAsync(row, token);
            var view = View(row.Request, snapshot, row.Decisions.SingleOrDefault());
            if (row.Request.State != ApprovalState.Approved) return view;
            var history = await evidence.HistoryAsync(row, snapshot, token);
            return view with { ActiveRevisionId = history.Active.Id, ActiveCount = history.Active.ActiveCount,
                ActiveSourceFingerprint = history.Active.VerifiedSource.Fingerprint };
        }, ct, resourceScopes: (_, token) => ScopesAsync(employee, id, token));

    public Task<KpiMissingEvidenceView> DecideAsync(Guid employee, Guid id, DecideKpiMissingEvidenceRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(KpiPermissions.Decide,
            "attendance.kpi.missing-evidence.decide", employee, key, new { id, request }, request.Reason, context, async (actor, token) => {
                repository.RequireTransaction();
                var row = await evidence.FindAsync(employee, id, token); var snapshot = await evidence.SnapshotAsync(row, token);
                if (row.Request.State != ApprovalState.Pending || row.Decisions.Count != 0) throw AttendanceOperation.Conflict("kpi_missing_evidence_already_decided");
                if (request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
                    throw AttendanceOperation.Invalid("invalid_kpi_missing_decision");
                repository.ExpectVersion(row.Request, KpiMissingEvidenceRules.Version(row.Request, request.ExpectedVersion));
                var scopes = await ScopesAsync(employee, id, token);
                var live = await authority.CheckAsync(actor, KpiPermissions.Decide, employee,
                    [row.Request.RequesterUserId, snapshot.Source.LeaveRequester], scopes, token);
                KpiMissingEvidenceSource? current = null;
                if (request.Decision == ApprovalState.Approved) {
                    current = await sources.ReadAsync(employee, snapshot.Source.LeaveRequestId, snapshot.Source.BusinessDate, token);
                    if (current.Fingerprint != snapshot.Source.Fingerprint) throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
                    KpiMissingEvidenceEvidenceReader.Ready(current);
                    var others = (await repository.TargetAsync(employee, current.BusinessDate, current.LeaveRequestId, token)).Where(x => x.Id != id).ToArray();
                    KpiMissingEvidenceRules.Unique(others);
                    live = await authority.CheckAsync(actor, KpiPermissions.Decide, employee,
                        [row.Request.RequesterUserId, snapshot.Source.LeaveRequester], scopes, token);
                }
                var before = View(row.Request, snapshot, null);
                var decision = row.Request.Decide(request.Decision, actor.Identity.AccountId.ToString(), live.RoleCode, request.Reason, live.AtUtc,
                    row.Request.SnapshotPrivateReference);
                if (current is not null) {
                    var proof = new KpiMissingEvidenceApprovedProof(1, id, decision.Id, KpiMissingEvidenceRules.Hash(snapshot), current, live);
                    var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof),
                        KpiMissingEvidenceRules.ProofPurpose(employee), token));
                    if ((await sources.ReadAsync(employee, current.LeaveRequestId, current.BusinessDate, token)).Fingerprint != current.Fingerprint)
                        throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
                    await authority.CheckAsync(actor, KpiPermissions.Decide, employee,
                        [row.Request.RequesterUserId, snapshot.Source.LeaveRequester], scopes, token);
                    repository.Add(new AuditEvent(KpiMissingEvidenceRules.ApprovedEvent, actor.Identity.AccountId.ToString(), live.RoleCode,
                        KpiMissingEvidenceRules.TargetType, id.ToString(), live.AtUtc, context.CorrelationId, employee, reference,
                        context.IpAddress, actor.Identity.SessionId.ToString()));
                }
                repository.Add(decision); await repository.SaveChangesAsync(token);
                return new AttendanceChange<KpiMissingEvidenceView>(id, before, View(row.Request, snapshot, decision));
            }, ct, resourceScopes: (_, token) => ScopesAsync(employee, id, token), beforeReplay: async (actor, token) => {
                var row = await evidence.FindAsync(employee, id, token); var snapshot = await evidence.SnapshotAsync(row, token);
                var scopes = await ScopesAsync(employee, id, token);
                await authority.CheckAsync(actor, KpiPermissions.Decide, employee,
                    [row.Request.RequesterUserId, snapshot.Source.LeaveRequester], scopes, token);
                if (request.Decision == ApprovalState.Approved) {
                    var current = await sources.ReadAsync(employee, snapshot.Source.LeaveRequestId, snapshot.Source.BusinessDate, token);
                    if (current.Fingerprint != snapshot.Source.Fingerprint) throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
                    KpiMissingEvidenceEvidenceReader.Ready(current);
                }
            });

    private async Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employee, Guid id, CancellationToken ct)
    {
        var row = await evidence.FindAsync(employee, id, ct); var snapshot = await evidence.SnapshotAsync(row, ct);
        var retained = row.Request.State == ApprovalState.Approved ? (await evidence.HistoryAsync(row, snapshot, ct)).Scopes : snapshot.Source.Scopes;
        return KpiMissingEvidenceRules.Scopes(employee, retained,
            await sources.ScopesAsync(employee, snapshot.Source.BusinessDate, KpiMissingEvidenceRules.End(snapshot.Source.BusinessDate), ct));
    }
    public Task<KpiMissingEvidenceRevisionView> ReviseAsync(Guid employee, Guid id, ReviseKpiMissingEvidenceRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(KpiPermissions.Decide,
            "attendance.kpi.missing-evidence.revise", employee, key, new { id, request }, request.Reason, context, async (actor, token) => {
                repository.RequireTransaction();
                var (row, snapshot, history, current, scopes) = await RevisionContextAsync(employee, id, request, token);
                if (history.Active.Id != request.ExpectedActiveRevision) throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_tip_changed");
                if (history.Revisions.Count > 32) throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_limit");
                if (current.Fingerprint == history.Active.VerifiedSource.Fingerprint)
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_source_unchanged");
                repository.ExpectVersion(row.Request, KpiMissingEvidenceRules.Version(row.Request, request.ExpectedVersion));
                var live = await authority.CheckAsync(actor, KpiPermissions.Decide, employee,
                    history.Requesters.Append(current.LeaveRequester), scopes, token);
                var proof = new KpiMissingEvidenceRevisionProof(1, Guid.NewGuid(), id, row.Decisions.Single().Id,
                    KpiMissingEvidenceRules.Hash(snapshot), history.Active.Id, history.Active.ProofFingerprint,
                    Convert.ToBase64String(row.Request.RowVersion), history.Active.VerifiedSource, current,
                    KpiMissingEvidenceEvidenceReader.RevisionCount(current), request.Reason.Trim(), live);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof),
                    KpiMissingEvidenceRules.RevisionPurpose(employee), token));
                var reread = await sources.ReadAsync(employee, current.LeaveRequestId, current.BusinessDate, token);
                if (KpiMissingEvidenceRules.Hash(reread) != KpiMissingEvidenceRules.Hash(current))
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
                KpiMissingEvidenceEvidenceReader.RevisionCount(reread);
                var checkedRow = await evidence.FindAsync(employee, id, token);
                KpiMissingEvidenceRules.Version(checkedRow.Request, request.ExpectedVersion);
                var checkedHistory = await evidence.HistoryAsync(checkedRow, await evidence.SnapshotAsync(checkedRow, token), token);
                if (checkedHistory.Active.Id != history.Active.Id || checkedHistory.Active.ProofFingerprint != history.Active.ProofFingerprint)
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_tip_changed");
                var finalScopes = KpiMissingEvidenceRules.Scopes(employee, checkedHistory.Scopes, reread.Scopes,
                    await sources.ScopesAsync(employee, current.BusinessDate, KpiMissingEvidenceRules.End(current.BusinessDate), token));
                if (KpiMissingEvidenceRules.Hash(finalScopes) != KpiMissingEvidenceRules.Hash(scopes))
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_scope_changed");
                await authority.CheckAsync(actor, KpiPermissions.Decide, employee,
                    checkedHistory.Requesters.Append(reread.LeaveRequester), finalScopes, token);
                var audit = new AuditEvent(KpiMissingEvidenceRules.RevisedEvent, actor.Identity.AccountId.ToString(), live.RoleCode,
                    KpiMissingEvidenceRules.TargetType, id.ToString(), live.AtUtc, context.CorrelationId, employee, reference,
                    context.IpAddress, actor.Identity.SessionId.ToString());
                repository.Add(audit); await repository.SaveChangesAsync(token);
                var view = new KpiMissingEvidenceRevisionView(audit.Id, id, history.Active.Id, Convert.ToBase64String(row.Request.RowVersion),
                    proof.ActiveCount, current.Fingerprint, KpiMissingEvidenceRules.Hash(proof));
                return new AttendanceChange<KpiMissingEvidenceRevisionView>(audit.Id, history.Active, view);
            }, ct, resourceScopes: (actor, token) => RevisionScopesAsync(employee, id, actor, token), beforeReplay: async (actor, token) => {
                var (_, _, history, current, scopes) = await RevisionContextAsync(employee, id, request, token);
                await authority.CheckAsync(actor, KpiPermissions.Decide, employee, history.Requesters.Append(current.LeaveRequester), scopes, token);
                if (history.Active.Id != request.ExpectedActiveRevision &&
                    (history.Active.SupersedesRevisionId != request.ExpectedActiveRevision ||
                        history.Active.VerifiedSource.Fingerprint != request.ExpectedSourceFingerprint ||
                        history.Active.ReviewAuthority.Actor.AccountId != actor.Identity.AccountId))
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_tip_changed");
            });

    private async Task<(KpiMissingEvidenceRow Row, KpiMissingEvidenceSnapshot Snapshot, KpiMissingEvidenceHistory History,
        KpiMissingEvidenceSource Current, IReadOnlyList<AttendanceResourceScope> Scopes)> RevisionContextAsync(
        Guid employee, Guid id, ReviseKpiMissingEvidenceRequest request, CancellationToken ct)
    {
        var row = await evidence.FindAsync(employee, id, ct); var snapshot = await evidence.SnapshotAsync(row, ct);
        var history = await evidence.HistoryAsync(row, snapshot, ct);
        KpiMissingEvidenceRules.Version(row.Request, request.ExpectedVersion);
        if (request.ExpectedActiveRevision == Guid.Empty || !history.Revisions.Any(x => x.Id == request.ExpectedActiveRevision))
            throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_tip_changed");
        var current = await sources.ReadAsync(employee, snapshot.Source.LeaveRequestId, snapshot.Source.BusinessDate, ct);
        if (current.EmployeeId != employee || current.LeaveRequestId != snapshot.Source.LeaveRequestId || current.BusinessDate != snapshot.Source.BusinessDate)
            throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_source_conflict");
        KpiMissingEvidenceEvidenceReader.RevisionCount(current);
        if (current.Fingerprint != request.ExpectedSourceFingerprint) throw AttendanceOperation.Conflict("kpi_missing_evidence_source_changed");
        KpiMissingEvidenceRules.Unique((await repository.TargetAsync(employee, current.BusinessDate, current.LeaveRequestId, ct)).Where(x => x.Id != id).ToArray());
        var scopes = KpiMissingEvidenceRules.Scopes(employee, history.Scopes, current.Scopes,
            await sources.ScopesAsync(employee, current.BusinessDate, KpiMissingEvidenceRules.End(current.BusinessDate), ct));
        return (row, snapshot, history, current, scopes);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> RevisionScopesAsync(Guid employee, Guid id, AttendanceActor actor, CancellationToken ct)
    {
        var row = await evidence.FindAsync(employee, id, ct); var snapshot = await evidence.SnapshotAsync(row, ct);
        var history = await evidence.HistoryAsync(row, snapshot, ct);
        KpiMissingEvidenceRules.Independent(actor.Identity, employee, history.Requesters);
        var current = await sources.ReadAsync(employee, snapshot.Source.LeaveRequestId, snapshot.Source.BusinessDate, ct);
        KpiMissingEvidenceRules.Independent(actor.Identity, employee, history.Requesters.Append(current.LeaveRequester));
        return KpiMissingEvidenceRules.Scopes(employee, history.Scopes, current.Scopes,
            await sources.ScopesAsync(employee, snapshot.Source.BusinessDate, KpiMissingEvidenceRules.End(snapshot.Source.BusinessDate), ct));
    }
    private static KpiMissingEvidenceView View(ApprovalRequest row, KpiMissingEvidenceSnapshot snapshot, ApprovalDecision? decision) =>
        new(row.Id, row.SubjectId, snapshot.Source.BusinessDate, snapshot.Source.LeaveRequestId, row.State.ToString(),
            Convert.ToBase64String(row.RowVersion), snapshot.Source.Fingerprint, decision?.Id);
}
