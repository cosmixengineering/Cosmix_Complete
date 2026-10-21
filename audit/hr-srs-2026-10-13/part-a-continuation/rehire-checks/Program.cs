using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

// Temporary acceptance source: reflection permits the first run to fail on absent
// behavior without making the harness itself fail to compile. No SQL is opened.
var domain = typeof(Employee).Assembly;
var app = typeof(EmployeeLifecyclePolicy).Assembly;
var now = "2026-10-13T06:00:00+00:00";
var employee = "10000000-0000-0000-0000-000000000001";
var oldEpisode = "20000000-0000-0000-0000-000000000001";
var terminal = "30000000-0000-0000-0000-000000000001";
var originalApproval = "40000000-0000-0000-0000-000000000001";
var requestId = "50000000-0000-0000-0000-000000000001";
var policyHash = new string('A', 64);
var checks = 0;
var failures = new List<string>();
var episodes = new Dictionary<Guid,object>();

JsonObject BanEvidence() => new()
{
    ["EmployeeId"]=employee,["Resolution"]=1,["PermanentlyBlacklisted"]=false,["SourceProvenance"]=1,
    ["ResolutionVersion"]="synthetic-register-v1",["RegisterFingerprint"]=new string('D',64),
    ["VerifiedAt"]=now,["Sources"]=new JsonArray()
};
JsonObject Identity()
{
    var identity=JsonNode.Parse($$"""
{"EmployeeId":"{{employee}}","EmployeeNumber":71,"FirstJoinedOn":"2024-01-01",
"OriginalOnboardingDecisionId":"{{originalApproval}}","PreviousEpisodeId":"{{oldEpisode}}",
"PreviousEpisodeSequence":1,"TerminalStatusChangeId":"{{terminal}}","TerminalStatus":4,
"PreviousEndedAt":"2026-09-01T06:00:00+00:00"}
""")!.AsObject();
    identity["BanEvidence"]=BanEvidence();return identity;
}
JsonObject Terms() => JsonNode.Parse("""
{"JoinedOn":"2026-10-14","EffectiveAt":"2026-10-13T19:00:00+00:00","ProbationEndsOn":"2027-01-14",
"DepartmentId":"60000000-0000-0000-0000-000000000001","DesignationId":"60000000-0000-0000-0000-000000000002",
"StaffCategoryId":"60000000-0000-0000-0000-000000000003","EmploymentTypeId":"60000000-0000-0000-0000-000000000004",
"BranchId":"60000000-0000-0000-0000-000000000005","InitialSiteId":null,
"ReportingManagerEmployeeId":"60000000-0000-0000-0000-000000000006",
"BaseAmount":45000,"GrossAmount":50000,"Currency":"PKR","WageBasis":1,"PaymentMethod":1,
"ApprovedLoginPhoneNumber":"+923000000001","MobileAccessRequested":true,"WebAccessRequested":false}
""")!.AsObject();
JsonObject Policy() => JsonNode.Parse($$"""
{"ApprovedVersion":"synthetic-policy-v1","ApprovalEvidenceReference":"synthetic-management-proof",
"ApprovalEvidenceSha256":"{{policyHash}}","EffectiveFrom":"2026-10-01T00:00:00+00:00",
"EffectiveTo":"2027-01-01T00:00:00+00:00","ApprovalMode":1,"IdentityMode":1,
"JoiningBoundaryMode":1,"EvidenceReuseMode":1,"ProbationMode":1,
"BusinessTimeZoneId":"Asia/Karachi","EligiblePreviousStatuses":[4,5,6]}
""")!.AsObject();
JsonObject Approval() => JsonNode.Parse($$"""
{"RequestId":"{{requestId}}","SubmissionId":"50000000-0000-0000-0000-000000000002",
"DecisionId":"50000000-0000-0000-0000-000000000003","RequesterAccountId":"70000000-0000-0000-0000-000000000001",
"RequesterEmployeeId":"70000000-0000-0000-0000-000000000002","ReviewerAccountId":"70000000-0000-0000-0000-000000000003",
"ReviewerEmployeeId":"70000000-0000-0000-0000-000000000004","ReviewerRoleAssignmentId":"70000000-0000-0000-0000-000000000005",
"ReviewerSystemRole":1,"SubmittedAt":"2026-10-12T06:00:00+00:00","DecidedAt":"{{now}}",
"PolicyVersion":"synthetic-policy-v1","PolicyFingerprint":"{{policyHash}}","SnapshotSha256":"{{new string('B',64)}}",
"Reason":"Synthetic approved new employment"}
""")!.AsObject();
JsonObject Candidate() => new()
{
    ["Identity"] = Identity(), ["Terms"] = Terms(), ["EmployeeVersion"] = "AAAAAAAAAAE=",
    ["RequestVersion"] = "AAAAAAAAAAI=", ["DraftRevision"] = 2,
    ["RequestId"] = requestId, ["SubmissionId"] = "50000000-0000-0000-0000-000000000002",
    ["SnapshotSha256"] = new string('B',64), ["PolicyVersion"] = "synthetic-policy-v1",
    ["PolicyFingerprint"] = policyHash, ["Reason"] = "Synthetic rehire",
    ["Documents"] = new JsonArray(new JsonObject { ["DocumentId"]="80000000-0000-0000-0000-000000000001", ["Version"]="AAAAAAAAAAM=" }),
    ["Approval"] = Approval()
    ,["Account"] = new JsonObject { ["AccountId"]="90000000-0000-0000-0000-000000000001",["Version"]="AAAAAAAAAAU=",["SecurityVersion"]=9 }
    ,["RequesterAccountId"]="70000000-0000-0000-0000-000000000001",["RequesterEmployeeId"]="70000000-0000-0000-0000-000000000002"
};
JsonObject Context() => new()
{
    ["Now"]=now, ["CurrentEmployeeVersion"]="AAAAAAAAAAE=", ["CurrentRequestVersion"]="AAAAAAAAAAI=",
    ["EmployeeId"]=employee, ["EmployeeNumber"]=71, ["FirstJoinedOn"]="2024-01-01",
    ["OriginalOnboardingDecisionId"]=originalApproval, ["CurrentEndedEpisodeId"]=oldEpisode,
    ["CurrentTerminalStatusChangeId"]=terminal, ["CurrentStatus"]=4,
    ["HasFutureLifecycleChange"]=false, ["HasCompetingRehire"]=false, ["IsArchived"]=false,
    ["IsProtectedSystemRole"]=false, ["PreviousEpisodeReconciled"]=true,
    ["CurrentAccountId"]="90000000-0000-0000-0000-000000000001",["CurrentAccountVersion"]="AAAAAAAAAAU=",["CurrentAccountSecurityVersion"]=9,
    ["CurrentBanEvidence"]=BanEvidence(),["CurrentRequesterAccountId"]="70000000-0000-0000-0000-000000000001",["CurrentRequesterEmployeeId"]="70000000-0000-0000-0000-000000000002"
};
JsonObject AccountState() => new()
{
    ["AccountId"]="90000000-0000-0000-0000-000000000001", ["EmployeeId"]=employee,
    ["OriginalOnboardingDecisionId"]=originalApproval, ["PreviousEpisodeId"]=oldEpisode,
    ["Status"]=5, ["StatusReason"]="employee-lifecycle", ["LifecycleBlockEpisodeId"]=oldEpisode,
    ["SecurityVersion"]=9, ["LockedUntil"]="2026-10-20T06:00:00+00:00",
    ["CurrentBanEvidence"]=BanEvidence(),["ObservedAt"]=now
};
Type TypeFor(string name) => domain.GetType("Cosmix.Domain.HR.EmploymentEpisodes."+name) ??
    app.GetType("Cosmix.Application.HR.Rehire."+name) ?? throw new InvalidOperationException("Missing rehire behavior: "+name);
