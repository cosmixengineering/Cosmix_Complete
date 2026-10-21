using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Api.Controllers.HR;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Microsoft.AspNetCore.Mvc;

var failures = new List<string>(); var checks = 0;
var now = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Run(string name, Func<Task> test) { try { await test(); } catch (Exception e) { failures.Add(name + ": " + e.Message); } }
Task Domain(string name, Action test) => Run(name, () => { test(); return Task.CompletedTask; });
async Task Denied(string code, Func<Task> action) { try { await action(); throw new Exception("Expected " + code); } catch (AuthenticationFailure e) { Check(e.Code == code, "Expected " + code + ", got " + e.Code); } }

foreach (var state in new[] { AccountStatus.PendingAccess, AccountStatus.ReadyForInvitation, AccountStatus.Invited })
    await Domain("Dormant " + state + " release", () => {
        var (employee, account, _) = DomainFixture.Create(now);
        var (assignment, scope) = DomainFixture.Access(account, now.AddHours(-2));
        AccountSetupInvitation? invitation = null;
        if (state != AccountStatus.PendingAccess) account.ConfigureInitialAccess(assignment, scope, now.AddHours(-2));
        if (state == AccountStatus.Invited) invitation = account.IssueSetupInvitation(assignment, scope, "synthetic-hash", "synthetic", "synthetic-destination", Guid.NewGuid(), now.AddHours(-1), now.AddHours(3));
        DomainFixture.SuspendAndRelease(employee, account, now);
        assignment.Revoke(Guid.NewGuid(), "employee lifecycle", now); invitation?.Revoke(now);
        account.PrepareForEmploymentReactivation(now.AddMinutes(1));
        Check(account.Status == AccountStatus.PendingAccess, "Never-activated identity must return to PendingAccess");
        Check(account.ActivatedAt is null && account.PasswordHash is null && account.PhoneVerifiedAt is null, "No activation evidence fabricated");
        Check(account.InitialRoleAssignmentId is null && account.InitialAccessScopeId is null && account.CurrentSetupInvitationId is null, "Dormant current pointers cleared");
        Check(assignment.RevokedAt == now && (invitation is null || invitation.RevokedAt == now), "Revoked historical objects retained");
        var fresh = DomainFixture.Access(account, now.AddMinutes(2));
        account.ConfigureInitialAccess(fresh.Assignment, fresh.Scope, now.AddMinutes(2));
        Check(account.Status == AccountStatus.ReadyForInvitation && account.InitialRoleAssignmentId == fresh.Assignment.Id, "Fresh explicit initial access works");
    });

foreach (var mobile in new[] { false, true })
    await Domain((mobile ? "Mobile" : "Web") + " prior activation survives release", () => {
        var (employee, account, submission) = DomainFixture.Create(now, mobile);
        var access = DomainFixture.Access(account, now.AddHours(-2)); account.ConfigureInitialAccess(access.Assignment, access.Scope, now.AddHours(-2));
        if (mobile) {
            account.SetMobileAccess(true, now.AddHours(-1));
            var binding = new LoginPhoneBinding(account, "+923001234567", submission.Id, now.AddHours(-1)); binding.Verify(now.AddMinutes(-30));
            account.ActivateMobile(binding, access.Assignment, access.Scope, now.AddMinutes(-30));
        } else {
            var invitation = account.IssueSetupInvitation(access.Assignment, access.Scope, "synthetic-hash", "synthetic", "synthetic-destination", Guid.NewGuid(), now.AddHours(-1), now.AddHours(1));
            account.Activate(invitation, access.Assignment, access.Scope, "synthetic-password-hash", "synthetic-format", now.AddMinutes(-30));
        }
        var evidence = (account.ActivatedAt, account.PasswordHash, account.PasswordHashFormat, account.PasswordChangedAt, account.PhoneVerifiedAt, account.InitialRoleAssignmentId, account.InitialAccessScopeId);
        DomainFixture.SuspendAndRelease(employee, account, now); access.Assignment.Revoke(Guid.NewGuid(), "employee lifecycle", now);
        account.PrepareForEmploymentReactivation(now.AddMinutes(1));
        Check(account.Status == AccountStatus.Active && !account.WebAccessEnabled && !account.MobileAccessEnabled, "Prior activated identity has disabled channels");
        Check(evidence == (account.ActivatedAt, account.PasswordHash, account.PasswordHashFormat, account.PasswordChangedAt, account.PhoneVerifiedAt, account.InitialRoleAssignmentId, account.InitialAccessScopeId), "Historical activation evidence retained");
        Check(access.Assignment.RevokedAt == now, "Prior grant remains revoked");
        Check((account.PasswordHash is not null || account.PhoneVerifiedAt.HasValue) && account.ActivatedAt.HasValue && account.InitialRoleAssignmentId.HasValue, "Existing active SQL constraint remains satisfied");
    });

