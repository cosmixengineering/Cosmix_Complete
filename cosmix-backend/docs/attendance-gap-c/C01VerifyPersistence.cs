#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=c01-no-connection.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var repository = new AdvancedPolicyRepository(db); var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; }
IQueryable<T> Query<T>(string name, params object?[] args) => (IQueryable<T>)typeof(AdvancedPolicyRepository)
    .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, args)!;
var family = AdvancedAttendanceFamily.EarlyCredit; var employee = Guid.NewGuid(); var policyId = Guid.NewGuid();
var date = new DateOnly(2026, 10, 20); var excluded = Guid.NewGuid();
var source = Query<AttendancePolicyCandidate>("ApplicableQuery", employee, family, date).OrderBy(x => x.Assignment.Id).Take(129).ToQueryString();
Check(source.Contains("[Category] = 5") && source.Contains("ATTADV_EARLYCREDIT"), "actual applicability query pins exception category and controlled requested family");
Check(source.Contains(employee.ToString()) && source.Contains("[EmployeeId] ="), "employee scope is translated before bounds");
Check(source.Contains("EmployeeAssignments") && source.Contains("[EffectiveFrom] <=") && source.Contains("[EffectiveTo] >"), "historical membership and exclusive dates are in actual query");
Check(source.Contains("TOP(") && source.Contains("129") && source.Contains("ORDER BY"), "ordered bounded source query translates");
Check(source.Contains("PolicyAssignmentEnds") && source.Contains("LEFT JOIN") && source.Contains("[Authority] = 2") &&
    source.Contains("[ApprovalRequestId] IS NULL"), "ending and actual direct authority provenance are translated");
Check(!source.Contains("ApprovalRequests"), "generic unrelated approval cannot establish policy authority");
var membership = Query<AttendancePolicyMembership>("MembershipQuery", employee, date).ToQueryString();
Check(membership.Contains(employee.ToString()) && membership.Contains("TOP(") && membership.Contains("3"), "actual membership query is scoped and bounded");
var categories = Query<AdvancedPolicyStaffCategorySource>("StaffCategoryQuery", employee, date).ToQueryString();
Check(categories.Contains("StaffCategories") && categories.Contains("[StaffCategoryId]") && categories.Contains("[Code]") &&
    categories.Contains(employee.ToString()) && categories.Contains("[EffectiveFrom] <="), "actual dated membership joins its real category code");
var overlapRows = Query<AttendancePolicyCandidate>("OverlapQuery", family, AttendancePolicyTarget.Employee, employee, date, date.AddDays(2), excluded);
var overlap = overlapRows.Select(x => x.Assignment.Id).Take(1).ToQueryString();
Check(overlap.Contains("ATTADV_EARLYCREDIT") && overlap.Contains("[Category] = 5") && overlap.Contains(employee.ToString()), "target overlap is confined to this family");
Check(overlap.Contains(excluded.ToString()) && overlap.Contains("<>") && overlap.Contains("[EffectiveFrom] <") &&
    overlap.Contains("[EffectiveTo] >"), "replacement excludes only the retained assignment with exclusive bounds");
var versions = Query<PolicyVersion>("VersionQuery", policyId).OrderBy(x => x.VersionNumber).Take(1025).ToQueryString();
Check(versions.Contains(policyId.ToString()) && versions.Contains("[Category] = 5") && versions.Contains("1025"), "version history scoped to controlled policy before bounds");
var policy = new Policy(AdvancedAttendanceRulesCodec.Code(family), "Synthetic policy", PolicyCategory.EmployeeException);
try { repository.Add(policy); throw new Exception("Mutation escaped serializable guard"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("serializable"), "real repository refuses mutation without outer transaction"); }
try { await repository.SaveChangesAsync(default); throw new Exception("Save escaped serializable guard"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("serializable"), "real repository refuses save without outer transaction"); }
try { await repository.HasTargetOverlapAsync(family, AttendancePolicyTarget.Employee, employee, date, null, null, default); throw new Exception("Overlap escaped serializable guard"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("serializable"), "overlap guard requires actual serializable transaction"); }
Check(db.Model.FindEntityType(typeof(Policy))!.FindProperty(nameof(Policy.Revision))!.IsConcurrencyToken &&
    db.Model.FindEntityType(typeof(Policy))!.FindProperty(nameof(Policy.RowVersion))!.IsConcurrencyToken, "existing revision and rowversion guards retained");
Check(new[] { typeof(PolicyVersion), typeof(PolicyAssignment), typeof(PolicyAssignmentEnd) }.All(x => typeof(IAppendOnlyEntity).IsAssignableFrom(x)),
    "existing version, assignment and end models remain append-only");
Check(db.ChangeTracker.Entries().Count() == 0 && guard.Attempts == 0, "guards staged no rows and opened no connections");
Console.WriteLine($"PASS: {checks} C01 EF translation/model/transaction-guard assertions; zero SQL connections or commands.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("C01 SQL execution forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("C01 SQL execution forbidden"); }
}
