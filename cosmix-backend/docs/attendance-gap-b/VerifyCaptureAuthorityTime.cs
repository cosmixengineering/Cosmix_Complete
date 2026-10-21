#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var checks=0;
void Check(bool ok,string name) { if(!ok)throw new Exception(name);checks++; }
var now=new DateTimeOffset(2026,10,13,4,0,0,TimeSpan.Zero);
var account=Guid.NewGuid();var employee=Guid.NewGuid();var roleId=Guid.NewGuid();
T Empty<T>() where T:class => (T)Activator.CreateInstance(typeof(T),true)!;
void Set(object source,string property,object? value)=>source.GetType().GetProperty(property)!.SetValue(source,value);
var role=Empty<Role>();Set(role,"Id",roleId);Set(role,"IsActive",true);
var assignment=Empty<UserRoleAssignment>();
Set(assignment,"Id",Guid.NewGuid());Set(assignment,"UserAccountId",account);Set(assignment,"EmployeeId",employee);
Set(assignment,"RoleId",roleId);Set(assignment,"Role",role);Set(assignment,"EffectiveFrom",now.AddDays(-1));
Set(assignment,"EffectiveTo",now.AddHours(1));Set(assignment,"RevokedAt",now.AddMinutes(15));
var permission=Empty<Permission>();Set(permission,"Code",AttendanceCapturePermissions.Capture);
var membership=Empty<RolePermission>();Set(membership,"RoleId",roleId);Set(membership,"Permission",permission);
Set(membership,"GrantedAt",now.AddHours(-1));
var scope=Empty<UserAccessScope>();Set(scope,"UserRoleAssignmentId",assignment.Id);
Set(scope,"Kind",AccessScopeKind.Self);Set(scope,"EmployeeId",employee);
var method=typeof(AccountAccessReader).GetMethod("BuildGrantQuery",BindingFlags.NonPublic|BindingFlags.Static)!;
IQueryable<PermissionGrant> Query(IQueryable<UserRoleAssignment> a,IQueryable<RolePermission> p,IQueryable<UserAccessScope> s)=>
    (IQueryable<PermissionGrant>)method.Invoke(null,[a,p,s,account,now])!;
var grant=Query(new[]{assignment}.AsQueryable(),new[]{membership}.AsQueryable(),new[]{scope}.AsQueryable()).Single();
Check(grant.AuthorityTimingKnown && grant.ValidFromUtc==membership.GrantedAt,"Actual projection retains later role/permission start");
Check(grant.ValidUntilUtc==assignment.RevokedAt,"Earlier scheduled revocation bounds an otherwise later assignment end");
Check(grant.IsEffectiveAt(now) && grant.IsEffectiveAt(now.AddMinutes(15).AddTicks(-1)) && !grant.IsEffectiveAt(now.AddMinutes(15)),
    "Retained interval is start-inclusive/end-exclusive at final server time");
Set(assignment,"RevokedAt",null);
var endOnly=Query(new[]{assignment}.AsQueryable(),new[]{membership}.AsQueryable(),new[]{scope}.AsQueryable()).Single();
Check(endOnly.ValidUntilUtc==assignment.EffectiveTo,"Assignment expiry remains authoritative without revocation");
Set(assignment,"EffectiveTo",null);
var open=Query(new[]{assignment}.AsQueryable(),new[]{membership}.AsQueryable(),new[]{scope}.AsQueryable()).Single();
Check(open.ValidUntilUtc is null && open.IsEffectiveAt(now.AddDays(1)),"Explicit authoritative open assignment is retained, not a guessed duration");
var legacy=new PermissionGrant(grant.Permission,grant.Sensitive,grant.RoleAssignmentId,grant.ScopeKind,grant.TargetId);
Check(!legacy.IsEffectiveAt(now),"Missing timing cannot authorize the new temporal capture gate");
Check(JsonSerializer.Serialize(legacy)==JsonSerializer.Serialize(grant),"Internal interval metadata leaves every historical grant JSON field unchanged");
Check(!(grant with{ValidFromUtc=now.AddSeconds(1)}).IsEffectiveAt(now),"Future start denied at final instant");
Check(!(grant with{ValidUntilUtc=grant.ValidFromUtc}).IsEffectiveAt(now),"Empty or malformed retained intervals fail closed");
Check(!(grant with{ValidFromUtc=now.ToOffset(TimeSpan.FromHours(5))}).IsEffectiveAt(now),"Non-UTC metadata fails closed");
var guard=new NoAuthorityConnections();
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=authority-check.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var sql=Query(db.UserRoleAssignments.AsNoTracking(),db.RolePermissions.AsNoTracking(),db.UserAccessScopes.AsNoTracking()).ToQueryString();
if(args.Contains("--show-query"))Console.WriteLine(sql);
Check(sql.Contains("CASE") && Regex.IsMatch(sql,@"\[EffectiveFrom\]\s*>\s*\[[^\]]+\]\.\[GrantedAt\]"),
    "Actual SQL projection selects authoritative start interval");
Check(sql.Contains("[EffectiveTo] IS NULL") && sql.Contains("[RevokedAt] IS NULL") &&
    Regex.IsMatch(sql,@"\[EffectiveTo\]\s*<\s*\[[^\]]+\]\.\[RevokedAt\]"),
    "Actual SQL projection retains the earliest closing authority event");
Check(sql.Contains("[UserAccountId] =") && sql.Contains(account.ToString()),"Authority query retains account scope predicate");
Check(guard.Attempts==0 && !db.ChangeTracker.Entries().Any(),"No SQL connection or fixture persistence");
Console.WriteLine($"PASS: {checks} retained capture-authority projection/interval checks; zero SQL writes/connections.");
sealed class NoAuthorityConnections:DbConnectionInterceptor {
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData data,InterceptionResult result)
    {Attempts++;throw new Exception("SQL forbidden");}
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData data,
        InterceptionResult result,CancellationToken ct=default)
    {Attempts++;throw new Exception("SQL forbidden");}
}
