#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

var passed = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; }
void Fails(Action action, string name) { try { action(); } catch (InvalidOperationException) { passed++; return; } throw new Exception(name); }
CosmixDbContext Db() => new(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer("Server=leave-model.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true").Options);
LeaveRequest Pending()
{
    var r = new LeaveRequest(Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 14), new(2026, 10, 14), 480, "Synthetic leave", null, null, null, null,
        LeavePayTreatment.PendingReview, null, "synthetic-requester", new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero), "synthetic-key");
    r.Days.Add(new(r.Id, r.StartDate, new(2026, 10, 14, 4, 0, 0, TimeSpan.Zero), new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero), Guid.NewGuid()));
    r.Submit(new("employee-leave-admin", r.Id, r.RequestedByUserId, "synthetic-private-proof", 1, r.CreatedAt));
    return r;
}
using (var db = Db())
{
    var model = db.GetService<IDesignTimeModel>().Model;
    var r = model.FindEntityType(typeof(LeaveRequest))!;
    Check(r.FindProperty(nameof(LeaveRequest.PolicyVersionId))!.IsNullable, "Pending intake has optional policy without invented FK");
    Check(r.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "SubmissionKey" })), "Permanent self submission key");
    Check(r.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "PreviousRequestId" })), "Returned request can have only one successor");
    var day = model.FindEntityType(typeof(LeaveRequestDay))!;
    Check(day.GetForeignKeys().Any(x => x.Properties.SingleOrDefault()?.Name == "ShiftVersionId" && x.DeleteBehavior == DeleteBehavior.Restrict), "Pinned exact shift restrictive FK");
    Check(day.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "LeaveRequestId", "Date" })), "No duplicate day allocations");
    var request = Pending(); db.Add(request); LeaveWriteGuard.Validate(db); passed++;
}
using (var db = Db())
{
    var r = Pending(); db.Attach(r);
    typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.Reason))!.SetValue(r, "tampered after submission");
    Fails(() => LeaveWriteGuard.Validate(db), "Submitted reason mutation must fail");
}
using (var db = Db()) { var r = Pending(); db.Attach(r); db.ChangeTracker.CascadeDeleteTiming = Microsoft.EntityFrameworkCore.ChangeTracking.CascadeTiming.Never; db.Remove(r); Fails(() => LeaveWriteGuard.Validate(db), "Deletion must fail"); }
using (var db = Db())
{
    var r = Pending(); db.Attach(r);
    db.Add(new LeaveRequestDay(r.Id, r.StartDate.AddDays(1), new(2026, 10, 15, 4, 0, 0, TimeSpan.Zero), new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), Guid.NewGuid()));
    Fails(() => LeaveWriteGuard.Validate(db), "Appending child to submitted request must fail");
}
using (var db = Db())
{
    var r = Pending(); db.Attach(r);
    typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.PayTreatment))!.SetValue(r, LeavePayTreatment.Paid);
    typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.PolicyVersionId))!.SetValue(r, Guid.NewGuid());
    Fails(() => LeaveWriteGuard.Validate(db), "Fabricated treatment without matching tracked approval must fail");
}
using (var db = Db())
{
    var r = Pending(); db.Attach(r);
    var d = r.ApprovalRequest!.Decide(ApprovalState.Approved, "synthetic-admin", "Admin", "Approved", r.CreatedAt.AddHours(1), "synthetic-private-decision");
    db.Add(d); r.SetApprovedTreatment(LeavePayTreatment.Paid, Guid.NewGuid());
    LeaveWriteGuard.Validate(db); passed++;
    db.ChangeTracker.AcceptAllChanges();
    typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.PayTreatment))!.SetValue(r, LeavePayTreatment.Unpaid);
    Fails(() => LeaveWriteGuard.Validate(db), "Second financial treatment must fail");
}
using (var db = Db())
{
    var r = Pending(); db.Attach(r);
    var d = r.ApprovalRequest!.Decide(ApprovalState.Approved, "synthetic-admin", "Admin", "Approved", r.CreatedAt.AddHours(1), "synthetic-private-decision");
    typeof(ApprovalDecision).GetProperty(nameof(ApprovalDecision.DecidedAt))!.SetValue(d, d.DecidedAt.AddMinutes(1));
    db.Add(d); r.SetApprovedTreatment(LeavePayTreatment.Paid, Guid.NewGuid());
    Fails(() => LeaveWriteGuard.Validate(db), "Mismatched decision/approval time must fail");
}
using (var db = Db())
{
    var r = Pending(); var c = new LeaveCancellationRequest(r.Id, r.RequestedByUserId, "Changed plans", r.CreatedAt);
    c.Submit(new("employee-leave-cancellation-admin", c.Id, c.RequestedByUserId, "private-cancellation", 1, c.CreatedAt));
    db.Attach(c); typeof(LeaveCancellationRequest).GetProperty(nameof(LeaveCancellationRequest.Reason))!.SetValue(c, "tampered");
    Fails(() => LeaveWriteGuard.Validate(db), "Cancellation mutation must fail");
}
using (var db = Db())
{
    var type = new LeaveType("CASUAL", "Casual", false, true, true, LeaveKind.Casual); db.Attach(type);
    typeof(LeaveType).GetProperty(nameof(LeaveType.Kind))!.SetValue(type, LeaveKind.Other);
    Fails(() => LeaveWriteGuard.Validate(db), "Stored leave type meaning cannot change");
}
Console.WriteLine($"PASS: {passed} leave model/write-guard checks; no database connection or write.");