var corrupt = new (string Name, Action<UserAccount> Set)[] {
    ("Activation timestamp only", a => DomainFixture.Set(a, "ActivatedAt", now.AddHours(-1))),
    ("Password without activation", a => { DomainFixture.Set(a, "PasswordHash", "hash"); DomainFixture.Set(a, "PasswordHashFormat", "format"); DomainFixture.Set(a, "PasswordChangedAt", now.AddHours(-1)); }),
    ("Partial password proof", a => DomainFixture.Set(a, "PasswordHash", "hash")),
    ("Phone proof without activation", a => DomainFixture.Set(a, "PhoneVerifiedAt", now.AddHours(-1))),
    ("Partial initial pair", a => DomainFixture.Set(a, "InitialRoleAssignmentId", Guid.NewGuid())),
    ("Web flag without credentials", a => DomainFixture.Set(a, "WebAccessEnabled", true))
};
foreach (var item in corrupt) await Domain(item.Name + " fails closed", () => {
    var (employee, account, _) = DomainFixture.Create(now); DomainFixture.SuspendAndRelease(employee, account, now); item.Set(account);
    var version = account.SecurityVersion;
    try { account.PrepareForEmploymentReactivation(now.AddMinutes(1)); throw new Exception("Corrupt activation evidence accepted"); }
    catch (InvalidOperationException) { Check(account.Status == AccountStatus.Suspended && account.SecurityVersion == version, "Manual review rejection leaves identity unchanged"); }
});
foreach (var status in new[] { AccountStatus.Disabled, AccountStatus.Suspended }) await Domain("Independent " + status + " remains blocked", () => {
    var (_, account, _) = DomainFixture.Create(now); if (status == AccountStatus.Disabled) account.Disable("independent", now); else account.Suspend("independent", now);
    try { account.PrepareForEmploymentReactivation(now.AddMinutes(1)); throw new Exception("Independent block accepted"); }
    catch (InvalidOperationException) { Check(account.Status == status, "Independent block retained"); }
});

