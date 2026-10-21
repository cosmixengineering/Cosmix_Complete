#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=PrepareAttendanceFixtures.packages.lock.json
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Explicit, one-use synthetic acceptance preparation. This does not migrate, reset, grant access,
// create accounts, change credentials, or write any schedule/geofence through the database.
// Compile safely with --help. Execution requires the separately approved private settings/backup.
if (args.SequenceEqual(new[] { "--help" }))
{
    Console.WriteLine("PrepareAttendanceFixtures --settings <private settings.json> --manifest <new private manifest.json> --backup <verified private .bak>");
    Console.WriteLine("Writes only synthetic organization/sites and one HR assignment to exact local Cosmix_Testing after strict guards. Never rerun/reset existing fixtures.");
    return;
}

const string privateRoot = @"C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009";
const string database = "Cosmix_Testing";
const string actor = "local-testing:synthetic-fixture-not-real-owner";
var parameters = ParseArguments(args);
var approvedRoot = PrivateBootstrapSnapshotStore.RequirePrivatePath(privateRoot, true);
var settingsPath = RequireUnderRoot(parameters["--settings"], true);
var backupPath = RequireUnderRoot(parameters["--backup"], true);
var manifestPath = RequireUnderRoot(parameters["--manifest"], false);
var pendingPath = RequireUnderRoot(manifestPath + ".pending", false);
Require(!File.Exists(manifestPath) && !File.Exists(pendingPath),
    "Manifest or pending evidence already exists. Inspect and recover it; never overwrite or rerun.");
Require(!settingsPath.Equals(backupPath, StringComparison.OrdinalIgnoreCase) &&
    Path.GetExtension(backupPath).Equals(".bak", StringComparison.OrdinalIgnoreCase), "Require a separate private .bak file.");

using var settings = JsonDocument.Parse(await File.ReadAllBytesAsync(settingsPath));
var config = settings.RootElement;
Require(config.GetProperty("LocalTesting").GetProperty("Enabled").GetBoolean(), "LocalTesting must be explicitly enabled.");
Require(Path.GetFullPath(config.GetProperty("LocalTesting").GetProperty("PrivateDirectory").GetString()!)
    .TrimEnd(Path.DirectorySeparatorChar).Equals(approvedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase),
    "Settings must use the approved private acceptance directory.");
var sms = config.GetProperty("MobileAuthentication").GetProperty("Sms");
Require(sms.GetProperty("Provider").GetString() == "LocalTesting" &&
    string.IsNullOrEmpty(sms.GetProperty("Endpoint").GetString()) && string.IsNullOrEmpty(sms.GetProperty("BearerToken").GetString()),
    "Only the synthetic local SMS adapter is permitted.");
var connection = new SqlConnectionStringBuilder(config.GetProperty("ConnectionStrings").GetProperty("CosmixDatabase").GetString());
Require(connection.DataSource.Equals(@"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) &&
    connection.InitialCatalog == database && connection.IntegratedSecurity &&
    string.IsNullOrEmpty(connection.AttachDBFilename) && string.IsNullOrEmpty(connection.UserID) && string.IsNullOrEmpty(connection.Password),
    "Target must be exact Cosmix_Testing on (localdb)\\MSSQLLocalDB using integrated security.");

// Hold the backup read-only throughout preparation so its verified content cannot be replaced.
await using var backupFile = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
Require(backupFile.Length > 0, "The required database backup is empty.");
var backupHash = Convert.ToHexString(await SHA256.HashDataAsync(backupFile));
await VerifyBackup(connection.ConnectionString, backupPath);

await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(connection.ConnectionString).Options);
await db.Database.OpenConnectionAsync();
Require(db.Database.GetDbConnection().Database == database, "Connected to an unexpected database.");
Require(!db.Database.HasPendingModelChanges(), "Model changes require a reviewed migration before fixtures.");
var expectedMigrations = db.Database.GetMigrations().Order(StringComparer.Ordinal).ToArray();
var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).Order(StringComparer.Ordinal).ToArray();
Require(expectedMigrations.Length == 13 && expectedMigrations.SequenceEqual(appliedMigrations, StringComparer.Ordinal) &&
    expectedMigrations.Contains("20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments"),
    "Require exactly all 13 reviewed migrations, with no missing or unknown applied migration.");

