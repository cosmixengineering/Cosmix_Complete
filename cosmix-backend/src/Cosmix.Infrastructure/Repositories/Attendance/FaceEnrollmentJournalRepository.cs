using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class FaceEnrollmentJournalRepository(CosmixDbContext db, IAccessManagementRepository transactions) : IFaceEnrollmentJournalRepository
{
    private bool auditOnly;
    public void RequireTransaction()
    { if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Face enrollment journal requires the attendance authorization transaction."); }
    public Task<IdempotencyReceipt?> FindAsync(string actor, string operation, string key, CancellationToken ct) =>
        db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key, ct);
    public Task<IdempotencyReceipt?> StartAsync(Guid id, CancellationToken ct) =>
        db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Operation == FaceEnrollmentJournal.StartOperation, ct);
    public void Add(IdempotencyReceipt receipt)
    {
        RequireTransaction();
        if (receipt.Operation is not (FaceEnrollmentJournal.StartOperation or FaceEnrollmentJournal.TerminalOperation) ||
            auditOnly && receipt.Operation != FaceEnrollmentJournal.TerminalOperation)
            throw new InvalidOperationException("Unexpected enrollment journal operation.");
        db.IdempotencyReceipts.Add(receipt);
    }
    public void Add(AuditEvent audit)
    {
        RequireTransaction();
        if (!audit.EventType.StartsWith("attendance.face.enrollment.", StringComparison.Ordinal) ||
            auditOnly && (audit.EventType is "attendance.face.enrollment.started" or "attendance.face.enrollment.succeeded"))
            throw new InvalidOperationException("Unexpected enrollment audit operation.");
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
        // Check BEFORE every save as well as before commit; checking only after SaveChanges would miss accepted mutations.
        if (db.ChangeTracker.Entries().Any(x => x.State is EntityState.Modified or EntityState.Deleted ||
            x.State == EntityState.Added && x.Entity is not (AuditEvent or IdempotencyReceipt)))
            throw new InvalidOperationException("Enrollment failure finalization permits only append-only audit and receipt records.");
    }
}
