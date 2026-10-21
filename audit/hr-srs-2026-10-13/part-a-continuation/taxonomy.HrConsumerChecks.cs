using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Organization;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.Policies;

var pass = 0; var failures = new List<string>();
async Task Case(string name, Func<Task> action) { try { await action(); pass++; Console.WriteLine("PASS: " + name); } catch(Exception e) { failures.Add(name + " => " + e.Message); Console.WriteLine("FAIL: " + failures[^1]); } }
void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
async Task Reject(Func<Task> action, string code) { try { await action(); throw new Exception("expected " + code); } catch(AuthenticationFailure e) { Check(e.Code == code, "expected " + code + "; actual " + e.Code); } }
await Case("guarded broader metadata creation has no eligibility prerequisite", async () => {
    var f = new ConsumerFixture(); var result = await f.Organization.CreateAsync(OrganizationMasterKind.StaffCategory,
        new() { Code="driver", Name="Synthetic driver" }, "driver-metadata-001", f.Context, default);
    Check(result.Code == "DRIVER", "canonical code not retained");
    Check(f.Taxonomy.Calls.Count == 0, "metadata consulted eligibility resolver");
});
await Case("guarded broader metadata reactivation preserves the immutable code", async () => {
    var f = new ConsumerFixture(); var id = Guid.NewGuid(); f.Master = new(id,"DRIVER","Synthetic driver",false,f.Version);
    var result = await f.Organization.SetStatusAsync(OrganizationMasterKind.StaffCategory,id,new(){IsActive=true,ExpectedVersion=f.Version,Reason="Synthetic metadata activation"},"driver-reactivate-001",f.Context,default);
    Check(result.IsActive && result.Code == "DRIVER", "metadata activation failed");
    Check(f.Taxonomy.Calls.Count == 0, "metadata grants eligibility");
});
await Case("metadata code length and grammar remain guarded", async () => {
    var f = new ConsumerFixture();
    await Reject(()=>f.Organization.CreateAsync(OrganizationMasterKind.StaffCategory,new(){Code=new string('A',31),Name="Invalid"},"bad-metadata-001",f.Context,default),"invalid_organization_request");
    await Reject(()=>f.Organization.CreateAsync(OrganizationMasterKind.StaffCategory,new(){Code=" bad",Name="Invalid"},"bad-metadata-002",f.Context,default),"invalid_organization_request");
});
await Case("approved Site mapped hiring submission omits activation-only site", async () => {
    var f = new ConsumerFixture(); var result = await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"driver-submit-001",f.Context,default);
    Check(result.SubmissionId.HasValue, "submission absent");
    using var json = JsonDocument.Parse(f.Store.Objects.Single(x=>x.Value.Purpose.EndsWith("/submission",StringComparison.Ordinal)).Value.Bytes);
    var taxonomy = json.RootElement.GetProperty("Taxonomy");
    Check(taxonomy.GetProperty("CategoryCode").GetString()=="DRIVER", "category pin absent");
    Check(taxonomy.GetProperty("Fingerprint").GetString()==f.Taxonomy.Resolution.Fingerprint,"fingerprint not frozen at proposal");
    Check(taxonomy.GetProperty("PolicyVersionId").GetGuid()==f.Taxonomy.Resolution.PolicyVersionId,"version identity not frozen");
});
await Case("approval rejects changed fingerprint even with unchanged human label", async () => {
    var f = new ConsumerFixture(); var submitted = await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"driver-submit-002",f.Context,default);
    var snapshot = await f.Snapshot(submitted.SubmissionId!.Value);
    f.Taxonomy.Resolution = f.Taxonomy.Resolution with { Fingerprint=new string('B',64) };
    await Reject(()=>f.ValidateSnapshot(snapshot),"employee_staff_taxonomy_changed_review_required");
});
await Case("approval rejects same historical pin with unavailable activation date", async () => {
    var f = new ConsumerFixture(); f.Profile = f.Profile with { JoinedOn = new(2026,10,12) }; f.UpdateDraft();
    var submitted = await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"driver-submit-003",f.Context,default);
    var snapshot = await f.Snapshot(submitted.SubmissionId!.Value);
    f.Taxonomy.OnDate = date=>date == new DateOnly(2026,10,13) ? new("taxonomy_not_effective",null,null,new string('C',64)) : f.Taxonomy.Resolution;
    await Reject(()=>f.ValidateSnapshot(snapshot),"employee_staff_taxonomy_changed_review_required");
});
await Case("legacy unpinned OFFICE snapshot stays readable before publication", async () => {
    var f = new ConsumerFixture("OFFICE"); f.Profile = f.Profile with { BranchId=Guid.NewGuid() }; f.UpdateDraft();
    var snapshot = f.UnpinnedSnapshot(); var validation = await f.ValidateSnapshot(snapshot);
    Check(validation.CanSubmit,"legacy office snapshot was rejected");
});
await Case("legacy unpinned broad category cannot acquire approval authority", async () => {
    var f = new ConsumerFixture();
    await Reject(()=>f.ValidateSnapshot(f.UnpinnedSnapshot()),"employee_staff_taxonomy_changed_review_required");
});
await Case("Office mapped submission reports missing branch", async () => {
    var f = new ConsumerFixture("PURCHASER",StaffWorkLocation.Office); var validation = await f.Forms.ValidateSubmissionAsync(f.Employee.Id,f.Context,default);
    Check(!validation.CanSubmit && validation.MissingFields.Contains("branchId"),"approved office mapping did not require branch");
});
await Case("Site mapped activation requires initial site", async () => {
    var f = new ConsumerFixture();
    await Reject(()=>f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,null,default),"staff_taxonomy_location_required");
    await f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,Guid.NewGuid(),default);
    Check(f.Taxonomy.Calls.Any(c=>c.Code=="DRIVER"&&c.On==f.Profile.JoinedOn),"joined-date mapping was not resolved");
});
await Case("Office mapped activation denies initial site", async () => {
    var f = new ConsumerFixture("PURCHASER",StaffWorkLocation.Office); f.Profile=f.Profile with{BranchId=Guid.NewGuid()}; f.UpdateDraft();
    await Reject(()=>f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,Guid.NewGuid(),default),"staff_taxonomy_location_conflict");
    await f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,null,default);
});
await Case("legacy SITE activation remains optional before publication", async () => {
    var f = new ConsumerFixture("SITE"); await f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,null,default);
});
await Case("approved Site mapped employee receives dated site assignment", async () => {
    var f=new ConsumerFixture();f.Activate();var result=await f.Sites.AssignSiteAsync(f.Employee.Id,new(f.Version,f.Site.Id,new(2026,10,14),"Synthetic assignment"),"driver-site-001",f.Context,default);
    Check(result.EmployeeId==f.Employee.Id&&f.Assignments.Count==1&&f.Assignments[0].StaffCategoryId==f.Profile.StaffCategoryId,"assignment changed taxonomy identity");
    Check(f.Taxonomy.Calls.Any(c=>c.On==new DateOnly(2026,10,14)),"assignment effective date was not resolved");
});
await Case("Office mapped employee cannot receive site assignment",async()=>{
    var f=new ConsumerFixture("PURCHASER",StaffWorkLocation.Office);f.Profile=f.Profile with{BranchId=Guid.NewGuid()};f.UpdateDraft();f.Activate();
    await Reject(()=>f.Sites.AssignSiteAsync(f.Employee.Id,new(f.Version,f.Site.Id,new(2026,10,14),"Synthetic assignment"),"office-site-001",f.Context,default),"employee_not_site_staff");
    Check(f.Assignments.Count==0,"denied assignment staged a row");
});
await Case("proposal pins foundation field authority and rejects same-label replacement",async()=>{
    var f=new ConsumerFixture(fieldOptions:new(){ApprovedVersion="synthetic-fields-v1",EffectiveFrom=new(2026,1,1)});
    var submitted=await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"fields-submit-001",f.Context,default);
    var snapshot=await f.Snapshot(submitted.SubmissionId!.Value);
    Check((string?)snapshot.GetType().GetProperty("FoundationFieldPolicyVersion")!.GetValue(snapshot)=="synthetic-fields-v1","foundation authority was not pinned in proposal");
    f.ReplaceFieldAuthority(new(){ApprovedVersion="synthetic-fields-v2",EffectiveFrom=new(2026,1,1)});
    await Reject(()=>f.ValidateSnapshot(snapshot),"employee_field_policy_changed_review_required");
});
await Case("legacy unpinned snapshot requires fresh proposal after field authority appears",async()=>{
    var f=new ConsumerFixture("SITE");var snapshot=f.UnpinnedSnapshot();
    f.ReplaceFieldAuthority(new(){ApprovedVersion="synthetic-fields-v1",EffectiveFrom=new(2026,1,1)});
    await Reject(()=>f.ValidateSnapshot(snapshot),"employee_field_policy_changed_review_required");
});
await Case("Site mapped activation materializes exact category and supplied site",async()=>{
    var f=new ConsumerFixture();f.Activate();var site=Guid.NewGuid();
    await f.Materializer.MaterializeAsync(f.Employee,f.Profile,null,null,site,"synthetic-approver",new ConsumerClock().GetUtcNow(),default);
    Check(f.Records is { } records&&records.Assignment.StaffCategoryId==f.Profile.StaffCategoryId&&records.Assignment.SiteId==site&&records.Assignment.BranchId is null,"materializer did not preserve reviewed employment shape");
});
await Case("failed Site activation stages no employment graph",async()=>{
    var f=new ConsumerFixture();f.Activate();
    await Reject(()=>f.Materializer.MaterializeAsync(f.Employee,f.Profile,null,null,null,"synthetic-approver",new ConsumerClock().GetUtcNow(),default),"staff_taxonomy_location_required");
    Check(f.Records is null,"failed activation staged records");
});
await Case("past Office joining baseline and now effective first publication approve with fresh dual pins",async()=>{
    var f=new ConsumerFixture("OFFICE");f.Profile=f.Profile with{JoinedOn=new(2026,10,12),BranchId=Guid.NewGuid()};f.UpdateDraft();
    var source=Published(new(2026,10,13),1);f.SetTimeline([source]);
    var submitted=await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"office-first-publish-001",f.Context,default);
    var snapshot=await f.Snapshot(submitted.SubmissionId!.Value);
    Check((await f.ValidateSnapshot(snapshot)).CanSubmit,"fresh Office dual pin rejected");
    var historical=(EmployeeHiringTaxonomyEvidence)snapshot.GetType().GetProperty("Taxonomy")!.GetValue(snapshot)!;
    var activation=(EmployeeHiringTaxonomyEvidence)snapshot.GetType().GetProperty("ActivationTaxonomy")!.GetValue(snapshot)!;
    Check(historical.Baseline&&historical.PolicyVersionId is null&&activation.PolicyVersionId==source.Version.Id&&!activation.Baseline,"joining history overwritten or current publication unpinned");
    await f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,null,default);
});
await Case("past Site joining baseline and now effective publication retain optional activation site",async()=>{
    var f=new ConsumerFixture("SITE");f.Profile=f.Profile with{JoinedOn=new(2026,10,12)};f.UpdateDraft();f.SetTimeline([Published(new(2026,10,13),1)]);
    var submitted=await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"site-first-publish-001",f.Context,default);
    Check((await f.ValidateSnapshot(await f.Snapshot(submitted.SubmissionId!.Value))).CanSubmit,"fresh Site dual pin rejected");
    await f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,null,default);
});
await Case("fresh Office proposal may pin different unchanged-semantic successor identity",async()=>{
    var f=new ConsumerFixture("OFFICE");f.Profile=f.Profile with{JoinedOn=new(2026,10,11),BranchId=Guid.NewGuid()};f.UpdateDraft();
    var first=Published(new(2026,10,10),1);var second=Published(new(2026,10,13),2,first.Policy);f.SetTimeline([first,second]);
    var submitted=await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"office-successor-001",f.Context,default);var snapshot=await f.Snapshot(submitted.SubmissionId!.Value);
    Check((await f.ValidateSnapshot(snapshot)).CanSubmit,"unchanged-semantic successor rejected");
    Check(((EmployeeHiringTaxonomyEvidence)snapshot.GetType().GetProperty("Taxonomy")!.GetValue(snapshot)!).PolicyVersionId==first.Version.Id,"joined identity not retained");
    Check(((EmployeeHiringTaxonomyEvidence)snapshot.GetType().GetProperty("ActivationTaxonomy")!.GetValue(snapshot)!).PolicyVersionId==second.Version.Id,"current successor not pinned");
    await f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,null,default);
});
await Case("frozen current activation identity change requires fresh review",async()=>{
    var f=new ConsumerFixture("OFFICE");f.Profile=f.Profile with{JoinedOn=new(2026,10,11),BranchId=Guid.NewGuid()};f.UpdateDraft();
    var first=Published(new(2026,10,10),1);f.SetTimeline([first]);
    var submitted=await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"office-stale-current-001",f.Context,default);var snapshot=await f.Snapshot(submitted.SubmissionId!.Value);
    var second=Published(new(2026,10,13),2,first.Policy);f.SetTimeline([first,second]);
    await Reject(()=>f.ValidateSnapshot(snapshot),"employee_staff_taxonomy_changed_review_required");
});
await Case("unchanged activation version remains reviewable on next business day",async()=>{
    var f=new ConsumerFixture("OFFICE");f.Profile=f.Profile with{JoinedOn=new(2026,10,12),BranchId=Guid.NewGuid()};f.UpdateDraft();f.SetTimeline([Published(new(2026,10,13),1)]);
    var submitted=await f.Forms.SubmitAsync(f.Employee.Id,new(f.Version),"office-next-day-001",f.Context,default);var snapshot=await f.Snapshot(submitted.SubmissionId!.Value);f.AdvanceDay();
    Check((await f.ValidateSnapshot(snapshot)).CanSubmit,"activation evidence expired solely because the business date changed");
});
await Case("old snapshot without activation pin needs new submission after publication",async()=>{
    var f=new ConsumerFixture("OFFICE");f.Profile=f.Profile with{JoinedOn=new(2026,10,12),BranchId=Guid.NewGuid()};f.UpdateDraft();var snapshot=f.UnpinnedSnapshot();f.SetTimeline([Published(new(2026,10,13),1)]);
    await Reject(()=>f.ValidateSnapshot(snapshot),"employee_staff_taxonomy_changed_review_required");
});
await Case("semantic location change cannot implicitly convert initial employment",async()=>{
    var f=new ConsumerFixture("PURCHASER",StaffWorkLocation.Office);f.Profile=f.Profile with{JoinedOn=new(2026,10,12),BranchId=Guid.NewGuid()};f.UpdateDraft();
    var history=f.Taxonomy.Resolution;var site=history with{PolicyVersionId=Guid.NewGuid(),Fingerprint=new string('D',64),Mapping=history.Mapping! with{WorkLocation=StaffWorkLocation.Site,ScheduleBasis=StaffScheduleBasis.Site,RequireSiteBeforeActivation=true}};
    f.Taxonomy.OnDate=date=>date<new DateOnly(2026,10,13)?history:site;
    await Reject(()=>f.Materializer.ValidateAsync(f.Employee,f.Profile,null,null,Guid.NewGuid(),default),"employee_staff_taxonomy_location_changed_review_required");
    Check(f.Records is null,"location conversion staged employment graph");
});
Console.WriteLine($"HR consumer checks: {pass} passed; {failures.Count} failed. Synthetic collaborators; no SQL/provider/host/account creation.");
if(failures.Count!=0) Environment.Exit(1);

