#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;

int passed=0;
void Check(bool value,string name){if(!value)throw new Exception(name);passed++;}
var actor=new AuthenticatedAccount(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
var employee=Guid.NewGuid(); var site=Guid.NewGuid(); var teamA=Guid.NewGuid(); var teamB=Guid.NewGuid(); var project=Guid.NewGuid();
var permission=AttendanceSummaryPermissions.Read; var role=Guid.NewGuid();
PermissionGrant Grant(AccessScopeKind kind,Guid? id)=>new(permission,false,role,kind,id);
AttendanceResourceScope Slice(Guid team,Guid? secondTeam=null)=>new(secondTeam is Guid extra?
    [new(employee,TeamId:team,SiteId:site,ProjectId:project),new(employee,TeamId:extra,SiteId:site,ProjectId:project)]:
    [new(employee,TeamId:team,SiteId:site,ProjectId:project)]);
var first=Slice(teamA,teamB);
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamA)],permission,[first]),"one equivalent team path authorizes one employee/date");
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamB)],permission,[first]),"second equivalent team can authorize the same slice");
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Project,project)],permission,[first]),"historical project scope supported");
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Site,site)],permission,[first]),"site scope unchanged");
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Employee,employee)],permission,[first]),"explicit employee scope unchanged");
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamA)],permission,[first,Slice(teamB)]),"different date slices must all be authorized");
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamA),Grant(AccessScopeKind.Team,teamB)],permission,[first,Slice(teamB)]),"distinct grants may cover distinct date slices");
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamA)],permission,[new([new(employee,TeamId:teamA)]),new([new(Guid.NewGuid(),TeamId:teamB)])]),"one employee never authorizes another");
Check(!AttendanceScopeEvaluation.IsValid([new([new(employee,TeamId:teamA),new(Guid.NewGuid(),TeamId:teamB)])]),"mixed employees cannot become alternatives");
var flat=AttendanceScopeEvaluation.FromResources([new(employee,SiteId:site),new(employee,SiteId:Guid.NewGuid())]);
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Site,site)],permission,flat),"legacy flat resources preserve ALL requirements");
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.All,null)],permission,flat),"All scope still covers valid explicit requirements");
Check(!AttendanceScopeEvaluation.Allows(actor with {Client=SessionClient.Mobile,MfaSatisfied=false},[Grant(AccessScopeKind.All,null)],permission,flat),"management scope denied on mobile");
Check(!AttendanceScopeEvaluation.Allows(actor with {MfaSatisfied=false},[Grant(AccessScopeKind.Team,teamA)],permission,[first]),"MFA still required");
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.All,null)],permission,[]),"empty scoped result never becomes All authority");
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.All,null)],permission,[new([])]),"empty alternatives denied");
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.All,null)],permission,[new([new AccessResource()])]),"resource with no identity denied");
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.All,null)],permission,[new([new(employee,TeamId:Guid.Empty)])]),"empty GUID rejected");
Check(!AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.All,null)],permission,[new(null!)]),"malformed protected scope denied");
Check(!AttendanceScopeEvaluation.IsValid(Enumerable.Repeat(first,AttendanceScopeEvaluation.MaximumScopes+1).ToArray()),"scope count bounded");
Check(!AttendanceScopeEvaluation.IsValid([new(Enumerable.Repeat(new AccessResource(employee),AttendanceScopeEvaluation.MaximumTotalAlternatives+1).ToArray())]),"alternatives bounded");
var nowScopes=new[]{Slice(teamB)}; var oldScopes=new[]{Slice(teamA)};
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamB)],permission,nowScopes)&&
    !AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamB)],permission,oldScopes),"current new-team access does not replace original receipt scope");
Check(AttendanceScopeEvaluation.Allows(actor,[Grant(AccessScopeKind.Team,teamB),Grant(AccessScopeKind.Team,teamA)],permission,nowScopes.Concat(oldScopes).ToArray()),"current and original scopes can both be reauthorized");
var siteReportGrant=new PermissionGrant(SiteReportPermissions.Read,false,role,AccessScopeKind.Team,teamA);
Check(!AttendanceScopeEvaluation.Allows(actor,[siteReportGrant],SiteReportPermissions.Read,
    AttendanceScopeEvaluation.FromResources([new AccessResource(SiteId:site)])),"site reports remain exact Site/All rather than inheriting employee alternatives");
Console.WriteLine($"PASS: {passed} historical scope alternative checks; pure evaluator, no SQL/auth fixtures.");