await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
var known = new[]
{
    new { Email = "superadmin@example.invalid", Phone = "+12025550101", Name = "TEST ONLY SuperAdmin", Passport = "TEST-PASSPORT-1" },
    new { Email = "admin@example.invalid", Phone = "+12025550102", Name = "TEST ONLY Admin", Passport = "TEST-PASSPORT-2" },
    new { Email = "hr@example.invalid", Phone = "+12025550103", Name = "TEST ONLY HR", Passport = "TEST-PASSPORT-3" }
};
// Project only identity/guard fields: passwords, tokens and private documents are never loaded.
var accounts = await db.UserAccounts.AsNoTracking().Select(a => new
{
    a.Id, a.EmployeeId, a.LoginName, a.Status, a.ApprovalDecisionId,
    EmployeeStatus = a.Employee.Status, a.Employee.OnboardingStatus,
    a.Employee.ApprovedOnboardingDecisionId, a.Employee.FullLegalName, a.Employee.ApprovedLoginPhoneNumber,
    a.Employee.IdentityDocumentType, a.Employee.IdentityDocumentNumber, a.Employee.CreatedByUserId
}).ToListAsync();
Require(accounts.Count == 3 && await db.Employees.CountAsync() == 3 && accounts.Select(a => a.EmployeeId).Distinct().Count() == 3,
    "Require exactly three existing synthetic accounts and employees.");
foreach (var expected in known)
{
    var matches = accounts.Where(a => a.LoginName == expected.Email).ToArray();
    Require(matches.Length == 1, "A required synthetic account is missing or duplicated.");
    var account = matches.Single();
    Require(account.FullLegalName == expected.Name && account.ApprovedLoginPhoneNumber == expected.Phone &&
        account.IdentityDocumentType == IdentityDocumentType.Passport && account.IdentityDocumentNumber == expected.Passport &&
        account.CreatedByUserId == actor && account.Status == AccountStatus.Active && account.EmployeeStatus == EmployeeStatus.Active &&
        account.OnboardingStatus.ToString() == "Approved" && account.ApprovedOnboardingDecisionId == account.ApprovalDecisionId,
        "Synthetic identity/provenance/approval guard failed. Never use real employee data.");
}
Require(!await db.Departments.AnyAsync() && !await db.Designations.AnyAsync() && !await db.EmploymentTypes.AnyAsync() &&
    !await db.StaffCategories.AnyAsync() && !await db.Sites.AnyAsync() && !await db.EmployeeAssignments.AnyAsync(),
    "Organization/site masters or HR assignment history already exists. Preserve it; no reset or overwrite is permitted.");

var now = DateTimeOffset.UtcNow;
var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi")).DateTime);
Require(today == new DateOnly(2026, 10, 9), "This dated acceptance preparation must be reviewed again on a different business day.");
var runId = "ACC" + today.ToString("yyyyMMdd") + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
var department = new Department(runId + "-DEP", "TEST ONLY Department " + runId);
var designation = new Designation(runId + "-DES", "TEST ONLY Designation " + runId);
var employmentType = new EmploymentType(runId + "-EMP", "TEST ONLY Employment " + runId);
// SITE is the production scheduling contract; the empty-master guard prevents commandeering it.
var category = new StaffCategory("SITE", "TEST ONLY Site staff " + runId);
var site = new Site(runId + "-ALLOW", "TEST ONLY Allowed Site " + runId, startsOn: today);
var deniedSite = new Site(runId + "-DENY", "TEST ONLY Denied Site " + runId, startsOn: today);
var hr = accounts.Single(a => a.LoginName == "hr@example.invalid");
var employee = await db.Employees.SingleAsync(e => e.Id == hr.EmployeeId);
var oldRevision = employee.DraftRevision;
var membership = new EmployeeAssignment(employee.Id, department.Id, designation.Id, category.Id,
    employmentType.Id, null, site.Id, null, today, null, actor, now);
var audit = new AuditEvent("development.attendance.fixture.seeded", actor, "local-testing", "AcceptanceRun",
    runId, now, runId, employeeScopeId: employee.Id);
db.AddRange(department, designation, employmentType, category, site, deniedSite, membership, audit);