object Call(string type, string method, params JsonNode?[] inputs)
{
    var function=TypeFor(type).GetMethod(method,BindingFlags.Public|BindingFlags.Static) ?? throw new InvalidOperationException("Missing rehire operation: "+method);
    var parameters=function.GetParameters();
    var values=inputs.Select((n,i)=>n is null ? null : parameters[i].ParameterType.Name=="EmployeeEmploymentEpisode"
        ? episodes[Guid.Parse(n["Id"]!.GetValue<string>())]
        : JsonSerializer.Deserialize(n.ToJsonString(),parameters[i].ParameterType)).ToArray();
    try
    {
        var result=function.Invoke(null,values)!;
        if(result?.GetType().Name=="EmployeeEmploymentEpisode") episodes[(Guid)result.GetType().GetProperty("Id")!.GetValue(result)!]=result;
        return result!;
    }
    catch(TargetInvocationException ex) when(ex.InnerException is not null) { throw ex.InnerException; }
}
JsonObject Result(object value) => JsonSerializer.SerializeToNode(value,value.GetType())!.AsObject();
void Assert(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
void Check(string name,Action test) { checks++; try { test(); } catch(Exception ex) { failures.Add(name+": "+ex.Message); } }
void HasIssue(JsonObject result,string code) => Assert(result["Issues"]!.AsArray().Any(x=>x!["Code"]!.GetValue<string>()==code),"Expected issue "+code);
void Reject(string name,Action action) => Check(name,()=> { try {action();} catch(ArgumentException) {return;} catch(InvalidOperationException) {return;} throw new Exception("Invalid employment evidence accepted"); });
JsonObject EvaluatePolicy(JsonObject options,JsonObject? terms=null) => Result(Call("EmployeeRehirePolicy","Evaluate",options,JsonValue.Create(now),terms ?? Terms()));

Check("Absent policy cannot approve a new employment period",()=>HasIssue(EvaluatePolicy(new JsonObject()),"employee_rehire_policy_approval_required"));
Check("Approved structural policy accepts supported future join without activating",()=>Assert(EvaluatePolicy(Policy())["IsReady"]!.GetValue<bool>(),"Supported approved policy rejected"));
foreach(var item in new[]{("ApprovalMode",0,"employee_rehire_approval_mode_unapproved"),("IdentityMode",0,"employee_rehire_identity_mode_unapproved"),("JoiningBoundaryMode",0,"employee_rehire_joining_boundary_unapproved"),("EvidenceReuseMode",0,"employee_rehire_evidence_reuse_unapproved"),("ProbationMode",0,"employee_rehire_probation_mode_unapproved")})
    Check("Unapproved "+item.Item1+" fails closed",()=>{var p=Policy();p[item.Item1]=item.Item2;HasIssue(EvaluatePolicy(p),item.Item3);});
Check("Elapsed policy window denies a decision",()=>{var p=Policy();p["EffectiveTo"]=now;HasIssue(EvaluatePolicy(p),"employee_rehire_policy_outside_effective_window");});
Check("Join outside approved policy window denies",()=>{var p=Policy();p["EffectiveTo"]="2026-10-13T18:00:00+00:00";HasIssue(EvaluatePolicy(p),"employee_rehire_policy_outside_effective_window");});
Check("Joining date cannot disagree with effective local date",()=>{var t=Terms();t["JoinedOn"]="2026-10-15";HasIssue(EvaluatePolicy(Policy(),t),"employee_rehire_joining_boundary_invalid");});
Check("Partial day start is not silently invented",()=>{var t=Terms();t["EffectiveAt"]="2026-10-13T20:00:00+00:00";HasIssue(EvaluatePolicy(Policy(),t),"employee_rehire_joining_boundary_invalid");});
Check("Policy fingerprint changes when eligibility changes",()=>{var p=Policy();var before=EvaluatePolicy(p)["Fingerprint"]!.GetValue<string>();p["EligiblePreviousStatuses"]=new JsonArray(4,5);Assert(before!=EvaluatePolicy(p)["Fingerprint"]!.GetValue<string>(),"Policy change did not alter fingerprint");});
Check("Policy rejects empty eligibility",()=>{var p=Policy();p["EligiblePreviousStatuses"]=new JsonArray();HasIssue(EvaluatePolicy(p),"employee_rehire_eligible_statuses_unapproved");});
Check("Policy rejects Active as previous rehire status",()=>{var p=Policy();p["EligiblePreviousStatuses"]=new JsonArray(2);HasIssue(EvaluatePolicy(p),"employee_rehire_eligible_statuses_unapproved");});

Check("New period retains lifetime identity and distinct original evidence",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));Assert(e["EmployeeId"]!.GetValue<string>()==employee,"Employee replaced");Assert(e["EmployeeNumber"]!.GetValue<long>()==71,"Employee number replaced");Assert(e["FirstJoinedOn"]!.GetValue<string>()=="2024-01-01","First joining rewritten");Assert(e["OriginalOnboardingDecisionId"]!.GetValue<string>()==originalApproval,"Original approval rewritten");Assert(e["Sequence"]!.GetValue<int>()==2,"Separate period absent");});
Reject("Original active employment cannot be rehire",()=>{var i=Identity();i["TerminalStatus"]=2;Call("EmployeeEmploymentEpisode","CreateRehire",i,Terms(),Approval(),JsonValue.Create(now));});
Reject("Rehire cannot overlap ended period",()=>{var i=Identity();i["PreviousEndedAt"]="2026-10-14T00:00:00+00:00";Call("EmployeeEmploymentEpisode","CreateRehire",i,Terms(),Approval(),JsonValue.Create(now));});
Reject("Requester cannot decide own rehire request",()=>{var a=Approval();a["ReviewerAccountId"]=a["RequesterAccountId"]!.GetValue<string>();Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),a,JsonValue.Create(now));});
Reject("Subject cannot approve rehire",()=>{var a=Approval();a["ReviewerEmployeeId"]=employee;Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),a,JsonValue.Create(now));});
Reject("Nonfixed reviewer cannot approve rehire",()=>{var a=Approval();a["ReviewerSystemRole"]=0;Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),a,JsonValue.Create(now));});
Reject("Rehire cannot use missing new submission",()=>{var a=Approval();a["SubmissionId"]=Guid.Empty.ToString();Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),a,JsonValue.Create(now));});
Reject("Gross salary below base cannot become new terms",()=>{var t=Terms();t["GrossAmount"]=100;Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),t,Approval(),JsonValue.Create(now));});
Reject("Subcent starting salary cannot enter SQL decimal contract",()=>{var t=Terms();t["BaseAmount"]=100.001m;Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),t,Approval(),JsonValue.Create(now));});
Reject("Self reporting manager cannot become new assignment",()=>{var t=Terms();t["ReportingManagerEmployeeId"]=employee;Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),t,Approval(),JsonValue.Create(now));});
Reject("Noncanonical login phone cannot reuse implicit approval",()=>{var t=Terms();t["ApprovedLoginPhoneNumber"]="03000000001";Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),t,Approval(),JsonValue.Create(now));});

