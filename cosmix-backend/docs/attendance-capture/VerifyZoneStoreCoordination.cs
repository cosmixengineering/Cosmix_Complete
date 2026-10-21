#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Runtime.CompilerServices;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Zones;

// No storage fixture: hold the production coordination gate, then cancel the writer before I/O.
// Deliberately unusable inner adapter ensures a missing gate fails immediately rather than writes.
using var store = new EncryptedZoneLocationStore(new(), new(), new FaceTemplateStorageOptions(), TimeProvider.System);
void Set(string name, object value) => typeof(EncryptedZoneLocationStore).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, value);
Set("inner", RuntimeHelpers.GetUninitializedObject(typeof(EncryptedPrivateFileStore)));
Set("directory", "unusable-synthetic-path"); Set("indexDirectory", "unusable-synthetic-path"); Set("maxBytes", 4096);
var gate = (SemaphoreSlim)typeof(EncryptedZoneLocationStore).GetField("gate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(store)!;
await gate.WaitAsync();
using var cancel = new CancellationTokenSource();
var now = DateTimeOffset.UtcNow;
var pending = store.StoreAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(1), 24m, 67m, 5m, now, Guid.NewGuid()), cancel.Token);
var waited = !pending.IsCompleted;
cancel.Cancel();
var cancelled = false;
try { await pending; } catch (OperationCanceledException) { cancelled = true; } catch (Exception) { }
finally { gate.Release(); }
if (!waited || !cancelled) throw new Exception("Location allocation bypassed the retention coordination gate.");
Console.WriteLine("PASS 2 storage coordination checks: writer waits for retention gate and cancellation exits before file I/O; no fixtures/SQL.");
