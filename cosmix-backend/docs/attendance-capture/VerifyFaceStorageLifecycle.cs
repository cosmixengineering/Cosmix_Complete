#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyFaceStorageLifecycle.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;

// Synthetic in-memory evidence only. No files, SQL, provider, model or real employee are used.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Reject(Func<Task> action, string name)
{
    try { await action(); }
    catch (AuthenticationFailure failure) when (failure.StatusCode is 409 or 503) { checks++; return; }
    throw new Exception(name);
}
var implementation = typeof(FaceStorageRetentionOptions).Assembly.GetType(
    "Cosmix.Application.Attendance.FaceVerification.RetentionControlledFaceTemplateStore");
Check(implementation is not null, "Missing retention control: retained objects need an approved, audited use boundary");
var now = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
var keyIsolation=typeof(EncryptedFaceStorageRetentionJournal).GetMethod("KeyPathsSeparated",BindingFlags.Static|BindingFlags.NonPublic);
Check(keyIsolation is not null,"all three active/historical keyrings require an explicit directory-isolation check");
var isolatedRoots=new[]{"C:/synthetic/index","C:/synthetic/metadata","C:/synthetic/biometric","C:/synthetic/ordinary"};
var metadataOptions=new FaceStorageRetentionJournalOptions {Storage=new() {KeyFile="C:/synthetic/keys/metadata.key"}};
var biometricOptions=new FaceTemplateStorageOptions {Storage=new() {KeyFile="C:/synthetic/keys/face.key"}};
var ordinaryOptions=new PrivateFileStorageOptions {KeyFile="C:/synthetic/keys/ordinary.key"};
bool Isolated(FaceStorageRetentionJournalOptions m,FaceTemplateStorageOptions b,PrivateFileStorageOptions o) =>
    (bool)keyIsolation!.Invoke(null,[m,b,o,isolatedRoots])!;
Check(Isolated(metadataOptions,biometricOptions,ordinaryOptions),"disjoint fully qualified synthetic key paths are eligible without filesystem reads");
biometricOptions.Storage=biometricOptions.Storage with {KeyFile="C:/synthetic/index/face.key"};
Check(!Isolated(metadataOptions,biometricOptions,ordinaryOptions),"biometric active key inside lifecycle index is rejected");
biometricOptions.Storage=biometricOptions.Storage with {KeyFile="C:/synthetic/keys/face.key",
    DecryptionKeyFiles=new(){{"retained-face","C:/synthetic/metadata/old.key"}}};
Check(!Isolated(metadataOptions,biometricOptions,ordinaryOptions),"historical biometric key inside metadata directory is rejected");
biometricOptions.Storage=biometricOptions.Storage with {DecryptionKeyFiles=new()};
Check(!Isolated(metadataOptions,biometricOptions,ordinaryOptions with {KeyFile="C:/synthetic/index/ordinary.key"}),
    "ordinary active key inside lifecycle index is rejected");
Check(!Isolated(metadataOptions,biometricOptions,ordinaryOptions with {DecryptionKeyFiles=new(){{"old","C:/synthetic/metadata/ordinary.key"}}}),
    "ordinary historical key inside lifecycle metadata is rejected");
var employee = Guid.Parse("00000000-0000-0000-0000-000000000001");
var capture = Guid.Parse("00000000-0000-0000-0000-000000000002");
var purpose = $"attendance:face:template:{employee:N}";
var capturePurpose = $"attendance:face:template:{employee:N}:capture:{capture:N}";
var settings = new FaceStorageRetentionOptions
{
    Enabled = true, AuthorityConfirmed = true, ApprovalReference = "synthetic-use-boundary-only",
    CaptureUseLifetimeSeconds = 10, EnrollmentUseLifetimeSeconds = 20
};
var clock = new Clock(now);
var raw = new SyntheticVault();
var journal = new SyntheticJournal();
IFaceTemplateStore Store(FaceStorageRetentionOptions? options = null, SyntheticVault? vault = null,
    IFaceStorageRetentionJournal? records = null) => (IFaceTemplateStore)Activator.CreateInstance(implementation!,
        vault ?? raw, records ?? journal, options ?? settings, clock)!;
