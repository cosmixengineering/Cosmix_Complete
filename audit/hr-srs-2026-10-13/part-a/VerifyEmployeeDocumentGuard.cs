using System.Reflection;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

var passed = 0; var failed = 0;
var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + (error.InnerException ?? error).Message); }
}
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
void Validate(CosmixDbContext db, bool full = false)
{
    db.ChangeTracker.DetectChanges();
    if (full) typeof(CosmixDbContext).GetMethod("ValidateTrackedWrites", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(db, []);
    else typeof(CosmixDbContext).Assembly.GetType("Cosmix.Infrastructure.Database.EmployeeDocumentWriteGuard")!
        .GetMethod("Validate", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, [db]);
}
void Reject(Action action)
{
    try { action(); }
    catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { return; }
    throw new Exception("Expected guard rejection");
}
(CosmixDbContext Db, Employee Employee, EmployeeDocument Document) Fixture(bool pending = false)
{
    // The connection string initializes provider metadata only; no database is opened or queried.
    var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
        .UseSqlServer("Server=127.0.0.1,1;Database=NeverConnected_A02;Integrated Security=true;TrustServerCertificate=true").Options);
    var employee = new Employee(now.AddDays(-3), "synthetic-hr");
    if (pending)
    {
        typeof(Employee).GetProperty("OnboardingStatus")!.SetValue(employee, OnboardingStatus.PendingApproval);
        typeof(Employee).GetProperty("Status")!.SetValue(employee, EmployeeStatus.PendingApproval);
    }
    var d = new EmployeeDocument(employee.Id, EmployeeDocumentType.Identity, "synthetic-opaque-reference", "identity.pdf",
        "application/pdf", 10, new string('A', 64), "synthetic-hr", now.AddDays(-2));
    d.RecordScan(DocumentScanStatus.Clean, true, "synthetic/v1", now.AddDays(-1));
    d.RecordVerification(DocumentVerificationStatus.Verified, "reviewer", "Verified evidence", now);
    db.Attach(employee); db.Attach(d);
    return (db, employee, d);
}
void Withdraw(CosmixDbContext db, EmployeeDocument d, bool addHistory = true)
{
    var before = EmployeeDocumentState.Capture(d).Encode();
    d.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked, "reviewer", "Withdraw evidence", now);
    if (addHistory) db.Add(d.RecordHistory(EmployeeDocumentHistoryAction.Revoked, "reviewer", "Withdraw evidence", now,
        before, EmployeeDocumentState.Capture(d).Encode()));
}

Check("lifecycle and attempt columns are mapped with restrictive retained history", () =>
{
    var f = Fixture(); using var db = f.Db;
    var model = db.GetService<IDesignTimeModel>().Model;
    var document = model.FindEntityType(typeof(EmployeeDocument))!;
    var history = model.FindEntityType(typeof(EmployeeDocumentHistory))!;
    Assert(document.FindProperty("LifecycleStatus") is not null && document.FindProperty("ActiveScanAttemptId") is not null &&
        document.FindProperty("HistoryRevision") is not null && document.FindProperty("RowVersion")!.IsConcurrencyToken, "Missing mapped lifecycle/concurrency state");
    Assert(history.GetForeignKeys().All(fk => fk.DeleteBehavior == DeleteBehavior.Restrict) &&
        history.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeDocumentId", "Revision" })), "History relationships/revisions are not retained");
    Assert(document.GetCheckConstraints().Any(c => c.Name == "CK_EmployeeDocuments_ScanAttempt" && c.Sql.Contains("[ScanStartedAt] IS NOT NULL")), "Nullable scan evidence lacks explicit check");
});
Check("revoke and exact appended history pass pure guard", () =>
{
    var f = Fixture(); using var db = f.Db; Withdraw(db, f.Document); Validate(db);
});
Check("pending approval permits versioned withdrawal without modifying frozen profile", () =>
{
    var f = Fixture(pending: true); using var db = f.Db; var draftVersion = f.Employee.DraftRevision;
    Withdraw(db, f.Document); Validate(db, full: true);
    Assert(f.Employee.DraftRevision == draftVersion && !f.Document.IsAvailable, "Withdrawal rewrote pending profile");
});
Check("mutation without history is denied", () =>
{
    var f = Fixture(); using var db = f.Db; Withdraw(db, f.Document, addHistory: false); Reject(() => Validate(db));
});
Check("document deletion is denied", () =>
{
    var f = Fixture(); using var db = f.Db; db.Remove(f.Document); Reject(() => Validate(db));
});
Check("upload identity cannot be replaced during lifecycle transition", () =>
{
    var f = Fixture(); using var db = f.Db; Withdraw(db, f.Document);
    typeof(EmployeeDocument).GetProperty("StorageKey")!.SetValue(f.Document, "substituted-private-reference"); Reject(() => Validate(db));
});
Check("withdrawal cannot rewrite scanner evidence", () =>
{
    var f = Fixture(); using var db = f.Db;
    var before = EmployeeDocumentState.Capture(f.Document).Encode();
    f.Document.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked, "reviewer", "Withdraw evidence", now);
    typeof(EmployeeDocument).GetProperty("ScanStatus")!.SetValue(f.Document, DocumentScanStatus.Failed);
    db.Add(f.Document.RecordHistory(EmployeeDocumentHistoryAction.Revoked, "reviewer", "Withdraw evidence", now,
        before, EmployeeDocumentState.Capture(f.Document).Encode())); Reject(() => Validate(db));
});
Check("history is append-only under full persistence guard", () =>
{
    var f = Fixture(); using var db = f.Db; Withdraw(db, f.Document); Validate(db);
    foreach (var entry in db.ChangeTracker.Entries()) entry.State = EntityState.Unchanged;
    var history = db.ChangeTracker.Entries<EmployeeDocumentHistory>().Single();
    history.Property(h => h.Reason).CurrentValue = "Rewritten historical reason";
    Reject(() => Validate(db, full: true));
});
Console.WriteLine($"A02 EF/model guard checks: {passed} passed, {failed} failed; no SQL connection opened");
return failed == 0 ? 0 : 1;