var manifest = new
{
    RunId = runId, Database = database, Committed = false, PreparedAtUtc = now,
    AdminAccountId = accounts.Single(a => a.LoginName == "admin@example.invalid").Id,
    SuperAdminAccountId = accounts.Single(a => a.LoginName == "superadmin@example.invalid").Id,
    HrAccountId = hr.Id, EmployeeAccountId = hr.Id, EmployeeId = employee.Id, EmployeeIds = new[] { employee.Id },
    SiteId = site.Id, DeniedSiteId = deniedSite.Id, DepartmentId = department.Id, DesignationId = designation.Id,
    StaffCategoryId = category.Id, EmploymentTypeId = employmentType.Id, EmployeeAssignmentId = membership.Id,
    MembershipEffectiveFrom = today, EffectiveFrom = today.AddDays(2), BusinessDate = today.AddDays(5),
    BackupFile = Path.GetFileName(backupPath), BackupSha256 = backupHash, AppliedMigrations = appliedMigrations
};
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
// Reserve before committing. A .pending artifact always requires manual inspection, never automatic retry.
await WriteManifest(pendingPath, manifest, FileMode.CreateNew);
await db.SaveChangesAsync();
Require(employee.DraftRevision == oldRevision + 1, "The real DbContext employee-related-history guard did not advance draft revision.");
Require(db.ChangeTracker.Entries<UserAccount>().Count() == 0, "Account entities must never be tracked or changed by this script.");
await transaction.CommitAsync();
await WriteManifest(pendingPath, manifest with { Committed = true }, FileMode.Truncate);
// Both absolute paths were checked inside the approved directory. Destination must remain absent.
File.Move(pendingPath, manifestPath, overwrite: false);
Console.WriteLine("Prepared synthetic attendance fixtures: six masters, one current HR membership, one audit event; private manifest committed.");
Console.WriteLine("No accounts, credentials, permissions, schedules or geofences were created or changed.");

string RequireUnderRoot(string path, bool exists)
{
    Require(Path.IsPathFullyQualified(path), "All input/output paths must be absolute private paths.");
    var full = Path.GetFullPath(path);
    Require(full.StartsWith(approvedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
        "All input/output files must stay inside the approved private acceptance directory.");
    PrivateBootstrapSnapshotStore.RequirePrivatePath(Path.GetDirectoryName(full)!, true);
    if (exists) PrivateBootstrapSnapshotStore.RequirePrivatePath(full, false);
    else Require(!Directory.Exists(full), "Output must be a new file, not a directory.");
    return full;
}

async Task WriteManifest(string path, object value, FileMode mode)
{
    await using (var output = new FileStream(path, mode, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
    {
        await JsonSerializer.SerializeAsync(output, value, jsonOptions);
        await output.FlushAsync();
        output.Flush(flushToDisk: true);
    }
    PrivateBootstrapSnapshotStore.RequirePrivatePath(path, false);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static Dictionary<string, string> ParseArguments(string[] input)
{
    Require(input.Length == 6, "Supply --settings, --manifest and --backup exactly once; --help is read-only.");
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < input.Length; index += 2)
        Require(new[] { "--settings", "--manifest", "--backup" }.Contains(input[index]) &&
            !string.IsNullOrWhiteSpace(input[index + 1]) && result.TryAdd(input[index], input[index + 1]), "Unknown, empty or duplicate argument.");
    return result;
}

static async Task VerifyBackup(string connectionString, string backup)
{
    await using var sql = new SqlConnection(connectionString);
    await sql.OpenAsync();
    Require(sql.Database == database, "Backup validation connected to an unexpected database.");
    await using (var header = new SqlCommand("RESTORE HEADERONLY FROM DISK = @backup", sql) { CommandTimeout = 120 })
    {
        header.Parameters.Add("@backup", SqlDbType.NVarChar, 4000).Value = backup;
        await using var reader = await header.ExecuteReaderAsync();
        Require(await reader.ReadAsync() && reader["DatabaseName"] is string name && name == database &&
            Convert.ToInt32(reader["BackupType"]) == 1 && reader["HasBackupChecksums"] is true &&
            reader["IsDamaged"] is false, "Require one undamaged, checksummed full backup of Cosmix_Testing.");
        Require(!await reader.ReadAsync(), "The dedicated backup must contain exactly one backup set.");
    }
    await using var verify = new SqlCommand("RESTORE VERIFYONLY FROM DISK = @backup WITH CHECKSUM", sql) { CommandTimeout = 120 };
    verify.Parameters.Add("@backup", SqlDbType.NVarChar, 4000).Value = backup;
    await verify.ExecuteNonQueryAsync();
}
