#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

var checks = 0; var failures = new List<string>();
void Check(bool condition, string name)
{ checks++; if (!condition) { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
async Task Scenario(string name, Func<Task> action)
{ try { await action(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message); } }
async Task RejectExpired(string name, Func<Task> action)
{
    try { await action(); Check(false, name + " unexpectedly returned protected result"); }
    catch (AuthenticationFailure e) {
        Check(e.Code == "invalid_authentication" && e.StatusCode == 401,
            name + ": expected invalid_authentication/401, got " + e.Code + "/" + e.StatusCode);
    }
}

foreach (var offset in new[] { -1L, 0L, 1L })
    await Scenario("read action storage returns at expiry offset " + offset, async () => {
        var f = new AuthorityTimeFixture();
        Task<AuthorityTimeResult> Read() => f.Operation.ReadAsync(LeaveBenefitPermissions.Review, "synthetic.authority.read",
            null, new("127.0.0.1", "synthetic-read-expiry"), async (_, ct) => {
                await f.CallbackStorageAsync(f.SessionExpiresAt.AddTicks(offset), ct);
                return f.Result;
            }, default, requireSystemRole: true);
        if (offset < 0)
        {
            Check(await Read() == f.Result, "Read returns evidence only while session remains active");
            Check(f.Audits.Count == 1, "Authorized read is audited once");
        }
        else
        {
            await RejectExpired("Read expiry offset " + offset, () => Read());
            Check(f.Audits.Count == 0, "Expired read cannot commit a success audit");
        }
        Check(f.Business.Count == 0 && f.Outbox.Count == 0 && f.Receipts.Count == 0, "Read has no business/receipt effects");
    });

foreach (var offset in new[] { -1L, 0L, 1L })
    await Scenario("beforeCommit storage returns at expiry offset " + offset, async () => {
        var f = new AuthorityTimeFixture(); var calls = 0; var validOnEntry = false; var staged = false;
        f.BeforeCommit = async (actor, ct) => {
            calls++; validOnEntry = f.SessionCurrent;
            staged = f.Business.Count == 1 && f.Outbox.Count == 1 && f.Receipts.Count == 0 && f.Audits.Count == 0 &&
                f.Writes.Contains(f.OperationPurpose("result")) && f.Writes.Contains(f.OperationPurpose("audit"));
            await f.CallbackStorageAsync(f.SessionExpiresAt.AddTicks(offset), ct);
        };
        var name = "beforeCommit offset " + offset;
        if (offset < 0)
        {
            Check(await f.WriteAsync() == f.Result, name + " returns result while session remains valid");
            Check(f.Business.Count == 1 && f.Outbox.Count == 1 && f.Receipts.Count == 1 && f.Audits.Count == 1,
                name + " commits business, outbox, receipt and audit once");
        }
        else
        {
            await RejectExpired(name, () => f.WriteAsync());
            Check(f.Business.Count == 0, name + " rolls back staged business effect");
            Check(f.Outbox.Count == 0, name + " rolls back staged outbox effect");
            Check(f.Receipts.Count == 0, name + " commits no receipt");
            Check(f.Audits.Count == 0, name + " commits no audit");
        }
        Check(calls == 1 && validOnEntry && staged, name + " callback starts authorized after private operation writes");
        Check(f.Clock.Now == f.SessionExpiresAt.AddTicks(offset) && f.CallbackWrites == 1,
            name + " awaited callback storage advances the clock to the exact boundary");
        Check(f.ActionCalls == 1, name + " invokes business action only once");
    });

foreach (var offset in new[] { -1L, 0L, 1L })
    await Scenario("second beforeReplay storage returns at expiry offset " + offset, async () => {
        var f = new AuthorityTimeFixture(); var original = await f.WriteAsync();
        var business = f.Business.ToArray(); var outbox = f.Outbox.ToArray();
        var receipts = f.Receipts.ToArray(); var audits = f.Audits.ToArray();
        var calls = 0; var firstValid = false; var secondValid = false; var afterResultRead = false;
        f.BeforeReplay = async (actor, ct) => {
            calls++;
            if (calls == 1) { firstValid = f.SessionCurrent && f.Reads.Count == 0; return; }
            if (calls != 2) throw new InvalidOperationException("Unexpected extra replay callback");
            secondValid = f.SessionCurrent;
            afterResultRead = f.Reads.SequenceEqual(new[] { f.OperationPurpose("result") });
            await f.CallbackStorageAsync(f.SessionExpiresAt.AddTicks(offset), ct);
        };
        f.BeforeCommit = (actor, ct) => throw new InvalidOperationException("Replay must not run commit callback");
        var name = "second beforeReplay offset " + offset;
        if (offset < 0)
        {
            Check(await f.WriteAsync() == original, name + " returns the saved result while session remains valid");
            Check(f.Audits.Count == audits.Length + 1 && f.Audits.Take(audits.Length).SequenceEqual(audits),
                name + " appends exactly one successful replay audit");
        }
        else
        {
            await RejectExpired(name, () => f.WriteAsync());
            Check(f.Audits.SequenceEqual(audits), name + " commits no successful replay audit");
        }
        Check(calls == 2 && firstValid && secondValid && afterResultRead,
            name + " invokes the second authorized callback only after reading the saved result");
        Check(f.Clock.Now == f.SessionExpiresAt.AddTicks(offset) && f.CallbackWrites == 1,
            name + " awaited second callback storage advances the clock to the exact boundary");
        Check(f.Business.SequenceEqual(business), name + " preserves committed business effects");
        Check(f.Outbox.SequenceEqual(outbox), name + " preserves committed outbox effects");
        Check(f.Receipts.SequenceEqual(receipts), name + " preserves the original receipt");
        Check(f.ActionCalls == 1, name + " never reruns the business action");
    });

Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} attendance operation authority-time checks. Real AttendanceOperation; synthetic session/storage/transaction; no SQL or host.");
return failures.Count == 0 ? 0 : 1;

public class AuthorityTimeProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, AuthorityTimeProxy>(); ((AuthorityTimeProxy)(object)value).Handler = handler; return value; }
}
public sealed class AuthorityTimeClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public sealed record AuthorityTimeResult(Guid Id, string Status);
public sealed class AuthorityTimeFixture
{
    private const string OperationName = "attendance.synthetic.authority-time";
    private const string CallbackPurpose = "synthetic:authority-time:callback";
    private readonly AuthenticatedAccount actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    private readonly Guid employee = Guid.NewGuid(), role = Guid.NewGuid();
    private readonly Dictionary<string, (byte[] Bytes, string Purpose)> files = [];
    private readonly IPrivateFileStore storage;
    private DateTimeOffset callbackReturnsAt;
    public readonly AuthorityTimeClock Clock = new();
    public readonly DateTimeOffset SessionExpiresAt = new(2026, 10, 19, 8, 1, 0, TimeSpan.Zero);
    public bool SessionCurrent => Clock.Now >= new DateTimeOffset(2026, 10, 19, 7, 0, 0, TimeSpan.Zero) && Clock.Now < SessionExpiresAt;
    public readonly AuthorityTimeResult Result = new(Guid.NewGuid(), "synthetic-committed");
    public readonly List<Guid> Business = [], Outbox = [];
    public readonly List<AuditEvent> Audits = [];
    public readonly List<IdempotencyReceipt> Receipts = [];
    public readonly List<string> Writes = [], Reads = [];
    public int ActionCalls, CallbackWrites;
    public Func<AttendanceActor, CancellationToken, Task>? BeforeCommit, BeforeReplay;
    public readonly AttendanceOperation Operation;

