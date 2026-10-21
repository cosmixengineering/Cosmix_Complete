#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.HR.Policies;

// Actual C01 resolver and C05 policy boundary; pure inputs only, never persisted employee/account fixtures.
var checks=0;
void Check(bool value,string name) { if(!value) throw new Exception(name); checks++; }
void Refuse(WaitingSource source) { try { WaitingSources.RequirePolicy(source); } catch(AuthenticationFailure e) when(e.Code=="waiting_policy_unavailable") { checks++;return; } throw new Exception("Missing/nonconfigured family accepted"); }
var date=new DateOnly(2026,10,20); var now=new DateTimeOffset(2026,10,13,12,0,0,TimeSpan.Zero);
var category=Guid.NewGuid(); var department=Guid.NewGuid(); var first=Guid.NewGuid(); var second=Guid.NewGuid();
var rules=new AdvancedAttendanceRules(1,AdvancedAttendanceFamily.Waiting,["OFFICE"],
    new("SYNTHETIC-CHECK-ONLY",WaitingPermissions.Decide,false,[]),Waiting:new(3600m,true,true,WaitingEffect.FactualOnly,null,null));
var policy=new Policy(AdvancedAttendanceRulesCodec.Code(AdvancedAttendanceFamily.Waiting),"Standalone waiting policy check",PolicyCategory.EmployeeException);
var version=new PolicyVersion(policy.Id,1,date.AddDays(-1),null,AdvancedAttendanceRulesCodec.Schema,
    AdvancedAttendanceRulesCodec.Validate(rules).CanonicalJson!,null,null,"synthetic-author",now,PolicyAuthority.DirectHr);
AdvancedPolicyResolution Resolve(Guid employee,bool assigned)
{
    var membership=new AttendancePolicyMembership(Guid.NewGuid(),employee,department,category,null,date.AddDays(-20),null);
    var assignment=new PolicyAssignment(version.Id,null,employee,null,null,null,date.AddDays(-1),null,"Standalone only",null,null,"synthetic-author",now,PolicyAuthority.DirectHr);
    return AdvancedPolicyReader.Resolve(employee,AdvancedAttendanceFamily.Waiting,date,new(policy,
        new([membership],assigned ? [new AttendancePolicyCandidate(assignment,version,null,true)] : []),[new(membership.Id,category,"OFFICE")]));
}
WaitingWorkerSource Worker(Guid employee,AdvancedPolicyResolution resolved) => new(employee,date,
    new(Guid.NewGuid(),category,"OFFICE",null,date.AddDays(-20),null),new(employee,date,"resolved","UTC"),[],
    new(LeaveContextStatus.Complete,[],"synthetic-no-leave",[]),resolved,[new AttendanceResourceScope([new AccessResource(EmployeeId:employee)])]);
var left=Resolve(first,true); var right=Resolve(second,true);
Check(left.State=="configured" && right.State=="configured","Actual C01 Waiting family state is configured");
var source=new WaitingSource(Worker(first,left),Worker(second,right),"synthetic-pair-source");
Check(WaitingSources.RequirePolicy(source).Effect==WaitingEffect.FactualOnly,"Explicit configured FactualOnly policy is accepted");
var missing=Resolve(first,false);
Check(missing.State=="missing" && missing.Rules is null,"Actual C01 absent family remains missing");
Refuse(source with {Waiting=Worker(first,missing)});
Refuse(source with {Waiting=Worker(first,left with {State="effective"})});
Refuse(source with {Causing=Worker(second,right with {PolicyVersionId=null})});
Check(WaitingAttribution.ExactPoints(0,rules.Waiting! with {Effect=WaitingEffect.PerformanceProposal,PerformancePointsPerHour=1m})==0m,
    "Only explicitly configured performance arithmetic permits a mathematical zero");
Console.WriteLine($"C05 actual C01 policy-boundary checks passed: {checks}. No SQL, host, provider or persisted fixtures.");