var sensitive = new[] { EmployeePermissions.FinancialRead, EmployeePermissions.FinancialEdit, EmployeeUpdatePermissions.BankRead, EmployeeUpdatePermissions.BankRequest, EmployeePermissions.MedicalRead, EmployeePermissions.MedicalEdit };
foreach (var permission in sensitive) foreach (var replay in new[] { false, true }) await Run("Approve " + (replay ? "replay" : "initial") + " requires " + permission, async () => {
    var fixture = new ServiceFixture(now); fixture.SetSnapshot(true, true, true); fixture.GrantSensitive(); fixture.Permissions.Remove(permission);
    if (replay) fixture.ApprovalReceipt(OnboardingDecision.Approve);
    await Denied("access_denied", () => fixture.Approval.DecideAsync(fixture.Employee.Id, fixture.Submission.Id, fixture.DecisionInput(OnboardingDecision.Approve), ServiceFixture.Key, new("synthetic", "foundation"), default));
    Check(fixture.Writes == 0 && fixture.Audits == 0, "Denied approval did not write/replay audit");
});
await Run("Fully authorized approval replay keeps exact frozen result", async () => {
    var fixture = new ServiceFixture(now); fixture.SetSnapshot(true, true, true); fixture.GrantSensitive(); var expected = fixture.ApprovalReceipt(OnboardingDecision.Approve);
    var result = await fixture.Approval.DecideAsync(fixture.Employee.Id, fixture.Submission.Id, fixture.DecisionInput(OnboardingDecision.Approve), ServiceFixture.Key, new("synthetic", "foundation"), default);
    Check(result == expected && fixture.SnapshotReads == 1, "Approval replay reauthorizes frozen snapshot and returns same IDs");
});
await Run("Profile-only approval replay needs no sensitive grant", async () => {
    var fixture = new ServiceFixture(now); fixture.SetSnapshot(false, false, false); fixture.ApprovalReceipt(OnboardingDecision.Approve);
    await fixture.Approval.DecideAsync(fixture.Employee.Id, fixture.Submission.Id, fixture.DecisionInput(OnboardingDecision.Approve), ServiceFixture.Key, new("synthetic", "foundation"), default);
    Check(fixture.SnapshotReads == 1, "Frozen profile snapshot read");
});
foreach (var decision in new[] { OnboardingDecision.ReturnForCorrection, OnboardingDecision.Reject }) await Run(decision + " replay retains base permissions", async () => {
    var fixture = new ServiceFixture(now); fixture.SetSnapshot(true, true, true); fixture.ApprovalReceipt(decision);
    await fixture.Approval.DecideAsync(fixture.Employee.Id, fixture.Submission.Id, fixture.DecisionInput(decision), ServiceFixture.Key, new("synthetic", "foundation"), default);
    Check(fixture.SnapshotReads == 0, "Non-approval does not inspect sensitive payload");
});
await Run("Approval callback binds exact frozen submission ID", async () => {
    var fixture = new ServiceFixture(now); fixture.SetSnapshot(true, true, true); fixture.GrantSensitive(); fixture.ApprovalReceipt(OnboardingDecision.Approve);
    await Denied("employee_resource_not_found", () => fixture.Approval.DecideAsync(fixture.Employee.Id, Guid.NewGuid(), fixture.DecisionInput(OnboardingDecision.Approve), ServiceFixture.Key, new("synthetic", "foundation"), default));
});

foreach (var replay in new[] { false, true }) await Run("Bank verify rejects inactive effective employment on " + (replay ? "replay" : "initial"), async () => {
    var fixture = new ServiceFixture(now) { ActiveEmployment = false }; fixture.GrantBank(); if (replay) fixture.BankReceipt(true);
    await Denied("employee_bank_verification_requires_active_employment", () => fixture.BankService.VerifyAsync(fixture.Employee.Id, fixture.Bank.Id, fixture.BankInput, ServiceFixture.Key, new("synthetic", "foundation"), default));
    Check(fixture.BankEvents == 0 && fixture.Writes == 0, "Cached Active cannot authorize an effective inactive or unreconciled release");
});
await Run("Bank verify accepts reconciled active employment", async () => {
    var fixture = new ServiceFixture(now); fixture.GrantBank();
    var result = await fixture.BankService.VerifyAsync(fixture.Employee.Id, fixture.Bank.Id, fixture.BankInput, ServiceFixture.Key, new("synthetic", "foundation"), default);
    Check(result.BankVerificationId.HasValue && fixture.BankEvents == 1, "Effective active verification appends one event");
});
await Run("Inactive bank revoke replay stays available", async () => {
    var fixture = new ServiceFixture(now) { ActiveEmployment = false }; fixture.GrantBank(); var expected = fixture.BankReceipt(false);
    var result = await fixture.BankService.RevokeAsync(fixture.Employee.Id, fixture.Bank.Id, fixture.BankInput, ServiceFixture.Key, new("synthetic", "foundation"), default);
    Check(result == expected && fixture.EmploymentReads == 0, "Revocation does not require active employment");
});
foreach (var method in new[] { "Verify", "Revoke" }) await Domain("Bank " + method + " accepts bounded Unicode JSON", () => {
    var input = new EmployeeBankTransitionRequest("AAAAAAAAAAE=", 0, new string('\u0627', 1000), new string('\u0627', 8000));
    var bytes = JsonSerializer.SerializeToUtf8Bytes(input, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    var limit = ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)typeof(EmployeeBankController).GetMethod(method)!.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize;
    Check(bytes.Length <= limit, "Valid escaped Unicode JSON length " + bytes.Length + " exceeds route bound " + limit);
});
foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")} {checks} foundation checks; {failures.Count} scenarios failed (actual domain/services/controller metadata; synthetic ports; no SQL/provider/host acceptance)");
return failures.Count == 0 ? 0 : 1;