    public AuthorityTimeFixture()
    {
        var current = AuthorityTimeProxy.Make<ICurrentAuthentication>((m, a) => actor);
        var access = AuthorityTimeProxy.Make<IAccountAccessReader>((m, a) => m.Name == "GetGrantsAsync"
            ? Task.FromResult<IReadOnlyList<PermissionGrant>>([new(LeaveBenefitPermissions.Review, false, role, AccessScopeKind.All, null)])
            : throw new InvalidOperationException("Unexpected access " + m.Name));
        var authorization = AuthorityTimeProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult((Guid)a[0]! == actor.AccountId && (Guid)a[1]! == role),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>((Guid)a[0]! == role ? "Admin" : null),
            _ => throw new InvalidOperationException("Unexpected authorization " + m.Name)
        });
        var journal = AuthorityTimeProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult((AuthenticatedAccount)a[0]! == actor && SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new InvalidOperationException("Unexpected journal " + m.Name)
        });
        storage = AuthorityTimeProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "WriteAsync" => WriteFileAsync((byte[])a[0]!, (string)a[1]!, (CancellationToken)a[2]!),
            "ReadAsync" => ReadFileAsync((PrivateFileReference)a[0]!, (string)a[1]!),
            _ => throw new InvalidOperationException("Unexpected private storage " + m.Name)
        });
        var authentication = AuthorityTimeProxy.Make<IAuthenticationRepository>((m, a) =>
            throw new InvalidOperationException("No host/device/provider calls permitted"));
        Operation = new(current, access, authorization, journal, authentication, Clock, storage);
    }
    public string OperationPurpose(string kind) => $"attendance:operation:{actor.AccountId:N}:{OperationName}:{kind}";
    public Task<AuthorityTimeResult> WriteAsync() => Operation.WriteAsync(LeaveBenefitPermissions.Review, OperationName,
        employee, "authority-time-001", new { EmployeeId = employee }, "Synthetic timing verification", new("127.0.0.1", "synthetic-authority-time"),
        (identity, ct) => {
            ActionCalls++; Business.Add(Result.Id); Outbox.Add(Result.Id);
            return Task.FromResult(new AttendanceChange<AuthorityTimeResult>(Result.Id, null, Result));
        }, default, beforeReplay: BeforeReplay, requireSystemRole: true, beforeCommit: BeforeCommit);
    public async Task CallbackStorageAsync(DateTimeOffset returnsAt, CancellationToken ct)
    {
        callbackReturnsAt = returnsAt;
        await storage.WriteAsync([1, 2, 3], CallbackPurpose, ct);
    }
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var business = Business.Count; var outbox = Outbox.Count; var audits = Audits.Count; var receipts = Receipts.Count;
        try { return await action(ct); }
        catch {
            Business.RemoveRange(business, Business.Count - business); Outbox.RemoveRange(outbox, Outbox.Count - outbox);
            Audits.RemoveRange(audits, Audits.Count - audits); Receipts.RemoveRange(receipts, Receipts.Count - receipts); throw;
        }
    }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private async Task<PrivateFileReference> WriteFileAsync(byte[] bytes, string purpose, CancellationToken ct)
    {
        await Task.Yield(); ct.ThrowIfCancellationRequested();
        var key = Guid.NewGuid().ToString("N"); files.Add(key, (bytes.ToArray(), purpose)); Writes.Add(purpose);
        if (purpose == CallbackPurpose) { CallbackWrites++; Clock.Now = callbackReturnsAt; }
        return new(key, "synthetic", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length);
    }
    private Task<byte[]> ReadFileAsync(PrivateFileReference reference, string purpose)
    {
        if (!files.TryGetValue(reference.StorageKey, out var saved) || saved.Purpose != purpose)
            throw AttendanceOperation.Conflict("private_evidence_unavailable");
        Reads.Add(purpose); return Task.FromResult(saved.Bytes.ToArray());
    }
}