StaffTaxonomyVersionSource Published(DateOnly from,int number,Policy? policy=null){
    policy??=new(StaffTaxonomyRulesCodec.PolicyCode,"Synthetic taxonomy",PolicyCategory.EmployeeException);
    var references=Enum.GetValues<StaffTaxonomyPolicyUse>().Select(use=>new StaffTaxonomyPolicyReference(use,StaffTaxonomyPolicyApplicability.NotApplicable,null)).ToArray();
    var rules=new StaffTaxonomyRules(1,[new("OFFICE","Office",StaffWorkLocation.Office,StaffScheduleBasis.Office,false,false,references),new("SITE","Site",StaffWorkLocation.Site,StaffScheduleBasis.Site,false,true,references)]);
    var version=new PolicyVersion(policy.Id,number,from,null,StaffTaxonomyRulesCodec.Schema,StaffTaxonomyRulesCodec.Canonicalize(rules),"synthetic-private",Guid.NewGuid(),"synthetic-independent-admin",new ConsumerClock().GetUtcNow());
    return new(policy,version,null,[],true);
}

public class ConsumerProxy : DispatchProxy {
    public Func<MethodInfo,object?[]?,object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
    public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var value=Create<T,ConsumerProxy>();((ConsumerProxy)(object)value).Handler=handler;return value;}
}
sealed class ConsumerClock:TimeProvider { public DateTimeOffset Now=new(2026,10,13,7,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now; }
sealed class ConsumerCurrent:ICurrentAuthentication { public AuthenticatedAccount Require()=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true); }
sealed class ConsumerTaxonomy:IStaffTaxonomyResolver {
    public StaffTaxonomyResolution Resolution; public Func<DateOnly,StaffTaxonomyResolution>? OnDate;public List<(string Code,DateOnly On)> Calls=[];
    public ConsumerTaxonomy(string code,StaffWorkLocation location){Resolution=code is "OFFICE" or "SITE"?StaffTaxonomyResolver.Resolve(code,new(2026,10,13),[]):new("effective",new(code,"Same human label",location,location==StaffWorkLocation.Site?StaffScheduleBasis.Site:StaffScheduleBasis.Office,location==StaffWorkLocation.Site,location==StaffWorkLocation.Site,Enum.GetValues<StaffTaxonomyPolicyUse>().Select(u=>new StaffTaxonomyPolicyReference(u,StaffTaxonomyPolicyApplicability.NotApplicable,null)).ToArray()),Guid.NewGuid(),new string('A',64));}
    public Task<StaffTaxonomyResolution> ResolveAsync(string code,DateOnly on,CancellationToken ct){Calls.Add((code,on));return Task.FromResult(OnDate?.Invoke(on)??Resolution);}
    public Task<StaffTaxonomyResolution> ResolveForRangeAsync(string code,DateOnly from,DateOnly to,CancellationToken ct)=>ResolveAsync(code,from,ct);
}
sealed class ConsumerStore:IPrivateFileStore {
    public Dictionary<string,(string Purpose,byte[] Bytes)> Objects=[];
    public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct){var key=Guid.NewGuid().ToString("N");Objects[key]=(purpose,bytes.ToArray());return Task.FromResult(new PrivateFileReference(key,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic-key",bytes.Length));}
    public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct){var item=Objects[reference.StorageKey];if(item.Purpose!=purpose)throw new Exception("wrong purpose");return Task.FromResult(item.Bytes.ToArray());}
}
sealed class ConsumerFixture {
    public Employee Employee;public EmployeeProfileInput Profile;public EmployeeHiringDraft Draft; public ConsumerStore Store=new();public ConsumerTaxonomy Taxonomy;
    public EmployeeHiringService Forms;public EmployeeApprovalMaterializer Materializer;public EmployeeSiteAssignmentService Sites;public OrganizationMasterService Organization;
    public Site Site=new("SYNTHETIC","Synthetic site","Synthetic address"); public List<EmployeeAssignment> Assignments=[]; public OrganizationMasterResponse? Master;
    readonly List<EmployeeDocument> documents=[];readonly List<EmployeeSubmission> submissions=[];readonly ConsumerClock clock=new();readonly EmployeeFieldPolicy fields;object?[] formArguments=[];public EmployeeApprovalRecords? Records;
    public string Version=>Convert.ToBase64String(new byte[8]);public AuthRequestContext Context=new("127.0.0.1","synthetic-hr-consumer"); public string Code;
    public ConsumerFixture(string code="DRIVER",StaffWorkLocation location=StaffWorkLocation.Site,EmployeeFieldPolicyOptions? fieldOptions=null){
        fields=new(fieldOptions??new());
        Code=code;Taxonomy=new(code,location);Employee=new(clock.GetUtcNow(),"synthetic-actor");Draft=new(Employee.Id);Set(Employee,"RowVersion",new byte[8]);
        Profile=new(){FullLegalName="Synthetic employee",IdentityDocumentType=IdentityDocumentType.Cnic,IdentityDocumentNumber="1234512345671",DateOfBirth=new(1990,1,1),PersonalPhone="+923001234567",JoinedOn=new(2026,10,13),DepartmentId=Guid.NewGuid(),DesignationId=Guid.NewGuid(),StaffCategoryId=Guid.NewGuid(),EmploymentTypeId=Guid.NewGuid(),ReportingManagerEmployeeId=Guid.NewGuid(),CurrentAddress=new(){Line1="Synthetic",City="Synthetic city",Province="Synthetic province"},PermanentAddress=new(){Line1="Synthetic",City="Synthetic city",Province="Synthetic province"},EmergencyContact=new(){Name="Synthetic contact",Relationship="Synthetic",Phone="+923001234568"}};
        foreach(var type in new[]{EmployeeDocumentType.Identity,EmployeeDocumentType.ProfilePhoto}){var doc=new EmployeeDocument(Employee.Id,type,"synthetic", "synthetic.png","image/png",1,new string('A',64),"synthetic-actor",clock.GetUtcNow());doc.RecordScan(DocumentScanStatus.Clean,true,"synthetic-scanner",clock.GetUtcNow());doc.RecordVerification(DocumentVerificationStatus.Verified,"synthetic-reviewer","Synthetic verification",clock.GetUtcNow());Set(doc,"RowVersion",new byte[8]);documents.Add(doc);}
        Profile=Profile with{DocumentIds=documents.Select(d=>d.Id).ToArray()};UpdateDraft();
        var hiring=ConsumerProxy.Make<IEmployeeHiringRepository>((m,a)=>m.Name switch{
            "FindAsync"=>Task.FromResult<Employee?>(Employee),"FindDraftAsync"=>Task.FromResult<EmployeeHiringDraft?>(Draft),"DocumentsAsync"=>Task.FromResult<IReadOnlyList<EmployeeDocument>>(documents),"StaffCategoryCodeAsync"=>Task.FromResult<string?>(Code),
            "FindSubmissionAsync"=>Task.FromResult(submissions.SingleOrDefault(s=>s.Id==(Guid)a![1]!)),"ValidateReferencesAsync" or "ValidateUniqueAsync" or "SaveChangesAsync"=>Task.CompletedTask,"FindReceiptAsync"=>Task.FromResult<IdempotencyReceipt?>(null),"AddSubmission"=>AddSubmission((EmployeeSubmission)a![0]!),"ExpectVersion" or "AddAudit" or "AddReceipt"=>null,_=>throw new Exception("unexpected hiring "+m.Name)});
        var grants=new[]{EmployeePermissions.Read,EmployeePermissions.Submit,EmployeePermissions.Review,EmployeePermissions.Edit,Permissions.OrganizationManage}.Select(p=>new PermissionGrant(p,true,Guid.NewGuid(),AccessScopeKind.All,null)).ToArray();
        var access=ConsumerProxy.Make<IAccountAccessReader>((m,a)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(grants));
        var auth=ConsumerProxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch{"InTransactionAsync"=>((Delegate)a![0]!).DynamicInvoke(a[1]),"IsCurrentAdministratorAsync" or "IsCurrentSystemRoleAssignmentAsync"=>Task.FromResult(true),"GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("Admin"),"FindAssignmentAsync"=>Task.FromResult<UserRoleAssignment?>((UserRoleAssignment)Activator.CreateInstance(typeof(UserRoleAssignment),true)!),_=>throw new Exception("unexpected auth "+m.Name)});
        var operation=new EmployeeOperation(new ConsumerCurrent(),access,auth,hiring,clock,Store);
        var approval=ConsumerProxy.Make<IEmployeeApprovalRepository>((m,a)=>m.Name switch{"ValidateAssetReturnReceiversAsync" or "ValidateInitialSiteAsync"=>Task.CompletedTask,"HasInitialRecordsAsync"=>Task.FromResult(false),"AddInitialRecords"=>RecordGraph((EmployeeApprovalRecords)a![0]!),_=>throw new Exception("unexpected approval "+m.Name)});
        var phones=ConsumerProxy.Make<IPhoneNumberNormalizer>((m,a)=>a![0]);var options=new MobileAuthenticationOptions(){BusinessTimeZoneId="Asia/Karachi"};
        formArguments=[operation,hiring,Store,options,clock,approval,phones,fields];Forms=Construct<EmployeeHiringService>(formArguments,Taxonomy);
        Materializer=Construct<EmployeeApprovalMaterializer>([approval,hiring,Store,options,clock,fields],Taxonomy);
        var siteRepo=ConsumerProxy.Make<IEmployeeSiteAssignmentRepository>((m,a)=>m.Name switch{"FindEmployeeAsync"=>Task.FromResult<Employee?>(Employee),"HasApprovedDecisionAsync" or "HasEligibleEmploymentHistoryAsync"=>Task.FromResult(true),"OpenAssignmentsAsync"=>Task.FromResult<IReadOnlyList<EmployeeAssignment>>([new(Employee.Id,Profile.DepartmentId!.Value,Profile.DesignationId!.Value,Profile.StaffCategoryId!.Value,Profile.EmploymentTypeId!.Value,Profile.BranchId,null,Profile.ReportingManagerEmployeeId,new(2026,10,13),null,"synthetic-actor",clock.GetUtcNow())]),"HasConflictingAssignmentAsync"=>Task.FromResult(false),"ActiveStaffCategoryCodeAsync"=>Task.FromResult<string?>(Code),"FindSiteAsync"=>Task.FromResult<Site?>(Site),"ExpectVersion"=>null,"SaveChangesAsync"=>Task.CompletedTask,"AddAssignment"=>AddAssignment((EmployeeAssignment)a![0]!),_=>throw new Exception("unexpected sites "+m.Name)});
        Sites=Construct<EmployeeSiteAssignmentService>([siteRepo,operation,options],Taxonomy);
        var orgRepo=ConsumerProxy.Make<IOrganizationRepository>((m,a)=>m.Name switch{"FindReceiptAsync"=>Task.FromResult<IdempotencyReceipt?>(null),"CreateAsync"=>Task.FromResult(new OrganizationMasterResponse(Guid.NewGuid(),((CreateOrganizationMasterRequest)a![1]!).Code,((CreateOrganizationMasterRequest)a[1]!).Name,true,Version)),"GetAsync"=>Task.FromResult(Master),"SetStatusAsync"=>Task.FromResult(Master! with{IsActive=((SetOrganizationStatusRequest)a![2]!).IsActive!.Value}),"AddChange"=>null,"SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception("unexpected org "+m.Name)});
        Organization=new(orgRepo,new OrganizationOperation(new ConsumerCurrent(),access,auth,orgRepo,clock));
    }
    public void UpdateDraft(){Employee.UpdateDraft(EmployeeFormValidation.ToDomain(Profile));var json=JsonSerializer.SerializeToUtf8Bytes(Profile);var reference=Store.WriteAsync(json,$"employee/{Employee.Id:N}/profile",default).Result;Draft.SetProfile(JsonSerializer.Serialize(reference),Profile.DepartmentId,Profile.DesignationId,Profile.StaffCategoryId,Profile.EmploymentTypeId,Profile.BranchId,Profile.ReportingManagerEmployeeId,Profile.ProposedShiftId);}
    public void Activate(){Set(Employee,"Status",EmployeeStatus.Active);Set(Employee,"OnboardingStatus",OnboardingStatus.Approved);Set(Employee,"ApprovedOnboardingDecisionId",Guid.NewGuid());}
    public void SetTimeline(IReadOnlyList<StaffTaxonomyVersionSource> sources)=>Taxonomy.OnDate=date=>StaffTaxonomyResolver.Resolve(Code,date,sources,true);
    public void AdvanceDay()=>clock.Now=clock.Now.AddDays(1);
    public void ReplaceFieldAuthority(EmployeeFieldPolicyOptions options){formArguments[7]=new EmployeeFieldPolicy(options);Forms=Construct<EmployeeHiringService>(formArguments,Taxonomy);}
    public async Task<object> Snapshot(Guid id){var method=typeof(EmployeeHiringService).GetMethod("SubmissionAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;var task=(Task)method.Invoke(Forms,[Employee.Id,id,CancellationToken.None])!;await task;var value=task.GetType().GetProperty("Result")!.GetValue(task)!;return value.GetType().GetField("Item2")!.GetValue(value)!;}
    public object UnpinnedSnapshot(){var type=typeof(EmployeeHiringService).GetNestedType("HiringSnapshot",BindingFlags.NonPublic)!;var constructor=type.GetConstructors().Single();object?[] args=[Employee.Id,Employee.EmployeeNumber,Employee.DraftRevision,Profile,null,null,documents.Select(d=>new EmployeeDocumentResponse(d.Id,d.DocumentType,d.OriginalFileName,d.ContentType,d.SizeBytes,d.IssuedOn,d.ExpiresOn,d.ScanStatus,d.VerificationStatus,d.IsAvailable,Version,d.Privacy)).ToArray()];if(constructor.GetParameters().Length>args.Length)args=args.Concat(new object?[constructor.GetParameters().Length-args.Length]).ToArray();return constructor.Invoke(args);}
    public async Task<EmployeeSubmissionValidation> ValidateSnapshot(object snapshot){var method=typeof(EmployeeHiringService).GetMethod("ValidateSubmissionCoreAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;object?[] args=[Employee.Id,snapshot,CancellationToken.None];if(method.GetParameters().Length>args.Length)args=[..args,true];return await (Task<EmployeeSubmissionValidation>)method.Invoke(Forms,args)!;}
    object? AddSubmission(EmployeeSubmission s){submissions.Add(s);return null;}object? AddAssignment(EmployeeAssignment a){Assignments.Add(a);return null;}object? RecordGraph(EmployeeApprovalRecords records){Records=records;return null;}
    static T Construct<T>(object?[] args,IStaffTaxonomyResolver taxonomy){var constructor=typeof(T).GetConstructors().Single();if(constructor.GetParameters().Length>args.Length)args=[..args,taxonomy];return (T)constructor.Invoke(args);}
    static void Set(object value,string property,object data)=>value.GetType().GetProperty(property)!.SetValue(value,data);
}

