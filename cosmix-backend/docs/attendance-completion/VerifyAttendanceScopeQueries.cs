#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var guard=new NoConnections();
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=attendance-scope-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var rosters=DispatchProxy.Create<IRosterRepository,NoCalls>();
var repository=new AttendanceSummaryRepository(db,rosters);
var employee=Guid.NewGuid(); var from=new DateOnly(2026,10,8); var to=from.AddDays(2);
var method=typeof(AttendanceSummaryRepository).GetMethod("UnmatchedSourceScopes",BindingFlags.Instance|BindingFlags.NonPublic)!;
var query=(IQueryable)method.Invoke(repository,[employee,from,to])!;
var sql=query.ToQueryString(); var passed=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name+"\n"+sql);passed++;}
Check(sql.Contains("[EmployeeId] =")&&sql.Contains(employee.ToString()),"source scope is bound to requested employee");
Check(sql.Contains("[AttendanceDate] >=")&&sql.Contains("[AttendanceDate] <="),"source selection uses requested inclusive range");
Check(sql.Contains("NOT EXISTS")&&sql.Contains("EmployeeAssignments"),"mismatched source sites selected by server historical anti-join");
Check(sql.Contains("[EffectiveFrom] <= [w].[AttendanceDate]")&&sql.Contains("[EffectiveTo] > [w].[AttendanceDate]"),
    "membership checked on each source business date with exclusive ending");
Check(sql.Contains("[e].[EmployeeId] = [w].[EmployeeId]"),"another employee's site history cannot cover a source");
Check(sql.Contains("[e].[SiteId] = [w].[SiteId]")&&sql.Contains("[e].[SiteId] IS NULL")&&sql.Contains("[w].[SiteId] IS NULL"),
    "nullable site identity has explicit SQL null semantics");
Check(sql.Contains("DISTINCT")&&sql.Contains("[AttendanceDate]")&&sql.Contains("TOP("),"date and site source pairs are distinct and bounded");
Check(guard.Attempts==0,"zero database connections attempted");
Console.WriteLine($"PASS: {passed} historical source-scope SQL translation checks; no database connections or commands.");
public class NoCalls:DispatchProxy
{
    protected override object? Invoke(MethodInfo? method,object?[]? arguments)=>throw new Exception("No roster repository IO allowed");
}
sealed class NoConnections:DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c,ConnectionEventData e,InterceptionResult r)
    {Attempts++;throw new Exception("Database access forbidden");}
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c,ConnectionEventData e,InterceptionResult r,CancellationToken ct=default)
    {Attempts++;throw new Exception("Database access forbidden");}
}
