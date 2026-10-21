#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyZoneLocationStorage.packages.lock.json
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Zones;

// Requires explicit runner approval: fresh uniquely named OS-temp fixture, owner-only ACL, disposable
// synthetic key/objects only. No SQL, accounts, providers, retained AppData acceptance roots or app config.
// Production storage rejects repository paths, so ignored repository artifacts cannot host this test.
if (!args.SequenceEqual(["--synthetic-private-files"])) throw new ArgumentException("Explicit --synthetic-private-files is required.");
var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
var root = Path.Combine(temporary,"Cosmix-ZoneRetention-Verification-"+Guid.NewGuid().ToString("N"));
if (Directory.Exists(root) || !Path.GetFullPath(root).StartsWith(temporary+Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Fresh bounded fixture required.");
Directory.CreateDirectory(root);
if (OperatingSystem.IsWindows())
{
    using var identity=WindowsIdentity.GetCurrent();var security=new DirectorySecurity();
    security.SetOwner(identity.User!);security.SetAccessRuleProtection(true,false);
    security.AddAccessRule(new FileSystemAccessRule(identity.User!,FileSystemRights.FullControl,
        InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
    new DirectoryInfo(root).SetAccessControl(security);
}
else File.SetUnixFileMode(root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
PrivateBootstrapSnapshotStore.RequirePrivatePath(root,true);
var checks=0;var keyBytes=RandomNumberGenerator.GetBytes(32);
void Check(bool value,string description){checks++;if(!value)throw new Exception(description);}
async Task Fails(Func<Task> action,string code)
{checks++;try{await action();}catch(AuthenticationFailure ex)when(ex.Code==code){return;}throw new Exception("Expected "+code);}
string Dir(string name){var path=Path.Combine(root,name);Directory.CreateDirectory(path);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);return path;}
try
{
    var owned=Dir("locations");var objects=Dir(Path.Combine("locations","objects"));var index=Dir(Path.Combine("locations","expiry-index"));
    var ordinary=Dir("ordinary");var biometric=Dir("biometric");var key=Path.Combine(root,"synthetic-disposable.key");
    await File.WriteAllBytesAsync(key,keyBytes);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(key,UnixFileMode.UserRead|UnixFileMode.UserWrite);
    var normal=new PrivateFileStorageOptions{Directory=ordinary,KeyFile=key,KeyReference="synthetic-shared-key"};
    var face=new FaceTemplateStorageOptions{Storage=normal with{Directory=biometric}};
    var options=new ZoneLocationStorageOptions{Enabled=true,Storage=normal with{Directory=owned}};
    var clock=new TestClock();using var store=new EncryptedZoneLocationStore(options,normal,face,clock);
    Check(store.IsConfigured,"dedicated provisioned root reuses existing encryption adapter/key without ordinary directory fallback");
    Check(!new EncryptedZoneLocationStore(new(),normal,face,clock).IsConfigured,"storage disabled by default");
    Check(!new EncryptedZoneLocationStore(new(){Enabled=true,Storage=normal},normal,face,clock).IsConfigured,"ordinary directory overlap rejected");
    Check(!new EncryptedZoneLocationStore(new(){Enabled=true,Storage=normal with{Directory=biometric}},normal,face,clock).IsConfigured,"biometric directory overlap rejected");
    Check(!new EncryptedZoneLocationStore(new(){Enabled=true,Storage=normal with{Directory=root}},normal,face,clock).IsConfigured,"parent directory containing ordinary store rejected");
    Check(!new EncryptedZoneLocationStore(new(){Enabled=true,Storage=normal with{Directory=Dir("unprovisioned")}},normal,face,clock).IsConfigured,"missing owned objects/index directories fail closed");
    var sample=new ZoneLocationPayload(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),clock.Now,clock.Now.AddMinutes(5),
        31.2345671m,74.3456782m,7.25m,clock.Now.AddSeconds(-1),Guid.NewGuid());
    var reference=await store.StoreAsync(sample,default);var parsed=JsonSerializer.Deserialize<PrivateFileReference>(reference)!;
    var objectPath=Path.Combine(objects,parsed.StorageKey);var indexPath=Directory.GetFiles(index,"*.zli",SearchOption.AllDirectories).Single();
    Check(File.Exists(objectPath)&&File.Exists(indexPath),"ciphertext and durable allocation index present");
    var indexText=await File.ReadAllTextAsync(indexPath);
    Check(!indexText.Contains("Latitude")&&!indexText.Contains("Longitude")&&!indexText.Contains("RequestNonce")&&!indexText.Contains(sample.RequestNonce.ToString())&&
        !indexText.Contains(sample.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)),"index contains no coordinates or private request nonce");
    Check(!Encoding.UTF8.GetString(await File.ReadAllBytesAsync(objectPath)).Contains("Latitude"),"location is encrypted on disk");
    Check(File.GetAttributes(objectPath).HasFlag(FileAttributes.ReadOnly)&&File.GetAttributes(indexPath).HasFlag(FileAttributes.ReadOnly),"object/index immutable after write");
    Check(await store.ReadAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default)==sample,"purpose-bound location read round trip");
    Check(await new EncryptedZoneLocationStore(options,normal,face,clock).ReadAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default)==sample,
        "new adapter instance reopens persisted ciphertext and key");
    // Hold the same OS lease as an in-flight writer; a separate adapter must wait before allocation.
    using(var peer=new EncryptedZoneLocationStore(options,normal,face,clock))
    {
        using var cancelled=new CancellationTokenSource();
        using(var lease=new FileStream(Path.Combine(owned,"location-operations.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            var pending=peer.StoreAsync(sample with{EventId=Guid.NewGuid(),RequestNonce=Guid.NewGuid()},cancelled.Token);
            await Task.Delay(50);Check(!pending.IsCompleted,"second adapter waits for cross-process allocation/retention lease");
            cancelled.Cancel();
            try{await pending;throw new Exception("Lease contention ignored cancellation");}catch(OperationCanceledException){checks++;}
            Check(Directory.GetFiles(index,"*.zli",SearchOption.AllDirectories).Length==1,"blocked writer creates no partial allocation");
        }
    }
    await Fails(()=>store.ReadAsync(Guid.NewGuid(),sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default),"zone_location_storage_unavailable");
    await Fails(()=>store.ReadAsync(sample.EventId,Guid.NewGuid(),sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default),"zone_location_storage_unavailable");
    await Fails(()=>store.ReadAsync(sample.EventId,sample.EmployeeId,Guid.NewGuid(),sample.ExpiresAtUtc,reference,clock.Now,default),"zone_location_storage_unavailable");
    await Fails(()=>store.ReadAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc.AddMinutes(1),reference,clock.Now,default),"zone_location_storage_unavailable");
    await Fails(()=>store.DeleteExpiredAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default),"zone_location_not_expired");
    await Fails(()=>store.DeleteExpiredAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,sample.ExpiresAtUtc.AddHours(1),default),"zone_location_not_expired");
    var before=clock.Now;clock.Reads.Enqueue(before);clock.Reads.Enqueue(sample.ExpiresAtUtc);
    await Fails(()=>store.ReadAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,before,default),"zone_location_expired");
    Check(clock.Reads.Count==0&&File.Exists(objectPath),"post-decryption expiry recheck blocks stale response without deleting evidence");
    var traversal=JsonSerializer.Serialize(parsed with{StorageKey="../synthetic-disposable.key"});
    clock.Now=sample.ExpiresAtUtc;
    await Fails(()=>store.DeleteExpiredAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,traversal,clock.Now,default),"zone_location_storage_unavailable");
    Check((await File.ReadAllBytesAsync(key)).SequenceEqual(keyBytes),"arbitrary paths never reach key deletion");
    await Fails(()=>store.ReadAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default),"zone_location_expired");
    var inventory=await store.ScanExpiredAsync(clock.Now,null,10,default);
    Check(inventory.Entries.Single().EventId==sample.EventId&&inventory.InvalidEntries==0,"expired orphan index discoverable without any database row");
    Check(await store.DeleteExpiredAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default)&&!File.Exists(objectPath)&&!File.Exists(indexPath),
        "expired exact object and allocation index deleted");
    Check(await store.DeleteExpiredAsync(sample.EventId,sample.EmployeeId,sample.WindowId,sample.ExpiresAtUtc,reference,clock.Now,default),"missing object/index deletion retry is idempotent");

    // Force actual key-read failure AFTER allocation-index flush but BEFORE ciphertext creation.
    var interrupted=sample with{EventId=Guid.NewGuid(),ReceivedAtUtc=clock.Now,ExpiresAtUtc=clock.Now.AddMinutes(5),RequestNonce=Guid.NewGuid()};
    await File.WriteAllBytesAsync(key,new byte[1]);
    try{await Fails(()=>store.StoreAsync(interrupted,default),"zone_location_storage_unavailable");}
    finally{await File.WriteAllBytesAsync(key,keyBytes);}
    var interruptedIndex=Directory.GetFiles(index,"*.zli",SearchOption.AllDirectories).Single();var interruptedEntry=JsonSerializer.Deserialize<ZoneLocationIndexEntry>(await File.ReadAllTextAsync(interruptedIndex))!;
    var interruptedReference=JsonSerializer.Deserialize<PrivateFileReference>(interruptedEntry.Reference)!;
    Check(interruptedEntry.EventId==interrupted.EventId&&!File.Exists(Path.Combine(objects,interruptedReference.StorageKey)),"failed pre-ciphertext write leaves durable identity/expiry reference, no unknown ciphertext");
    clock.Now=interrupted.ExpiresAtUtc;
    Check((await store.ScanExpiredAsync(clock.Now,null,10,default)).Entries.Single().EventId==interrupted.EventId,"failed allocation remains discoverable after expiry");
    Check(await store.DeleteExpiredAsync(interrupted.EventId,interrupted.EmployeeId,interrupted.WindowId,interrupted.ExpiresAtUtc,interruptedEntry.Reference,clock.Now,default)&&!File.Exists(interruptedIndex),
        "expired allocation with no object reconciles safely");

    var partial=sample with{EventId=Guid.NewGuid(),ReceivedAtUtc=clock.Now,ExpiresAtUtc=clock.Now.AddMinutes(5),RequestNonce=Guid.NewGuid()};
    var partialReference=await store.StoreAsync(partial,default);var partialRef=JsonSerializer.Deserialize<PrivateFileReference>(partialReference)!;var partialPath=Path.Combine(objects,partialRef.StorageKey);
    var partialBytes=await File.ReadAllBytesAsync(partialPath);File.SetAttributes(partialPath,File.GetAttributes(partialPath)&~FileAttributes.ReadOnly);
    await File.WriteAllBytesAsync(partialPath,partialBytes[..(partialBytes.Length/2)]);File.SetAttributes(partialPath,File.GetAttributes(partialPath)|FileAttributes.ReadOnly);
    clock.Now=partial.ExpiresAtUtc;
    await Fails(()=>store.DeleteExpiredAsync(partial.EventId,partial.EmployeeId,partial.WindowId,partial.ExpiresAtUtc,partialReference,clock.Now,default),"zone_location_storage_unavailable");
    Check(File.Exists(partialPath)&&Directory.GetFiles(index,"*.zli",SearchOption.AllDirectories).Any(x=>x.Contains(partial.EventId.ToString("N"))),"partial ciphertext is retained for verification/manual review, never falsely reported deleted");

    var unrelatedStore=new EncryptedPrivateFileStore(normal with{Directory=objects});
    var unrelated=await unrelatedStore.WriteAsync(Encoding.UTF8.GetBytes("SYNTHETIC-NON-ZONE-DOCUMENT"),"synthetic:hr:document",default);
    await Fails(()=>store.DeleteExpiredAsync(Guid.NewGuid(),sample.EmployeeId,sample.WindowId,clock.Now.AddMinutes(-1),JsonSerializer.Serialize(unrelated),clock.Now,default),"zone_location_storage_unavailable");
    Check(File.Exists(Path.Combine(objects,unrelated.StorageKey)),"wrong-purpose object cannot be deleted even in owned directory");
    var unrelatedOutside=await new EncryptedPrivateFileStore(normal).WriteAsync(Encoding.UTF8.GetBytes("SYNTHETIC-ORDINARY"),"synthetic:ordinary",default);
    _=await store.DeleteExpiredAsync(Guid.NewGuid(),sample.EmployeeId,sample.WindowId,clock.Now.AddMinutes(-1),JsonSerializer.Serialize(unrelatedOutside),clock.Now,default);
    Check(File.Exists(Path.Combine(ordinary,unrelatedOutside.StorageKey)),"ordinary store never traversed or deleted from zone adapter");

    var queued=new List<(ZoneLocationPayload Payload,string Reference)>();
    for(var i=0;i<3;i++){var value=sample with{EventId=Guid.NewGuid(),ReceivedAtUtc=clock.Now,ExpiresAtUtc=clock.Now.AddMinutes(5),RequestNonce=Guid.NewGuid()};queued.Add((value,await store.StoreAsync(value,default)));}
    clock.Now=clock.Now.AddMinutes(5);var page1=await store.ScanExpiredAsync(clock.Now,null,1,default);var page2=await store.ScanExpiredAsync(clock.Now,page1.NextCursor,1,default);
    Check(page1.Entries.Count==1&&page1.NextCursor is not null&&page2.Entries.Count==1&&page1.Entries[0].EventId!=page2.Entries[0].EventId,"bounded streaming scan advances past retained orphans without exposing payloads");
    await Fails(()=>store.ScanExpiredAsync(clock.Now,"../outside",25,default),"invalid_zone_retention_scan");
    await Fails(()=>store.ScanExpiredAsync(clock.Now,null,501,default),"invalid_zone_retention_scan");
    var invalidIndex=Directory.GetFiles(index,"*.zli",SearchOption.AllDirectories).Single(x=>x.Contains(queued[0].Payload.EventId.ToString("N")));
    File.SetAttributes(invalidIndex,File.GetAttributes(invalidIndex)&~FileAttributes.ReadOnly);await File.WriteAllTextAsync(invalidIndex,"{broken-index");
    var broken=await store.ScanExpiredAsync(clock.Now,null,500,default);
    Check(broken.InvalidEntries==1&&!broken.Entries.Any(x=>x.EventId==queued[0].Payload.EventId),"malformed allocation index explicitly counted for manual review, never trusted for purge");
    await Fails(()=>store.StoreAsync(sample with{EventId=Guid.NewGuid(),ReceivedAtUtc=clock.Now,ExpiresAtUtc=clock.Now.AddMinutes(1),RequestNonce=Guid.Empty},default),"zone_location_storage_unavailable");
    Console.WriteLine($"Zone encrypted location retention checks passed: {checks}. Fresh synthetic private temp fixture only; no SQL/provider/accounts/configuration.");
}
finally
{
    CryptographicOperations.ZeroMemory(keyBytes);
    // Cleanup only this newly created unique test root. Resolve/check every absolute target, reject
    // reparse paths, and use nonrecursive deletes after enumerating the verified finite test tree.
    var expected=Path.GetFullPath(root);var prefix=expected+Path.DirectorySeparatorChar;
    if(!expected.StartsWith(temporary+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(expected).StartsWith("Cosmix-ZoneRetention-Verification-",StringComparison.Ordinal))
        throw new Exception("Unsafe synthetic cleanup root");
    var directories=Directory.GetDirectories(expected,"*",SearchOption.AllDirectories);
    var files=Directory.GetFiles(expected,"*",SearchOption.AllDirectories);
    foreach(var path in directories.Concat(files))if(!Path.GetFullPath(path).StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
        throw new Exception("Unexpected synthetic cleanup path");
    foreach(var path in files){File.SetAttributes(path,File.GetAttributes(path)&~FileAttributes.ReadOnly);File.Delete(path);}
    foreach(var path in directories.OrderByDescending(x=>x.Length))Directory.Delete(path,false);
    Directory.Delete(expected,false);
}
public sealed class TestClock:TimeProvider
{
    public DateTimeOffset Now=new(2026,10,9,6,0,0,TimeSpan.Zero);
    public Queue<DateTimeOffset> Reads=[];
    public override DateTimeOffset GetUtcNow()=>Reads.Count==0?Now:Reads.Dequeue();
}
