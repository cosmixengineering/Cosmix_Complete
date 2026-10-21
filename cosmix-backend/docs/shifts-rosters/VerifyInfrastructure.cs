#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyInfrastructure.packages.lock.json

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Standalone checks against the real SQL Server model, context guard and DI registrations.
// Run from the repository root using cached packages and a local-only restore source:
// dotnet run --file docs/shifts-rosters/VerifyInfrastructure.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false
// A connection interceptor rejects every database open; no migration or schema is applied.
// PrivateFilePath correctly rejects storage anywhere below a repository. This harness is confined
// to the checkout, so a rejecting IPrivateFileStore substitutes ONLY for DI construction. No file
// operation, encrypted replay, SQL query/transaction, live authentication or HTTP is verified here.
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:CosmixDatabase"] =
        "Server=cosmix-infrastructure-only.invalid;Database=InfrastructureVerificationOnly;Integrated Security=true;Encrypt=true;Connect Timeout=1",
    ["MobileAuthentication:Enabled"] = "false",
    ["WebAuthentication:Enabled"] = "false"
}).Build();
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAILED: {name}");
    checks++;
    Console.WriteLine($"PASS: {name}");
}

var connections = new RejectConnections();
var files = new RejectPrivateFiles();
var services = new ServiceCollection();
services.AddApplication();
services.AddInfrastructure(configuration);
services.AddDbContext<CosmixDbContext>(options => options.AddInterceptors(connections));

foreach (var (contract, implementation) in new (Type Contract, Type Implementation)[]
{
    (typeof(ShiftService), typeof(ShiftService)),
    (typeof(IndividualRosterService), typeof(IndividualRosterService)),
    (typeof(IIndividualRosterResolver), typeof(SiteRosterResolver)),
    (typeof(SiteRosterService), typeof(SiteRosterService)),
    (typeof(SchedulingOperation), typeof(SchedulingOperation)),
    (typeof(IShiftRepository), typeof(ShiftRepository)),
    (typeof(IRosterRepository), typeof(RosterRepository)),
    (typeof(ISiteRosterRepository), typeof(SiteRosterRepository)),
    (typeof(ISchedulingJournalRepository), typeof(SchedulingJournalRepository)),
    (typeof(SiteGeofenceService), typeof(SiteGeofenceService)),
    (typeof(ISiteGeofenceRepository), typeof(SiteGeofenceRepository)),
    (typeof(ISiteAttendanceIdentity), typeof(SiteAttendanceIdentity))
})
{
    var registrations = services.Where(x => x.ServiceType == contract).ToArray();
    Check(registrations.Length == 1 && registrations[0].ImplementationType == implementation &&
        registrations[0].Lifetime == ServiceLifetime.Scoped,
        $"{contract.Name} retains exactly one real scoped registration");
}
Check(services.Count(x => x.ServiceType == typeof(IPrivateFileStore) &&
    x.ImplementationType == typeof(EncryptedPrivateFileStore) && x.Lifetime == ServiceLifetime.Singleton) == 1,
    "production encrypted private storage remains registered before the harness override");
foreach (var permission in SchedulingPermissions.Definitions)
    Check(AccessPermissionCatalog.All.Count(x => x.Code == permission.Code) == 1,
        $"{permission.Code} appears once in the actual shared permission catalog");

// API composition-root contracts and the explicitly bounded storage substitute.
services.AddSingleton<ICurrentAuthentication, RejectAuthentication>();
services.AddSingleton<IAccessTokenIssuer, RejectTokens>();
services.AddSingleton<IPrivateFileStore>(files);
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CosmixDbContext>();
var model = db.GetService<IDesignTimeModel>().Model;
Check(!db.Database.HasPendingModelChanges(), "current model matches the locally generated migration snapshot; database application is not implied");
var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model
    ?? throw new Exception("Existing model snapshot is required for the exact gap check.");
var initializedSnapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
var pendingDifferences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
    initializedSnapshot.GetRelationalModel(), model.GetRelationalModel());
Check(pendingDifferences.Count == 0, "no unrepresented model changes remain after local migration generation");
var migrations = db.GetService<IMigrationsAssembly>();
var preparedMigration = migrations.CreateMigration(migrations.Migrations.Single(x =>
    x.Key == "20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments").Value, db.Database.ProviderName!);
