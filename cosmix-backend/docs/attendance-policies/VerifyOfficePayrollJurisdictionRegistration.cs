#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:package Microsoft.Extensions.DependencyInjection@10.0.12
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Text.Json;
using Cosmix.Application;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.CompanySettings;
using Cosmix.Domain.HR.CompanySettings;
using Microsoft.Extensions.DependencyInjection;

// Actual Application registration descriptors, with read-only synthetic repository dependencies.
// No host, EF model, SQL, provider, or taxonomy consumer is instantiated.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
var all = new ServiceCollection().AddApplication();
var types = new[] { typeof(CompanyPayrollJurisdictionReader), typeof(OfficeAttendancePayrollJurisdictionReader),
    typeof(IAttendancePayrollJurisdictionReader) };
var bindings = all.Where(x => types.Contains(x.ServiceType)).ToArray();
Check(bindings.Length == 3 && types.All(t => bindings.Count(x => x.ServiceType == t) == 1) &&
    bindings.All(x => x.Lifetime == ServiceLifetime.Scoped), "exact parent reader registrations are present once and scoped");
Check(bindings.Single(x => x.ServiceType == typeof(CompanyPayrollJurisdictionReader)).ImplementationType == typeof(CompanyPayrollJurisdictionReader),
    "concrete Site reader has no circular interface dependency");
Check(bindings.Where(x => x.ServiceType != typeof(CompanyPayrollJurisdictionReader)).All(x => x.ImplementationFactory is not null),
    "actual Office factory and interface alias are used");
var date = new DateOnly(2026, 10, 12); var employee = Guid.NewGuid(); var branch = Guid.NewGuid(); var site = Guid.NewGuid();
var rosterId = Guid.NewGuid(); var siteReads = 0; var boundaryReads = 0;
var roster = new EffectiveRosterResponse(employee, date, "scheduled", RosterSchedule.TimeZoneId,
    AssignmentId: rosterId, ShiftVersionId: Guid.NewGuid(),
    Explanation: new("office", null, Guid.NewGuid(), null, null, "synthetic") { OfficeRosterAssignmentId = rosterId },
    Holiday: new("configured", date, Guid.NewGuid(), "synthetic", 1, [])) { BranchId = branch };
var boundary = new OfficeBoundaryVersionResponse(Guid.NewGuid(), branch, 1, date.AddDays(-1), 24m, 67m, 100m, 20m,
    "PK-SD", "synthetic-management-approval", "synthetic", new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero));
var source = OfficeAttendanceSource.Capture(employee, date, roster, boundary)!;
var company = RegistrationProxy.Make<ICompanySettingsRepository>((method, args) =>
{
    if (method.Name != "VersionsForSitesAsync" || !((IReadOnlyList<Guid>)args[0]!).SequenceEqual([site]))
        throw new Exception("Unexpected Site/Head Office read or write: " + method.Name);
    siteReads++; return Task.FromResult<IReadOnlyList<CompanySiteJurisdictionVersion>>([]);
});
var offices = RegistrationProxy.Make<IOfficeBoundaryRepository>((method, args) =>
{
    if (method.Name != "VersionsAsync" || (Guid)args[0]! != branch) throw new Exception("Unrelated Office read or write: " + method.Name);
    boundaryReads++; return Task.FromResult<IReadOnlyList<OfficeBoundaryVersionResponse>>([boundary]);
});
IServiceCollection selected = new ServiceCollection();
foreach (var descriptor in bindings) selected.Add(descriptor);
selected.AddSingleton(company); selected.AddSingleton(offices);
using var provider = selected.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
using var scope = provider.CreateScope();
var reader = scope.ServiceProvider.GetRequiredService<IAttendancePayrollJurisdictionReader>();
Check(ReferenceEquals(reader, scope.ServiceProvider.GetRequiredService<OfficeAttendancePayrollJurisdictionReader>()),
    "interface alias resolves the exact selected Office instance");
Check(ReferenceEquals(reader, scope.ServiceProvider.GetRequiredService<IAttendancePayrollJurisdictionReader>()), "reader is stable within the authorized scope");
var result = (await reader.ReadAsync([new(date, null, branch, source)], default)).Single();
Check(result is { State: "configured", SiteId: null } && result.BranchId == branch &&
    result.ConfigurationVersionId == boundary.Id && result.JurisdictionCode == boundary.JurisdictionCode, "actual parent factory reads retained Office boundary metadata");
Check(siteReads == 0 && boundaryReads == 1, "Office does not infer a Site or Head Office jurisdiction");
var siteDays = new AttendancePayrollJurisdictionDay[] { new(date, site) };
var direct = await scope.ServiceProvider.GetRequiredService<CompanyPayrollJurisdictionReader>().ReadAsync(siteDays, default);
var decorated = await reader.ReadAsync(siteDays, default);
Check(JsonSerializer.Serialize(direct) == JsonSerializer.Serialize(decorated), "actual Site implementation remains byte-identical through the Office decorator");
Check(siteReads == 2 && boundaryReads == 1, "Site passthrough does not read unrelated Office history");
var missing = (await reader.ReadAsync([new(date, null, branch)], default)).Single();
Check(missing is { State: "unavailable", ConfigurationVersionId: null, JurisdictionCode: null } && siteReads == 2 && boundaryReads == 1,
    "missing Office source stays unavailable without location or money defaults");
using var other = provider.CreateScope();
Check(!ReferenceEquals(reader, other.ServiceProvider.GetRequiredService<IAttendancePayrollJurisdictionReader>()), "separate authorization scopes get separate readers");
var deniedRoot = false;
try { provider.GetRequiredService<IAttendancePayrollJurisdictionReader>(); } catch (InvalidOperationException) { deniedRoot = true; }
Check(deniedRoot, "root resolution cannot accidentally promote a scoped reader");
Console.WriteLine($"PASS: {checks} actual Office payroll DI/alias/scope checks; synthetic repositories, no host/SQL/taxonomy.");

public class RegistrationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, RegistrationProxy>(); ((RegistrationProxy)(object)value).Handler = handler; return value; }
}
