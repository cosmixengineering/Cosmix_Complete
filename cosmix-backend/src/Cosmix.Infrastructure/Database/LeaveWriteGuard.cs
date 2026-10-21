using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Database;

/// <summary>Protects frozen leave submissions at persistence as well as at the HTTP/service boundary.</summary>
public static class LeaveWriteGuard
{
    public static void Validate(CosmixDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        foreach (var entry in db.ChangeTracker.Entries<LeaveRequest>())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;
            var request = entry.Entity;
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Leave requests cannot be deleted.");
            if (entry.State == EntityState.Added)
            {
                if (request.SubmissionKey is null || request.SubmittedAt is null || request.ApprovalRequestId is null || request.PayTreatment != LeavePayTreatment.PendingReview ||
                    request.PolicyVersionId is not null || request.Days.Count == 0 || request.Days.Any(x => x.ShiftVersionId is null) ||
                    request.Days.Sum(x => x.Minutes) != request.RequestedMinutes || request.Days.Any(x => x.LeaveRequestId != request.Id))
                    throw new InvalidOperationException("New leave needs one frozen pending submission and exact pinned allocations.");
                var approval = request.ApprovalRequest;
                if (approval is null || approval.State != ApprovalState.Pending || !Matches(approval, request.Id, request.RequestedByUserId, "employee-leave-admin") ||
                    approval.Id != request.ApprovalRequestId || approval.RequestedAt != request.SubmittedAt)
                    throw new InvalidOperationException("Leave submission approval provenance is invalid.");
                continue;
            }
            string[] allowed = [nameof(LeaveRequest.PayTreatment), nameof(LeaveRequest.PolicyVersionId), nameof(LeaveRequest.RowVersion)];
            if (entry.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name)) ||
                entry.OriginalValues.GetValue<LeavePayTreatment>(nameof(LeaveRequest.PayTreatment)) != LeavePayTreatment.PendingReview ||
                request.PayTreatment == LeavePayTreatment.PendingReview || request.PolicyVersionId is null)
                throw new InvalidOperationException("Submitted leave is immutable except for its first approved policy treatment.");
            var tracked = db.ChangeTracker.Entries<ApprovalRequest>().SingleOrDefault(x => x.Entity.Id == request.ApprovalRequestId);
            var decisions = db.ChangeTracker.Entries<ApprovalDecision>().Where(x => x.Entity.ApprovalRequestId == request.ApprovalRequestId).ToArray();
            if (tracked is null || tracked.State != EntityState.Modified || tracked.OriginalValues.GetValue<ApprovalState>(nameof(ApprovalRequest.State)) != ApprovalState.Pending ||
                tracked.Entity.State != ApprovalState.Approved || !Matches(tracked.Entity, request.Id, request.RequestedByUserId, "employee-leave-admin") ||
                decisions.Length != 1 || decisions[0].State != EntityState.Added || decisions[0].Entity.Decision != ApprovalState.Approved ||
                decisions[0].Entity.ActorUserId == request.RequestedByUserId || decisions[0].Entity.ActorRoleCode is not ("Admin" or "SuperAdmin") ||
                decisions[0].Entity.DecidedAt != tracked.Entity.ClosedAt || decisions[0].Entity.DecidedAt < tracked.Entity.RequestedAt ||
                decisions[0].Entity.ProtectedChangeReference is null)
                throw new InvalidOperationException("Leave treatment needs a matching new fixed Admin approval decision.");
        }
        foreach (var entry in db.ChangeTracker.Entries<LeaveRequestDay>())
            if (entry.State == EntityState.Added && !db.ChangeTracker.Entries<LeaveRequest>().Any(x => x.Entity.Id == entry.Entity.LeaveRequestId && x.State == EntityState.Added))
                throw new InvalidOperationException("Allocations cannot be appended to an existing leave submission.");
        foreach (var entry in db.ChangeTracker.Entries<LeaveCancellationRequest>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Submitted cancellation evidence is immutable.");
            if (entry.State == EntityState.Added && (entry.Entity.ApprovalRequest is null || entry.Entity.SubmittedAt is null ||
                entry.Entity.ApprovalRequest.State != ApprovalState.Pending || entry.Entity.ApprovalRequest.Id != entry.Entity.ApprovalRequestId ||
                !Matches(entry.Entity.ApprovalRequest, entry.Entity.Id, entry.Entity.RequestedByUserId, "employee-leave-cancellation-admin") ||
                entry.Entity.ApprovalRequest.RequestedAt != entry.Entity.SubmittedAt))
                throw new InvalidOperationException("Leave cancellation needs a matching pending approval.");
        }
        foreach (var entry in db.ChangeTracker.Entries<LeaveType>())
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(LeaveType.IsActive) or nameof(LeaveType.RowVersion))))
                throw new InvalidOperationException("Leave type meaning is immutable once stored.");
    }
    private static bool Matches(ApprovalRequest a, Guid subject, string requester, string type) =>
        a.SubjectId == subject && a.RequesterUserId == requester && a.RequestType == type && a.Stage == 1;
}
