#:sdk Microsoft.NET.Sdk.Web
#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var checks = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL: " + name); checks++; }
var at = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero); var id = Guid.NewGuid();
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["ZoneOfflineObservations:Enabled"] = "true", ["ZoneOfflineObservations:Policies:0:ConfigurationVersionId"] = id.ToString(),
    ["ZoneOfflineObservations:Policies:0:ApprovedAtUtc"] = at.AddDays(-1).ToString("O"),
    ["ZoneOfflineObservations:Policies:0:AuthorityEvidenceReference"] = "SYNTHETIC explicit approval",
    ["ZoneOfflineObservations:Policies:0:MaximumDelaySeconds"] = "600", ["ZoneOfflineObservations:Policies:0:ReportRetentionDays"] = "1"
}).Build();
var bound = configuration.GetSection(ZoneOfflineObservationOptions.SectionName).Get<ZoneOfflineObservationOptions>()!;
Check(bound.Enabled && bound.Policies.Single().ConfigurationVersionId == id && bound.Policies.Single().MaximumDelaySeconds == 600 &&
    bound.Policies.Single().ReportRetentionDays == 1 && bound.Policies.Single().ApprovedAtUtc == at.AddDays(-1),
    "real options binding keeps explicit original configuration/approval/delay/retention");
var defaults = new ZoneOfflineObservationOptions();
Check(!defaults.Enabled && defaults.Policies.Count == 0, "no activated reporting/role/delay/retention defaults");
var services = new ServiceCollection().AddAttendanceZonesApplication();
Check(services.Any(x => x.ServiceType == typeof(ZoneOfflineObservationService)) && services.Any(x => x.ServiceType == typeof(ZoneClaimRetentionService)),
    "existing Application feature entrypoint registers actual intake and cleanup");
var grant = new PermissionGrant(ZonePermissions.Self, false, Guid.NewGuid(), AccessScopeKind.Self, Guid.NewGuid()) {
    AuthorityTimingKnown = true, ValidFromUtc = at, ValidUntilUtc = at.AddSeconds(1) };
Check(!grant.IsEffectiveAt(at.AddTicks(-1)) && grant.IsEffectiveAt(at) && !grant.IsEffectiveAt(at.AddSeconds(1)), "authoritative interval has exact inclusive/exclusive boundaries");
Check(!(grant with { AuthorityTimingKnown = false }).IsEffectiveAt(at), "unknown grant timing cannot authorize a current write");
Check(!JsonSerializer.Serialize(grant).Contains("ValidFromUtc") && !JsonSerializer.Serialize(grant).Contains("AuthorityTimingKnown"),
    "parent timing evidence stays outside client grant JSON");
Console.WriteLine($"PASS: {checks} B06 Application-only real policy binding/registration/authority timing checks; no host/SQL/provider.");