static class DomainFixture {
    public static void Set(object item, string property, object value) => item.GetType().GetProperty(property)!.SetValue(item, value);
    public static (Employee Employee, UserAccount Account, EmployeeSubmission Submission) Create(DateTimeOffset now, bool mobile = false) {
        var employee = new Employee(now.AddDays(-2), "synthetic-hr");
        employee.UpdateDraft(new() { FullLegalName="Synthetic Foundation Employee", IdentityDocumentType=IdentityDocumentType.Cnic, IdentityDocumentNumber="1234512345671", PersonalPhone="+923001234567", JoinedOn=new(2026,10,11) });
        if (mobile) employee.SelectLoginPhone("+923001234567", true);
        var submission = employee.SubmitForApproval(Guid.NewGuid(), now.AddDays(-1), "synthetic-reference", "v1", new string('A',64), "synthetic-key", 1);
        var decision = employee.DecideSubmission(submission, OnboardingDecision.Approve, Guid.NewGuid(), Guid.NewGuid(), null, now.AddDays(-1));
        return (employee, new UserAccount(employee, decision, "synthetic:foundation", now.AddDays(-1)), submission);
    }
    public static (UserRoleAssignment Assignment, UserAccessScope Scope) Access(UserAccount account, DateTimeOffset at) {
        var role = new Role("SYNTHETIC", "Synthetic", Guid.NewGuid(), at); var assignment = new UserRoleAssignment(account, role, Guid.NewGuid(), at, null); return (assignment, new UserAccessScope(assignment, AccessScopeKind.All));
    }
    public static void SuspendAndRelease(Employee employee, UserAccount account, DateTimeOffset now) {
        var suspension = new EmployeeStatusChange(employee.Id, EmployeeStatus.Active, EmployeeStatus.Suspended, now, "synthetic suspension", Guid.NewGuid().ToString(), now, Guid.NewGuid()); employee.ApplyStatusChange(suspension, now); account.Suspend("employee-lifecycle", now);
        var releaseAt = now.AddMinutes(1); var release = new EmployeeStatusChange(employee.Id, EmployeeStatus.Suspended, EmployeeStatus.Active, releaseAt, "synthetic release", Guid.NewGuid().ToString(), releaseAt, Guid.NewGuid(), "fixture-approved-policy-v1"); employee.ApplyStatusChange(release, releaseAt);
    }
}

