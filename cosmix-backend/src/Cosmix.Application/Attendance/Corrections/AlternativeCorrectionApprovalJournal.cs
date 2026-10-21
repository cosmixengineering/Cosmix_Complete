using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.Corrections;

/// <summary>Scoped stage capability and durable decision-time authority. No missing-audit inference.</summary>
public sealed class AlternativeCorrectionApprovalJournal(IAlternativeCorrectionApprovalRepository repository,
    ICurrentAuthentication current, IAccountAccessReader access, IAccessManagementRepository authority,
    ISchedulingJournalRepository journal, IAttendanceSummaryRepository summaries, AttendanceOperation operation,
    IPrivateFileStore files, TimeProvider clock)
{
    public const string AuthorityOperation = "attendance.correction.alternative.authority";
    public const string AuthorityTarget = "AttendanceAlternativeDecision";
    private readonly Dictionary<Guid, (Guid Transaction, AlternativeCorrectionAuthoritySnapshot Snapshot)> staged = [];
    public static string Purpose(Guid employeeId, Guid decisionId) => $"attendance:alternative-authority:{employeeId:N}:{decisionId:N}";

    // Only the correction application assembly can issue this capability, after its exact decision/session flush.
    internal async Task<IDisposable> StageAsync(CorrectionRecord row, ApprovalDecision decision, WorkSession session,
        AttendanceActor actor, AuthRequestContext context, CancellationToken ct)
    {
        if (repository.TransactionId is not Guid transaction || staged.Count != 0 || actor.RoleCode != "ADMIN" ||
            current.Require() != actor.Identity || actor.Identity.EmployeeId == row.Correction.EmployeeId) throw Unavailable();
        var source = await repository.SourceAsync(row.Correction.EmployeeId, row.Correction.Id, decision.Id, ct);
        if (source is null || source.Session.Id != session.Id || source.Approval.Id != row.Approval.Id ||
            source.Decision.ActorUserId != actor.Identity.AccountId.ToString()) throw Unavailable();
        var evidence = await Evidence(source, ct);
        var scopes = await summaries.ResourceScopesAsync(source.Correction.EmployeeId, source.Correction.AttendanceDate,
            source.Correction.AttendanceDate, ct);
        var allScopes = evidence.OriginalResourceScopes!.Concat(scopes).DistinctBy(x => JsonSerializer.Serialize(x)).ToArray();
        if (!ValidScopes(allScopes, source.Correction.EmployeeId)) throw Unavailable();
        var queriedAt = clock.GetUtcNow();
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, queriedAt, ct);
        var checkedAt = clock.GetUtcNow();
        if (checkedAt < queriedAt || checkedAt < decision.DecidedAt || !ValidActor(actor.Identity) ||
            !await journal.IsCurrentSessionAsync(actor.Identity, ct)) throw Unavailable();
        Guid? assignment = null;
        PermissionGrant[] selected = [];
        foreach (var group in grants.Where(x => x.Permission == CorrectionPermissions.AlternativesDecide &&
            x.IsEffectiveAt(checkedAt) && x.IsEffectiveAt(decision.DecidedAt)).GroupBy(x => x.RoleAssignmentId).OrderBy(x => x.Key))
        {
            if (group.Key == Guid.Empty || !await authority.IsCurrentSystemRoleAssignmentAsync(actor.Identity.AccountId, group.Key, checkedAt, ct) ||
                await authority.GetAssignmentRoleCodeAsync(group.Key, ct) != "ADMIN") continue;
            var finalAt = clock.GetUtcNow();
            if (finalAt < checkedAt) throw Unavailable();
            var candidate = group.Where(x => x.IsEffectiveAt(finalAt)).ToArray();
            if (!AttendanceScopeEvaluation.Allows(actor.Identity, candidate, CorrectionPermissions.AlternativesDecide, allScopes)) continue;
            assignment = group.Key; selected = candidate; break;
        }
        if (assignment is null || selected.Length is < 1 or > AttendanceScopeEvaluation.MaximumTotalAlternatives) throw Unavailable();
        var alternative = evidence.AlternativeEvidence!;
        var snapshot = new AlternativeCorrectionAuthoritySnapshot(1, source.Correction.EmployeeId, source.Correction.AttendanceDate,
            source.Correction.Id, source.Approval.Id, source.Decision.Id, source.Session.Id, source.Correction.PrivateEvidenceReference,
            Hash(evidence), evidence.Source.SourceFingerprint!, alternative.Policy.Kind, alternative.Policy.Id,
            alternative.Policy.Fingerprint, alternative.Documents.Select(x => x.Id).Order().ToArray(), decision.DecidedAt,
            clock.GetUtcNow(), actor.Identity, SystemRoleKind.Admin, assignment.Value,
            selected.Select(AlternativeCorrectionPermissionInterval.Capture).OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray(), allScopes);
        await CurrentAuthority(snapshot, ct);
        if ((await repository.AuthorityAuditsAsync(snapshot.EmployeeId, snapshot.DecisionId, ct)).Count != 0 ||
            (await repository.DecisionAuditsAsync(snapshot.EmployeeId, snapshot.CorrectionId, ct)).Count != 0) throw Unavailable();
        var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot),
            Purpose(snapshot.EmployeeId, snapshot.DecisionId), ct));
        await CurrentAuthority(snapshot, ct);
        if (repository.TransactionId != transaction) throw Unavailable();
        journal.AddAudit(new AuditEvent(AuthorityOperation, actor.Identity.AccountId.ToString(), "ADMIN", AuthorityTarget,
            snapshot.DecisionId.ToString(), snapshot.VerifiedAtUtc, context.CorrelationId, employeeScopeId: snapshot.EmployeeId,
            protectedChangesReference: reference, ipAddress: context.IpAddress, deviceReference: actor.Identity.SessionId.ToString()));
        await journal.SaveChangesAsync(ct);
        staged.Add(decision.Id, (transaction, snapshot));
        return new StageLease(() => staged.Remove(decision.Id));
    }

    public async Task<AttendanceAlternativeCorrectionApprovalProof?> ReadAsync(Guid employeeId, Guid correctionId, Guid decisionId, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || correctionId == Guid.Empty || decisionId == Guid.Empty) return null;
        var source = await repository.SourceAsync(employeeId, correctionId, decisionId, ct);
        if (source is null) return null;
        var evidence = await Evidence(source, ct);
        var audits = await repository.AuthorityAuditsAsync(employeeId, decisionId, ct);
        if (audits.Count != 1) return null;
        var audit = audits[0];
        if (audit.EventType != AuthorityOperation || audit.TargetType != AuthorityTarget || audit.TargetId != decisionId.ToString() ||
            audit.EmployeeScopeId != employeeId || audit.ActorRoleCode != "ADMIN" || audit.ActorUserId != source.Decision.ActorUserId ||
            audit.ProtectedChangesReference is null) return null;
        var snapshot = await operation.ReadPrivateAsync<AlternativeCorrectionAuthoritySnapshot>(audit.ProtectedChangesReference, Purpose(employeeId, decisionId), ct);
        if (!Matches(snapshot, source, evidence) || audit.OccurredAt != snapshot.VerifiedAtUtc ||
            audit.DeviceReference != snapshot.Actor.SessionId.ToString()) return null;
        if (staged.TryGetValue(decisionId, out var stage))
        {
            if (repository.TransactionId != stage.Transaction || Hash(snapshot) != Hash(stage.Snapshot)) return null;
            var now = await CurrentAuthority(snapshot, ct);
            return Proof(snapshot, source.Decision, now, true, null);
        }
        var decisions = await repository.DecisionAuditsAsync(employeeId, correctionId, ct);
        if (decisions.Count != 1) return null;
        var outer = decisions[0];
        if (outer.EventType != "attendance.correction.decide" || outer.TargetType != "Attendance" || outer.TargetId != correctionId.ToString() ||
            outer.EmployeeScopeId != employeeId || outer.ActorUserId != source.Decision.ActorUserId || outer.ActorRoleCode != "ADMIN" ||
            outer.OccurredAt < snapshot.VerifiedAtUtc || outer.OccurredAt > clock.GetUtcNow() || outer.ProtectedChangesReference is null) return null;
        var saved = await operation.ReadPrivateAsync<DecisionAudit>(outer.ProtectedChangesReference,
            $"attendance:operation:{snapshot.Actor.AccountId:N}:attendance.correction.decide:audit", ct);
        if (saved.Operation != outer.EventType || saved.ResourceId != correctionId || saved.After is null || saved.After.Request is null ||
            saved.After.DecisionId != decisionId || saved.After.AppliedSessionRevisionId != snapshot.AppliedSessionId ||
            saved.After.Request.Id != correctionId || saved.After.Request.EmployeeId != employeeId || saved.After.Request.BusinessDate != snapshot.BusinessDate ||
            saved.After.Request.State != "Approved" || saved.After.Request.AlternativeKind != snapshot.AlternativeKind ||
            saved.Request?.Permission != CorrectionPermissions.AlternativesDecide || saved.Request.EmployeeId != employeeId ||
            saved.Request.Payload?.Id != correctionId || saved.Request.Payload.Request?.Decision != ApprovalState.Approved ||
            saved.Request.Payload.Request.AlternativeKind != snapshot.AlternativeKind) return null;
        return Proof(snapshot, source.Decision, source.Decision.DecidedAt, false, outer);
    }

    private async Task<CorrectionEvidenceSnapshot> Evidence(AlternativeCorrectionApprovalSource source, CancellationToken ct)
    {
        var c = source.Correction; var a = source.Approval; var d = source.Decision; var s = source.Session;
        if (a.RequestType != AttendanceCorrectionService.RequestType || a.State != ApprovalState.Approved ||
            c.Kind != AttendanceCorrectionKind.ManualSession || c.ApprovalRequestId != a.Id || d.ApprovalRequestId != a.Id ||
            a.SnapshotPrivateReference != c.PrivateEvidenceReference || d.ProtectedChangeReference != c.PrivateEvidenceReference ||
            d.Decision != ApprovalState.Approved || d.ActorRoleCode != "ADMIN" || d.ActorUserId == a.RequesterUserId ||
            !Guid.TryParse(a.RequesterUserId, out var requester) || !Guid.TryParse(d.ActorUserId, out var actor) || actor == Guid.Empty ||
            a.ClosedAt != d.DecidedAt || s.CalculatedAtUtc != d.DecidedAt || s.EmployeeId != c.EmployeeId || s.AttendanceDate != c.AttendanceDate ||
            s.CorrectionId != c.Id || s.Revision != 1 || s.CheckInPunchId is not null || s.CheckOutPunchId is not null ||
            s.StartsAtUtc != c.RequestedTimeUtc || s.EndsAtUtc != c.RequestedEndUtc || s.PolicyVersionId is not null) throw Unavailable();
        var evidence = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(c.PrivateEvidenceReference, AttendanceCorrectionService.Purpose(c.EmployeeId), ct);
        if (evidence.SchemaVersion != 5 || evidence.AlternativeEvidence is null || !AttendanceCorrectionService.Matches(new(c, a), evidence, requester)) throw Unavailable();
        return evidence;
    }

    private async Task<DateTimeOffset> CurrentAuthority(AlternativeCorrectionAuthoritySnapshot snapshot, CancellationToken ct)
    {
        if (repository.TransactionId is not Guid transaction || current.Require() != snapshot.Actor || !ValidActor(snapshot.Actor) ||
            snapshot.Actor.EmployeeId == snapshot.EmployeeId ||
            !await journal.IsCurrentSessionAsync(snapshot.Actor, ct)) throw Unavailable();
        var start = clock.GetUtcNow();
        var queried = await access.GetGrantsAsync(snapshot.Actor.AccountId, start, ct);
        var checkedAt = clock.GetUtcNow();
        if (checkedAt < start || checkedAt < snapshot.VerifiedAtUtc ||
            !await authority.IsCurrentSystemRoleAssignmentAsync(snapshot.Actor.AccountId, snapshot.RoleAssignmentId, checkedAt, ct) ||
            await authority.GetAssignmentRoleCodeAsync(snapshot.RoleAssignmentId, ct) != "ADMIN") throw Unavailable();
        var scopes = await summaries.ResourceScopesAsync(snapshot.EmployeeId, snapshot.BusinessDate, snapshot.BusinessDate, ct);
        var finalAt = clock.GetUtcNow();
        var retained = snapshot.Permissions.Select(x => x.Grant()).ToArray();
        var live = queried.Where(x => x.Permission == CorrectionPermissions.AlternativesDecide && x.RoleAssignmentId == snapshot.RoleAssignmentId &&
            x.IsEffectiveAt(finalAt) && retained.Any(y => Hash(AlternativeCorrectionPermissionInterval.Capture(x)) == Hash(AlternativeCorrectionPermissionInterval.Capture(y)))).ToArray();
        if (finalAt < checkedAt || !ValidScopes(scopes, snapshot.EmployeeId) ||
            !AttendanceScopeEvaluation.Allows(snapshot.Actor, live, CorrectionPermissions.AlternativesDecide, snapshot.AuthorizationScopes) ||
            !AttendanceScopeEvaluation.Allows(snapshot.Actor, live, CorrectionPermissions.AlternativesDecide, scopes) ||
            !await journal.IsCurrentSessionAsync(snapshot.Actor, ct)) throw Unavailable();
        var verifiedAt = clock.GetUtcNow();
        var finalGrants = live.Where(x => x.IsEffectiveAt(verifiedAt)).ToArray();
        if (verifiedAt < finalAt || repository.TransactionId != transaction || current.Require() != snapshot.Actor ||
            !AttendanceScopeEvaluation.Allows(snapshot.Actor, finalGrants, CorrectionPermissions.AlternativesDecide, snapshot.AuthorizationScopes) ||
            !AttendanceScopeEvaluation.Allows(snapshot.Actor, finalGrants, CorrectionPermissions.AlternativesDecide, scopes)) throw Unavailable();
        return verifiedAt;
    }

    private bool Matches(AlternativeCorrectionAuthoritySnapshot s, AlternativeCorrectionApprovalSource source, CorrectionEvidenceSnapshot evidence)
    {
        var alternative = evidence.AlternativeEvidence!;
        if (s.SchemaVersion != 1 || s.EmployeeId != source.Correction.EmployeeId || s.CorrectionId != source.Correction.Id || s.DecisionId != source.Decision.Id ||
            s.ApprovalId != source.Approval.Id || s.AppliedSessionId != source.Session.Id || s.BusinessDate != source.Correction.AttendanceDate ||
            s.SourceEvidenceReference != source.Correction.PrivateEvidenceReference || s.SourceEvidenceDigest != Hash(evidence) ||
            s.SourceFingerprint != evidence.Source.SourceFingerprint || s.AlternativeKind != alternative.Policy.Kind || s.AlternativePolicyId != alternative.Policy.Id ||
            s.AlternativePolicyFingerprint != alternative.Policy.Fingerprint || s.DocumentIds is null ||
            !s.DocumentIds.SequenceEqual(alternative.Documents.Select(x => x.Id).Order()) ||
            s.DecidedAtUtc != source.Decision.DecidedAt || s.VerifiedAtUtc.Offset != TimeSpan.Zero || s.VerifiedAtUtc < s.DecidedAtUtc || s.VerifiedAtUtc > clock.GetUtcNow() ||
            s.Actor is null || !ValidActor(s.Actor) || s.Actor.EmployeeId == s.EmployeeId || s.Actor.AccountId.ToString() != source.Decision.ActorUserId ||
            s.SystemRole != SystemRoleKind.Admin || s.RoleAssignmentId == Guid.Empty || !ValidScopes(s.AuthorizationScopes, s.EmployeeId) ||
            evidence.OriginalResourceScopes!.Any(x => !s.AuthorizationScopes.Any(y => Hash(x) == Hash(y))) ||
            s.Permissions is null || s.Permissions.Count is < 1 or > AttendanceScopeEvaluation.MaximumTotalAlternatives || s.Permissions.Any(x => x is null)) return false;
        return s.Permissions.All(x => x.Permission == CorrectionPermissions.AlternativesDecide && x.RoleAssignmentId == s.RoleAssignmentId && x.Grant().IsEffectiveAt(s.DecidedAtUtc)) &&
            AttendanceScopeEvaluation.Allows(s.Actor, s.Permissions.Select(x => x.Grant()), CorrectionPermissions.AlternativesDecide, s.AuthorizationScopes);
    }

    private static AttendanceAlternativeCorrectionApprovalProof Proof(AlternativeCorrectionAuthoritySnapshot s, ApprovalDecision d,
        DateTimeOffset at, bool isStaged, AuditEvent? audit) => new(d, s.Actor, s.SystemRole, s.RoleAssignmentId,
            s.Permissions.Select(x => x.Grant()).ToArray(), s.AuthorizationScopes, at, isStaged, audit);
    private static bool ValidActor(AuthenticatedAccount actor) => actor.AccountId != Guid.Empty && actor.EmployeeId != Guid.Empty &&
        actor.SessionId != Guid.Empty && actor.Client == SessionClient.Web && actor.MfaSatisfied && actor.SecurityVersion > 0;
    private static bool ValidScopes(IReadOnlyList<AttendanceResourceScope> scopes, Guid employee) => AttendanceScopeEvaluation.IsValid(scopes) &&
        scopes.SelectMany(x => x.Alternatives).All(x => x.EmployeeId == employee);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static AuthenticationFailure Unavailable() => AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
    private sealed class StageLease(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed record DecisionAudit([property: JsonPropertyName("operation")] string Operation, Guid ResourceId,
        [property: JsonPropertyName("request")] AuditRequest Request, CorrectionDecisionResponse After);
    private sealed record AuditRequest([property: JsonPropertyName("permission")] string Permission,
        [property: JsonPropertyName("employeeId")] Guid EmployeeId, [property: JsonPropertyName("payload")] AuditPayload Payload);
    private sealed record AuditPayload([property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("request")] DecideCorrectionRequest Request);
}