var differences = preparedMigration.UpOperations;
string Describe(MigrationOperation operation) => operation switch
{
    CreateTableOperation x => $"table:{x.Schema}.{x.Name}",
    AlterColumnOperation x => $"alter:{x.Schema}.{x.Table}.{x.Name}",
    AddColumnOperation x => $"add:{x.Schema}.{x.Table}.{x.Name}",
    CreateIndexOperation x => $"index:{x.Schema}.{x.Table}.{x.Name}",
    AddForeignKeyOperation x => $"fk:{x.Schema}.{x.Table}.{x.Name}",
    _ => operation.GetType().Name
};
var expectedDifferences = new[]
{
    "alter:attendance.ShiftPopulationAssignments.ApprovalRequestId",
    "alter:attendance.ShiftPopulationAssignmentEnds.ApprovalRequestId",
    "alter:attendance.TemporaryShiftChanges.ApprovalRequestId",
    "add:attendance.ShiftPopulationAssignments.HolidayCalendarId",
    "index:attendance.ShiftPopulationAssignments.IX_ShiftPopulationAssignments_HolidayCalendarId",
    "fk:attendance.ShiftPopulationAssignments.FK_ShiftPopulationAssignments_HolidayCalendars_HolidayCalendarId",
    "table:attendance.TemporaryShiftChangeAmendments",
    "index:attendance.TemporaryShiftChangeAmendments.IX_TemporaryShiftChangeAmendments_OriginalChangeId",
    "index:attendance.TemporaryShiftChangeAmendments.IX_TemporaryShiftChangeAmendments_ReplacementChangeId"
}.Order().ToArray();
Check(differences.Select(Describe).Order().SequenceEqual(expectedDifferences),
    "model differs by exactly the intended columns, holiday index and holiday FK; actual=" +
    string.Join(",", differences.Select(Describe)));
foreach (var column in differences.OfType<AlterColumnOperation>())
    Check(column.ColumnType == "uniqueidentifier" && column.ClrType == typeof(Guid) && column.IsNullable &&
        column.OldColumn.ColumnType == "uniqueidentifier" && column.OldColumn.ClrType == typeof(Guid) && !column.OldColumn.IsNullable &&
        column.DefaultValue is null && column.DefaultValueSql is null && column.ComputedColumnSql is null,
        $"{column.Table}.ApprovalRequestId changes only to nullable Guid without fabricated default");
var holidayColumn = differences.OfType<AddColumnOperation>().Single();
Check(holidayColumn.ColumnType == "uniqueidentifier" && holidayColumn.ClrType == typeof(Guid) && holidayColumn.IsNullable &&
    holidayColumn.DefaultValue is null && holidayColumn.DefaultValueSql is null && holidayColumn.ComputedColumnSql is null,
    "new exact holiday revision reference is nullable with no automatic calendar default");
var holidayIndex = differences.OfType<CreateIndexOperation>().Single(x => x.Table == "ShiftPopulationAssignments");
Check(!holidayIndex.IsUnique && holidayIndex.Columns.SequenceEqual(["HolidayCalendarId"]) && holidayIndex.Filter is null,
    "new holiday reference index covers only its exact nullable foreign key");
var holidayForeignKey = differences.OfType<AddForeignKeyOperation>().Single();
Check(holidayForeignKey.PrincipalSchema == "hr" && holidayForeignKey.PrincipalTable == "HolidayCalendars" &&
    holidayForeignKey.Columns.SequenceEqual(["HolidayCalendarId"]) && holidayForeignKey.PrincipalColumns.SequenceEqual(["Id"]) &&
    holidayForeignKey.OnDelete == ReferentialAction.Restrict && holidayForeignKey.OnUpdate == ReferentialAction.NoAction,
    "new holiday foreign key pins an exact existing immutable revision without cascade deletion");

// Compile the repository's worst-case bounded identifier query without executing SQL.
// Guard against a provider upgrade expanding both collections beyond SQL Server's parameter ceiling.
var siteShiftIds = Enumerable.Range(0, 2048).Select(_ => Guid.NewGuid()).ToArray();
var siteVersionIds = Enumerable.Range(0, 4096).Select(_ => Guid.NewGuid()).ToArray();
var siteVersionSql = db.ShiftVersions.AsNoTracking()
    .Where(x => siteShiftIds.Contains(x.ShiftId) || siteVersionIds.Contains(x.Id))
    .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.VersionNumber).ThenBy(x => x.Id)
    .Take(2049).ToQueryString();
Check(siteVersionSql.Contains("ShiftVersions", StringComparison.Ordinal) &&
    siteVersionSql.Contains("ShiftId", StringComparison.Ordinal) && siteVersionSql.Contains("TOP", StringComparison.Ordinal),
    "bounded site version query translates without any SQL connection");
var parameterDeclarations = System.Text.RegularExpressions.Regex.Matches(siteVersionSql, @"(?m)^DECLARE\s+@").Count;
Check(parameterDeclarations <= 2100,
    $"maximum site version lookup stays within SQL Server parameter ceiling ({parameterDeclarations} declarations)");
