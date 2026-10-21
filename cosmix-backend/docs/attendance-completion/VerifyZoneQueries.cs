#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

var checks=0;
void Check(bool value,string name) { if(!value) throw new Exception(name); checks++; }
var guard=new NoConnections();
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").AddInterceptors(guard).Options);
var repository=new ZoneRepository(db); var now=new DateTimeOffset(2026,10,9,12,0,0,TimeSpan.Zero); var employee=Guid.NewGuid();var site=Guid.NewGuid();
IQueryable<T> Query<T>(string name,params object?[] args)=>(IQueryable<T>)typeof(ZoneRepository).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(repository,args)!;
var duty=Query<WorkSession>("ActiveDutyQuery",employee,now).Take(2).ToQueryString();
Check(duty.Contains("NOT EXISTS") && duty.Contains("[Revision] >"),"latest session and assessment query");
Check(duty.Contains("[EndsAtUtc] IS NULL") && duty.Contains("[StartsAtUtc] <="),"only current open duty");
Check(duty.Contains("FaceVerificationAttempts") && duty.Contains("[ConsumedByPunchId] =") && duty.Contains("[LivenessPassed]") && duty.Contains("[MatchPassed]"),"consumed passing face proof required");
Check(duty.Contains("[BusinessDate] =") && duty.Contains("[SiteId] =") && duty.Contains("[ReceivedAtUtc] ="),"accepted raw duty source date site time binding");
Check(duty.Contains("[Action] = 2") && duty.Contains("[ReceivedAtUtc] >="),"accepted Checkout closes tracking even if imported latest session open");
var assigned=Query<ZoneAssignment>("ApplicableQuery",employee,site,now).Take(2049).ToQueryString();
Check(assigned.Contains("ZoneAssignmentEnds") && assigned.Contains("[EffectiveToUtc] <="),"controlled assignment ending applied");
Check(assigned.Contains("TeamMembers") && assigned.Contains("TerminatedEffectiveTo"),"current historical team membership bound");
Check(assigned.Contains("UserRoleAssignments") && assigned.Contains("RevokedAt"),"revoked and ended role membership excluded");
Check(assigned.Contains("OperationalZones") && assigned.Contains("[SiteId] =") && assigned.Contains("[IsActive]"),"zone administration site and active state filtered in SQL");
var events=Query<ZoneEvent>("EventsQuery",employee,site,new AttendanceCursorPosition(now,Guid.NewGuid())).Take(101).ToQueryString();
Check(events.Contains("[EmployeeId] =") && events.Contains("[SiteId] ="),"event employee and site filter before bound");
Check(events.Contains("[ReceivedAtUtc] <") && events.Contains("ORDER BY") && events.Contains("DESC") && events.Contains("TOP("),"bounded deterministic descending cursor query");
var reviewers=Query<Guid>("ReviewerQuery",site,now).ToQueryString();
Check(reviewers.Contains("RolePermissions") && reviewers.Contains("UserAccessScopes") && reviewers.Contains("[UserRoleAssignmentId]"),"reviewer permission and scope bound to same role assignment");
Check(reviewers.Contains("[WebAccessEnabled]") && reviewers.Contains("[RevokedAt] IS NULL") && reviewers.Contains("[SiteId] ="),"only active web reviewers with current exact-site/All grants");
Check(reviewers.Contains("257") && reviewers.Contains("DISTINCT"),"reviewer notification fanout bounded and deduplicated in SQL");
var model=db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
var configuration=model.FindEntityType(typeof(ZoneMonitoringConfigurationVersion))!;
Check(configuration.FindProperty(nameof(ZoneMonitoringConfigurationVersion.LiveVisibilitySeconds))!.IsNullable==false,"explicit live visibility required");
Check(configuration.GetCheckConstraints().Any(x=>x.Sql.Contains("[MinimumIntervalSeconds] <= [MaximumMonitoringSeconds]")),"duty interval bounded by maximum duration");
var window=model.FindEntityType(typeof(DutyMonitoringWindow))!;
Check(window.GetCheckConstraints().Any(x=>x.Sql.Contains("[ConfigurationVersionId] IS NOT NULL") && x.Sql.Contains("[AcceptedCheckInId] IS NOT NULL") && x.Sql.Contains("[RetentionPolicyVersionId] IS NULL")),"modern notice proof pins cannot be partial");
var location=model.FindEntityType(typeof(ZoneEvent))!;
Check(location.FindProperty(nameof(ZoneEvent.Latitude))!.IsNullable,"legacy GPS now nullable for expiring evidence");
Check(location.GetCheckConstraints().Any(x=>x.Sql.Contains("[LocationExpiresAtUtc] IS NOT NULL") && x.Sql.Contains("[Latitude] IS NULL")),"modern location must be encrypted with explicit expiry");
Check(model.FindEntityType(typeof(ZoneMonitoringCheckpoint))!.GetIndexes().Any(x=>x.IsUnique),"one optimistic checkpoint per window");
Check(model.FindEntityType(typeof(ZoneLocationDeletion))!.GetIndexes().Any(x=>x.IsUnique),"one retained deletion tombstone per event");
Check(model.FindEntityType(typeof(ZoneAssignmentEnd))!.GetIndexes().Any(x=>x.IsUnique),"concurrent assignment ending constrained");
Check(model.FindEntityType(typeof(ZoneMonitoringCheckpoint))!.FindProperty("LastWarning") is not null,"coarse warning transitions persisted separately from position");
var migrations=db.GetService<IMigrationsAssembly>();
var selected=migrations.Migrations.Single(x=>x.Key.EndsWith("_RetainZoneWarningTransitions",StringComparison.Ordinal));
var migration=migrations.CreateMigration(selected.Value,db.Database.ProviderName!);
Check(migration.UpOperations.OfType<AddColumnOperation>().Single().Name=="LastWarning","incremental warning fix adds only its required state field");
Check(migration.DownOperations.First() is SqlOperation sql && sql.Sql.Contains("THROW") && sql.Sql.Contains("LastWarning"),"rollback refuses loss of observed warning state");
Check(!db.Database.HasPendingModelChanges(),"zone model matches final migration snapshot");
Check(guard.Attempts==0 && !db.ChangeTracker.Entries().Any(),"no database connection or staged rows");
Console.WriteLine($"PASS {checks} Zones model and actual SQL translation checks; no database connection.");

sealed class NoConnections:DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c,ConnectionEventData e,InterceptionResult r) { Attempts++; throw new Exception("No DB"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c,ConnectionEventData e,InterceptionResult r,CancellationToken ct=default) { Attempts++;throw new Exception("No DB"); }
}
