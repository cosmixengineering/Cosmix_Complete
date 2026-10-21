using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.StaffCategories;

var passed=0; var failed=new List<string>();
void Check(bool value,string message) {if(!value)throw new Exception(message);}
async Task Case(string name,Func<Task> action) {try {await action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed.Add(name+": "+e.Message);Console.WriteLine("FAIL "+failed[^1]);}}
async Task Deny(Func<Task> action,string? code=null) {try{await action();throw new Exception("expected denial");}catch(AuthenticationFailure e){Check(code is null||e.Code==code,"unexpected denial "+e.Code);}}
await Case("approved broader Office category routes to Office roster with exact pin",async()=>{var f=new Fixture("PURCHASER",true);var r=await f.Employee().ResolveAsync(f.EmployeeId,f.Date,default);Check(r.BranchId==f.Branch.Id&&r.SiteId is null,"not Office");f.CheckPin(r);});
await Case("approved broader Site category routes to Site roster with exact pin",async()=>{var f=new Fixture("DRIVER",false);var r=await f.Employee().ResolveAsync(f.EmployeeId,f.Date,default);Check(r.SiteId==f.SiteId&&r.BranchId is null,"not Site");f.CheckPin(r);});
await Case("direct Office resolver supports approved Office category",async()=>{var f=new Fixture("PURCHASER",true);f.CheckPin(await f.Office().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("direct Site resolver supports approved Site category",async()=>{var f=new Fixture("DRIVER",false);f.CheckPin(await f.Site().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("metadata alone cannot schedule arbitrary category",async()=>{var f=new Fixture("DRIVER",false);f.Taxonomy.Available=false;await Deny(()=>f.Employee().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("unavailable publication suppresses legacy Office fallback",async()=>{var f=new Fixture("OFFICE",true);f.Taxonomy.Available=false;await Deny(()=>f.Office().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("semantic mismatch cannot route Office location through Site mapping",async()=>{var f=new Fixture("PURCHASER",true);f.Taxonomy.Office=false;await Deny(()=>f.Employee().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("overnight mapping change denies Office duty",async()=>{var f=new Fixture("PURCHASER",true,true);f.Taxonomy.RangeChange=true;await Deny(()=>f.Office().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("overnight mapping change denies Site duty",async()=>{var f=new Fixture("DRIVER",false,true);f.Taxonomy.RangeChange=true;await Deny(()=>f.Site().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("same-day source reinspection rejects taxonomy change during roster IO",async()=>{var f=new Fixture("PURCHASER",true);f.OnRead=()=>f.Taxonomy.Hash=new string('B',64);await Deny(()=>f.Office().ResolveAsync(f.EmployeeId,f.Date,default),"staff_taxonomy_schedule_source_changed");});
await Case("same-day source reinspection rejects membership change during roster IO",async()=>{var f=new Fixture("PURCHASER",true);f.OnRead=()=>f.ReplaceMembership();await Deny(()=>f.Office().ResolveAsync(f.EmployeeId,f.Date,default),"roster_office_relationship_changed");});
await Case("legacy Office baseline JSON has no taxonomy property",async()=>{var f=new Fixture("OFFICE",true);f.Taxonomy.Baseline=true;var r=await f.Office().ResolveAsync(f.EmployeeId,f.Date,default);Check(!JsonSerializer.Serialize(r).Contains("Taxonomy",StringComparison.Ordinal),"legacy bytes changed");});
await Case("published existing Office category freezes actual version identity",async()=>{var f=new Fixture("OFFICE",true);f.CheckPin(await f.Office().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("Office reader recognizes approved broader Office membership",async()=>{var f=new Fixture("PURCHASER",true);Check(await f.Reader().CurrentMembershipMatchesAsync(f.EmployeeId,f.Branch.Id,f.Membership.Id,f.Date,default),"mapped membership denied");});
await Case("Office reader rejects unavailable taxonomy for legacy membership",async()=>{var f=new Fixture("OFFICE",true);f.Taxonomy.Available=false;Check(!await f.Reader().CurrentMembershipMatchesAsync(f.EmployeeId,f.Branch.Id,f.Membership.Id,f.Date,default),"unavailable membership accepted");});
await Case("Office reader retains exact published roster proof",async()=>{var f=new Fixture("OFFICE",true);var r=await f.Office().ResolveAsync(f.EmployeeId,f.Date,default);f.Taxonomy.Hash=new string('C',64);await Deny(()=>f.Reader().ReadAsync(f.EmployeeId,f.Date,r,null,default));});
await Case("direct Site resolver rejects Office category even when location is Site",async()=>{var f=new Fixture("OFFICE",false);f.Taxonomy.Office=true;await Deny(()=>f.Site().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("old Office fixtures without resolver retain confirmed baseline",async()=>{var f=new Fixture("OFFICE",true);var r=await new OfficeRosterResolver(f.Offices,TimeProvider.System).ResolveAsync(f.EmployeeId,f.Date,default);Check(r.BranchId==f.Branch.Id,"legacy fixture denied");});
await Case("old Site fixtures without resolver retain confirmed baseline",async()=>{var f=new Fixture("SITE",false);var r=await new SiteRosterResolver(f.Sites,f.Employees).ResolveAsync(f.EmployeeId,f.Date,default);Check(r.SiteId==f.SiteId&&!JsonSerializer.Serialize(r).Contains("Taxonomy",StringComparison.Ordinal),"legacy Site bytes changed");});
await Case("same-day Site reinspection rejects taxonomy change",async()=>{var f=new Fixture("DRIVER",false);f.OnRead=()=>f.Taxonomy.Hash=new string('B',64);await Deny(()=>f.Site().ResolveAsync(f.EmployeeId,f.Date,default),"staff_taxonomy_schedule_source_changed");});
await Case("same-day Site reinspection rejects changed assignment",async()=>{var f=new Fixture("DRIVER",false);f.OnRead=()=>f.ReplaceMembership();await Deny(()=>f.Site().ResolveAsync(f.EmployeeId,f.Date,default),"roster_site_relationship_changed");});
await Case("stable overnight published Office taxonomy is retained",async()=>{var f=new Fixture("PURCHASER",true,true);f.CheckPin(await f.Office().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("stable overnight published Site taxonomy is retained",async()=>{var f=new Fixture("DRIVER",false,true);f.CheckPin(await f.Site().ResolveAsync(f.EmployeeId,f.Date,default));});
await Case("broader category cannot be authorized by null resolver",async()=>{var f=new Fixture("PURCHASER",true);await Deny(()=>new OfficeRosterResolver(f.Offices,TimeProvider.System).ResolveAsync(f.EmployeeId,f.Date,default),"staff_taxonomy_category_not_configured");});
await Case("broader category cannot inherit null retained source",()=>{Check(!RosterStaffTaxonomy.Valid(null,"PURCHASER",StaffWorkLocation.Office,StaffScheduleBasis.Office),"unproven source accepted");return Task.CompletedTask;});
await Case("published baseline category cannot inherit null retained source",async()=>{var f=new Fixture("OFFICE",true);var mapping=await f.Taxonomy.ResolveAsync("OFFICE",f.Date,default);Check(!RosterStaffTaxonomy.Matches("OFFICE",mapping,null),"publication converted to baseline");});
await Case("source cannot swap category code while retaining taxonomy fingerprint",async()=>{var f=new Fixture("PURCHASER",true);var r=await f.Office().ResolveAsync(f.EmployeeId,f.Date,default);Check(!RosterStaffTaxonomy.Matches("DRIVER",await f.Taxonomy.ResolveAsync("PURCHASER",f.Date,default),r.Taxonomy),"category swap accepted");});
await Case("Office reader denies unpinned published broader roster",async()=>{var f=new Fixture("PURCHASER",true);var r=await f.Office().ResolveAsync(f.EmployeeId,f.Date,default);await Deny(()=>f.Reader().ReadAsync(f.EmployeeId,f.Date,r with{Taxonomy=null},null,default),"staff_taxonomy_schedule_source_changed");});
await Case("Office membership source reinspection catches assignment replacement during policy IO",async()=>{var f=new Fixture("OFFICE",true);var r=await f.Office().ResolveAsync(f.EmployeeId,f.Date,default);var reads=0;f.Taxonomy.OnResolve=()=>{if(++reads==3)f.ReplaceMembership();};await Deny(()=>f.Reader().ReadAsync(f.EmployeeId,f.Date,r,null,default),"office_membership_source_changed");});
await Case("legacy taxonomy cannot resolve an empty duty interval",async()=>{var f=new Fixture("OFFICE",true);await Deny(()=>RosterStaffTaxonomy.ResolveAsync(null,"OFFICE",f.Date,f.Date,default),"invalid_staff_taxonomy_dates");});
Console.WriteLine($"RESULT {passed} passed, {failed.Count} failed"); if(failed.Count>0)Environment.ExitCode=1;

sealed class Fixture
{
 public DateOnly Date=new(2026,10,13); public Employee EmployeeEntity=new(DateTimeOffset.UtcNow,"synthetic"); public Guid EmployeeId=>EmployeeEntity.Id;
 public Branch Branch=new("SYNTHETIC","Synthetic Office"); public Guid SiteId=Guid.NewGuid(); public EmployeeAssignment Membership=null!;
 public Taxonomy Taxonomy; public IOfficeRosterRepository Offices;public ISiteRosterRepository Sites;public IRosterRepository Employees;public IOfficeBoundaryRepository Boundaries;
 public Action? OnRead;
 private readonly string code;private readonly bool office;
 public Fixture(string code,bool office,bool overnight=false)
 {
  this.code=code;this.office=office;Taxonomy=new(code,office);ReplaceMembership();
  var shift=Guid.NewGuid();var version=new ShiftVersion(shift,1,Date,new TimeOnly(overnight?20:9,0),new TimeOnly(overnight?5:18,0),overnight?1:0,540,0,0,ShiftWeekdays.All);
  var officeState=new OfficeRosterState(Branch.Id,[new(Branch.Id,shift,Date,null,null,"synthetic",DateTimeOffset.UtcNow)],[],[version]);
  var siteState=new SiteRosterState(SiteId,[ShiftPopulationAssignment.CreateDirect(shift,ShiftPopulationKind.Site,Date,null,siteId:SiteId)],[],[],[version]);
  Offices=Proxy.Create<IOfficeRosterRepository>((m,a)=>m switch {"EmploymentAsync"=>Task.FromResult<EmployeeAssignment?>(Membership),"FindBranchAsync"=>Task.FromResult<Branch?>(Branch),"ReadAsync"=>Read(officeState),_=>throw new Exception("unexpected "+m)});
  Sites=Proxy.Create<ISiteRosterRepository>((m,a)=>m switch {"MembershipsAsync"=>Task.FromResult<IReadOnlyList<EmployeeAssignment>>([Membership]),"ReadAsync"=>Read(siteState),_=>throw new Exception("unexpected "+m)});
  Employees=Proxy.Create<IRosterRepository>((m,a)=>m switch {"FindEmployeeAsync"=>Task.FromResult<Employee?>(EmployeeEntity),"EffectiveSiteAsync"=>Task.FromResult<Guid?>(SiteId),_=>throw new Exception("unexpected "+m)});
  Boundaries=Proxy.Create<IOfficeBoundaryRepository>((m,a)=>m switch {"VersionsAsync"=>Task.FromResult<IReadOnlyList<OfficeBoundaryVersionResponse>>([]),_=>throw new Exception("unexpected "+m)});
 }
 public void ReplaceMembership(){var category=new StaffCategory(code,"Synthetic category"); Membership=new(EmployeeId,Guid.NewGuid(),Guid.NewGuid(),category.Id,Guid.NewGuid(),office?Branch.Id:null,office?null:SiteId,null,Date.AddDays(-1),null,"synthetic",DateTimeOffset.UtcNow);typeof(EmployeeAssignment).GetProperty("StaffCategory")!.SetValue(Membership,category);}
 Task<T> Read<T>(T state){OnRead?.Invoke();return Task.FromResult(state);}
 public OfficeRosterResolver Office()=>Construct<OfficeRosterResolver>(Offices,TimeProvider.System,Taxonomy);
 public SiteRosterResolver Site()=>Construct<SiteRosterResolver>(Sites,Employees,Offices,Taxonomy);
 public EmployeeRosterResolver Employee()=>Construct<EmployeeRosterResolver>(Offices,Office(),Site(),Taxonomy);
 public OfficeAttendanceSourceReader Reader()=>Construct<OfficeAttendanceSourceReader>(Boundaries,Offices,Taxonomy);
 static T Construct<T>(params object[] args){var c=typeof(T).GetConstructors().Single();var selected=c.GetParameters().Select(p=>args.Single(x=>p.ParameterType.IsAssignableFrom(x.GetType()))).ToArray();return (T)c.Invoke(selected);}
 public void CheckPin(EffectiveRosterResponse roster){var pin=roster.GetType().GetProperty("Taxonomy")?.GetValue(roster)??throw new Exception("mapping proof absent");CheckValue("StaffCategoryCode",code);CheckValue("PolicyVersionId",Taxonomy.Version);CheckValue("Fingerprint",Taxonomy.Hash);void CheckValue(string key,object expected){if(!Equals(pin.GetType().GetProperty(key)?.GetValue(pin),expected))throw new Exception("wrong proof "+key);}}
}
sealed class Taxonomy(string code,bool office):IStaffTaxonomyResolver
{
 public bool Office=office,Available=true,Baseline,RangeChange;public Guid Version=Guid.NewGuid();public string Hash=new('A',64);public Action? OnResolve;
 public Task<StaffTaxonomyResolution> ResolveAsync(string categoryCode,DateOnly on,CancellationToken ct){OnResolve?.Invoke();return Task.FromResult(Resolution(categoryCode,on));}
 public Task<StaffTaxonomyResolution> ResolveForRangeAsync(string categoryCode,DateOnly from,DateOnly toExclusive,CancellationToken ct)=>Task.FromResult(RangeChange&&toExclusive>from.AddDays(1)?new StaffTaxonomyResolution("mapping_changes_during_interval",null,null,Hash):Resolution(categoryCode,from));
 StaffTaxonomyResolution Resolution(string categoryCode,DateOnly on)=>!Available?new("taxonomy_not_effective",null,null,Hash):Baseline?StaffTaxonomyResolver.Resolve(categoryCode,on,[]):new("effective",new(code,"Synthetic",Office?StaffWorkLocation.Office:StaffWorkLocation.Site,Office?StaffScheduleBasis.Office:StaffScheduleBasis.Site,false,!Office,[]),Version,Hash,EffectiveFrom:new(2026,1,1));
}
class Proxy:DispatchProxy
{
 public Func<string,object?[]?,object?> Handler=null!;
 protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)=>Handler(targetMethod!.Name,args);
 public static T Create<T>(Func<string,object?[]?,object?> handler)where T:class {var result=DispatchProxy.Create<T,Proxy>();((Proxy)(object)result).Handler=handler;return result;}
}