async Task Retire(IFaceTemplateStore store, PrivateFileReference reference, string boundPurpose,
    FaceStorageRetentionReason reason, string operationKey)
{
    Task pending;
    try
    {
        pending = (Task)implementation!.GetMethod("RetireAsync")!.Invoke(store,
            [reference, boundPurpose, reason, operationKey, CancellationToken.None])!;
    }
    catch (TargetInvocationException error) when (error.InnerException is not null)
    { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    await pending;
}
var store = Store();
Check(store.IsConfigured, "explicit approved use boundary can be configured independently");
var reference = await store.WriteAsync([7, 9, 11], purpose, default);
Check(journal.Events.Count == 1 && journal.Events[0].Kind == FaceStorageRetentionEventKind.Stored &&
    journal.Events[0].Reference == reference && journal.Events[0].Purpose == purpose &&
    journal.Events[0].UseUntilUtc == now.AddSeconds(20), "reference release requires exact approved durable enrollment record");
var bytes = await store.ReadAsync(reference, purpose, default);
Check(bytes.SequenceEqual(new byte[] { 7, 9, 11 }) && journal.Events[^1].Kind == FaceStorageRetentionEventKind.UseReleasePrepared,
    "successful byte release is audited");
CryptographicOperations.ZeroMemory(bytes);
await Retire(store, reference, purpose, FaceStorageRetentionReason.EnrollmentRevoked, "revoke:synthetic-0001");
var retiredCount = journal.Events.Count;
await Retire(store, reference, purpose, FaceStorageRetentionReason.EnrollmentRevoked, "revoke:synthetic-0001");
Check(journal.Events.Count == retiredCount, "retirement retry does not duplicate disposition");
var priorReads = raw.Reads;
await Reject(() => store.ReadAsync(reference, purpose, default), "retired evidence must not be released");
Check(raw.Reads == priorReads && raw.Contains(reference), "retirement denies before decrypt while retaining original evidence");

var captureReference = await store.WriteAsync([13, 17, 19], capturePurpose, default);
Check(journal.Events[^1].UseUntilUtc == now.AddSeconds(10), "capture uses its own approved boundary");
clock.Now = now.AddSeconds(10);
priorReads = raw.Reads;
await Reject(() => store.ReadAsync(captureReference, capturePurpose, default), "exact boundary is exclusive");
Check(raw.Reads == priorReads && raw.Contains(captureReference), "expiry limits use without deleting evidence");
clock.Now = now;
var restart = Store();
await Reject(() => restart.ReadAsync(reference, purpose, default), "new instance retains retirement state");
var legacy = await raw.WriteAsync([23, 29, 31], purpose, default);
priorReads = raw.Reads;
await Reject(() => store.ReadAsync(legacy, purpose, default), "untracked legacy evidence requires explicit reconciliation");
Check(raw.Reads == priorReads && raw.Contains(legacy), "untracked objects are preserved and not decrypted");
await Reject(() => store.ReadAsync(captureReference, purpose, default), "purpose substitution rejected");
await Reject(() => store.ReadAsync(captureReference with { Sha256 = new string('A', 64) }, capturePurpose, default),
    "reference substitution rejected");
await Reject(() => store.WriteAsync([1], "attendance:face:template:invalid", default), "malformed face purpose rejected");
await Reject(() => store.WriteAsync([1], "legal:raw:evidence", default), "ordinary/legal evidence cannot enter this lifecycle");
foreach (var options in new[]
{
    settings with { Enabled = false }, settings with { AuthorityConfirmed = false },
    settings with { ApprovalReference = "" }, settings with { CaptureUseLifetimeSeconds = null },
    settings with { EnrollmentUseLifetimeSeconds = 0 }
})
{
    var disabled = Store(options);
    Check(!disabled.IsConfigured, "missing approved boundary is unavailable");
    await Reject(() => disabled.WriteAsync([1], purpose, default), "unapproved retention cannot write evidence");
}

journal.FailAppend = true;
var objectCount = raw.Count;
await Reject(() => store.WriteAsync([37, 41, 43], purpose, default), "registration outage cannot return untracked object authority");
Check(raw.Count == objectCount + 1, "uncertain write preserves encrypted orphan for reconciliation");
journal.FailAppend = false;
var audited = await store.WriteAsync([47, 53, 59], purpose, default);
journal.FailAppend = true;
await Reject(() => store.ReadAsync(audited, purpose, default), "audit outage cannot release plaintext");
Check(raw.LastRead is null || raw.LastRead.All(b => b == 0), "failed release audit leaves no returned plaintext buffer");
journal.FailAppend = false;
raw.BeforeRead = () => { clock.Now = now.AddSeconds(20); return Task.CompletedTask; };
await Reject(() => store.ReadAsync(audited, purpose, default), "boundary reached during decryption cannot release bytes");
Check(raw.LastRead is null || raw.LastRead.All(b => b == 0), "late boundary failure zeroes decrypted bytes");
raw.BeforeRead = null;
clock.Now = now;
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    try { await store.ReadAsync(audited, purpose, cancellation.Token); throw new Exception("cancelled operation released bytes"); }
    catch (OperationCanceledException) { checks++; }
}

