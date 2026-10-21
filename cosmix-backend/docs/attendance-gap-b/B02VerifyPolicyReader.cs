#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Common.Files;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
async Task Refuse(Func<Task> action, string code)
{
    try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; }
    throw new Exception("Expected " + code);
}
var files = new Files(); var repo = new Policies();
T Unused<T>() where T : class => Proxy.Make<T>((_, _) => throw new Exception("Reader must not open a nested authorization transaction or provider"));
var operation = new AttendanceOperation(Unused<ICurrentAuthentication>(), Unused<IAccountAccessReader>(),
    Unused<IAccessManagementRepository>(), Unused<ISchedulingJournalRepository>(), Unused<IAuthenticationRepository>(), TimeProvider.System, files);
var service = new CaptureValidationPolicyService(operation, repo, files);
var now = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
var disabled = new CaptureValidationSettings(new(false, null), new(false, null), new(false, null, null, null), new(false, null, null));
var entry = new CaptureValidationPolicyEntry(Guid.NewGuid(), 1, null, now, now.AddDays(-1), "synthetic-admin",
    "SYNTHETIC-APPROVAL", disabled, "Synthetic approved disabled fixture", "", "");
entry = entry with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(entry) };
var proof = new CaptureValidationPolicyProof(1, entry);
var reference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), "attendance:capture-validation:global-policy:v1", default);
entry = entry with { ProtectedEvidenceReference = AttendanceOperation.Reference(reference) };
repo.Rows.Add(entry);
Check((await service.EffectiveAsync(now, default))?.Id == entry.Id, "Actual reader resolves exact approved version using protected proof");
Check(await service.EffectiveAsync(now.AddTicks(-1), default) is null, "Future-effective publication cannot govern an earlier received event");
repo.Rows[0] = entry with { ApprovalReference = "changed-reference" };
await Refuse(() => service.EffectiveAsync(now, default), "capture_policy_source_conflict");
repo.Rows[0] = entry;
files.Data[reference.StorageKey] = JsonSerializer.SerializeToUtf8Bytes(proof with { SchemaVersion = 2 });
await Refuse(() => service.EffectiveAsync(now, default), "capture_policy_evidence_unavailable");
files.Data[reference.StorageKey] = JsonSerializer.SerializeToUtf8Bytes(proof with { Entry = proof.Entry with { PublishedBy = "changed-actor" } });
await Refuse(() => service.EffectiveAsync(now, default), "capture_policy_source_conflict");
files.Data[reference.StorageKey] = JsonSerializer.SerializeToUtf8Bytes(proof);
var next = entry with { Id = Guid.NewGuid(), Revision = 2, SupersedesVersionId = entry.Id,
    PublishedAtUtc = now, EffectiveFromUtc = now.AddHours(1), ProtectedEvidenceReference = "" };
next = next with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(next) };
var nextReference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new CaptureValidationPolicyProof(1, next)),
    "attendance:capture-validation:global-policy:v1", default);
next = next with { ProtectedEvidenceReference = AttendanceOperation.Reference(nextReference) };
repo.Rows.Add(next);
Check((await service.EffectiveAsync(now.AddMinutes(59), default))?.Id == entry.Id &&
    (await service.EffectiveAsync(now.AddHours(1), default))?.Id == next.Id, "Dated policy change retains earlier capture source");
repo.Rows[1] = next with { SupersedesVersionId = Guid.NewGuid() };
await Refuse(() => service.EffectiveAsync(now, default), "capture_policy_source_conflict");
Console.WriteLine($"PASS: {checks} B02 actual policy reader checks; protected IO is synthetic; no nested authorization, SQL or provider.");

sealed class Policies : ICaptureValidationPolicyRepository
{
    public List<CaptureValidationPolicyEntry> Rows = [];
    public Task<IReadOnlyList<CaptureValidationPolicyEntry>> TimelineAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CaptureValidationPolicyEntry>>(Rows.ToArray());
    public void Add(CaptureValidationPolicyEntry entry) => Rows.Add(entry);
}
sealed class Files : IPrivateFileStore
{
    public Dictionary<string, byte[]> Data = [];
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        if (purpose != "attendance:capture-validation:global-policy:v1") throw new Exception("Wrong protected purpose");
        var id = Guid.NewGuid().ToString("N"); Data[id] = content.ToArray();
        return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(content)), "synthetic-key", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        if (purpose != "attendance:capture-validation:global-policy:v1") throw new Exception("Wrong protected purpose");
        return Task.FromResult(Data[reference.StorageKey].ToArray());
    }
}
class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var instance = Create<T, Proxy>(); ((Proxy)(object)instance).Handler = handler; return instance; }
}