var shift = model.FindEntityType(typeof(Shift)) ?? throw new Exception("Shift is absent from the real EF model.");
var version = model.FindEntityType(typeof(ShiftVersion)) ?? throw new Exception("ShiftVersion is absent from the real EF model.");
var assignment = model.FindEntityType(typeof(EmployeeShiftAssignment))
    ?? throw new Exception("EmployeeShiftAssignment is absent from the real EF model.");

foreach (var (entity, table) in new[] { (shift, "Shifts"), (version, "ShiftVersions"), (assignment, "EmployeeShiftAssignments") })
    Check(entity.GetSchema() == "attendance" && entity.GetTableName() == table,
        $"{entity.ClrType.Name} uses its existing attendance table");
Check(typeof(IAppendOnlyEntity).IsAssignableFrom(version.ClrType), "shift schedule versions retain append-only semantics");
Check(shift.GetIndexes().Any(index => index.IsUnique && index.Properties.Select(x => x.Name)
    .SequenceEqual(new[] { nameof(Shift.Code) })), "shift codes retain their unique database index");
foreach (var entity in new[] { shift, assignment })
{
    var rowVersion = entity.FindProperty("RowVersion");
    Check(rowVersion is not null && rowVersion.IsConcurrencyToken &&
        rowVersion.ValueGenerated == ValueGenerated.OnAddOrUpdate && rowVersion.GetColumnType() == "rowversion",
        $"{entity.ClrType.Name} retains a SQL rowversion concurrency token");
}
foreach (var secondColumn in new[] { nameof(ShiftVersion.VersionNumber), nameof(ShiftVersion.EffectiveFrom) })
    Check(version.GetIndexes().Any(index => index.IsUnique && index.Properties.Select(x => x.Name)
        .SequenceEqual(new[] { nameof(ShiftVersion.ShiftId), secondColumn })),
        $"shift and {secondColumn} retain a unique version index");
Check(version.FindProperty(nameof(ShiftVersion.EffectiveFrom))?.GetColumnType() == "date" &&
    assignment.FindProperty(nameof(EmployeeShiftAssignment.EffectiveFrom))?.GetColumnType() == "date" &&
    assignment.FindProperty(nameof(EmployeeShiftAssignment.EffectiveTo))?.GetColumnType() == "date",
    "effective schedule and assignment dates retain SQL date columns");
Check(version.FindProperty(nameof(ShiftVersion.StartTime))?.GetColumnType() == "time(7)" &&
    version.FindProperty(nameof(ShiftVersion.EndTime))?.GetColumnType() == "time(7)",
    "duty-window times retain SQL time precision");
Check(version.GetForeignKeys().Any(key => key.PrincipalEntityType.ClrType == typeof(Shift) &&
    key.DeleteBehavior == DeleteBehavior.Restrict && key.Properties.Select(x => x.Name)
        .SequenceEqual(new[] { nameof(ShiftVersion.ShiftId) })),
    "shift deletion cannot cascade through immutable version history");
foreach (var target in new[] { typeof(Employee), typeof(Shift), typeof(Cosmix.Domain.Attendance.Sites.Site) })
    Check(assignment.GetForeignKeys().Any(key => key.PrincipalEntityType.ClrType == target &&
        key.DeleteBehavior == DeleteBehavior.Restrict),
        $"{target.Name} deletion cannot cascade through individual roster history");
Check(version.GetCheckConstraints().Select(x => x.Name).ToHashSet().IsSupersetOf(new[]
{
    "CK_ShiftVersions_VersionNumber", "CK_ShiftVersions_EndDayOffset", "CK_ShiftVersions_Duration",
    "CK_ShiftVersions_Minutes", "CK_ShiftVersions_ApplicableWeekdays"
}), "existing database version, overnight, duration, minutes and weekday constraints are present");
Check(assignment.GetCheckConstraints().Any(x => x.Name == "CK_EmployeeShiftAssignments_EffectiveDates"),
    "existing database assignment interval constraint is present");