sealed class ServiceFixture {
    public const string Key = "foundation-check-0001";
    public Employee Employee; public EmployeeSubmission Submission; public EmployeeBankDetails Bank; public EmployeeApprovalService Approval; public EmployeeBankService BankService;
    public HashSet<string> Permissions = [EmployeePermissions.Read, EmployeePermissions.Review, EmployeePermissions.DocumentsRead];
    public bool ActiveEmployment = true; public int Writes, Audits, SnapshotReads, EmploymentReads, BankEvents;
    public EmployeeBankTransitionRequest BankInput = new("AAAAAAAAAAE=", 0, "Synthetic reason", "Synthetic proof");
    private readonly AuthenticatedAccount identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    private readonly DateTimeOffset now; private IdempotencyReceipt? receipt; private byte[] snapshot = []; private readonly PrivateFileReference reference = new("synthetic", "v1", new string('A',64), "synthetic-key", 1);
    public ServiceFixture(DateTimeOffset now) {
        this.now = now; (Employee, _, Submission) = DomainFixture.Create(now); DomainFixture.Set(Employee,"RowVersion",new byte[] { 0,0,0,0,0,0,0,1 });
        DomainFixture.Set(Submission, "SnapshotStorageKey", JsonSerializer.Serialize(reference));
        Bank = new(Employee.Id,1,"name","title","number",null,"key","**1234",false,null,null,new(2026,10,11),null,"synthetic",now.AddDays(-1));
        var assignmentId = Guid.NewGuid(); var assignment = (UserRoleAssignment)Activator.CreateInstance(typeof(UserRoleAssignment), true)!; DomainFixture.Set(assignment,"RoleId",Guid.NewGuid());
        var current = Proxy.Make<ICurrentAuthentication>((m,a) => identity);
        var reader = Proxy.Make<IAccountAccessReader>((m,a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Permissions.Select(p => new PermissionGrant(p,true,assignmentId,AccessScopeKind.All,null)).ToArray()));
        var authorization = Proxy.Make<IAccessManagementRepository>((m,a) => m.Name switch { "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]), "IsCurrentAdministratorAsync" => Task.FromResult(true), "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(true), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ADMIN"), "FindAssignmentAsync" => Task.FromResult<UserRoleAssignment?>(assignment), "IsEmployeeActiveForAccessAsync" => Employment(a), _ => throw new Exception("Unexpected authorization " + m.Name) });
        var hiring = Proxy.Make<IEmployeeHiringRepository>((m,a) => m.Name switch { "FindReceiptAsync" => Task.FromResult(receipt), "FindAsync" => Task.FromResult<Employee?>((Guid)a[0]! == Employee.Id ? Employee : null), "FindSubmissionAsync" => Task.FromResult<EmployeeSubmission?>((Guid)a[0]! == Employee.Id && (Guid)a[1]! == Submission.Id ? Submission : null), "FindDraftAsync" => Task.FromResult<EmployeeHiringDraft?>(null), "ExpectVersion" => null, "AddAudit" => AddAudit(), "AddReceipt" => AddReceipt(a), "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception("Unexpected hiring action " + m.Name) });
        var files = Proxy.Make<IPrivateFileStore>((m,a) => m.Name switch { "ReadAsync" => ReadSnapshot(a), "WriteAsync" => Write(a), _ => throw new Exception(m.Name) });
        var operation = new EmployeeOperation(current,reader,authorization,hiring,new Clock(now),files);
        var forms = new EmployeeHiringService(operation,hiring,files,new MobileAuthenticationOptions { BusinessTimeZoneId="UTC" },new Clock(now),null!,null!,new(new()));
        Approval = new(operation,hiring,null!,forms,null!,null!);
        var bankRepo = Proxy.Make<IEmployeeBankRepository>((m,a) => m.Name switch { "FindBankAsync" => Task.FromResult<EmployeeBankDetails?>((Guid)a[0]! == Employee.Id && (Guid)a[1]! == Bank.Id ? Bank : null), "CurrentBankAsync" => Task.FromResult<EmployeeBankDetails?>(Bank), "HasConflictingHistoryAsync" => Task.FromResult(false), "LatestAsync" => Task.FromResult<EmployeeBankVerification?>(null), "AddVerification" => AddBank(), _ => throw new Exception(m.Name) });
        var updates = Proxy.Make<IEmployeeUpdateRepository>((m,a) => m.Name == "HasConflictingHistoryAsync" ? Task.FromResult(false) : throw new Exception(m.Name));
        BankService = new(operation,hiring,bankRepo,updates,forms,files);
    }
    public void GrantSensitive() { foreach (var p in new[] { EmployeePermissions.FinancialRead, EmployeePermissions.FinancialEdit, EmployeeUpdatePermissions.BankRead, EmployeeUpdatePermissions.BankRequest, EmployeePermissions.MedicalRead, EmployeePermissions.MedicalEdit }) Permissions.Add(p); }
    public void GrantBank() { Permissions.Add(EmployeeUpdatePermissions.BankRead); Permissions.Add(EmployeeBankPermissions.Verify); }
    public void SetSnapshot(bool financial, bool bank, bool medical) { snapshot = JsonSerializer.SerializeToUtf8Bytes(new { EmployeeId=Employee.Id, EmployeeNumber=Employee.EmployeeNumber, DraftRevision=Submission.DraftRevision, Profile=new EmployeeProfileInput(), Financial=financial ? new EmployeeFinancialInput { Bank=bank ? new BankInput { BankName="Synthetic" } : null } : null, Medical=medical ? new EmployeeMedicalInput { Details="Synthetic" } : null, Documents=Array.Empty<EmployeeDocumentResponse>() }); }
    public EmployeeDecisionRequest DecisionInput(OnboardingDecision decision) => new("AAAAAAAAAAE=",decision,decision == OnboardingDecision.Approve ? null : "Synthetic decision");
    public EmployeeWriteResult ApprovalReceipt(OnboardingDecision decision) => Seed("hr.employee.submission.decided",new { submissionId=Submission.Id,input=DecisionInput(decision) },new(Employee.Id,"AAAAAAAAAAI=",OnboardingStatus.Approved,SubmissionId:Submission.Id,DecisionId:Guid.NewGuid(),AccountId:Guid.NewGuid()));
    public EmployeeWriteResult BankReceipt(bool verify) => Seed(verify ? "hr.employee.bank.verified" : "hr.employee.bank.verification.revoked",new { bankDetailsId=Bank.Id,input=BankInput },new(Employee.Id,"AAAAAAAAAAI=",OnboardingStatus.Approved,BankVerificationId:Guid.NewGuid()));
    private EmployeeWriteResult Seed(string operation,object payload,EmployeeWriteResult result) { var digest=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { employeeId=(Guid?)Employee.Id,payload }))); receipt=new(identity.AccountId.ToString(),operation,Key,digest,now.AddMinutes(-1),now.AddHours(24)); receipt.Complete("employee-hiring",JsonSerializer.Serialize(result,new JsonSerializerOptions { DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull }),now.AddSeconds(-30)); return result; }
    private Task<bool> Employment(object?[] args) { if ((Guid)args[0]! != Employee.Id || (DateTimeOffset)args[1]! != now) throw new Exception("Employment check not bound to target/time"); EmploymentReads++; return Task.FromResult(ActiveEmployment); }
    private Task<byte[]> ReadSnapshot(object?[] args) { if ((PrivateFileReference)args[0]! != reference || (string)args[1]! != $"employee/{Employee.Id:N}/submission") throw new Exception("Snapshot read not exact frozen reference/purpose"); SnapshotReads++; return Task.FromResult(snapshot); }
    private Task<PrivateFileReference> Write(object?[] args) { Writes++; return Task.FromResult(new PrivateFileReference("synthetic","v1",new string('A',64),"synthetic-key",((byte[])args[0]!).Length)); }
    private object? AddAudit() { Audits++; return null; } private object? AddReceipt(object?[] args) { receipt=(IdempotencyReceipt)args[0]!; return null; } private object? AddBank() { BankEvents++; return null; }
}
public class Proxy : DispatchProxy { public Func<MethodInfo,object?[],object?> Handler = null!; protected override object? Invoke(MethodInfo? method,object?[]? args) => Handler(method!,args!); public static T Make<T>(Func<MethodInfo,object?[],object?> handler) where T : class { var proxy=Create<T,Proxy>(); ((Proxy)(object)proxy).Handler=handler; return proxy; } }
sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