var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
raw.BeforeRead = async () => { entered.SetResult(); await release.Task; };
var reading = store.ReadAsync(audited, purpose, default);
await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
var retiring = Retire(store, audited, purpose, FaceStorageRetentionReason.EnrollmentReplaced, "replace:synthetic-0001");
Check(!retiring.IsCompleted, "retirement shares byte-release lock");
release.SetResult();
CryptographicOperations.ZeroMemory(await reading);
await retiring;
raw.BeforeRead = null;
await Reject(() => store.ReadAsync(audited, purpose, default), "completed retirement excludes later byte release");
Check(raw.Contains(audited), "concurrent retirement retains ciphertext");

var journalType = typeof(FaceStorageRetentionJournalOptions).Assembly.GetType(
    "Cosmix.Infrastructure.FaceVerification.EncryptedFaceStorageRetentionJournal");
Check(journalType is not null, "Missing encrypted lifecycle journal: interrupted dispositions must fail closed");
var metadata = new SyntheticMetadata();
var index = new SyntheticIndex();
IFaceStorageRetentionJournal Journal() => (IFaceStorageRetentionJournal)journalType!
    .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
        [typeof(IPrivateFileStore), typeof(IFaceStorageRetentionIndex)], null)!.Invoke([metadata, index]);
var persisted = Journal();
var record = new FaceStorageRetentionEvent(1, legacy, purpose, FaceStorageRetentionEventKind.Stored,
    now, now.AddSeconds(20), settings.ApprovalReference);
var deserialize = journalType!.GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)!
    .MakeGenericMethod(typeof(FaceStorageRetentionEvent));
foreach (var missing in new[] { "Kind", "OccurredAtUtc", "UseUntilUtc", "Sequence", "Reference", "Purpose", "ApprovalReference" })
{
    var malformed = JsonSerializer.SerializeToNode(record)!.AsObject();
    malformed.Remove(missing);
    try
    {
        _ = deserialize.Invoke(null, [JsonSerializer.SerializeToUtf8Bytes(malformed)]);
        throw new Exception("Missing journal field must not become a C# default: " + missing);
    }
    catch (TargetInvocationException error) when (error.InnerException is JsonException or AuthenticationFailure) { checks++; }
}
await persisted.WithObjectLockAsync(legacy, async token =>
{
    await persisted.AppendAsync(record, token);
    return true;
}, default);
var restarted = Journal();
var recovered = await restarted.WithObjectLockAsync(legacy, token => restarted.ReadAsync(legacy, token), default);
Check(recovered.Count == 1 && recovered[0] == record, "metadata round-trips through recreated journal");
await Reject(() => persisted.ReadAsync(legacy, default), "journal access outside object lock denied");
await Reject(() => persisted.WithObjectLockAsync(legacy, async token =>
    { await persisted.AppendAsync(record with { Sequence = 3 }, token); return true; }, default), "non-contiguous append denied");
