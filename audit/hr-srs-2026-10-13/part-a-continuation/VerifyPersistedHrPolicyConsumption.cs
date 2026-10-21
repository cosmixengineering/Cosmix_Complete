#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.FoundationPolicies;

var reader=new SyntheticPolicies();
var options=new EmployeeFieldPolicyOptions();
var constructor=typeof(EmployeeFieldPolicy).GetConstructors().Single();
var policy=(EmployeeFieldPolicy)(constructor.GetParameters().Length==1
 ? constructor.Invoke([options]) : constructor.Invoke([options,reader]));
var method=typeof(EmployeeFieldPolicy).GetMethod("MissingFieldsAsync");
var profile=new EmployeeProfileInput();
var missing=method is null ? policy.MissingFields(profile,SyntheticPolicies.Today)
 : await (Task<IReadOnlyList<string>>)method.Invoke(policy,[profile,SyntheticPolicies.Today,CancellationToken.None])!;
if(!missing.Contains("gender"))throw new Exception("Published effective HR field policy is ignored; empty static configuration must not bypass required gender.");
Console.WriteLine("PASS published effective field rules govern missing fields instead of empty static configuration");

sealed class SyntheticPolicies:IHrFoundationPolicyReader {
 public static DateOnly Today=new(2026,10,13);
 public Task<HrFoundationPolicyResolution<HrFoundationFieldRules>> GetFieldsAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(
  new HrFoundationPolicyResolution<HrFoundationFieldRules>("effective",new HrFoundationFieldRules{RequireGender=true,GenderValues=["SYNTHETIC-ONLY"]},
   new(Guid.NewGuid(),Guid.NewGuid(),1,"synthetic-policy-only",Today,null),null));
 public Task<HrFoundationPolicyResolution<HrFoundationLifecycleRules>> GetLifecycleAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(
  new HrFoundationPolicyResolution<HrFoundationLifecycleRules>("none",null,null,null));
 public Task<HrFoundationPolicyResolution<HrFoundationRecordRules>> GetRecordsAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(
  new HrFoundationPolicyResolution<HrFoundationRecordRules>("none",null,null,null));
}
