#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Api.Authentication;
using Cosmix.Api.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
var assembly = typeof(DocumentExpiryReminderWorker).Assembly;
var local = assembly.GetType("Cosmix.Api.Development.LocalTesting")!;
var remove = local.GetMethod("RemoveUnboundedWorkers", BindingFlags.Static | BindingFlags.NonPublic)!;
var configure = local.GetMethod("ConfigureOwnedHost", BindingFlags.Static | BindingFlags.NonPublic)!;
var services = new ServiceCollection();
services.AddHostedService<OtpDeliveryWorker>();
services.AddHostedService<EmployeeLifecycleWorker>();
services.AddHostedService<AttendanceOutboxWorker>();
services.AddHostedService<ZoneRetentionWorker>();
services.AddHostedService<MissingCheckoutReminderWorker>();
services.AddHostedService<FaceRetirementWorker>();
services.AddHostedService<DocumentExpiryReminderWorker>();
services.AddSingleton<WorkerSentinel>();
var original = services.ToArray();
configure.Invoke(null, [services, null]);
Check(original.SequenceEqual(services), "Non-isolated registration is unchanged");
remove.Invoke(null, [services]);
var workers = services.Where(x => x.ServiceType == typeof(IHostedService)).ToArray();
Check(workers.Length == 1 && workers[0].ImplementationType == typeof(OtpDeliveryWorker),
    "Bounded acceptance keeps only OTP; document expiry and face retirement must not write autonomously");
Check(services.Count(x => x.ServiceType == typeof(WorkerSentinel)) == 1, "Unrelated dependency registration is preserved");
var after = services.ToArray();
remove.Invoke(null, [services]);
Check(after.SequenceEqual(services), "Removal is idempotent");
Console.WriteLine($"PASS: {checks} isolated worker registration checks. No host, SQL, keys, providers or worker execution.");
public sealed class WorkerSentinel;