await Reject(() => persisted.WithObjectLockAsync(legacy,
    token => persisted.ReadAsync(legacy with { Sha256 = new string('B', 64) }, token), default), "complete reference substitution denied");
metadata.Corrupt = true;
await Reject(() => persisted.WithObjectLockAsync(legacy, token => persisted.ReadAsync(legacy, token), default),
    "encrypted metadata authentication failure denied");
metadata.Corrupt = false;
index.FailAfterReservation = true;
await Reject(() => persisted.WithObjectLockAsync(legacy, async token => { await persisted.AppendAsync(record with
    { Sequence = 2, Kind = FaceStorageRetentionEventKind.Retired, RetirementReason = FaceStorageRetentionReason.EnrollmentRevoked,
      OperationKey = "revoke:synthetic-0002" }, token); return true; }, default), "interrupted metadata write preserves reservation");
index.FailAfterReservation = false;
var interruptedRestart = Journal();
await Reject(() => interruptedRestart.WithObjectLockAsync(legacy, token => interruptedRestart.ReadAsync(legacy, token), default),
    "interrupted index denies use after restart");
var disabledJournal = (IFaceStorageRetentionJournal)Activator.CreateInstance(journalType!,
    new FaceStorageRetentionJournalOptions(), new FaceTemplateStorageOptions(), new PrivateFileStorageOptions())!;
Check(!disabledJournal.IsConfigured, "default production journal unavailable");
await Reject(() => disabledJournal.WithObjectLockAsync(legacy, _ => Task.FromResult(true), default), "unconfigured journal performs no IO");

var integratedJournal = Journal();
var integrated = Store(records: integratedJournal);
var integratedReference = await integrated.WriteAsync([61, 67, 71], purpose, default);
var integratedBytes = await integrated.ReadAsync(integratedReference, purpose, default);
Check(integratedBytes.SequenceEqual(new byte[] { 61, 67, 71 }), "decorator releases bytes through actual journal arbitration");
CryptographicOperations.ZeroMemory(integratedBytes);
await Retire(integrated, integratedReference, purpose, FaceStorageRetentionReason.EnrollmentReplaced, "replace:synthetic-0002");
await Reject(() => Store(records: Journal()).ReadAsync(integratedReference, purpose, default), "actual journal retirement survives decorator restart");
Check(raw.Contains(integratedReference) && metadata.LastRead!.All(b => b == 0), "evidence remains while journal read buffers are zeroed");
var uncertainReference = await integrated.WriteAsync([73, 79, 83], purpose, default);
index.FailAfterMetadata = true;
await Reject(() => integrated.ReadAsync(uncertainReference, purpose, default), "interrupted release index cannot return bytes");
Check(raw.LastRead!.All(b => b == 0) && raw.Contains(uncertainReference), "interrupted release zeroes plaintext and preserves evidence");
index.FailAfterMetadata = false;
await Reject(() => Store(records: Journal()).ReadAsync(uncertainReference, purpose, default), "orphaned release metadata does not erase incomplete reservation");
Console.WriteLine($"Face storage lifecycle checks passed: {checks}. Synthetic persistence only; no filesystem durability, SQL, biometric or production acceptance.");

