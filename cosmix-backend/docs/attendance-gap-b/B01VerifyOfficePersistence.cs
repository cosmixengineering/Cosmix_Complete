#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Domain.Attendance.Offices;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.HR.Branches;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

var guard = new NoOfficeConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=office-contract.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var model = db.GetService<IDesignTimeModel>().Model;
var checks = 0;
void Check(bool condition,string name) { if (!condition) throw new Exception(name); checks++; }
var boundary = model.FindEntityType(typeof(OfficeGeofenceVersion))!;
Check(boundary.GetSchema() == "attendance" && boundary.GetTableName() == "OfficeGeofenceVersions","Office boundary has a real mapped history table");
Check(boundary.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(Branch) &&
    x.DeleteBehavior == DeleteBehavior.Restrict),"Office boundary belongs to a real retained Branch");
Check(boundary.GetIndexes().Count(x => x.IsUnique) == 2,"Unique branch revision and effective-date history");
Check(boundary.FindProperty("Latitude")!.GetPrecision() == 10 && boundary.FindProperty("Latitude")!.GetScale() == 7,
    "Stored geometry preserves approved precision");
foreach (var entity in new[] { typeof(RawPunchEvent),typeof(WorkSession) }) {
    var mapped = model.FindEntityType(entity)!;
    foreach(var property in new[] { "BranchId","OfficeRosterAssignmentId","OfficeGeofenceVersionId" })
        Check(mapped.FindProperty(property)?.IsNullable == true,"Legacy-compatible office field: " + entity.Name + "." + property);
    Check(mapped.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(OfficeRosterAssignment) &&
        x.Properties.Select(p => p.Name).SequenceEqual(new[] { "BranchId","OfficeRosterAssignmentId" })),
        "Exact office/roster relational binding: " + entity.Name);
    Check(mapped.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(OfficeGeofenceVersion) &&
        x.Properties.Select(p => p.Name).SequenceEqual(new[] { "BranchId","OfficeGeofenceVersionId" })),
        "Exact office/boundary relational binding: " + entity.Name);
    Check(mapped.GetCheckConstraints().Any(x => x.Sql.Contains("[SiteId] IS NULL") && x.Sql.Contains("[BranchId]")),
        "Office and Site contexts cannot be mixed: " + entity.Name);
}
var employee=Guid.NewGuid();
var raw=new RawPunchEvent(employee,PunchAction.CheckIn,Guid.NewGuid().ToString("D"),"office-immutable-test",new string('A',64),
    new(2026,10,14,4,0,0,TimeSpan.Zero),false,businessDate:new(2026,10,14),branchId:Guid.NewGuid());
db.Attach(raw);
typeof(RawPunchEvent).GetProperty("BranchId")!.SetValue(raw,Guid.NewGuid());
try {
    typeof(CosmixDbContext).GetMethod("ValidateTrackedWrites",BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(db,[]);
    throw new Exception("Office raw provenance modification was not refused");
} catch(TargetInvocationException error) when(error.InnerException is InvalidOperationException) { checks++; }
Check(guard.Attempts == 0,"No SQL connection attempted while checking actual relational/append-only contracts");
Console.WriteLine($"PASS: {checks} office EF/provenance checks; zero SQL connections and writes.");
sealed class NoOfficeConnections : DbConnectionInterceptor {
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData data,InterceptionResult result)
    { Attempts++; throw new Exception("SQL forbidden for this contract check"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData data,
        InterceptionResult result,CancellationToken ct=default)
    { Attempts++; throw new Exception("SQL forbidden for this contract check"); }
}
