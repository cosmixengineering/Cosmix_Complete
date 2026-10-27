using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Api.Authentication;
using Cosmix.Application.Common.Files;

// Runs the actual adapter, with in-memory backing stores only. No real file/SQL/host IO.
var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
var type = typeof(CurrentAuthentication).Assembly.GetType("Cosmix.Api.Development.HrVehicleV2ContinuationFiles", true)!;
var checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
async Task Reject(Func<Task> action, string label)
{
    try { await action(); } catch (InvalidOperationException) { checks++; return; }
    throw new Exception(label);
}
var oldReferences = Enumerable.Range(1, 10).Select(i =>
    (Reference: new PrivateFileReference(i.ToString("x32") + ".enc", i.ToString("x32"), new string('A', 64), "hr-vehicle-v2", 1), Purpose: "old-purpose-" + i)).ToArray();
object Create(MemoryStore oldStore, MemoryStore newStore) => Activator.CreateInstance(type, flags, null, new object[] { oldStore, newStore, oldReferences }, null)!;
IDisposable Enter(object adapter, string operation, Func<CancellationToken, Task> authorize, Guid? adjustment = null)
{
    try { return (IDisposable)type.GetMethod("Enter", flags)!.Invoke(adapter, new object?[] { operation, authorize, adjustment })!; }
    catch (TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
}
var old = new MemoryStore("hr-vehicle-v2"); var current = new MemoryStore("hr-vehicle-v2-continue01");
var adapter = Create(old, current); var files = (IPrivateFileStore)adapter;
var authorizations = 0;
Task Allow(CancellationToken ct) { authorizations++; return Task.CompletedTask; }
using (Enter(adapter, "correction", Allow))
{
    foreach (var entry in oldReferences)
        Check((await files.ReadAsync(entry.Reference, entry.Purpose, default)).SequenceEqual(new byte[] { 1 }), "allowlisted old reference routed");
    Check(old.Reads == 10 && current.Reads == 0 && old.Writes == 0 && authorizations == 20, "old reads and two authorization checks each");
    var source = oldReferences[0];
    foreach (var altered in new[]
    {
        source.Reference with { StorageKey = Guid.NewGuid().ToString("N") + ".enc" },
        source.Reference with { ObjectVersion = Guid.NewGuid().ToString("N") },
        source.Reference with { Sha256 = new string('B', 64) },
        source.Reference with { KeyReference = "hr-vehicle-v2-continue01" },
        source.Reference with { SizeBytes = 2 }
    }) await Reject(async () => { await files.ReadAsync(altered, source.Purpose, default); }, "full-reference allowlist enforced");
    await Reject(async () => { await files.ReadAsync(source.Reference, source.Purpose + "wrong", default); }, "purpose binding enforced");
    Check(old.Reads == 10 && current.Reads == 0, "denied reads never touch either store");
}
await Reject(async () => { await files.ReadAsync(oldReferences[0].Reference, oldReferences[0].Purpose, default); }, "read requires authorization lease");
await Reject(async () => { await files.WriteAsync(new byte[] { 1 }, "unknown", default); }, "write requires authorization lease");

var budgets = new Dictionary<string, int>((Dictionary<string, int>)type.GetField("remaining", flags)!.GetValue(adapter)!);
Check(budgets.Values.Sum() == 49, "exact49 object ceiling");
var created = new List<(PrivateFileReference Reference, string Purpose, string Operation, Guid? Adjustment)>();
foreach (var (bucket, count) in budgets)
{
    for (var i = 0; i < count; i++)
    {
        var purpose = bucket;
        Guid? adjustment = null;
        var operation = bucket.StartsWith("vehicle:service:", StringComparison.Ordinal) || bucket.Contains(":vehicle.service.submit:", StringComparison.Ordinal) ? "service-overhaul-1" :
            bucket.Contains(":vehicle.service.approve:", StringComparison.Ordinal) ? "service-overhaul-1-approve" :
            bucket.Contains("trip:correction:", StringComparison.Ordinal) || bucket.Contains("trip.correction.submit:", StringComparison.Ordinal) ? "correction" :
            bucket.Contains("trip.correction.review:", StringComparison.Ordinal) ? "correction-review" :
            bucket.Contains("fuel:reconciliation:", StringComparison.Ordinal) || bucket.Contains("fuel.reconciliation.submit:", StringComparison.Ordinal) ? "reconciliation" : "reconciliation-review";
        if (bucket == "adjustment-decisions")
        {
            adjustment = Guid.NewGuid(); operation = i == 0 ? "correction-review" : "reconciliation-review";
            purpose = $"vehicle:adjustment:decision:{adjustment:N}";
        }
        using (Enter(adapter, operation, Allow, adjustment))
        {
            var reference = await files.WriteAsync(new byte[] { 1 }, purpose, default);
            Check(reference.KeyReference == "hr-vehicle-v2-continue01", "new key label");
            created.Add((reference, purpose, operation, adjustment));
            Check((await files.ReadAsync(reference, purpose, default)).SequenceEqual(new byte[] { 1 }), "own new allocation readable");
        }
    }
}
Check(current.Writes == 49 && current.Reads == 49 && old.Writes == 0, "49writes all in new store, zero old writes");
Check(authorizations == 20 + 49 * 4, "authorize before/after every actual read and write");
var first = created[0];
using (Enter(adapter, first.Operation, Allow, first.Adjustment))
{
    await Reject(async () => { await files.WriteAsync(new byte[] { 1 }, first.Purpose, default); }, "exhausted purpose budget");
    await Reject(async () => { await files.ReadAsync(first.Reference with { ObjectVersion = Guid.NewGuid().ToString("N") }, first.Purpose, default); }, "new references cannot be forged");
}
Check(current.Writes == 49, "over-budget call performs no allocation");

var isolatedOld = new MemoryStore("hr-vehicle-v2"); var isolatedNew = new MemoryStore("hr-vehicle-v2-continue01");
var revoked = Create(isolatedOld, isolatedNew);
using (Enter(revoked, "service-overhaul-1", _ => throw new InvalidOperationException("revoked")))
    await Reject(async () => { await ((IPrivateFileStore)revoked).WriteAsync(new byte[] { 1 }, first.Purpose, default); }, "revoked before allocation");
Check(isolatedNew.Writes == 0 && isolatedOld.Writes == 0, "revocation prevents IO");

var afterNew = new MemoryStore("hr-vehicle-v2-continue01"); var after = Create(new MemoryStore("hr-vehicle-v2"), afterNew); var calls = 0;
using (Enter(after, "service-overhaul-1", _ => ++calls == 2 ? throw new InvalidOperationException("revoked after IO") : Task.CompletedTask))
    await Reject(async () => { await ((IPrivateFileStore)after).WriteAsync(new byte[] { 1 }, first.Purpose, default); }, "revoke after allocation fails closed");
Check(afterNew.Writes == 1 && calls == 2, "failed post-IO authority leaves allocated evidence intact");
using (Enter(after, "service-overhaul-1", _ => Task.CompletedTask))
    await Reject(async () => { await ((IPrivateFileStore)after).WriteAsync(new byte[] { 1 }, first.Purpose, default); }, "no retry after uncertain allocation");
Check(afterNew.Writes == 1, "uncertain retry cannot allocate");

var brokenNew = new MemoryStore("hr-vehicle-v2-continue01") { FailWrite = true }; var broken = Create(new MemoryStore("hr-vehicle-v2"), brokenNew);
using (Enter(broken, "service-overhaul-1", _ => Task.CompletedTask))
{
    await Reject(async () => { await ((IPrivateFileStore)broken).WriteAsync(new byte[] { 1 }, first.Purpose, default); }, "storage failure fails closed");
    brokenNew.FailWrite = false;
    await Reject(async () => { await ((IPrivateFileStore)broken).WriteAsync(new byte[] { 1 }, first.Purpose, default); }, "failed allocation never retried");
}
Check(brokenNew.Writes == 1, "failed allocation consumes budget and stops store");

var afterReadStore = new MemoryStore("hr-vehicle-v2"); var afterRead = Create(afterReadStore, new MemoryStore("hr-vehicle-v2-continue01")); var readCalls = 0;
using (Enter(afterRead, "correction", _ => ++readCalls == 2 ? throw new InvalidOperationException("revoked after read") : Task.CompletedTask))
    await Reject(async () => { await ((IPrivateFileStore)afterRead).ReadAsync(oldReferences[0].Reference, oldReferences[0].Purpose, default); }, "read revalidates authority after IO");
Check(readCalls == 2 && afterReadStore.LastRead!.All(x => x == 0), "unauthorized plaintext erased before returning");

var wrongPurpose = Create(new MemoryStore("hr-vehicle-v2"), new MemoryStore("hr-vehicle-v2-continue01"));
using (Enter(wrongPurpose, "correction", _ => Task.CompletedTask))
    await Reject(async () => { await ((IPrivateFileStore)wrongPurpose).WriteAsync(new byte[] { 1 }, first.Purpose, default); }, "operation cannot borrow another purpose budget");
var wrongDecision = Create(new MemoryStore("hr-vehicle-v2"), new MemoryStore("hr-vehicle-v2-continue01"));
using (Enter(wrongDecision, "correction-review", _ => Task.CompletedTask, Guid.NewGuid()))
    await Reject(async () => { await ((IPrivateFileStore)wrongDecision).WriteAsync(new byte[] { 1 }, "vehicle:adjustment:decision:" + Guid.NewGuid().ToString("N"), default); }, "decision bound to captured request ID");
Console.WriteLine($"PASS {checks} actual continuation adapter checks;49 finite allocations in memory, zero old writes, no host/SQL/private IO.");

sealed class MemoryStore(string key) : IPrivateFileStore
{
    public int Reads, Writes;
    public bool FailWrite;
    public byte[]? LastRead;
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        Writes++;
        if (FailWrite) throw new InvalidOperationException("simulated storage failure");
        return Task.FromResult(new PrivateFileReference(Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"), Convert.ToHexString(SHA256.HashData(content)), key, content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    { Reads++; LastRead = new byte[] { 1 }; return Task.FromResult(LastRead); }
}