Check("Lifecycle-only disabled account prepares fresh dormant setup",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));var p=Result(Call("EmploymentEpisodeAccessRules","BuildResetPlan",AccountState(),Identity(),e));Assert(p["TargetStatus"]!.GetValue<int>()==0,"Did not prepare PendingAccess");Assert(p["EnableMobile"]!.GetValue<bool>()==false && p["EnableWeb"]!.GetValue<bool>()==false,"Channels revived");Assert(p["SecurityVersion"]!.GetValue<int>()==10,"Old proofs remain current");Assert(p["LockedUntil"]!.GetValue<string>()=="2026-10-20T06:00:00+00:00","Independent lock lost");Assert(p["Actions"]!.AsArray().Count>=14,"Fresh evidence cleanup incomplete");});
Check("Independent disabled account remains blocked",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));var s=AccountState();s["StatusReason"]="independent-admin-block";var p=Result(Call("EmploymentEpisodeAccessRules","BuildResetPlan",s,Identity(),e));Assert(p["TargetStatus"]!.GetValue<int>()==5 && p["PreserveIndependentBlock"]!.GetValue<bool>(),"Independent block released");});
Check("Mismatched lifecycle episode cannot release an old account",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));var s=AccountState();s["LifecycleBlockEpisodeId"]="20000000-0000-0000-0000-000000000099";var p=Result(Call("EmploymentEpisodeAccessRules","BuildResetPlan",s,Identity(),e));Assert(p["RequiresManualReview"]!.GetValue<bool>() && p["TargetStatus"]!.GetValue<int>()==5,"Mismatched old block released");});

