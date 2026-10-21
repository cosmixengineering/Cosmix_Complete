#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.FaceVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Application.Authentication;
using Cosmix.Application.Attendance.Punches;

// SQL translation only. No connection, SQL execution, storage IO, host or provider is possible.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
var options = new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=offline.invalid;Database=NeverConnectedB05;Integrated Security=true;Encrypt=true").Options;
using var db = new CosmixDbContext(options);
var repository = new FaceRetirementRepository(db, null!, null!);
repository.RequireOutsideTransaction(); checks++;
try { repository.RequireTransaction(); throw new Exception("Missing transaction accepted"); }
catch (InvalidOperationException) { checks++; }
var cursor = new FaceRetirementCursor(new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());
IQueryable Query(string method, FaceRetirementCursor? after) => (IQueryable)typeof(FaceRetirementRepository)
    .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, [after, 17])!;
var intents = Query("IntentQuery", cursor).ToQueryString();
Check(intents.Contains("TOP(", StringComparison.Ordinal) && intents.Contains("ORDER BY", StringComparison.Ordinal) &&
    intents.Contains("OccurredAt", StringComparison.Ordinal) && intents.Contains(FaceRetirementService.IntentEvent, StringComparison.Ordinal),
    "actual intent query translates bounded stable keyset and owned event filter");
var revoked = Query("RevocationQuery", cursor).ToQueryString();
Check(revoked.Contains("NOT EXISTS", StringComparison.Ordinal) && revoked.Contains("RevokedAtUtc", StringComparison.Ordinal) &&
    revoked.Contains("IsActive", StringComparison.Ordinal) && revoked.Contains("TOP(", StringComparison.Ordinal) &&
    revoked.Contains(FaceRetirementService.EnrollmentTarget, StringComparison.Ordinal),
    "actual revoked-row recovery query translates immutable terminal filter and absent exact intent");
Check(Query("IntentQuery", null).ToQueryString().Contains("ORDER BY", StringComparison.Ordinal) &&
    Query("RevocationQuery", null).ToQueryString().Contains("NOT EXISTS", StringComparison.Ordinal),
    "restart queries translate without a retained cursor");
var audit = new AuditEvent(FaceRetirementService.IntentEvent, "synthetic", "ExistingAuditRole", FaceRetirementService.EnrollmentTarget,
    Guid.NewGuid().ToString(), cursor.OccurredAtUtc, "synthetic");
db.AuditEvents.Add(audit);
var guard = typeof(FaceRetirementRepository).GetMethod("ValidateAuditOnly", BindingFlags.NonPublic | BindingFlags.Instance)!;
guard.Invoke(repository, []); checks++;
try { repository.RequireOutsideTransaction(); throw new Exception("Dirty context allowed"); }
catch (InvalidOperationException) { checks++; }
db.ChangeTracker.Clear();
var unrelated = new AuditEvent("unrelated.operation", "synthetic", "ExistingAuditRole", "Synthetic", "synthetic", cursor.OccurredAtUtc, "synthetic");
db.AuditEvents.Add(unrelated);
try { guard.Invoke(repository, []); throw new Exception("Unrelated audit allowed"); }
catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { checks++; }
db.ChangeTracker.Clear();
db.Attach(audit); db.Entry(audit).State = EntityState.Deleted;
try { guard.Invoke(repository, []); throw new Exception("Audit delete allowed"); }
catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { checks++; }
db.ChangeTracker.Clear();
var enrollment = new FaceEnrollment(Guid.NewGuid(), "synthetic-reference", "synthetic-key", "synthetic", "1",
    "synthetic", "synthetic", "Synthetic", cursor.OccurredAtUtc, "synthetic");
db.FaceEnrollments.Add(enrollment);
try { guard.Invoke(repository, []); throw new Exception("Business mutation allowed"); }
catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { checks++; }
Check(db.Database.GetDbConnection().State == System.Data.ConnectionState.Closed, "no database connection was opened");
foreach (var environment in new[] { "Development", "Production" })
{
    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["SelfHostedFace:Enabled"] = "true" }).Build());
    services.AddSingleton<IHostEnvironment>(new RetirementEnvironment(environment));
    services.AddSingleton(new PrivateFileStorageOptions());
    services.AddSingleton<TimeProvider>(TimeProvider.System);
    services.AddAttendanceCaptureInfrastructure();
    services.AddFaceRetirementInfrastructure();
    services.AddAttendanceCaptureApplication(); // Parent's actual module registration; no host.
    using var composition = services.BuildServiceProvider();
    var selected = composition.GetRequiredService<IFaceTemplateStore>();
    var retirement = composition.GetRequiredService<IFaceEvidenceRetirement>();
    Check(services.Count(x => x.ServiceType == typeof(IFaceRetirementRepository)) == 1 &&
        services.Count(x => x.ServiceType == typeof(IFaceEvidenceRetirement)) == 1,
        "idempotent retirement extension adds one repository and one selected-store retirement binding");
    Check(environment == "Development" ? selected is RetentionControlledFaceTemplateStore && ReferenceEquals(selected, retirement)
        : retirement is UnavailableFaceEvidenceRetirement && !retirement.IsConfigured,
        "selected Development gate is reused exactly; Production cannot retire through an undecorated vault");
    Check(!composition.GetRequiredService<FaceDisposalOptions>().Enabled &&
        !composition.GetRequiredService<FaceRetirementWorkerOptions>().Enabled &&
        composition.GetRequiredService<IFaceDisposalPolicy>() is PendingFaceDisposalPolicy &&
        !composition.GetRequiredService<IFacePhysicalDisposal>().IsConfigured,
        "real module registration supplies disabled unapproved disposal and worker defaults");
    Check(!retirement.IsConfigured && !selected.IsConfigured, "missing approved storage remains unavailable through both interfaces");
    using var scope = composition.CreateScope();
    Check(scope.ServiceProvider.GetRequiredService<FaceDisposalRunner>() is not null,
        "parent Application disposal-runner hook resolves through actual Infrastructure defaults");
}
Console.WriteLine($"B05 adapter/composition checks passed: {checks}. Actual SQL translation/guards/registration; no SQL execution or host.");

public sealed class RetirementEnvironment(string environment) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environment;
    public string ApplicationName { get; set; } = "SyntheticB05";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
