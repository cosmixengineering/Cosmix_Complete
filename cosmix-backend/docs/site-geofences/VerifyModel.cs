#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyModel.packages.lock.json

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common;
using Cosmix.Infrastructure;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Standalone model/registration verification; no database is opened and no schema is applied.
// Run from the repository root with a local-only restore source:
// dotnet run --file docs/site-geofences/VerifyModel.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false
// The connection interceptor blocks before a provider could open the synthetic connection.
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:CosmixDatabase"] =
        "Server=cosmix-model-only.invalid;Database=ModelVerificationOnly;Integrated Security=true;Encrypt=true;Connect Timeout=1",
    ["MobileAuthentication:Enabled"] = "false",
    ["WebAuthentication:Enabled"] = "false"
}).Build();
var connections = new RejectConnections();
var services = new ServiceCollection();
services.AddApplication();
services.AddInfrastructure(configuration);
services.AddDbContext<CosmixDbContext>(options => options.AddInterceptors(connections));
// These are supplied by the API composition root in production. No behavior is substituted in the module.
services.AddSingleton<ICurrentAuthentication, RejectAuthentication>();
services.AddSingleton<IAccessTokenIssuer, RejectTokens>();
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CosmixDbContext>();
var checks = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception($"FAILED: {name}");
    checks++;
    Console.WriteLine($"PASS: {name}");
}

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
var boundaryType = model.FindEntityType(typeof(SiteGeofenceVersion))
    ?? throw new Exception("SiteGeofenceVersion is missing from the real EF model.");
Check(typeof(IAppendOnlyEntity).IsAssignableFrom(boundaryType.ClrType), "geofence version is append-only");
Check(boundaryType.GetTableName() == "SiteGeofenceVersions" && boundaryType.GetSchema() == "attendance",
    "geofence versions use the existing attendance table");
foreach (var (name, precision, scale) in new[]
{
    (nameof(SiteGeofenceVersion.Latitude), 10, 7),
    (nameof(SiteGeofenceVersion.Longitude), 10, 7),
    (nameof(SiteGeofenceVersion.RadiusMeters), 10, 2),
    (nameof(SiteGeofenceVersion.MaximumAccuracyMeters), 10, 2)
})
{
    var property = boundaryType.FindProperty(name) ?? throw new Exception($"Missing property {name}");
    Check(property.GetPrecision() == precision && property.GetScale() == scale,
        $"{name} has SQL precision {precision},{scale}");
}
Check(boundaryType.FindProperty(nameof(SiteGeofenceVersion.EffectiveFrom))?.GetColumnType() == "date",
    "effective boundary date is stored as SQL date");
foreach (var secondColumn in new[] { nameof(SiteGeofenceVersion.VersionNumber), nameof(SiteGeofenceVersion.EffectiveFrom) })
    Check(boundaryType.GetIndexes().Any(index => index.IsUnique && index.Properties.Select(p => p.Name)
        .SequenceEqual(new[] { nameof(SiteGeofenceVersion.SiteId), secondColumn })),
        $"site and {secondColumn} form a unique index");
Check(boundaryType.GetKeys().Any(key => key.Properties.Select(p => p.Name)
        .SequenceEqual(new[] { nameof(SiteGeofenceVersion.SiteId), nameof(SiteGeofenceVersion.Id) })),
    "site and boundary ID retain their composite key");
Check(boundaryType.GetForeignKeys().Any(key => key.PrincipalEntityType.ClrType == typeof(Site) &&
        key.DeleteBehavior == DeleteBehavior.Restrict &&
        key.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(SiteGeofenceVersion.SiteId) })),
    "site deletion cannot cascade through immutable boundary history");
Check(boundaryType.GetCheckConstraints().Select(c => c.Name).ToHashSet().IsSupersetOf(new[]
    { "CK_SiteGeofenceVersions_VersionNumber", "CK_SiteGeofenceVersions_Coordinates", "CK_SiteGeofenceVersions_Distances" }),
    "database guards retain version, coordinate and distance constraints");

foreach (var delete in new[] { false, true })
foreach (var asynchronous in new[] { false, true })
{
    db.ChangeTracker.Clear();
    db.ChangeTracker.AutoDetectChangesEnabled = false;
    var boundary = new SiteGeofenceVersion(Guid.NewGuid(), 1, new DateOnly(2030, 1, 1), 0m, 0m, 100m, 10m);
    var entry = db.Attach(boundary);
    if (delete) db.Remove(boundary);
    else entry.Property(x => x.RadiusMeters).CurrentValue = 101m;
    var rejected = false;
    try
    {
        if (asynchronous) await db.SaveChangesAsync();
        else db.SaveChanges();
    }
    catch (InvalidOperationException exception) when
        (exception.Message.Contains("SiteGeofenceVersion", StringComparison.Ordinal) &&
         exception.Message.Contains("append-only", StringComparison.Ordinal))
    {
        rejected = true;
    }
    Check(rejected, $"{(asynchronous ? "async" : "sync")} save rejects {(delete ? "deletion" : "mutation")} before database access");
}
db.ChangeTracker.Clear();

Check(scope.ServiceProvider.GetRequiredService<ISiteGeofenceRepository>() is SiteGeofenceRepository,
    "real repository registration resolves");
Check(scope.ServiceProvider.GetRequiredService<ISiteAttendanceIdentity>() is SiteAttendanceIdentity,
    "real current-employee identity registration resolves");
Check(scope.ServiceProvider.GetRequiredService<SiteGeofenceService>() is not null,
    "real SiteGeofenceService dependency graph resolves");
Check(connections.Attempts == 0 && db.Database.GetDbConnection().State == ConnectionState.Closed,
    "no database connection was attempted or opened");
Console.WriteLine($"Verified {checks} model, append-only guard and dependency-registration checks without database access.");

sealed class RejectConnections : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result)
    {
        Attempts++;
        throw new InvalidOperationException("Database connection attempts are prohibited in this model-only harness.");
    }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Attempts++;
        throw new InvalidOperationException("Database connection attempts are prohibited in this model-only harness.");
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
