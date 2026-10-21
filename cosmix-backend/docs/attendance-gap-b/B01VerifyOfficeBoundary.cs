#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.CompanySettings;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.Organization;

// Real organization authorization/replay boundary with synthetic repository adapters.
// No SQL, genuine accounts, network or device/biometric acceptance.
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; }
var type = typeof(OfficeAttendanceSource).Assembly.GetType("Cosmix.Application.Attendance.Offices.OfficeBoundaryService");
Check(type is not null, "office boundary has an authorized versioned publication workflow");
var f = new OfficeBoundaryFixture();
dynamic service = Activator.CreateInstance(type!, f.Operation, f.Repository, f.Current, f.Clock)!;
async Task Fail(Func<Task> call, string code)
{
    try { await call(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; }
    throw new Exception("FAIL: expected " + code);
}
CreateOfficeBoundaryRequest Input() => new(new(2026,10,14),24m,67m,100m,20m,
    Convert.ToBase64String(f.Branch.RowVersion),0,true,"PK-SD","SYNTHETIC only","Synthetic approved boundary");
f.SuperAdmin = false;
await Fail(async()=>await service.PublishAsync(f.Branch.Id,Input(),"office-boundary-001",f.Context,CancellationToken.None),"access_denied");
f.SuperAdmin = true; f.Actor = f.Actor with { MfaSatisfied = false };
await Fail(async()=>await service.PublishAsync(f.Branch.Id,Input(),"office-boundary-001",f.Context,CancellationToken.None),"access_denied");
f.Actor = f.Actor with { MfaSatisfied = true };
var input=Input();
OfficeBoundaryVersionResponse first=await service.PublishAsync(f.Branch.Id,input,"office-boundary-001",f.Context,CancellationToken.None);
Check(first.BranchId==f.Branch.Id && first.VersionNumber==1 && first.JurisdictionCode=="PK-SD" && f.Versions.Count==1,
    "office version retains exact configured geometry and jurisdiction");
OfficeBoundaryVersionResponse retry=await service.PublishAsync(f.Branch.Id,input,"office-boundary-001",f.Context,CancellationToken.None);
Check(first==retry && f.Audits.Count==1 && f.Receipts.Count==1,"exact retry is one audited publication");
await Fail(async()=>await service.PublishAsync(f.Branch.Id,input with {RadiusMeters=101m},"office-boundary-001",f.Context,CancellationToken.None),"idempotency_key_reused");
await Fail(async()=>await service.PublishAsync(f.Branch.Id,input,"office-stale-version",f.Context,CancellationToken.None),"office_boundary_version_conflict");
await Fail(async()=>await service.PublishAsync(f.Branch.Id,input with{EffectiveFrom=new(2026,10,13)},"office-retroactive",f.Context,CancellationToken.None),"office_boundary_requires_future_date");
await Fail(async()=>await service.PublishAsync(f.Branch.Id,input with{RadiusMeters=0},"office-invalid-radius",f.Context,CancellationToken.None),"invalid_office_boundary_values");
await Fail(async()=>await service.PublishAsync(f.Branch.Id,input with{ManagementApproved=false},"office-unapproved",f.Context,CancellationToken.None),"office_boundary_approval_required");
f.SuperAdmin=false;
await Fail(async()=>await service.PublishAsync(f.Branch.Id,input,"office-boundary-001",f.Context,CancellationToken.None),"access_denied");
f.SuperAdmin=true;f.Locked=true;
await Fail(async()=>await service.PublishAsync(f.Branch.Id,input with{ExpectedLatestVersionNumber=1,EffectiveFrom=new(2026,10,15)},"office-locked-boundary",f.Context,CancellationToken.None),"attendance_period_locked");
f.Locked=false;
OfficeBoundaryVersionResponse second=await service.PublishAsync(f.Branch.Id,input with{ExpectedLatestVersionNumber=1,EffectiveFrom=new(2026,10,15),RadiusMeters=150m},"office-boundary-002",f.Context,CancellationToken.None);
OfficeBoundaryConfigurationResponse historical=await service.ReadAsync(f.Branch.Id,new DateOnly(2026,10,14),CancellationToken.None);
Check(historical.Boundary!.Id==first.Id && second.Id!=first.Id && f.Versions.Count==2,"later boundary does not rewrite historical version");
Check(f.Changes.Count==f.Audits.Count && f.Audits.Count==f.Receipts.Count,"boundary audit and receipt commit together");
var jsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
foreach(var omitted in new[] { "latitude","longitude","radiusMeters","maximumAccuracyMeters" })
{
    var body=JsonSerializer.SerializeToNode(input,jsonOptions)!.AsObject(); body.Remove(omitted);
    try { JsonSerializer.Deserialize<CreateOfficeBoundaryRequest>(body.ToJsonString(),jsonOptions); }
    catch(JsonException) { checks++; continue; }
    throw new Exception("FAIL: explicit JSON presence required for " + omitted);
}
var zeroCoordinates=JsonSerializer.Deserialize<CreateOfficeBoundaryRequest>(JsonSerializer.Serialize(
    input with { Latitude=0,Longitude=0,EffectiveFrom=new(2026,10,16),ExpectedLatestVersionNumber=2 },jsonOptions),jsonOptions)!;
OfficeBoundaryVersionResponse explicitZero=await service.PublishAsync(f.Branch.Id,zeroCoordinates,"office-explicit-zero",f.Context,CancellationToken.None);
Check(explicitZero.Latitude==0 && explicitZero.Longitude==0,"explicit approved zero coordinates remain different from omitted coordinates");
Console.WriteLine($"PASS: {checks} office boundary service checks; synthetic IO only.");

public class OfficeBoundaryProxy:DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args??[]);
    public static T Make<T>(Func<MethodInfo,object?[],object?> handler)where T:class
    {var p=Create<T,OfficeBoundaryProxy>();((OfficeBoundaryProxy)(object)p).Handler=handler;return p;}
}
public sealed class OfficeBoundaryClock:TimeProvider
{public override DateTimeOffset GetUtcNow()=>new(2026,10,13,5,0,0,TimeSpan.Zero);}
public sealed class OfficeBoundaryFixture
{
    public Branch Branch=new("SYNTH-OFFICE","Synthetic office");
    public AuthenticatedAccount Actor=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
    public bool SuperAdmin=true,Locked;
    public OfficeBoundaryClock Clock=new();
    public AuthRequestContext Context=new("127.0.0.1","synthetic-office-boundary");
    public List<OfficeBoundaryVersionResponse> Versions=[];
    public List<OrganizationChange> Changes=[];public List<AuditEvent> Audits=[];public List<IdempotencyReceipt> Receipts=[];
    public ICurrentAuthentication Current;public IOfficeBoundaryRepository Repository;public OrganizationOperation Operation;
    public OfficeBoundaryFixture()
    {
        typeof(Branch).GetProperty(nameof(Branch.RowVersion))!.SetValue(Branch,BitConverter.GetBytes(1L));
        Current=OfficeBoundaryProxy.Make<ICurrentAuthentication>((_,_)=>Actor);
        var access=OfficeBoundaryProxy.Make<IAccountAccessReader>((_,_)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(
            CompanySettingsPermissions.Definitions.Select(p=>new PermissionGrant(p.Code,p.Sensitive,Guid.NewGuid(),AccessScopeKind.All,null)).ToArray()));
        var auth=OfficeBoundaryProxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch{
            "InTransactionAsync"=>((Delegate)a[0]!).DynamicInvoke(a[1]),"IsCurrentAdministratorAsync"=>Task.FromResult(true),
            "IsCurrentSuperAdminAsync"=>Task.FromResult(SuperAdmin),"GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>(SuperAdmin?"SuperAdmin":"Admin"),_=>throw new Exception(m.Name)});
        var org=OfficeBoundaryProxy.Make<IOrganizationRepository>((m,a)=>m.Name switch{
            "FindReceiptAsync"=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==(string)a[0]!&&x.Operation==(string)a[1]!&&x.Key==(string)a[2]!)),
            "FindChangeAsync"=>Task.FromResult(Changes.SingleOrDefault(x=>x.Id==(Guid)a[0]!)),"AddChange"=>Add(a),"SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception(m.Name)});
        Repository=OfficeBoundaryProxy.Make<IOfficeBoundaryRepository>((m,a)=>m.Name switch{
            "FindBranchAsync"=>Task.FromResult<Branch?>((Guid)a[0]! == Branch.Id?Branch:null),"VersionsAsync"=>Task.FromResult<IReadOnlyList<OfficeBoundaryVersionResponse>>(Versions.ToArray()),
            "AnyLockedFromAsync"=>Task.FromResult(Locked),"AddAsync"=>Append((OfficeBoundaryVersionResponse)a[0]!),_=>throw new Exception(m.Name)});
        Operation=new(Current,access,auth,org,Clock);
    }
    private Task Append(OfficeBoundaryVersionResponse v){Versions.Add(v);return Task.CompletedTask;}
    private object? Add(object?[] a){var c=(OrganizationChange)a[0]!;var audit=(AuditEvent)a[1]!;
        typeof(OrganizationChange).GetProperty(nameof(OrganizationChange.AuditEvent))!.SetValue(c,audit);Changes.Add(c);Audits.Add(audit);Receipts.Add((IdempotencyReceipt)a[2]!);return null;}
}