Check("Missing policy prevents a workflow candidate",()=>HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",Candidate(),Context(),new JsonObject())),"employee_rehire_policy_approval_required"));
foreach(var item in new[]{("CurrentEmployeeVersion","AAAAAAAAAAQ=","employee_rehire_employee_version_conflict"),("CurrentRequestVersion","AAAAAAAAAAQ=","employee_rehire_request_version_conflict"),("CurrentEndedEpisodeId","20000000-0000-0000-0000-000000000099","employee_rehire_source_episode_changed"),("CurrentTerminalStatusChangeId","30000000-0000-0000-0000-000000000099","employee_rehire_terminal_evidence_changed")})
    Check("Changed "+item.Item1+" prevents old approval",()=>{var c=Context();c[item.Item1]=item.Item2;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",Candidate(),c,Policy())),item.Item3);});
foreach(var item in new[]{("HasFutureLifecycleChange",true,"employee_rehire_conflicting_lifecycle_change"),("HasCompetingRehire",true,"employee_rehire_competing_request"),("IsArchived",true,"employee_rehire_archived_identity"),("IsProtectedSystemRole",true,"employee_rehire_protected_owner_handover_required"),("PreviousEpisodeReconciled",false,"employee_rehire_previous_episode_unreconciled")})
    Check("Unsafe "+item.Item1+" prevents candidate",()=>{var c=Context();c[item.Item1]=item.Item2;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",Candidate(),c,Policy())),item.Item3);});
Check("Candidate rejects invalid rowversion",()=>{var c=Candidate();c["EmployeeVersion"]="junk";HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",c,Context(),Policy())),"employee_rehire_employee_version_invalid");});
Check("Policy change forces a fresh request",()=>HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",Candidate(),Context(),Policy())),"employee_rehire_policy_changed_request_again"));

Check("Missing consumer evidence lists every boundary without activation success",()=>{var c=Candidate();c["PolicyFingerprint"]=EvaluatePolicy(Policy())["Fingerprint"]!.GetValue<string>();c["Approval"]!["PolicyFingerprint"]=c["PolicyFingerprint"]!.GetValue<string>();var r=Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),new JsonArray()));Assert(!r["IsReady"]!.GetValue<bool>(),"Missing consumer proofs allowed activation");Assert(r["Issues"]!.AsArray().Count>=12,"Missing boundary inventory incomplete");Assert(r["Plan"] is null,"Unready result exposed an activation plan");});
JsonObject ValidCandidate()
{
    var c=Candidate();c["PolicyFingerprint"]=EvaluatePolicy(Policy())["Fingerprint"]!.GetValue<string>();
    c["Approval"]!["PolicyFingerprint"]=c["PolicyFingerprint"]!.GetValue<string>();return c;
}
JsonObject Reviewer() => new()
{
    ["AccountId"]="70000000-0000-0000-0000-000000000003",["EmployeeId"]="70000000-0000-0000-0000-000000000004",
    ["RoleAssignmentId"]="70000000-0000-0000-0000-000000000005",["SystemRole"]=1,
    ["CurrentWebSmsMfa"]=true,["CurrentAllScopeReviewPermission"]=true,["VerifiedAt"]=now,
    ["ValidUntil"]="2026-10-13T07:00:00+00:00"
};
JsonArray Proofs(JsonObject candidate)
{
    var fingerprint=(string)Call("EmployeeRehireWorkflowRules","Fingerprint",candidate);
    var proofs=new JsonArray();
    for(var boundary=1;boundary<=17;boundary++) proofs.Add(new JsonObject
    {
        ["Boundary"]=boundary,["Outcome"]=1,["EmployeeId"]=employee,["PreviousEpisodeId"]=oldEpisode,
        ["RequestId"]=requestId,["SubmissionId"]="50000000-0000-0000-0000-000000000002",
        ["EmployeeVersion"]="AAAAAAAAAAE=",["RequestVersion"]="AAAAAAAAAAI=",["PolicyVersion"]="synthetic-policy-v1",
        ["PolicyFingerprint"]=candidate["PolicyFingerprint"]!.GetValue<string>(),["CandidateFingerprint"]=fingerprint,
        ["EvidenceSha256"]=new string('C',64),["EvidencePrivateReference"]="synthetic/private-proof",
        ["EvidenceObjectVersion"]="synthetic-object-v1",["VerifiedAt"]=now,["ExpiresAt"]="2026-10-13T07:00:00+00:00",
        ["AccountId"]="90000000-0000-0000-0000-000000000001",["AccountVersion"]="AAAAAAAAAAU=",["AccountSecurityVersion"]=9
    });
    return proofs;
}
Check("Valid frozen candidate is structurally ready without source mutation",()=>Assert(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),Context(),Policy()))["IsReady"]!.GetValue<bool>(),"Valid candidate rejected"));
Check("Current independent fixed Admin proof allows review",()=>Assert(Result(Call("EmployeeRehireWorkflowRules","ValidateReview",ValidCandidate(),Context(),Policy(),Reviewer()))["IsReady"]!.GetValue<bool>(),"Valid reviewer rejected"));
foreach(var field in new[]{"CurrentWebSmsMfa","CurrentAllScopeReviewPermission"})
    Check("Missing "+field+" denies review",()=>{var r=Reviewer();r[field]=false;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateReview",ValidCandidate(),Context(),Policy(),r)),"employee_rehire_current_independent_admin_review_required");});
Check("A different current assignment cannot substitute for the frozen reviewer",()=>{var r=Reviewer();r["RoleAssignmentId"]="70000000-0000-0000-0000-000000000099";HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateReview",ValidCandidate(),Context(),Policy(),r)),"employee_rehire_current_independent_admin_review_required");});
Check("Expired review authority cannot approve",()=>{var r=Reviewer();r["ValidUntil"]=now;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateReview",ValidCandidate(),Context(),Policy(),r)),"employee_rehire_current_independent_admin_review_required");});
Check("Exact verified boundaries produce only a transaction-revalidation plan",()=>{var c=ValidCandidate();var r=Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),Proofs(c)));Assert(r["IsReady"]!.GetValue<bool>(),"Verified integration plan rejected");Assert(r["Plan"]!["RequiresTransactionRevalidation"]!.GetValue<bool>() && r["Plan"]!["KeepsChannelsDisabled"]!.GetValue<bool>() && r["Plan"]!["RequiresFreshAccessSetup"]!.GetValue<bool>(),"Plan would revive access or allow stale apply");});
var boundaryNames=new[]{"stable_identity","previous_episode","new_approval","current_policy","employment_terms","required_documents","authentication_proofs","account_reset","fresh_access_setup","face_isolation","attendance_duty","historical_coverage","policy_and_entitlements","financial_history","atomic_persistence_and_receipts","legacy_data_reconciliation","employment_blacklist"};
for(var boundary=1;boundary<=17;boundary++)
{
    var index=boundary-1;var name=boundaryNames[index];
    Check("Unavailable "+name+" blocks plan",()=>{var c=ValidCandidate();var p=Proofs(c);p[index]!["Outcome"]=0;var r=Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),p));HasIssue(r,"employee_rehire_"+name+"_not_ready");Assert(r["Plan"] is null,"Unavailable consumer supplied an activation plan");});
    Check("Stale "+name+" blocks plan",()=>{var c=ValidCandidate();var p=Proofs(c);p[index]!["ExpiresAt"]=now;HasIssue(Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),p)),"employee_rehire_"+name+"_evidence_stale");});
}
Check("Proof from a different request cannot approve this episode",()=>{var c=ValidCandidate();var p=Proofs(c);p[0]!["RequestId"]="50000000-0000-0000-0000-000000000099";HasIssue(Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),p)),"employee_rehire_stable_identity_evidence_binding_invalid");});
Check("Duplicate consumer proof is ambiguous",()=>{var c=ValidCandidate();var p=Proofs(c);p.Add(p[0]!.DeepClone());HasIssue(Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),p)),"employee_rehire_stable_identity_evidence_ambiguous");});
Check("Changed salary cannot use an earlier consumer proof",()=>{var c=ValidCandidate();var p=Proofs(c);c["Terms"]!["BaseAmount"]=46000;HasIssue(Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),p)),"employee_rehire_employment_terms_evidence_binding_invalid");});
Check("No new approval never yields a plan",()=>{var c=ValidCandidate();c["Approval"]=null;HasIssue(Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),Proofs(c))),"employee_rehire_new_approval_required");});
Check("Candidate must carry a valid existing account rowversion",()=>{var c=ValidCandidate();c["Account"]!["Version"]="bad";HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",c,Context(),Policy())),"employee_rehire_account_version_invalid");});
Check("A different existing account cannot satisfy the stable identity handoff",()=>{var x=Context();x["CurrentAccountId"]="90000000-0000-0000-0000-000000000099";HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_existing_account_changed");});
Check("Concurrent account security change invalidates frozen candidate",()=>{var x=Context();x["CurrentAccountSecurityVersion"]=10;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_account_version_conflict");});
Check("Consumer proof cannot describe a prior account version",()=>{var c=ValidCandidate();var p=Proofs(c);p[7]!["AccountVersion"]="AAAAAAAAAAY=";HasIssue(Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),p)),"employee_rehire_account_reset_evidence_binding_invalid");});
Check("Due activation accepts an approved start without treating it as a new late decision",()=>{var c=ValidCandidate();var x=Context();x["Now"]="2026-10-13T19:00:00+00:00";var p=Proofs(c);foreach(var n in p){n!["VerifiedAt"]="2026-10-13T19:00:00+00:00";n["ExpiresAt"]="2026-10-13T20:00:00+00:00";}var r=Result(Call("EmployeeRehireActivationPlanValidator","ValidateDue",c,x,Policy(),p));Assert(r["IsReady"]!.GetValue<bool>(),"Delayed start proof was rejected as a new decision");Assert(r["Plan"]!["KeepsChannelsDisabled"]!.GetValue<bool>(),"Due plan revived channels");});
Check("Due activation before joining never yields a plan",()=>{var c=ValidCandidate();HasIssue(Result(Call("EmployeeRehireActivationPlanValidator","ValidateDue",c,Context(),Policy(),Proofs(c))),"employee_rehire_effective_start_not_due");});
Reject("Independent Admin cannot approve a permanently blacklisted identity",()=>{var i=Identity();i["BanEvidence"]!["PermanentlyBlacklisted"]=true;i["BanEvidence"]!["Resolution"]=2;Call("EmployeeEmploymentEpisode","CreateRehire",i,Terms(),Approval(),JsonValue.Create(now));});
Check("Permanent ban after submission blocks the frozen request",()=>{var x=Context();x["CurrentBanEvidence"]!["PermanentlyBlacklisted"]=true;x["CurrentBanEvidence"]!["Resolution"]=2;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_permanently_blacklisted");});
Check("Permanent late ban after approval blocks a due activation plan",()=>{var c=ValidCandidate();var x=Context();x["Now"]="2026-10-13T19:00:00+00:00";x["CurrentBanEvidence"]!["PermanentlyBlacklisted"]=true;x["CurrentBanEvidence"]!["Resolution"]=2;var p=Proofs(c);foreach(var n in p){n!["VerifiedAt"]="2026-10-13T19:00:00+00:00";n["ExpiresAt"]="2026-10-13T20:00:00+00:00";}var r=Result(Call("EmployeeRehireActivationPlanValidator","ValidateDue",c,x,Policy(),p));HasIssue(r,"employee_rehire_permanently_blacklisted");Assert(r["Plan"] is null,"Late ban bypassed by old approved proof");});
Check("Relabeling a blacklisted termination as resignation cannot bypass ban",()=>{var c=ValidCandidate();c["Identity"]!["BanEvidence"]!["PermanentlyBlacklisted"]=true;c["Identity"]!["BanEvidence"]!["Resolution"]=2;c["Identity"]!["TerminalStatus"]=4;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",c,Context(),Policy())),"employee_rehire_permanently_blacklisted");});
Check("Override-looking proposal and verified consumer proofs cannot bypass a permanent ban",()=>{var c=ValidCandidate();c["Identity"]!["BanEvidence"]!["PermanentlyBlacklisted"]=true;c["Identity"]!["BanEvidence"]!["Resolution"]=2;c["ManagementBanOverrideApproved"]=true;var r=Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,Context(),Policy(),Proofs(c)));HasIssue(r,"employee_rehire_permanently_blacklisted");Assert(r["Plan"] is null,"Override bypassed permanent blacklist");});
Check("Missing authoritative ban resolution is unavailable, never clear",()=>{var x=Context();x["CurrentBanEvidence"]=null;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_blacklist_resolution_required");});
Check("Ambiguous authoritative ban resolution fails closed",()=>{var x=Context();x["CurrentBanEvidence"]!["Resolution"]=3;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_blacklist_resolution_required");});
Check("A rehire proposal cannot assert its own authoritative ban clearance",()=>{var x=Context();x["CurrentBanEvidence"]!["SourceProvenance"]=0;HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_blacklist_resolution_required");});
Check("Changed ban register version requires a fresh frozen candidate",()=>{var x=Context();x["CurrentBanEvidence"]!["RegisterFingerprint"]=new string('E',64);HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_blacklist_evidence_changed");});
Check("A new employee ID cannot replace the blacklisted stable identity",()=>{var c=ValidCandidate();c["Identity"]!["EmployeeId"]="10000000-0000-0000-0000-000000000099";var x=Context();x["CurrentBanEvidence"]!["PermanentlyBlacklisted"]=true;x["CurrentBanEvidence"]!["Resolution"]=2;var r=Result(Call("EmployeeRehireActivationPlanValidator","Validate",c,x,Policy(),Proofs(c)));Assert(!r["IsReady"]!.GetValue<bool>() && r["Plan"] is null,"New ID bypassed stable ban");});
Reject("Account reset cannot release a permanent ban recorded after episode approval",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));var s=AccountState();s["CurrentBanEvidence"]!["PermanentlyBlacklisted"]=true;s["CurrentBanEvidence"]!["Resolution"]=2;Call("EmploymentEpisodeAccessRules","BuildResetPlan",s,Identity(),e);});
Reject("Account reset requires current blacklist resolution",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));var s=AccountState();s["CurrentBanEvidence"]=null;Call("EmploymentEpisodeAccessRules","BuildResetPlan",s,Identity(),e);});
Reject("New episode cannot relabel the original hiring decision as its rehire decision",()=>{var a=Approval();a["DecisionId"]=originalApproval;Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),a,JsonValue.Create(now));});
Check("A raw 120 late count cannot manufacture an approved ban",()=>{var b=BanEvidence();b["Resolution"]=0;b["PermanentlyBlacklisted"]=null;b["AnnualLateCount"]=120;Assert((string?)Call("EmploymentEpisodeEligibilityRules","BlockingCode",b,JsonValue.Create(employee),JsonValue.Create(now))=="employee_rehire_blacklist_resolution_required","Late counter was treated as disciplinary authority");});
JsonObject CaseSource() => new()
{
    ["EmployeeId"]=employee,["DisciplinaryCaseId"]="A0000000-0000-0000-0000-000000000001",["RecordVersion"]="AAAAAAAAAAc=",
    ["ApprovalRequestId"]="A0000000-0000-0000-0000-000000000002",["ApprovalDecisionId"]="A0000000-0000-0000-0000-000000000003",
    ["ApprovedPolicyVersion"]="synthetic-disciplinary-policy",["OutcomeSha256"]=new string('F',64),
    ["OutcomeObjectVersion"]="synthetic-outcome-v1",["Disposition"]=1
};
Check("An approved permanent case outcome cannot be hidden by a false flag",()=>{var b=BanEvidence();var source=CaseSource();source["Disposition"]=2;b["Sources"]=new JsonArray(source);Assert((string?)Call("EmploymentEpisodeEligibilityRules","BlockingCode",b,JsonValue.Create(employee),JsonValue.Create(now))=="employee_rehire_permanently_blacklisted","False summary flag hid permanent approved outcome");});
Check("Incomplete approved case provenance is unavailable",()=>{var b=BanEvidence();var source=CaseSource();source["ApprovalDecisionId"]=Guid.Empty.ToString();b["Sources"]=new JsonArray(source);Assert((string?)Call("EmploymentEpisodeEligibilityRules","BlockingCode",b,JsonValue.Create(employee),JsonValue.Create(now))=="employee_rehire_blacklist_resolution_required","Missing disciplinary approval treated as clear");});
Check("Unresolved case disposition cannot be treated as clearance",()=>{var b=BanEvidence();var source=CaseSource();source["Disposition"]=0;b["Sources"]=new JsonArray(source);Assert((string?)Call("EmploymentEpisodeEligibilityRules","BlockingCode",b,JsonValue.Create(employee),JsonValue.Create(now))=="employee_rehire_blacklist_resolution_required","Unresolved case treated as clearance");});
Check("Exact case rowversion is rechecked independently of supplied register hash",()=>{var c=ValidCandidate();c["Identity"]!["BanEvidence"]!["Sources"]=new JsonArray(CaseSource());var x=Context();x["CurrentBanEvidence"]!["Sources"]=new JsonArray(CaseSource());x["CurrentBanEvidence"]!["Sources"]![0]!["RecordVersion"]="AAAAAAAAAAg=";HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",c,x,Policy())),"employee_rehire_blacklist_evidence_changed");});
Check("Episode gap is ineligible for current duty",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));Assert(!(bool)Call("EmploymentEpisodeAccessRules","IsWithinEpisode",e,JsonValue.Create(now),null),"Gap allowed current duty");});
Check("Episode end is exclusive for current duty",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));Assert(!(bool)Call("EmploymentEpisodeAccessRules","IsWithinEpisode",e,JsonValue.Create("2026-11-01T00:00:00+00:00"),JsonValue.Create("2026-11-01T00:00:00+00:00")),"End boundary included duty");});
Check("Unmapped rehire types cannot alter current EF model",()=>{using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer("Server=synthetic.invalid;Database=NotOpened;Integrated Security=true;TrustServerCertificate=true").Options);Assert(!db.Model.GetEntityTypes().Any(t=>t.ClrType.Namespace?.Contains("EmploymentEpisodes")==true||t.ClrType.Namespace?.Contains("HR.Rehire")==true),"Preparation types changed EF model");});