sealed class Clock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
sealed class SyntheticVault : IFaceTemplateStore
{
    private readonly Dictionary<string, byte[]> objects = new();
    public bool IsConfigured => true;
    public int Reads { get; private set; }
    public int Count => objects.Count;
    public byte[]? LastRead { get; private set; }
    public Func<Task>? BeforeRead { get; set; }
    public bool Contains(PrivateFileReference reference) => objects.ContainsKey(reference.StorageKey);
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var reference = new PrivateFileReference(Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"),
            Convert.ToHexString(SHA256.HashData(content)), "synthetic-biometric-key", content.Length);
        objects.Add(reference.StorageKey, content.ToArray());
        return Task.FromResult(reference);
    }
    public async Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Reads++;
        if (BeforeRead is not null) await BeforeRead();
        return LastRead = objects[reference.StorageKey].ToArray();
    }
}
sealed class SyntheticJournal : IFaceStorageRetentionJournal
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool IsConfigured => true;
    public bool FailAppend { get; set; }
    public List<FaceStorageRetentionEvent> Events { get; } = [];
    public async Task<T> WithObjectLockAsync<T>(PrivateFileReference reference,
        Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return await action(ct); }
        finally { gate.Release(); }
    }
    public Task<IReadOnlyList<FaceStorageRetentionEvent>> ReadAsync(PrivateFileReference reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FaceStorageRetentionEvent>>(Events
            .Where(e => e.Reference.StorageKey == reference.StorageKey && e.Reference.ObjectVersion == reference.ObjectVersion).ToArray());
    }
    public Task AppendAsync(FaceStorageRetentionEvent value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailAppend) throw new AuthenticationFailure("face_retention_unavailable", 503);
        Events.Add(value);
        return Task.CompletedTask;
    }
}

sealed class SyntheticMetadata : IPrivateFileStore
{
    private readonly Dictionary<string, (byte[] Bytes, string Purpose, PrivateFileReference Reference)> values = new();
    public bool Corrupt { get; set; }
    public byte[]? LastRead { get; private set; }
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var reference = new PrivateFileReference(Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"),
            Convert.ToHexString(SHA256.HashData(content)), "synthetic-metadata-key", content.Length);
        values.Add(reference.StorageKey, (content.ToArray(), purpose, reference));
        return Task.FromResult(reference);
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var stored = values[reference.StorageKey];
        if (Corrupt || stored.Reference != reference || stored.Purpose != purpose)
            throw new AuthenticationFailure("synthetic_metadata_unavailable", 503);
        return Task.FromResult(LastRead = stored.Bytes.ToArray());
    }
}
sealed class SyntheticIndex : IFaceStorageRetentionIndex
{
    private readonly Dictionary<string, List<PrivateFileReference?>> entries = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool IsConfigured => true;
    public bool FailAfterReservation { get; set; }
    public bool FailAfterMetadata { get; set; }
    public async Task<T> WithObjectLockAsync<T>(string objectId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return await action(ct); }
        finally { gate.Release(); }
    }
    public Task<IReadOnlyList<PrivateFileReference>> ReadAsync(string objectId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!entries.TryGetValue(objectId, out var values)) return Task.FromResult<IReadOnlyList<PrivateFileReference>>([]);
        if (values.Any(x => x is null)) throw new AuthenticationFailure("synthetic_index_incomplete", 503);
        return Task.FromResult<IReadOnlyList<PrivateFileReference>>(values.Select(x => x!).ToArray());
    }
    public async Task AppendAsync(string objectId, int sequence,
        Func<CancellationToken, Task<PrivateFileReference>> writeMetadata, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!entries.TryGetValue(objectId, out var values)) entries.Add(objectId, values = []);
        if (sequence != values.Count + 1 || values.Any(x => x is null)) throw new AuthenticationFailure("synthetic_index_invalid", 503);
        values.Add(null);
        if (FailAfterReservation) throw new AuthenticationFailure("synthetic_write_interrupted", 503);
        var reference = await writeMetadata(ct);
        if (FailAfterMetadata) throw new AuthenticationFailure("synthetic_pointer_interrupted", 503);
        values[^1] = reference;
    }
}
