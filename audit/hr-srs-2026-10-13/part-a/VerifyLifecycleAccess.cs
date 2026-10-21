#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Domain.HR.Employees;
var count=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
var now=new DateTimeOffset(2026,10,13,0,0,0,TimeSpan.Zero);
var id=Guid.NewGuid();
var release=new EmployeeStatusChange(id,EmployeeStatus.Suspended,EmployeeStatus.Active,now,"synthetic","synthetic",now,Guid.NewGuid(),"synthetic-approved-policy");
var first=new EmployeeStatusChange(id,EmployeeStatus.PendingApproval,EmployeeStatus.Active,now,"synthetic","synthetic",now);
Check(!EmployeeLifecycleAccessRules.IsReconciled(release,null),"Delayed worker release denies old identity until cleanup");
Check(!EmployeeLifecycleAccessRules.IsReconciled(release,Guid.NewGuid()),"Wrong lifecycle marker denies old identity");
Check(EmployeeLifecycleAccessRules.IsReconciled(release,release.Id),"Exact applied release marker eligible for explicit fresh setup");
Check(EmployeeLifecycleAccessRules.IsReconciled(first,null),"Retained baseline initial active employment still supported");
Check(EmployeeLifecycleAccessRules.IsReconciled(null,null),"Retained no-history baseline still supported");
Console.WriteLine($"PASS {count} lifecycle access reconciliation checks (offline domain, no SQL)");