Check("A requester cannot review a pending candidate with no final approval yet",()=>{var c=ValidCandidate();c["Approval"]=null;c["RequesterAccountId"]="70000000-0000-0000-0000-000000000003";c["RequesterEmployeeId"]="70000000-0000-0000-0000-000000000004";var x=Context();x["CurrentRequesterAccountId"]=c["RequesterAccountId"]!.GetValue<string>();x["CurrentRequesterEmployeeId"]=c["RequesterEmployeeId"]!.GetValue<string>();HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateReview",c,x,Policy(),Reviewer())),"employee_rehire_current_independent_admin_review_required");});
Check("The requester cannot be replaced inside a frozen candidate",()=>{var x=Context();x["CurrentRequesterAccountId"]="70000000-0000-0000-0000-000000000099";HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",ValidCandidate(),x,Policy())),"employee_rehire_requester_evidence_changed");});
Check("Final approval must retain the frozen requester",()=>{var c=ValidCandidate();c["Approval"]!["RequesterAccountId"]="70000000-0000-0000-0000-000000000099";HasIssue(Result(Call("EmployeeRehireWorkflowRules","ValidateCandidate",c,Context(),Policy())),"employee_rehire_new_approval_binding_invalid");});
Check("A reviewed case for another employee cannot prove blacklist clearance",()=>{var b=BanEvidence();var s=CaseSource();s["EmployeeId"]="10000000-0000-0000-0000-000000000099";b["Sources"]=new JsonArray(s);Assert((string?)Call("EmploymentEpisodeEligibilityRules","BlockingCode",b,JsonValue.Create(employee),JsonValue.Create(now))=="employee_rehire_blacklist_resolution_required","Another employee case supplied clearance");});
Check("Access reset explicitly requires every old credential and grant to be retired",()=>{var e=Result(Call("EmployeeEmploymentEpisode","CreateRehire",Identity(),Terms(),Approval(),JsonValue.Create(now)));var p=Result(Call("EmploymentEpisodeAccessRules","BuildResetPlan",AccountState(),Identity(),e));Assert(Enumerable.Range(1,15).All(action=>p["Actions"]!.AsArray().Any(v=>v!.GetValue<int>()==action)),"An old credential/grant was omitted from the reset plan");});
foreach(var failure in failures) Console.WriteLine("FAIL "+failure);
Console.WriteLine($"Rehire standalone checks: {checks-failures.Count}/{checks} passed; {failures.Count} failed. No database/provider calls.");
Environment.ExitCode=failures.Count==0?0:1;
