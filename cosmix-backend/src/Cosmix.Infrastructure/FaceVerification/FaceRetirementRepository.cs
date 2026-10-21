using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.FaceVerification;

/// <summary>Existing append-only Audit and receipt tables only; never registers an EF model or queue.</summary>
public sealed class FaceRetirementRepository(CosmixDbContext db, IAccessManagementRepository transactions,
    IPrivateFileStore evidence) : IFaceRetirementRepository
{
    private bool auditOnly;
    public void RequireTransaction()
    { if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Face retirement staging requires an authorized transaction."); }
    public void RequireOutsideTransaction()
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Face retirement processing requires a clean context outside business transactions.");
    }
    public Task<AuditEvent?> FindIntentAsync(string targetType, Guid subjectId, CancellationToken ct)
    {
        var id = subjectId.ToString();
        var staged = db.AuditEvents.Local.SingleOrDefault(x => x.EventType == FaceRetirementService.IntentEvent &&
            x.TargetType == targetType && x.TargetId == id);
        return staged is not null ? Task.FromResult<AuditEvent?>(staged) : db.AuditEvents.AsNoTracking().SingleOrDefaultAsync(x =>
            x.EventType == FaceRetirementService.IntentEvent && x.TargetType == targetType && x.TargetId == id, ct);
    }
    public Task<AuditEvent?> LatestOutcomeAsync(Guid intentId, CancellationToken ct)
    {
        var id = intentId.ToString();
        return db.AuditEvents.AsNoTracking().Where(x => x.TargetType == "FaceRetirementOutcome" && x.TargetId == id &&
            (x.EventType == FaceRetirementService.OutcomeEvent || x.EventType == FaceRetirementService.DisposedEvent))
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyList<AuditEvent>> IntentsAsync(FaceRetirementCursor? after, int size, CancellationToken ct)
    {
        RequireOutsideTransaction();
        if (size is < 1 or > 100) throw FaceRetirementEvidence.Unavailable();
        return await IntentQuery(after, size).ToArrayAsync(ct);
    }
    internal IQueryable<AuditEvent> IntentQuery(FaceRetirementCursor? after, int size)
    {
        var query = db.AuditEvents.AsNoTracking().Where(x => x.EventType == FaceRetirementService.IntentEvent);
        if (after is not null) query = query.Where(x => x.OccurredAt > after.OccurredAtUtc ||
            x.OccurredAt == after.OccurredAtUtc && x.Id.CompareTo(after.Id) > 0);
        return query.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(size);
    }

    public async Task<IReadOnlyList<FaceRevocationSource>> UnstagedRevocationsAsync(FaceRetirementCursor? after, int size, CancellationToken ct)
    {
        RequireOutsideTransaction();
        if (size is < 1 or > 100) throw FaceRetirementEvidence.Unavailable();
        var rows = await RevocationQuery(after, size).ToArrayAsync(ct);
        var result = new List<FaceRevocationSource>();
        foreach (var row in rows)
        {
            var replacements = await db.FaceEnrollments.AsNoTracking().Where(x => x.EmployeeId == row.EmployeeId && x.ReplacesEnrollmentId == row.Id)
                .Take(2).ToArrayAsync(ct);
            if (replacements.Length > 1) { result.Add(new(row, null)); continue; }
            AuditEvent? authority = null;
            if (row.RevokedBy == "system:employee-lifecycle" && row.RevocationReason is { } reason &&
                reason.StartsWith("employee-lifecycle:", StringComparison.Ordinal) && Guid.TryParseExact(reason[19..], "D", out var change))
            {
                var target = change.ToString();
                authority = await db.AuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.EventType == "hr.employee.lifecycle.effective" &&
                    x.TargetType == "EmployeeStatusChange" && x.TargetId == target && x.EmployeeScopeId == row.EmployeeId &&
                    x.ActorUserId == row.RevokedBy && x.OccurredAt == row.RevokedAtUtc, ct);
            }
            else if (replacements.Length == 0)
                authority = await db.AuditEvents.AsNoTracking().Where(x => x.EventType == "attendance.face.revoke" &&
                    x.TargetId == row.Id.ToString() && x.EmployeeScopeId == row.EmployeeId && x.ActorUserId == row.RevokedBy &&
                    x.OccurredAt >= row.RevokedAtUtc).OrderBy(x => x.OccurredAt).FirstOrDefaultAsync(ct);
            else
            {
                var candidates = await db.AuditEvents.AsNoTracking().Where(x => x.EventType == "attendance.face.enrollment.succeeded" &&
                    x.TargetType == "FaceEnrollmentAttempt" && x.EmployeeScopeId == row.EmployeeId && x.ActorUserId == row.RevokedBy &&
                    x.OccurredAt >= row.RevokedAtUtc).OrderBy(x => x.OccurredAt).Take(100).ToArrayAsync(ct);
                foreach (var candidate in candidates)
                {
                    if (!Guid.TryParseExact(candidate.TargetId, "D", out var start)) continue;
                    var terminal = await FaceRetirementEvidence.ReadAsync<EnrollmentTerminal>(evidence, candidate.ProtectedChangesReference,
                        $"attendance:face:enrollment:{start:N}:terminal", ct);
                    if (terminal.Status == 200 && terminal.StartReceiptId == start && terminal.EmployeeId == row.EmployeeId &&
                        terminal.PriorEnrollment?.Id == row.Id && terminal.Enrollment?.Id == replacements[0].Id)
                    { authority = candidate; break; }
                }
            }
            result.Add(new(row, replacements.SingleOrDefault()?.Id, authority));
        }
        return result;
    }

    internal IQueryable<FaceEnrollment> RevocationQuery(FaceRetirementCursor? after, int size)
    {
        var query = db.FaceEnrollments.AsNoTracking().Where(x => !x.IsActive && x.RevokedAtUtc != null &&
            !db.AuditEvents.Any(a => a.EventType == FaceRetirementService.IntentEvent &&
                a.TargetType == FaceRetirementService.EnrollmentTarget && a.TargetId == x.Id.ToString()));
        if (after is not null) query = query.Where(x => x.RevokedAtUtc > after.OccurredAtUtc ||
            x.RevokedAtUtc == after.OccurredAtUtc && x.Id.CompareTo(after.Id) > 0);
        return query.OrderBy(x => x.RevokedAtUtc).ThenBy(x => x.Id).Take(size);
    }

    public async Task<FaceRetirementProof> ValidateCommittedAsync(FaceRetirementIntent intent, CancellationToken ct)
    {
        RequireOutsideTransaction(); FaceRetirementEvidence.RequireIntent(intent);
        if (intent.Capture is null)
        {
            var row = await db.FaceEnrollments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == intent.SubjectId && x.EmployeeId == intent.EmployeeId, ct);
            var replacement = intent.ReplacementEnrollmentId is Guid id
                ? await db.FaceEnrollments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.EmployeeId == intent.EmployeeId, ct) : null;
            var serialized = System.Text.Json.JsonSerializer.Serialize(intent.Reference);
            var active = await db.FaceEnrollments.AsNoTracking().AnyAsync(x => x.IsActive && x.EncryptedTemplateReference == serialized, ct);
            return FaceRetirementSourceRules.Enrollment(intent, row, replacement, active);
        }
        var e = intent.Capture; var b = e.Binding;
        var key = $"capture:{e.CaptureId:N}"; var actor = b.AccountId.ToString();
        var claim = await db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor &&
            x.Operation == "attendance.face.capture.claim" && x.Key == key, ct);
        if (claim is null || claim.CompletedAt is null) return FaceRetirementProof.Pending;
        var receipt = await FaceRetirementEvidence.ReadAsync<CaptureReceipt>(evidence, claim.ProtectedResultReference,
            $"attendance:operation:{b.AccountId:N}:attendance.face.capture.claim:result", ct);
        if (receipt.ResourceId != e.CaptureId || receipt.RequiresAll || receipt.ResourceScopes is not null ||
            receipt.Resources is not { Count: > 0 } || receipt.Resources.Any(x => x is null || x.EmployeeId != intent.EmployeeId) ||
            !FaceRetirementSourceRules.Claim(intent, claim, receipt.Result)) return FaceRetirementProof.Rejected;
        if (b.AttemptId is Guid attemptId)
        {
            var attempt = await db.FaceVerificationAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.EmployeeId == intent.EmployeeId, ct);
            return FaceRetirementSourceRules.Verification(intent, claim, attempt);
        }
        var startId = Guid.ParseExact(intent.OperationKey[11..], "N");
        var startRow = await db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == startId &&
            x.ActorUserId == actor && x.Operation == FaceEnrollmentJournal.StartOperation, ct);
        if (startRow is null || startRow.CompletedAt is null) return FaceRetirementProof.Rejected;
        var start = await FaceRetirementEvidence.ReadAsync<EnrollmentStart>(evidence, startRow.ProtectedResultReference,
            $"attendance:face:enrollment:{startId:N}:start", ct);
        var terminalKey = startId.ToString("N");
        var terminalRow = await db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor &&
            x.Operation == FaceEnrollmentJournal.TerminalOperation && x.Key == terminalKey, ct);
        var terminal = terminalRow is null ? null : await FaceRetirementEvidence.ReadAsync<EnrollmentTerminal>(evidence,
            terminalRow.ProtectedResultReference, $"attendance:face:enrollment:{startId:N}:terminal", ct);
        return FaceRetirementSourceRules.EnrollmentCapture(intent, claim, startRow, start, terminalRow, terminal);
    }

    public void Add(AuditEvent audit)
    {
        RequireTransaction();
        if (audit.EventType is not (FaceRetirementService.IntentEvent or FaceRetirementService.OutcomeEvent or FaceRetirementService.DisposedEvent))
            throw new InvalidOperationException("Unexpected face retirement audit event.");
        db.AuditEvents.Add(audit);
    }
    public async Task SaveChangesAsync(CancellationToken ct)
    { RequireTransaction(); if (auditOnly) ValidateAuditOnly(); await db.SaveChangesAsync(ct); }
    public Task<T> InAuditTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) =>
        transactions.InTransactionAsync(async token =>
        {
            auditOnly = true;
            try { var result = await action(token); ValidateAuditOnly(); return result; }
            finally { auditOnly = false; }
        }, ct);
    private void ValidateAuditOnly()
    {
        if (db.ChangeTracker.Entries().Any(x => x.State is EntityState.Modified or EntityState.Deleted ||
            x.State == EntityState.Added && (x.Entity is not AuditEvent a ||
                a.EventType is not (FaceRetirementService.IntentEvent or FaceRetirementService.OutcomeEvent or FaceRetirementService.DisposedEvent))))
            throw new InvalidOperationException("Face retirement recovery may only append owned audit records.");
    }
    private sealed record CaptureReceipt(Guid ResourceId, FaceCaptureEnvelope Result, IReadOnlyList<AccessResource> Resources,
        bool RequiresAll, IReadOnlyList<AttendanceResourceScope>? ResourceScopes = null);
}
