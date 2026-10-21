using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Authentication.Providers;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

var failures = new List<string>(); int count = 0;
void Check(bool value, string name) { count++; if(!value) failures.Add(name); }
void Reject(Action action, string name) { try { action(); Check(false,name); } catch(ArgumentException) { Check(true,name); } catch(InvalidOperationException) { Check(true,name); } }
async Task Failure(Func<Task> action, string code, string name) { try { await action(); Check(false,name); } catch(AuthenticationFailure f) { Check(f.Code == code,name + ": " + f.Code); } }
var today = new DateOnly(2026,10,13); var now = new DateTimeOffset(2026,10,13,10,0,0,TimeSpan.Zero);
var phones = new LibPhoneNumberNormalizer();
var normalized = EmployeeFormValidation.NormalizePhones(new() { PersonalPhone = "+92 (300) 123-4567", CompanyPhone = "+92 301 2345678", LoginPhoneNumber = "+92-302-3456789", EmergencyContact = new() { Phone = "+92 303 4567890", AlternatePhone = "+92 304 5678901" } }, phones);
Check(normalized.PersonalPhone == "+923001234567" && normalized.CompanyPhone == "+923012345678" && normalized.LoginPhoneNumber == "+923023456789" && normalized.EmergencyContact?.Phone == "+923034567890" && normalized.EmergencyContact?.AlternatePhone == "+923045678901", "All employee and SIM/emergency/login contact paths canonicalize");
await Failure(() => { EmployeeFormValidation.NormalizePhones(new() { PersonalPhone="03001234567" },phones); return Task.CompletedTask; }, "invalid_employee_phone", "No unapproved default country is inferred");
var employee = new Employee(now.AddDays(-10), "fixture");
Reject(() => employee.UpdateDraft(new() { PersonalPhone = "unusable" }), "Domain rejects malformed stored contact");
employee.UpdateDraft(EmployeeFormValidation.ToDomain(normalized));
Check(employee.PersonalPhone == "+923001234567", "Canonical draft domain storage");
Set(employee,"Status",EmployeeStatus.Active); Set(employee,"OnboardingStatus",OnboardingStatus.Approved); Set(employee,"ApprovedOnboardingDecisionId",Guid.NewGuid()); Set(employee,"JoinedOn",today.AddDays(-10)); Set(employee,"RowVersion",BitConverter.GetBytes(1L));
EmployeeFormValidation.ValidateProfile(new(), today);
Check(EmployeeFormValidation.RequiredFields(new()).Contains("dateOfBirth") && EmployeeFormValidation.RequiredFields(new()).Contains("emergencyContact.phone"), "Incomplete draft saves and completeness returns exact paths");
var fieldPolicy = new EmployeeFieldPolicy(new());
Check(fieldPolicy.MissingFields(new() { Gender="legacy" },today).Count == 0, "Absent approved policy invents no restrictions");
var configured = new EmployeeFieldPolicy(new() { ApprovedVersion="fixture-policy-v1", EffectiveFrom=today.AddDays(-1), GenderValues=["ApprovedGenderA"], MaritalStatusValues=["ApprovedMaritalA"], RequireGender=true, RequireMaritalStatus=true, RequireFatherOrSpouseName=true, RequireProbationEnd=true, MinimumAgeAtJoining=18, MaximumAgeAtJoining=60, MaximumProbationDays=90, RequireExpiryForIdentityTypes=[IdentityDocumentType.Passport] });
configured.ValidateDraft(new(),today);
Check(configured.MissingFields(new(),today).SequenceEqual(new[]{"gender","maritalStatus","fatherOrSpouseName","probationEndsOn"}), "Approved required fields deferred until submit");
await Failure(() => { configured.ValidateDraft(new() { Gender="unapproved" },today); return Task.CompletedTask; }, "invalid_employee_gender", "Configured controls reject unknown values");
var missing = configured.MissingFields(new() { Gender="ApprovedGenderA", MaritalStatus="ApprovedMaritalA", FatherOrSpouseName="Fixture parent", DateOfBirth=new(2010,10,14), JoinedOn=today, ProbationEndsOn=today.AddDays(91), IdentityDocumentType=IdentityDocumentType.Passport },today);
Check(missing.Contains("dateOfBirth.ageAtJoining") && missing.Contains("probationEndsOn.maximumPeriod") && missing.Contains("identityDocumentExpiresOn"), "Age, probation and type-specific expiry use approved fixture policy");
await Failure(() => { new EmployeeFieldPolicy(new() { GenderValues=["Unapproved"] }).ValidateDraft(new(),today); return Task.CompletedTask; }, "employee_field_policy_not_configured", "Partially configured policy fails closed");
var bank = new EmployeeBankDetails(employee.Id,1,"name","title","number",null,"key","****4567",false,null,null,today.AddDays(-10),null,"fixture",now.AddDays(-10));
var verified = new EmployeeBankVerification(bank,null,true,"verifier",now,"private-proof-1");
var revoked = new EmployeeBankVerification(bank,verified,false,"revoker",now.AddMinutes(1),"private-proof-2");
Check(verified.Version==1 && revoked.Version==2 && revoked.PreviousVerificationId==verified.Id && verified.IsVerified && !revoked.IsVerified && !bank.IsVerified, "Verification/revoke append history without rewriting bank");
Reject(() => new EmployeeBankVerification(bank,verified,true,"actor",now.AddMinutes(2),"proof"), "Repeated verify denied");
Reject(() => new EmployeeBankVerification(bank,verified,false,"actor",now,"proof"), "Backdated transition denied");
var otherBank = new EmployeeBankDetails(Guid.NewGuid(),1,"name","title","number",null,"key","****7890",false,null,null,today,null,"fixture",now);
Reject(() => new EmployeeBankVerification(otherBank,verified,false,"actor",now.AddMinutes(2),"proof"), "Cross-employee predecessor denied");
var clock = new Clock(now); var identity = new AuthenticatedAccount(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
var assignment = Guid.NewGuid(); var permissions = new HashSet<string> { EmployeePermissions.Read, EmployeeUpdatePermissions.BankRead, EmployeeBankPermissions.Verify };
var current = Proxy.Make<ICurrentAuthentication>((m,a) => identity);
var access = Proxy.Make<IAccountAccessReader>((m,a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(permissions.Select(p => new PermissionGrant(p,true,assignment,AccessScopeKind.All,null)).ToArray()));
// This fixture represents an approved initial hire with no lifecycle history rows.
// Derive active access from that synthetic persisted employee; do not blanket-allow unknown IDs.
bool ActiveFixtureEmployment(Guid id, DateTimeOffset at) => id == employee.Id &&
    employee.Status == EmployeeStatus.Active && employee.OnboardingStatus == OnboardingStatus.Approved &&
    employee.ApprovedOnboardingDecisionId.HasValue && employee.JoinedOn <= DateOnly.FromDateTime(at.UtcDateTime) &&
    EmployeeLifecycleAccessRules.IsReconciled(null, employee.LastAppliedStatusChangeId);
var authorization = Proxy.Make<IAccessManagementRepository>((m,a) => m.Name switch { "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(CancellationToken.None), "IsCurrentAdministratorAsync" => Task.FromResult(true), "IsEmployeeActiveForAccessAsync" => Task.FromResult(ActiveFixtureEmployment((Guid)a[0]!, (DateTimeOffset)a[1]!)), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("HR"), _ => throw new Exception("Unexpected authorization call: "+m.Name) });
var receipts = new List<IdempotencyReceipt>(); var audit = new List<AuditEvent>(); long rowVersion=1;
var hiring = Proxy.Make<IEmployeeHiringRepository>((m,a) => m.Name switch {
"FindAsync" => Task.FromResult<Employee?>((Guid)a[0]! == employee.Id ? employee : null), "FindDraftAsync" => Task.FromResult<EmployeeHiringDraft?>(null),
"ExpectVersion" => Expect((byte[])a[1]!), "FindReceiptAsync" => Task.FromResult(receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
"AddReceipt" => AddReceipt((IdempotencyReceipt)a[0]!), "AddAudit" => AddAudit((AuditEvent)a[0]!), "SaveChangesAsync" => Save(), _ => throw new Exception("Unexpected hiring call: "+m.Name) });
object? Expect(byte[] value) { if(!value.SequenceEqual(employee.RowVersion)) throw new AuthenticationFailure("employee_version_conflict",409); return null; }
object? AddReceipt(IdempotencyReceipt value) { receipts.Add(value); return null; } object? AddAudit(AuditEvent value) { audit.Add(value); return null; }
Task Save() { Set(employee,"RowVersion",BitConverter.GetBytes(++rowVersion)); return Task.CompletedTask; }
var files = new Files(); var events = new List<EmployeeBankVerification>();
var bankRepo = Proxy.Make<IEmployeeBankRepository>((m,a) => m.Name switch {
"FindBankAsync" => Task.FromResult<EmployeeBankDetails?>((Guid)a[0]! == employee.Id && (Guid)a[1]! == bank.Id ? bank : null),
"CurrentBankAsync" => Task.FromResult<EmployeeBankDetails?>(bank), "LatestAsync" => Task.FromResult(events.LastOrDefault()), "HasConflictingHistoryAsync" => Task.FromResult(false),
"FindVerificationAsync" => Task.FromResult(events.SingleOrDefault(e=>e.Id==(Guid)a[2]!)), "AddVerification" => AddEvent((EmployeeBankVerification)a[0]!), _ => throw new Exception("Unexpected bank call: "+m.Name) });
object? AddEvent(EmployeeBankVerification value) { events.Add(value); return null; }
var updates = Proxy.Make<IEmployeeUpdateRepository>((m,a)=>Task.FromResult(false));
var operation = new EmployeeOperation(current,access,authorization,hiring,clock,files);
var approvals = Proxy.Make<IEmployeeApprovalRepository>((m,a)=>throw new Exception("Unexpected approval call"));
var forms = new EmployeeHiringService(operation,hiring,files,new() { BusinessTimeZoneId="UTC" },clock,approvals,phones,fieldPolicy);
var service = new EmployeeBankService(operation,hiring,bankRepo,updates,forms,files);
var context = new AuthRequestContext("local-fixture","a03-fixture");
EmployeeBankTransitionRequest Input(int version) => new(Convert.ToBase64String(employee.RowVersion),version,"Fixture bank verification reason","Fixture private proof");
var initial = Input(0); var result = await service.VerifyAsync(employee.Id,bank.Id,initial,"a03-verify-0001",context,default);
Check(events.Count==1 && result.BankVerificationId==events[0].Id, "Authorized service verify saves exact actor/version/proof");
var status = await service.CurrentAsync(employee.Id,context,default);
Check(status.IsVerified && status.VerificationVersion==1 && status.MaskedAccountIdentifier=="****4567", "Masked current status resolves latest journal");
var evidence = await service.EvidenceAsync(employee.Id,bank.Id,events[0].Id,context,default);
Check(evidence.Proof=="Fixture private proof" && evidence.Reason=="Fixture bank verification reason", "Protected proof reads exact stored evidence");
await Failure(()=>service.RevokeAsync(employee.Id,bank.Id,Input(0),"a03-stale-0001",context,default),"employee_bank_verification_version_conflict","Stale verification version fails");
await Failure(()=>service.RevokeAsync(employee.Id,bank.Id,initial,"a03-stale-0002",context,default),"employee_version_conflict","Stale employee rowversion fails");
await Failure(()=>service.RevokeAsync(Guid.NewGuid(),bank.Id,Input(1),"a03-wrong-0001",context,default),"employee_resource_not_found","Wrong employee fails before proof access");
permissions.Remove(EmployeeBankPermissions.Verify);
await Failure(()=>service.RevokeAsync(employee.Id,bank.Id,Input(1),"a03-denied-0001",context,default),"access_denied","Missing separate bank verify permission fails");
await Failure(()=>service.VerifyAsync(employee.Id,bank.Id,initial,"a03-verify-0001",context,default),"access_denied","Replay rechecks current verification permission");
permissions.Add(EmployeeBankPermissions.Verify); clock.Now=now.AddMinutes(1);
await service.RevokeAsync(employee.Id,bank.Id,Input(1),"a03-revoke-0001",context,default);
Check(events.Count==2 && !events[1].IsVerified && events[1].PreviousVerificationId==events[0].Id, "Service revoke retains original verified evidence");
var replay = await service.VerifyAsync(employee.Id,bank.Id,initial,"a03-verify-0001",context,default);
Check(replay.BankVerificationId==result.BankVerificationId && events.Count==2,"Idempotent replay retains exact original result without duplicate transition");
Check(!JsonSerializer.Serialize(status).Contains("Proof") && !JsonSerializer.Serialize(status).Contains("EncryptedReference") && typeof(EmployeeProfileResponse).GetProperties().All(p=>p.Name!="Bank"),"Ordinary DTO/status omit private bank evidence/full account data");
using(var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options)) {
var model = db.Model.FindEntityType(typeof(EmployeeBankVerification))!;
Check(model.GetIndexes().Any(i=>i.IsUnique && i.Properties.Select(p=>p.Name).SequenceEqual(new[]{"EmployeeBankDetailsId","Version"})),"EF prevents duplicate bank verification versions");
db.Attach(events[0]); Set(events[0],"PrivateEvidenceReference","rewritten");
try { await db.SaveChangesAsync(); Check(false,"Append-only write guard rejects evidence rewrite"); } catch(InvalidOperationException ex) { Check(ex.Message.Contains("append-only"),"Append-only write guard rejects evidence rewrite"); }
}
foreach(var failure in failures) Console.WriteLine("FAIL: "+failure);
Console.WriteLine($"Assertions: {count}; failures: {failures.Count}; SQL/provider/accounts side effects: none");
return failures.Count==0 ? 0 : 1;
static void Set(object value,string property,object? data) => value.GetType().GetProperty(property)!.SetValue(value,data);
public class Proxy : DispatchProxy { public Func<MethodInfo,object?[],object?> Handler=null!; protected override object? Invoke(MethodInfo? m,object?[]? a)=>Handler(m!,a!); public static T Make<T>(Func<MethodInfo,object?[],object?> handler) where T:class { var p=Create<T,Proxy>(); ((Proxy)(object)p).Handler=handler; return p; } }
public sealed class Clock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now=now; public override DateTimeOffset GetUtcNow()=>Now; }
public sealed class Files : IPrivateFileStore { private readonly Dictionary<string,(string Purpose,byte[] Bytes)> data=new(); public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct) { var key=Guid.NewGuid().ToString("N"); data[key]=(purpose,bytes.ToArray()); return Task.FromResult(new PrivateFileReference(key,"v1",Convert.ToHexString(SHA256.HashData(bytes)),"fixture-key",bytes.Length)); } public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct) { var value=data[reference.StorageKey]; if(value.Purpose!=purpose) throw new InvalidOperationException("Wrong storage purpose"); return Task.FromResult(value.Bytes.ToArray()); } }
