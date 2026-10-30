using System.Reflection;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Infrastructure.Authentication;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Infrastructure.BackgroundJobs;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

// Owns a fresh LocalDB database. Business repositories, session validation, authorization,
// transactions, audited replay receipts and encrypted files are production implementations.
// Initial employee/access provisioning and preverified biometric proof are explicit fixtures;
// this is not SMS, camera/liveness, HTTP middleware or physical device acceptance.
internal sealed class AttendanceSqlFixture : IAsyncDisposable
{
    public static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-attendance-sql");
    public readonly AttendanceSqlClock Clock = new();
    public readonly string DatabaseName = "Cosmix_Attendance_CI_" + Guid.NewGuid().ToString("N");
    public const string Proof = "synthetic-proof-never-real-biometric";
    public const string CheckInKey = "synthetic-sql-checkin";
    public readonly string DirectoryPath;
    public readonly IPrivateFileStore Files;
    private readonly string masterConnection;
    public readonly string Connection;
    private bool created;
    public AuthenticatedAccount Actor = null!;
    public Guid DeviceId, EnrollmentId, AttemptId, NoticeId;
    public DateOnly Day => DateOnly.FromDateTime(Clock.Now.AddHours(5).DateTime);
    public AttendanceSqlFixture()
    {
        var selected = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COSMIX_ATTENDANCE_SQL_CONNECTION")
            ?? throw new InvalidOperationException("Explicit disposable LocalDB master connection required."));
        if (!selected.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            !selected.IntegratedSecurity || selected.InitialCatalog != "master" || !string.IsNullOrEmpty(selected.AttachDBFilename))
            throw new InvalidOperationException("Attendance SQL verification only accepts integrated LocalDB master.");
        masterConnection = selected.ConnectionString;
        selected.InitialCatalog = DatabaseName;
        Connection = selected.ConnectionString;
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("COSMIX_ATTENDANCE_SQL_WORKDIR")
            ?? throw new InvalidOperationException("Explicit disposable SQL artifact directory required."));
        DirectoryPath = Path.Combine(root, DatabaseName);
        if (Directory.Exists(DirectoryPath)) throw new InvalidOperationException("Refusing an existing fixture directory.");
        Directory.CreateDirectory(DirectoryPath);
        if (OperatingSystem.IsWindows()) RestrictDirectory(DirectoryPath);
        else throw new PlatformNotSupportedException("LocalDB integration requires Windows.");
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "objects"));
        var key = Path.Combine(DirectoryPath, "receipt.key");
        File.WriteAllBytes(key, RandomNumberGenerator.GetBytes(32));
        Files = new EncryptedPrivateFileStore(new() { Directory = Path.Combine(DirectoryPath, "objects"),
            KeyFile = key, KeyReference = "attendance-sql-synthetic-v1" });
    }
    public CosmixDbContext Db() => new(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(Connection,
        sql => sql.CommandTimeout(120)).Options);
    [SupportedOSPlatform("windows")]
    private static void RestrictDirectory(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User!;
        var acl = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access);
        acl.SetAccessRuleProtection(true, false);
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            acl.RemoveAccessRuleSpecific(rule);
        acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(acl);
    }
    public async Task InitializeAsync()
    {
        await using (var master = new SqlConnection(masterConnection))
        {
            await master.OpenAsync();
            var data = Path.Combine(DirectoryPath, "database.mdf").Replace("'", "''");
            var log = Path.Combine(DirectoryPath, "database.ldf").Replace("'", "''");
            await using var create = new SqlCommand($"CREATE DATABASE [{DatabaseName}] ON PRIMARY (NAME=N'{DatabaseName}', FILENAME=N'{data}', SIZE=64MB, FILEGROWTH=64MB) LOG ON (NAME=N'{DatabaseName}_log', FILENAME=N'{log}', SIZE=32MB, FILEGROWTH=32MB)", master);
            await create.ExecuteNonQueryAsync(); created = true;
        }
        await using var db = Db();
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await SeedAsync(db);
    }
    private async Task SeedAsync(CosmixDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var at = Clock.Now.AddDays(-2);
        var administrator = Guid.NewGuid();
        var adminRole = Role.CreateSystem(SystemRoleKind.Admin, administrator, at);
        var role = new Role("SYNTHETIC_SELF", "Synthetic attendance self", administrator, at);
        var employee = new Employee(at, administrator.ToString());
        employee.UpdateDraft(new() { FullLegalName = "SYNTHETIC Attendance SQL", IdentityDocumentType = IdentityDocumentType.Passport,
            IdentityDocumentNumber = "SYNTHETIC-ATTENDANCE-SQL", PersonalPhone = "+923001234567", JoinedOn = Day.AddDays(-5) });
        employee.SelectLoginPhone("+923001234567", true);
        db.AddRange(employee, adminRole, role); await db.SaveChangesAsync();
        var evidence = await Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { Synthetic = true }), "synthetic:hiring", default);
        var submission = employee.SubmitForApproval(administrator, at.AddMinutes(1), evidence.StorageKey, evidence.ObjectVersion,
            evidence.Sha256, evidence.KeyReference, 1);
        db.Add(submission); await db.SaveChangesAsync();
        var decision = employee.DecideSubmission(submission, OnboardingDecision.Approve, Guid.NewGuid(), adminRole.Id,
            "SYNTHETIC upstream hiring fixture", at.AddMinutes(2));
        db.Add(decision); await db.SaveChangesAsync();
        var account = new UserAccount(employee, decision, "attendance.sql@example.invalid", at.AddMinutes(3));
        db.Add(account); await db.SaveChangesAsync();
        var assignment = new UserRoleAssignment(account, role, administrator, at.AddMinutes(4), null);
        var scope = new UserAccessScope(assignment, AccessScopeKind.Self);
        db.AddRange(assignment, scope); await db.SaveChangesAsync();
        foreach (var code in new[] { AccessPermissionCatalog.SelfRead, AttendanceCapturePermissions.Capture,
            AttendanceCapturePermissions.FaceSelf, AttendanceCapturePermissions.SelfRead }.Distinct())
        {
            var definition = AccessPermissionCatalog.Require(code);
            var permission = await db.Set<Permission>().SingleOrDefaultAsync(x => x.Code == code)
                ?? new Permission(code, "Synthetic runtime permission", definition.Sensitive);
            if (db.Entry(permission).State == EntityState.Detached) db.Add(permission);
            db.Add(new RolePermission(role, permission, administrator, at.AddMinutes(4)));
        }
        account.ConfigureInitialAccess(assignment, scope, at.AddMinutes(5));
        account.SetMobileAccess(true, at.AddMinutes(5));
        var phone = new LoginPhoneBinding(account, "+923001234567", submission.Id, at.AddMinutes(5));
        phone.Verify(at.AddMinutes(6));
        account.ActivateMobile(phone, assignment, scope, at.AddMinutes(6));
        var device = new RegisteredDevice(account.Id, "synthetic-sql-device", "android", "Synthetic SQL device", at.AddMinutes(6));
        device.MarkVerified(at.AddMinutes(6));
        var session = new UserSession(account, SessionClient.Mobile, device, null, at.AddMinutes(7), Clock.Now.AddDays(2), Context.CorrelationId);
        db.AddRange(phone, device, session); await db.SaveChangesAsync();
        Actor = new(account.Id, employee.Id, session.Id, account.SecurityVersion, SessionClient.Mobile, false);
        DeviceId = device.Id;

        var department = new Department("SYNTH", "Synthetic department");
        var designation = new Designation("SYNTH", "Synthetic designation");
        var category = new StaffCategory("SITE", "Synthetic site category");
        var employment = new EmploymentType("SYNTH", "Synthetic employment");
        var site = new Site("SYNTH", "Synthetic attendance site");
        var shift = new Shift("SYNTH", "Synthetic attendance shift");
        var version = new ShiftVersion(shift.Id, 1, Day.AddDays(-5), new(8, 0), new(18, 0), 0, 600, 0, 0, ShiftWeekdays.All);
        db.AddRange(department, designation, category, employment, site, shift, version); await db.SaveChangesAsync();
        db.Add(new EmployeeAssignment(employee.Id, department.Id, designation.Id, category.Id, employment.Id,
            null, site.Id, null, Day.AddDays(-5), null, administrator.ToString(), at));
        db.Add(ShiftPopulationAssignment.CreateDirect(shift.Id, ShiftPopulationKind.Site, Day.AddDays(-5), null, siteId: site.Id));
        db.Add(new SiteGeofenceVersion(site.Id, 1, Day.AddDays(-5), 24m, 67m, 100m, 20m));
        var notice = new LocationNoticePolicy(1, "SYNTHETIC location policy for isolated SQL verification", administrator.ToString(), at);
        notice.Publish(administrator.ToString(), at.AddMinutes(1)); NoticeId = notice.Id;
        db.Add(notice);
        var enrollment = new FaceEnrollment(employee.Id, "synthetic-template-not-biometric", "synthetic", "synthetic", "1",
            administrator.ToString(), device.Id.ToString(), "SYNTHETIC upstream biometric fixture", at, Context.CorrelationId);
        EnrollmentId = enrollment.Id;
        var attempt = new FaceVerificationAttempt(enrollment, account, device, CheckInKey, FaceService.Digest(Proof),
            "synthetic", "1", Clock.Now.AddSeconds(-10), Clock.Now.AddMinutes(2), Context.CorrelationId);
        attempt.RecordProviderResult(true, true, true, "SYNTHETIC_FIXTURE_ONLY", Clock.Now.AddSeconds(-5)); AttemptId = attempt.Id;
        db.AddRange(enrollment, attempt);
        var entry = new CaptureValidationPolicyEntry(Guid.NewGuid(), 1, null, at.AddHours(1), at, administrator.ToString(),
            "SYNTHETIC capture policy", new(new(false, null), new(false, null), new(false, null, null, null), new(false, null, null)),
            "SYNTHETIC upstream configuration", "", "");
        entry = entry with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(entry) };
        var reference = await Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new CaptureValidationPolicyProof(1, entry)), CaptureValidationPolicyService.EvidencePurpose, default);
        new CaptureValidationPolicyRepository(db).Add(entry with { ProtectedEvidenceReference = AttendanceOperation.Reference(reference) });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public Services ServicesFor(CosmixDbContext db, bool consentAvailable = true)
    {
        var current = new Current(this);
        var auth = new AuthenticationRepository(db);
        var access = new AccountAccessReader(db);
        var options = new MobileAuthenticationOptions { Enabled = true, OtpLifetimeSeconds = 120, ResendCooldownSeconds = 30,
            MaximumOtpAttempts = 5, RateWindowSeconds = 60, RequestsPerPhone = 5, RequestsPerIp = 10, RequestsGlobal = 20,
            VerificationAttemptsPerPhone = 5, VerificationAttemptsPerIp = 10, VerificationAttemptsGlobal = 20,
            AccessTokenLifetimeSeconds = 300, RefreshTokenLifetimeSeconds = 3600, SessionLifetimeSeconds = 864000,
            MinimumRequestDurationMilliseconds = 100, BusinessTimeZoneId = "Asia/Karachi" };
        var mobile = new MobileAuthenticationService(auth, access, null!, null!, null!, null!, options, Clock);
        var journal = new SchedulingJournalRepository(db, mobile, null!);
        var authorization = new AccessManagementRepository(db, options);
        var operation = new AttendanceOperation(current, access, authorization, journal, auth, Clock, Files);
        var officeRepository = new OfficeRosterRepository(db);
        var taxonomy = new StaffTaxonomyResolver(new StaffTaxonomyRepository(db), Files);
        var siteResolver = new SiteRosterResolver(new SiteRosterRepository(db), new RosterRepository(db, new EmployeeLifecycleRepository(db), Clock), officeRepository, taxonomy);
        var resolver = new EmployeeRosterResolver(officeRepository, new OfficeRosterResolver(officeRepository, Clock, taxonomy), siteResolver, taxonomy);
        var policy = new CaptureValidationPolicyService(operation, new CaptureValidationPolicyRepository(db), Files);
        var evidence = new CaptureValidationEvidenceRepository(db);
        var consent = new LocationConsentRepository(db);
        var service = new AttendanceCaptureService(operation, new AttendanceCaptureRepository(db), resolver, new SiteGeofenceRepository(db),
            new SyntheticProvider(), new() { AuthorityConfirmed = true, ApprovalReference = "synthetic-only", RetentionPolicyReference = "synthetic-only",
                ChallengeLifetimeSeconds = 120, MaximumAttemptsPerPunch = 3 }, Clock, access, new SyntheticTemplates(),
            new NotificationOutbox(new DurableJobRepository(db, Clock), Files, Clock),
            new OfficeAttendanceSourceReader(new OfficeBoundaryRepository(db), officeRepository, taxonomy), new CaptureValidationEvaluator(policy, evidence, Clock),
            policy, evidence, auth, journal, consentAvailable ? consent : null);
        return new(service, new LocationConsentService(consent, Clock, current, authorization, journal, null!), mobile);
    }
    public SubmitPunchRequest CheckInRequest => new(false, null, 24m, 67m, 5m, AttemptId, Proof);
    public async Task<T> Run<T>(Func<Services, Task<T>> action, bool consentAvailable = true)
    { await using var db = Db(); return await action(ServicesFor(db, consentAvailable)); }
    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(masterConnection); await master.OpenAsync();
        // Database name is generated here and never caller-controlled; only this fixture is dropped.
        await using var drop = new SqlCommand($"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]", master);
        await drop.ExecuteNonQueryAsync();
        await using var verify = new SqlCommand("SELECT DB_ID(@name)", master);
        verify.Parameters.AddWithValue("@name", DatabaseName);
        var absent = await verify.ExecuteScalarAsync() is DBNull;
        await File.WriteAllTextAsync(Path.Combine(DirectoryPath, "owned-database-cleanup.json"), JsonSerializer.Serialize(new
            { DatabaseName, OwnedDatabaseDropped = absent }));
        Assert.True(absent, "Owned SQL fixture database was not dropped.");
    }
    public sealed record Services(AttendanceCaptureService Capture, LocationConsentService Consent, MobileAuthenticationService Mobile);
    private sealed class Current(AttendanceSqlFixture fixture) : ICurrentAuthentication { public AuthenticatedAccount Require() => fixture.Actor; }
    private sealed class SyntheticProvider : IFaceVerificationProvider
    {
        public FaceProviderCapability Capability => new(true, "synthetic", "1", "synthetic-preverified-fixture");
        public Task<FaceProviderEnrollmentResult> EnrollAsync(FaceProviderEnrollmentInput input, CancellationToken ct) => throw new InvalidOperationException("No biometric provider in SQL fixture.");
        public Task<FaceProviderVerificationResult> VerifyAsync(FaceProviderVerificationInput input, CancellationToken ct) => throw new InvalidOperationException("No biometric provider in SQL fixture.");
    }
    private sealed class SyntheticTemplates : IFaceTemplateStore
    {
        public bool IsConfigured => true;
        public Task<PrivateFileReference> WriteAsync(byte[] template, string purpose, CancellationToken ct) => throw new InvalidOperationException("No biometric templates in SQL fixture.");
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => throw new InvalidOperationException("No biometric templates in SQL fixture.");
    }
}
internal sealed class AttendanceSqlClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
