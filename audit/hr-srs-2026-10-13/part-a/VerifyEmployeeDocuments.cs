using System.Reflection;
using System.Text.Json;
using Cosmix.Domain.HR.EmployeeDocuments;

var failed = 0;
var checks = 0;
void Check(string name, Action action)
{
    try { action(); Console.WriteLine("PASS " + name); checks++; }
    catch (Exception error) { Console.WriteLine("FAIL " + name + ": " + (error.InnerException ?? error).Message); failed++; }
}
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
EmployeeDocument Document(DateOnly? expiry = null)
{
    var document = new EmployeeDocument(Guid.NewGuid(), EmployeeDocumentType.Identity, "synthetic-private-reference",
        "identity.pdf", "application/pdf", 10, new string('A', 64), "uploader", now.AddDays(-2), expiresOn: expiry);
    document.RecordScan(DocumentScanStatus.Clean, true, "synthetic-scanner/v1", now.AddDays(-1));
    document.RecordVerification(DocumentVerificationStatus.Verified, "verifier", "synthetic decision", now);
    return document;
}
MethodInfo RequiredMethod(string name) => typeof(EmployeeDocument).GetMethod(name) ?? throw new Exception("Missing " + name);
object? Invoke(EmployeeDocument d, string method, params object?[] arguments) => RequiredMethod(method).Invoke(d, arguments);
Check("dated availability blocks naturally expired evidence", () =>
{
    var d = Document(new DateOnly(2026, 10, 12));
    Assert(!(bool)Invoke(d, "IsAvailableOn", new DateOnly(2026, 10, 13))!, "Expired evidence remained available");
});
Check("expiry date remains inclusive", () =>
{
    var d = Document(new DateOnly(2026, 10, 13));
    Assert((bool)Invoke(d, "IsAvailableOn", new DateOnly(2026, 10, 13))!, "Valid-through evidence was denied early");
});
Check("interrupted rescan withdraws availability", () =>
{
    var d = Document();
    Invoke(d, "BeginScan", Guid.NewGuid(), "operator", "Updated scanner verification", now.AddMinutes(1));
    Assert(!d.IsAvailable, "Rescanning verified evidence remained downloadable");
});
Check("old scan attempt cannot resolve a newer attempt", () =>
{
    var d = Document(); var old = Guid.NewGuid(); var current = Guid.NewGuid();
    Invoke(d, "BeginScan", old, "operator", "First rescan", now.AddMinutes(1));
    Invoke(d, "BeginScan", current, "operator", "Retry interrupted rescan", now.AddMinutes(2));
    var rejected = false;
    try { Invoke(d, "CompleteScan", old, DocumentScanStatus.Clean, true, "synthetic-scanner/v2", now.AddMinutes(3)); }
    catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { rejected = true; }
    Assert(rejected && !d.IsAvailable, "A stale scan completion restored evidence");
});
Check("clean rescan requires a new manual verification", () =>
{
    var d = Document(); var attempt = Guid.NewGuid();
    Invoke(d, "BeginScan", attempt, "operator", "Updated scanner verification", now.AddMinutes(1));
    Invoke(d, "CompleteScan", attempt, DocumentScanStatus.Clean, true, "synthetic-scanner/v2", now.AddMinutes(2));
    Assert(!d.IsAvailable, "Old verification silently followed a new clean scan");
    d.RecordVerification(DocumentVerificationStatus.Verified, "verifier", "Reviewed new scan", now.AddMinutes(3));
    Assert(d.IsAvailable, "New clean verification did not enable evidence");
});
Check("explicit lifecycle withdrawal keeps private object", () =>
{
    foreach (var value in new[] { "Expired", "Revoked", "Archived" })
    {
        var d = Document(); var reference = d.StorageKey;
        var statusType = typeof(EmployeeDocument).Assembly.GetType("Cosmix.Domain.HR.EmployeeDocuments.EmployeeDocumentLifecycleStatus")
            ?? throw new Exception("Missing lifecycle status");
        Invoke(d, "ChangeLifecycle", Enum.Parse(statusType, value), "operator", "Withdrawn evidence", now.AddMinutes(1));
        Assert(!d.IsAvailable && d.StorageKey == reference, value + " deleted or allowed evidence");
    }
});
Check("lifecycle action requires a nonblank reason", () =>
{
    var d = Document();
    var statusType = typeof(EmployeeDocument).Assembly.GetType("Cosmix.Domain.HR.EmployeeDocuments.EmployeeDocumentLifecycleStatus")
        ?? throw new Exception("Missing lifecycle status");
    var rejected = false;
    try { Invoke(d, "ChangeLifecycle", Enum.Parse(statusType, "Revoked"), "operator", " ", now.AddMinutes(1)); }
    catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException) { rejected = true; }
    Assert(rejected && d.IsAvailable, "Missing reason mutated evidence");
});
Check("full-length Unicode reason fits retained JSON history", () =>
{
    var d = Document(); var reason = new string('漢', 1000);
    d.ChangeLifecycle((EmployeeDocumentLifecycleStatus)2, "operator", reason, now.AddMinutes(1));
    var state = JsonSerializer.Serialize(new { d.LifecycleStatus, d.LifecycleReason });
    d.RecordHistory(EmployeeDocumentHistoryAction.Revoked, "operator", reason, now.AddMinutes(1), "{}", state);
    Assert(d.HistoryRevision == 1, "Valid Unicode reason did not retain history");
});
Console.WriteLine($"A02 domain checks: {checks} passed, {failed} failed");
return failed == 0 ? 0 : 1;
