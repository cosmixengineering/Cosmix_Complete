#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Common.Approvals;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Fail(string code, Action action) { try { action(); throw new Exception("Expected " + code); } catch(AuthenticationFailure e) { Check(e.Code == code, e.Code); } }
var today=new DateOnly(2026,10,13);
ApprovalRouteRule Rule(string code,int stage,decimal? min=null,decimal? max=null,string? risk=null)=>new(code,1,"allowance",ApprovalPermissions.StageReview(stage),stage,today,null,"synthetic-approver",minimumAmount:min,maximumAmount:max,currency:min.HasValue||max.HasValue?"PKR":null,riskCode:risk);
var context=new ApprovalRoutingContext("allowance",today,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),100m,"PKR",60,"ordinary");
var resolver=new ApprovalRouteResolver();
var route=resolver.Resolve(context,new[]{Rule("ONE",1),Rule("TWO",2)});
Check(route.Stages.Count==2 && route.Stages[1].Stage==2,"Ordered two-stage route");
Fail("approval_route_unconfigured",()=>resolver.Resolve(context,Array.Empty<ApprovalRouteRule>()));
Fail("approval_route_ambiguous",()=>resolver.Resolve(context,new[]{Rule("ONE",1),Rule("OTHER",1)}));
Fail("approval_route_incomplete",()=>resolver.Resolve(context,new[]{Rule("ONE",1),Rule("THREE",3)}));
Fail("approval_route_unconfigured",()=>resolver.Resolve(context,new[]{Rule("HIGH",1,101m)}));
Check(resolver.Resolve(context,new[]{Rule("EXACT",1,100m,100m)}).Stages.Count==1,"Inclusive amount bounds");
Fail("approval_route_unconfigured",()=>resolver.Resolve(context with { Amount=null },new[]{Rule("AMOUNT",1,1m)}));
Fail("fixed_approval_route",()=>resolver.Resolve(context with { RequestType="leave" },new[]{Rule("ONE",1)}));
Fail("approval_route_unconfigured",()=>resolver.Resolve(context with { On=today.AddDays(-1) },new[]{Rule("ONE",1)}));
Check(route.Fingerprint==resolver.Resolve(context,new[]{route.Stages[1].Rule,route.Stages[0].Rule}).Fingerprint,"Stable route fingerprint independent of query order");
Fail("approval_route_unconfigured",()=>resolver.Resolve(context with { Currency="USD" },new[]{Rule("PKR",1,1m)}));
Fail("approval_route_unconfigured",()=>resolver.Resolve(context,new[]{Rule("RISK",1,risk:"high")}));
var duration=new ApprovalRouteRule("DURATION",1,"allowance",ApprovalPermissions.Review,1,today,null,"synthetic",minimumDurationMinutes:61);
Fail("approval_route_unconfigured",()=>resolver.Resolve(context,new[]{duration}));
Fail("approval_route_unconfigured",()=>resolver.Resolve(context with { DurationMinutes=null },new[]{duration}));
var department=new ApprovalRouteRule("DEPT",1,"allowance",ApprovalPermissions.Review,1,today,null,"synthetic",departmentId:Guid.NewGuid());
Fail("approval_route_unconfigured",()=>resolver.Resolve(context,new[]{department}));
var expired=new ApprovalRouteRule("EXPIRED",1,"allowance",ApprovalPermissions.Review,1,today.AddDays(-1),today,"synthetic");
Fail("approval_route_unconfigured",()=>resolver.Resolve(context,new[]{expired}));
var old=Rule("SAME",1);
var successor=new ApprovalRouteRule("SAME",2,"allowance",ApprovalPermissions.Review,1,today.AddDays(1),today.AddDays(2),"synthetic",minimumAmount:200m,currency:"PKR");
Check(resolver.Resolve(context,new[]{old,successor}).Stages[0].Rule.Id==old.Id,"Future version does not replace current");
Fail("approval_route_unconfigured",()=>resolver.Resolve(context with{On=today.AddDays(1)},new[]{old,successor}));
Fail("approval_route_unconfigured",()=>resolver.Resolve(context with{On=today.AddDays(3)},new[]{old,successor}));
Fail("invalid_approval_routing_context",()=>resolver.Resolve(context with{Amount=-1m},new[]{old}));
Fail("approval_route_invalid_authority",()=>resolver.Resolve(context,new[]{new ApprovalRouteRule("WRONG_ONE",1,"allowance","approvals.stages.2.review",1,today,null,"synthetic")}));
Fail("approval_route_invalid_authority",()=>resolver.Resolve(context,new[]{old,new ApprovalRouteRule("WRONG_TWO",1,"allowance",ApprovalPermissions.Review,2,today,null,"synthetic")}));
Fail("approval_route_invalid_authority",()=>resolver.Resolve(context,new[]{new ApprovalRouteRule("GENERIC",1,"allowance","approvals.requests.review",1,today,null,"synthetic")}));
Fail("approval_route_invalid_authority",()=>resolver.Resolve(context,new[]{old,new ApprovalRouteRule("UNKNOWN",1,"allowance","approvals.stages.21.review",21,today,null,"synthetic")}));
Console.WriteLine($"PASS {checks} routing checks (synthetic/offline; no SQL/provider acceptance)");

