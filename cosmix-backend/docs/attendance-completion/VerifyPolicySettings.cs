#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Policies;

var fixture = new Fixture(); var passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
async Task Denied(string code)
{
    try { await fixture.Validate(); } catch (AuthenticationFailure e) when (e.Code == code) { passed++; return; }
    throw new Exception("Expected " + code);
}
var missing = await fixture.Validate();
Check(!missing.IsValid && missing.Errors.Count > 0, "Incomplete settings return actionable validation errors");
Check(missing.CanonicalJson is null && missing.ConfigurationSha256 is null && !missing.Published, "Invalid input must not become a publishable policy");
Check(fixture.Audits.Single().EventType == "attendance.policy-settings.validate", "Validation reuses authorized audit boundary");
// Synthetic numbers exercise the form; they are not approved company defaults.
var rules = new AttendancePolicyRules(1, new(900, 3600, 9000),
    new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
        AttendanceEarlyExitTreatment.ActualWorkedTime, 16200, AttendanceNonScheduledDayTreatment.NoCredit, AttendanceNonScheduledDayTreatment.NoCredit),
    new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
        AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.WholeCurrencyUnitHalfUp));
var valid = await fixture.Validate(rules);
Check(valid.IsValid && !valid.Published && valid.ConfigurationSha256 is { Length: 64 }, "Valid settings get an identity without policy activation");
Check(valid.CanonicalJson == AttendancePolicyRulesCodec.Canonicalize(rules) && valid.Errors.Count == 0, "Service returns exactly the validated canonical settings");
fixture.Grants.Clear(); await Denied("access_denied");
fixture.Grant(AccessScopeKind.Site, Guid.NewGuid()); await Denied("access_denied");
fixture.Grants.Clear(); fixture.Grant(AccessScopeKind.All, null);
fixture.Actor = fixture.Actor with { MfaSatisfied = false }; await Denied("access_denied");
fixture.Actor = fixture.Actor with { Client = SessionClient.Mobile, MfaSatisfied = true }; await Denied("access_denied");
fixture.Actor = fixture.Actor with { Client = SessionClient.Web }; fixture.Current = false; await Denied("invalid_authentication");
Check(fixture.Audits.Count == 2, "Denied validation cannot disclose config or append success audit");
Check(AccessPermissionCatalog.Find(AttendancePolicyPermissions.Configure) is { SelfService: false, Sensitive: true }, "Policy settings require separately assignable sensitive management permission");
Console.WriteLine($"PASS: {passed} policy-settings service checks; synthetic authorization only, no SQL or private IO.");

public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var result = Create<T, Proxy>(); ((Proxy)(object)result).Handler = handler; return result; }
}
public sealed class Fixture
{
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> Audits = [];
    public bool Current = true;
    public AttendancePolicySettingsService Service;
    public Fixture()
    {
        Grant(AccessScopeKind.All, null);
        var current = Proxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authority = Proxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticHR"), _ => throw new Exception(m.Name)
        });
        var journal = Proxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current), "SaveChangesAsync" => Task.CompletedTask,
            "AddAudit" => Audit((AuditEvent)a[0]!), _ => throw new Exception("Unexpected mutation: " + m.Name)
        });
        var authentication = Proxy.Make<IAuthenticationRepository>((m, _) => throw new Exception("Unexpected auth IO: " + m.Name));
        var files = Proxy.Make<IPrivateFileStore>((m, _) => throw new Exception("Unexpected private IO: " + m.Name));
        Service = new(new AttendanceOperation(current, access, authority, journal, authentication, TimeProvider.System, files));
    }
    public Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    object? Audit(AuditEvent value) { Audits.Add(value); return null; }
    public void Grant(AccessScopeKind kind, Guid? target) => Grants.Add(new(AttendancePolicyPermissions.Configure, true, Guid.NewGuid(), kind, target));
    public Task<AttendancePolicySettingsValidationResponse> Validate(AttendancePolicyRules? rules = null) => Service.ValidateAsync(new(rules), new("127.0.0.1", "synthetic-policy-validation"), default);
}