// All four SaveChanges entry points must detect mutations even when automatic detection is off.
foreach (var delete in new[] { false, true })
foreach (var asynchronous in new[] { false, true })
foreach (var explicitAccept in new[] { false, true })
{
    db.ChangeTracker.Clear();
    db.ChangeTracker.AutoDetectChangesEnabled = false;
    var schedule = new ShiftVersion(Guid.NewGuid(), 1, new DateOnly(2030, 1, 1),
        new TimeOnly(22, 0), new TimeOnly(6, 0), 1, 450, 15, 30, ShiftWeekdays.All);
    var entry = db.Attach(schedule);
    if (delete) db.Remove(schedule);
    else entry.Property(x => x.ScheduledMinutes).CurrentValue = 451;
    var rejected = false;
    try
    {
        if (asynchronous)
        {
            if (explicitAccept) await db.SaveChangesAsync(acceptAllChangesOnSuccess: false);
            else await db.SaveChangesAsync();
        }
        else if (explicitAccept) db.SaveChanges(acceptAllChangesOnSuccess: false);
        else db.SaveChanges();
    }
    catch (InvalidOperationException exception) when
        (exception.Message.Contains(nameof(ShiftVersion), StringComparison.Ordinal) &&
         exception.Message.Contains("append-only", StringComparison.Ordinal))
    {
        rejected = true;
    }
    Check(rejected, $"{(asynchronous ? "async" : "sync")} save{(explicitAccept ? "(false)" : "()")} rejects " +
        $"shift-version {(delete ? "deletion" : "mutation")} before database access");
}
db.ChangeTracker.Clear();
db.ChangeTracker.AutoDetectChangesEnabled = true;

Check(scope.ServiceProvider.GetRequiredService<IShiftRepository>() is ShiftRepository,
    "real ShiftRepository dependency graph resolves");
Check(scope.ServiceProvider.GetRequiredService<IRosterRepository>() is RosterRepository,
    "real RosterRepository dependency graph resolves");
Check(scope.ServiceProvider.GetRequiredService<ISchedulingJournalRepository>() is SchedulingJournalRepository,
    "real SchedulingJournalRepository dependency graph resolves");
Check(scope.ServiceProvider.GetRequiredService<ShiftService>() is not null,
    "real ShiftService dependency graph resolves with explicitly rejecting storage");
Check(scope.ServiceProvider.GetRequiredService<IndividualRosterService>() is not null,
    "real IndividualRosterService dependency graph resolves with explicitly rejecting storage");
Check(scope.ServiceProvider.GetRequiredService<IIndividualRosterResolver>() is SiteRosterResolver,
    "runtime roster contract resolves through the confirmed site-only resolver");
Check(scope.ServiceProvider.GetRequiredService<ISiteRosterRepository>() is SiteRosterRepository,
    "real SiteRosterRepository dependency graph resolves without metadata or SQL execution");
Check(scope.ServiceProvider.GetRequiredService<SiteRosterService>() is not null,
    "real SiteRosterService dependency graph resolves with explicitly rejecting storage");
Check(scope.ServiceProvider.GetRequiredService<SchedulingOperation>() is not null,
    "real SchedulingOperation dependency graph resolves with explicitly rejecting storage");
Check(scope.ServiceProvider.GetRequiredService<ISiteGeofenceRepository>() is SiteGeofenceRepository &&
    scope.ServiceProvider.GetRequiredService<ISiteAttendanceIdentity>() is SiteAttendanceIdentity &&
    scope.ServiceProvider.GetRequiredService<SiteGeofenceService>() is not null,
    "existing real geofence repository, identity and service dependency graphs remain registered");
Check(ReferenceEquals(scope.ServiceProvider.GetRequiredService<IPrivateFileStore>(), files) && files.Attempts == 0,
    "explicit harness storage override performed no file reads or writes");
Check(connections.Attempts == 0 && db.Database.GetDbConnection().State == ConnectionState.Closed,
    "no database connection was attempted or opened");
Console.WriteLine($"Verified {checks} model, append-only guard and dependency-registration checks without database access. " +
    "No SQL behavior, encrypted storage/replay, live authentication or HTTP acceptance is claimed.");

sealed class RejectConnections : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result)
    {
        Attempts++;
        throw new InvalidOperationException("Database connection attempts are prohibited in this infrastructure harness.");
    }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Attempts++;
        throw new InvalidOperationException("Database connection attempts are prohibited in this infrastructure harness.");
    }
}

sealed class RejectAuthentication : ICurrentAuthentication
{
    public AuthenticatedAccount Require() => throw new InvalidOperationException("This harness does not authenticate accounts.");
}

sealed class RejectTokens : IAccessTokenIssuer
{
    public bool IsConfigured => false;
    public string Issue(UserAccount account, UserSession session, DateTimeOffset now, DateTimeOffset expiresAt) =>
        throw new InvalidOperationException("This harness does not issue tokens.");
}

sealed class RejectPrivateFiles : IPrivateFileStore
{
    public int Attempts { get; private set; }
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        Attempts++;
        throw new InvalidOperationException("This harness does not write private files.");
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        Attempts++;
        throw new InvalidOperationException("This harness does not read private files.");
    }
}
