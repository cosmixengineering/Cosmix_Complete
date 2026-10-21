#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Zones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var missing = new List<string>();
void Gate(bool exists, string name) { if (!exists) missing.Add("FAIL: " + name); }
Gate(typeof(ZoneMonitoringService).Assembly.GetType("Cosmix.Application.Attendance.Zones.ZoneOfflineObservationService") is not null,
    "B06 offline observations have a policy-gated own validation service");
Gate(typeof(MyZonesController).Assembly.GetType("Cosmix.Api.Controllers.Attendance.ZoneOfflineObservationsController") is not null,
    "B06 offline observations have a separate authenticated API contract");
Gate(typeof(ZoneRepository).GetMethod("HistoricalDutyQuery", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
    "B06 historical duty query binds employee/session/latest accepted revision at capture");
Gate(typeof(ZoneEvent).GetProperty("WrittenClaimPrivateReference") is not null &&
    typeof(ZoneEvent).GetProperty("WrittenClaimExpiresAtUtc") is not null && typeof(ZoneEvent).GetMethod("EmployeeReport") is not null,
    "B06 employee-report factory retains metadata and separate expiring claim reference without GPS");
Gate(typeof(ZoneRepository).Assembly.GetType("Cosmix.Infrastructure.Zones.EncryptedZoneClaimStore") is not null,
    "B06 claim storage is separate from the location vault and supports expiration");
Gate(typeof(ZoneOfflineObservationService).GetMethod("ReconcileAsync") is not null,
    "B06 expired/missing receipts have authenticated original-key reconciliation");
var registrations = new ServiceCollection(); registrations.AddAttendanceZonesApplication();
registrations.AddAttendanceZonesInfrastructure(new ConfigurationBuilder().Build());
Gate(registrations.Any(x => x.ServiceType == typeof(ZoneOfflineObservationService)) &&
    registrations.Any(x => x.ServiceType == typeof(ZoneOfflineObservationOptions)) &&
    registrations.Any(x => x.ServiceType == typeof(IZoneClaimStore)) && registrations.Any(x => x.ServiceType == typeof(ZoneClaimRetentionService)),
    "B06 existing feature registration entrypoints wire real offline service/options/claim storage/cleanup");
Gate(typeof(ZoneRepository).Assembly.GetType("Cosmix.Infrastructure.Zones.ZoneClaimRetentionWorker") is not null,
    "B06 approved configured claim retention has a real feature-local scheduled worker");
foreach (var failure in missing) Console.Error.WriteLine(failure);
if (missing.Count != 0) return 1;
var checks = 0; var ct = CancellationToken.None;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); checks++; }
async Task Fails(Func<Task> call, string code, int status)
{
    try { await call(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, $"{code}/{status}, got {e.Code}/{e.StatusCode}"); }
}
registrations.AddSingleton(new PrivateFileStorageOptions()); registrations.AddSingleton(TimeProvider.System);
using (var disabledProvider = registrations.BuildServiceProvider())
{
    var disabledOptions = disabledProvider.GetRequiredService<ZoneOfflineObservationOptions>();
    Check(!disabledOptions.Enabled && disabledOptions.Policies.Count == 0 && !disabledProvider.GetRequiredService<IZoneClaimStore>().IsConfigured,
        "real legacy feature entrypoint defaults have no policy, storage path, grace or role activation");
    Check(!disabledProvider.GetRequiredService<IZoneClaimRetentionStatus>().IsAvailable(TimeProvider.System.GetUtcNow()) &&
        registrations.Any(x => x.ImplementationType == typeof(ZoneClaimRetentionWorker)), "feature-local cleanup is registered and health is unavailable before a successful sweep");
}
var f = new OfflineFixture();
var configured = new ServiceCollection();
configured.AddAttendanceZonesInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["ZoneOfflineObservations:Enabled"] = "true", ["ZoneOfflineObservations:Policies:0:ConfigurationVersionId"] = f.Configuration.Id.ToString(),
    ["ZoneOfflineObservations:Policies:0:ApprovedAtUtc"] = f.Policy.ApprovedAtUtc.ToString("O"),
    ["ZoneOfflineObservations:Policies:0:AuthorityEvidenceReference"] = "SYNTHETIC approval",
    ["ZoneOfflineObservations:Policies:0:MaximumDelaySeconds"] = "600", ["ZoneOfflineObservations:Policies:0:ReportRetentionDays"] = "1"
}).Build());
using (var configuredProvider = configured.BuildServiceProvider())
{
    var bound = configuredProvider.GetRequiredService<ZoneOfflineObservationOptions>();
    Check(bound.Enabled && bound.Policies.Single().ConfigurationVersionId == f.Configuration.Id && bound.Policies.Single().MaximumDelaySeconds == 600 &&
        bound.Policies.Single().ReportRetentionDays == 1, "actual feature configuration binder retains explicit synthetic approval and exact delay/retention");
}
await Fails(() => f.Submit(), "zone_offline_policy_unavailable", 503);
Check(f.HistoricalEmployee == f.EmployeeId && f.HistoricalSession == f.Duty.SessionKey && f.HistoricalAt == f.Captured,
    "validation binds the historical reader to server employee and original session at the claimed capture, not receipt time");
Check(f.EligibilityAt == f.Captured && f.SiteDate == ZoneProjection.Date(f.Captured), "historical membership/eligibility uses capture time");
Check(f.Window.EndsAtUtc < f.Now && f.Duty.EndsAtUtc < f.Now, "a closed retained duty/window can be validated without live tracking");
Check(f.Saves == 0 && f.Adds == 0 && f.PrivateWrites == 0 && f.Receipts.Count == 0 && f.Audits.Count == 0,
    "unavailable intake creates no event, checkpoint, window, private object, receipt or success audit");
f.EnablePolicy();
var positive = new OfflineFixture(); positive.EnablePolicy();
var received = await positive.Submit();
var report = positive.Events.Single(); var envelope = positive.Claims.Single().Value;
Check(received.EventId == report.Id && received.EvidenceStatus == "unverified_employee_report" && positive.Adds == 1,
    "explicit approved synthetic policy really appends one unverified employee report");
Check(report.Source == ZoneEventSource.EmployeeReport && report.IsOffline && report.Latitude is null && report.Longitude is null &&
    report.AccuracyMeters is null && report.LocationPrivateReference is null && report.LocationExpiresAtUtc is null && report.ShownInstruction is null,
    "employee-report persistence never manufactures GPS/live instruction or location-vault objects");
Check(report.DeviceCapturedAtUtc == positive.Captured && report.WrittenClaimPrivateReference is not null &&
    report.WrittenClaimExpiresAtUtc == positive.Captured.AddDays(positive.Policy.ReportRetentionDays), "expiry is exactly explicit policy from capture, not an invented grace");
Check(envelope.WrittenClaim == positive.Input.WrittenClaim && envelope.EmployeeId == positive.EmployeeId && envelope.AccountId == positive.Account.Id &&
    envelope.DeviceId == positive.Device.Id && envelope.WindowId == positive.Window.Id && envelope.ConfigurationVersionId == positive.Configuration.Id &&
    envelope.AcceptedCheckInId == positive.Duty.CheckInPunchId && envelope.WorkSessionRevision == positive.Duty.Revision &&
    envelope.NoticeSha256 == positive.Input.NoticeSha256 && envelope.Policy == positive.Policy, "expiring protected claim retains full original forensic bindings");
Check(positive.Receipts.Count == 1 && positive.Audits.Count == 1 && positive.Saves == 1 && positive.PrivateWrites == 2 && positive.ClaimWrites == 1,
    "event/24-hour receipt/audit share the authorized write boundary and only separate protected claim storage holds text");
Check(positive.PrivateObjects.Values.All(x => !System.Text.Encoding.UTF8.GetString(x.Bytes).Contains(positive.Input.WrittenClaim)) &&
    positive.PrivateObjects.Values.All(x => !System.Text.Encoding.UTF8.GetString(x.Bytes).Contains("WrittenClaimPrivateReference")),
    "ordinary audit/receipt contain fingerprint and neutral metadata, never claim text/private reference");
Check(await positive.Submit() == received && positive.Events.Count == 1 && positive.ClaimWrites == 1 && positive.Receipts.Count == 1,
    "exact same-body/device/key retry replays without new event or claim object");
await Fails(() => positive.Submit(positive.Input with { WrittenClaim = "Changed report" }), "idempotency_key_reused", 409);
await Fails(() => positive.Submit(positive.Input with { EventKindHint = ZoneEventKind.Exited }), "idempotency_key_reused", 409);
var originalDevice = positive.Device;
positive.Device = new(positive.Account.Id, "replacement-device", "android", "synthetic", positive.Now.AddDays(-1)); positive.Device.MarkVerified(positive.Now.AddDays(-1));
OfflineFixture.Set(positive.Session, "RegisteredDeviceId", (Guid?)positive.Device.Id);
await Fails(() => positive.Submit(), "idempotency_key_reused", 409);
Check((await positive.Service().ReconcileAsync(positive.Window.Id, "claim-key-001", positive.Context, ct)).Found && positive.Events.Count == 1,
    "new current verified device can only reconcile prior own metadata, never automatically resubmit original claim");
positive.Device = originalDevice; OfflineFixture.Set(positive.Session, "RegisteredDeviceId", (Guid?)originalDevice.Id);
var foreign = new OfflineFixture(); foreign.Events.Add(report);
Check(!(await foreign.Service().ReconcileAsync(positive.Window.Id, "claim-key-001", foreign.Context, ct)).Found,
    "another employee/account cannot reconcile an otherwise known original window/key");
var own = await positive.Service().ReconcileAsync(positive.Window.Id, "claim-key-001", positive.Context, ct);
Check(own.Found && !own.CanResubmit && own.ReceiptState == "current_retained_report_not_original_receipt" && own.Report?.Id == report.Id &&
    own.Report.Kind is null && own.Report.EventKindHint == ZoneEventKind.Entered, "own reconciliation is current neutral metadata, explicitly not the frozen receipt");
var caseMiss = await positive.Service().ReconcileAsync(positive.Window.Id, "CLAIM-key-001", positive.Context, ct);
Check(!caseMiss.Found && !caseMiss.CanResubmit, "original key uses exact case and absence cannot authorize fresh-key retry");
var history = await positive.Monitoring().EventsAsync(null, new(PageSize: 25), true, positive.Context, ct);
Check(history.Items.Single().Id == report.Id && history.Items.Single().Kind is null && history.Items.Single().EvidenceStatus == "unverified_employee_report",
    "actual appended metadata report appears in existing own history without claim text/GPS");
var acknowledged = await positive.Monitoring().RespondAsync(null, report.Id, new(ZoneEventResponseKind.Acknowledged, "Acknowledged unverified claim", null),
    "claim-ack-001", true, positive.Context, ct);
var disputed = await positive.Monitoring().RespondAsync(null, report.Id, new(ZoneEventResponseKind.LocationDisputed, "Disputed unverified claim", acknowledged.LastResponseId),
    "claim-dispute-001", true, positive.Context, ct);
Check(acknowledged.LastResponseKind == ZoneEventResponseKind.Acknowledged && disputed.LastResponseKind == ZoneEventResponseKind.LocationDisputed &&
    disputed.Kind is null && disputed.EvidenceStatus == "unverified_employee_report", "existing own ack/dispute pipeline never promotes claim to verified transition");
var mobile = positive.Identity; positive.Identity = mobile with { Client = SessionClient.Web, MfaSatisfied = true };
positive.Grants = [new(ZonePermissions.Review, true, positive.RoleId, AccessScopeKind.Site, positive.SiteId)];
var reviewed = await positive.Monitoring().RespondAsync(positive.SiteId, report.Id, new(ZoneEventResponseKind.Reviewed, "Reviewed employee claim", disputed.LastResponseId),
    "claim-review-001", false, positive.Context, ct);
Check(reviewed.LastResponseKind == ZoneEventResponseKind.Reviewed && reviewed.Kind is null && reviewed.EvidenceStatus == "unverified_employee_report",
    "current+historical scoped factual review reuses real response pipeline and remains unverified");
var reviewedJson = JsonSerializer.Serialize(reviewed);
Check(!reviewedJson.Contains("CONFIDENTIAL_REASON") && !reviewedJson.Contains("WrittenClaim") && !reviewedJson.Contains("PrivateReference") &&
    !reviewedJson.Contains("Latitude"), "review metadata excludes confidential reason, claim text, private reference and coordinates");
positive.Identity = mobile; positive.Self();
Check((await positive.Service().ReconcileAsync(positive.Window.Id, "claim-key-001", positive.Context, ct)).Report?.LastResponseKind == ZoneEventResponseKind.Reviewed,
    "reconciliation returns current review state rather than the stale frozen original receipt");
positive.Receipts.Clear();
await Fails(() => positive.Submit(), "zone_observation_already_received", 409);
Check(positive.Events.Count == 1 && positive.ClaimWrites == 1, "missing receipt cannot bypass permanent key or rewrite protected claim");
positive.Now = positive.Captured.AddDays(2); OfflineFixture.Set(positive.Session, "ExpiresAt", positive.Now.AddDays(1));
var afterExpiry = await positive.Service().ReconcileAsync(positive.Window.Id, "claim-key-001", positive.Context, ct);
Check(afterExpiry.Found && !afterExpiry.CanResubmit, "claim expiry does not delete immutable own metadata or permit resubmission");
foreach (var (change, index) in new Action<OfflineFixture>[] { g => g.Grants.Clear(), g => g.CurrentSession = false,
    g => OfflineFixture.Set(g.Session, "ExpiresAt", g.Now), g => OfflineFixture.Set(g.Device, "RevokedAt", (DateTimeOffset?)g.Now) }.Select((change, index) => (change, index)))
{
    var final = new OfflineFixture(); final.EnablePolicy(); final.OnClaimStored = () => change(final);
    await Fails(() => final.Submit(), index == 0 ? "access_denied" : "invalid_authentication", index == 0 ? 403 : 401);
    Check(final.Events.Count == 0 && final.Receipts.Count == 0 && final.Audits.Count == 0 && final.PrivateWrites == 0 && final.ClaimWrites == 1,
        "current authorization loss after private I/O never stages a successful event/receipt/audit");
}
var finalDelay = new OfflineFixture(); finalDelay.EnablePolicy(); finalDelay.OnClaimStored = () => finalDelay.Now = finalDelay.Now.AddSeconds(1);
await Fails(() => finalDelay.Submit(), "zone_offline_delay_exceeded", 409);
Check(finalDelay.Events.Count == 0 && finalDelay.Receipts.Count == 0, "policy delay is rechecked after private I/O");
var noStore = new OfflineFixture(); noStore.EnablePolicy(); noStore.IsConfigured = false;
await Fails(() => noStore.Submit(), "zone_claim_storage_unavailable", 503);
var noRetention = new OfflineFixture(); noRetention.EnablePolicy(); noRetention.RetentionAvailable = false;
await Fails(() => noRetention.Submit(), "zone_claim_retention_unavailable", 503);
Check(noRetention.Events.Count == 0 && noRetention.ClaimWrites == 0, "unconfigured/unhealthy claim cleanup cannot activate intake");
var finalRetention = new OfflineFixture(); finalRetention.EnablePolicy(); finalRetention.OnClaimStored = () => finalRetention.RetentionAvailable = false;
await Fails(() => finalRetention.Submit(), "zone_claim_retention_unavailable", 503);
Check(finalRetention.Events.Count == 0 && finalRetention.Receipts.Count == 0, "claim cleanup loss during storage prevents business commit");
var unknownTiming = new OfflineFixture(); unknownTiming.EnablePolicy();
unknownTiming.Grants = [unknownTiming.Grants.Single() with { AuthorityTimingKnown = false }];
await Fails(() => unknownTiming.Submit(), "access_denied", 403);
Check(unknownTiming.ClaimWrites == 0 && unknownTiming.Events.Count == 0, "unknown authority timing is not a role or permit default");
var timedGrant = new OfflineFixture(); timedGrant.EnablePolicy();
timedGrant.Options = timedGrant.Options with { Policies = [timedGrant.Policy with { MaximumDelaySeconds = 601 }] };
timedGrant.Grants = [timedGrant.Grants.Single() with { ValidUntilUtc = timedGrant.Now.AddSeconds(1) }];
timedGrant.OnClaimStored = () => timedGrant.Now = timedGrant.Now.AddSeconds(1);
await Fails(() => timedGrant.Submit(), "access_denied", 403);
Check(timedGrant.Events.Count == 0 && timedGrant.Receipts.Count == 0, "permission interval is checked at last clock after claim I/O, not assumed permanent");
var finalGrantRead = new OfflineFixture(); finalGrantRead.EnablePolicy();
finalGrantRead.Options = finalGrantRead.Options with { Policies = [finalGrantRead.Policy with { MaximumDelaySeconds = 601 }] };
finalGrantRead.Grants = [finalGrantRead.Grants.Single() with { ValidUntilUtc = finalGrantRead.Now.AddSeconds(1) }];
finalGrantRead.OnFinalGrantRead = () => finalGrantRead.Now = finalGrantRead.Now.AddSeconds(1);
await Fails(() => finalGrantRead.Submit(), "access_denied", 403);
Check(finalGrantRead.Events.Count == 0 && finalGrantRead.Receipts.Count == 0, "grant lookup latency is included in the final IsEffectiveAt clock check");
var receiptExpiry = new OfflineFixture(); receiptExpiry.EnablePolicy(); await receiptExpiry.Submit();
receiptExpiry.Now = receiptExpiry.Now.AddDays(2); OfflineFixture.Set(receiptExpiry.Session, "ExpiresAt", receiptExpiry.Now.AddDays(1));
await Fails(() => receiptExpiry.Submit(), "idempotency_key_expired", 409);
Check((await receiptExpiry.Service().ReconcileAsync(receiptExpiry.Window.Id, "claim-key-001", receiptExpiry.Context, ct)).Found,
    "expired 24-hour receipt reconciles original permanent record without resubmission");

foreach (var invalid in new[] {
    f.Input with { AssignmentId = Guid.Empty }, f.Input with { ConfigurationVersionId = Guid.Empty },
    f.Input with { SessionKey = Guid.Empty }, f.Input with { EventKindHint = (ZoneEventKind)0 },
    f.Input with { WrittenClaim = " " }, f.Input with { WrittenClaim = new string('x', 1001) }, f.Input with { WrittenClaim = "bad\0claim" },
    f.Input with { DeviceCapturedAtUtc = default }, f.Input with { DeviceCapturedAtUtc = f.Now.AddSeconds(1) },
    f.Input with { DeviceCapturedAtUtc = f.Captured.ToOffset(TimeSpan.FromHours(5)) },
    f.Input with { DeviceCapturedAtUtc = f.Window.StartsAtUtc.AddTicks(-1) }, f.Input with { DeviceCapturedAtUtc = f.Window.EndsAtUtc },
    f.Input with { NoticeSha256 = "bad" } })
    await Fails(() => f.Submit(invalid), "invalid_zone_offline_observation", 400);
foreach (var key in new string?[] { null, "", "1234567", new('x', 101), "bad key-01", "key-0001\n", "key-0001/" })
    await Fails(() => f.Submit(key: key!), "invalid_idempotency_key", 400);
foreach (var key in new[] { "12345678", new string('x', 100), "Aa09_.:-" })
{
    var validKey = new OfflineFixture(); validKey.EnablePolicy();
    Check((await validKey.Submit(key: key)).EvidenceStatus == "unverified_employee_report", "existing exact 8-100 key rule accepts " + key.Length);
}
await Fails(() => f.Service().SubmitAsync(Guid.Empty, f.Input, "claim-key-001", f.Context, ct), "invalid_zone_offline_observation", 400);
await Fails(() => f.Service().SubmitAsync(f.Window.Id, null!, "claim-key-001", f.Context, ct), "invalid_zone_offline_observation", 400);

var denied = new OfflineFixture(); denied.Grants.Clear();
await Fails(() => denied.Submit(), "access_denied", 403);
Check(denied.WindowReads == 0 && denied.KeyReads == 0, "permission denial precedes owned window/key queries");
denied.Self(); denied.CurrentSession = false;
await Fails(() => denied.Submit(), "invalid_authentication", 401);
denied.CurrentSession = true; denied.Identity = denied.Identity with { Client = SessionClient.Web, MfaSatisfied = true };
await Fails(() => denied.Submit(), "access_denied", 403);
denied.Identity = denied.Identity with { Client = SessionClient.Mobile, MfaSatisfied = false };
OfflineFixture.Set(denied.Device, "RevokedAt", (DateTimeOffset?)denied.Now.AddMinutes(-1));
await Fails(() => denied.Submit(), "invalid_authentication", 401);

foreach (var change in new Action<OfflineFixture>[] {
    g => g.WindowMissing = true,
    g => OfflineFixture.Set(g.Window, "EmployeeId", Guid.NewGuid()),
    g => OfflineFixture.Set(g.Window, "RegisteredDeviceId", (Guid?)Guid.NewGuid()),
    g => OfflineFixture.Set(g.Window, "WorkSessionKey", (Guid?)Guid.NewGuid()),
    g => OfflineFixture.Set(g.Window, "ConfigurationVersionId", (Guid?)null),
    g => OfflineFixture.Set(g.Window, "AcceptedCheckInId", (Guid?)Guid.NewGuid()),
    g => OfflineFixture.Set(g.Window, "NoticeShownAtUtc", g.Captured.AddSeconds(1)) })
{
    var g = new OfflineFixture(); change(g);
    await Fails(() => g.Submit(), "zone_offline_window_unavailable", 409);
}
var laterDevice = new OfflineFixture(); OfflineFixture.Set(laterDevice.Device, "RegisteredAt", laterDevice.Captured.AddSeconds(1));
await Fails(() => laterDevice.Submit(), "zone_capture_precedes_registered_device", 400);
var laterVerification = new OfflineFixture(); OfflineFixture.Set(laterVerification.Device, "VerifiedAt", (DateTimeOffset?)laterVerification.Captured.AddSeconds(1));
await Fails(() => laterVerification.Submit(), "zone_capture_precedes_verified_device", 400);
foreach (var change in new Action<OfflineFixture>[] {
    g => g.DutyMissing = true, g => OfflineFixture.Set(g.Duty, "EmployeeId", Guid.NewGuid()),
    g => OfflineFixture.Set(g.Duty, "SessionKey", Guid.NewGuid()), g => OfflineFixture.Set(g.Duty, "CheckInPunchId", (Guid?)null),
    g => OfflineFixture.Set(g.Duty, "SiteId", (Guid?)null),
    g => OfflineFixture.Set(g.Duty, "StartsAtUtc", g.Captured.AddSeconds(1)),
    g => OfflineFixture.Set(g.Duty, "EndsAtUtc", (DateTimeOffset?)g.Captured) })
{
    var g = new OfflineFixture(); change(g);
    await Fails(() => g.Submit(), "zone_historical_accepted_duty_required", 409);
}
var transfer = new OfflineFixture(); transfer.HistoricalSite = Guid.NewGuid();
await Fails(() => transfer.Submit(), "zone_offline_site_assignment_unavailable", 409);
foreach (var change in new Action<OfflineFixture>[] {
    g => g.Configurations.Clear(), g => OfflineFixture.Set(g.Configuration, "EffectiveToUtc", g.Captured),
    g => OfflineFixture.Set(g.Configuration, "ApprovedAtUtc", g.Window.StartsAtUtc.AddSeconds(1)),
    g => g.Configurations.Add(new(g.SiteId, 2, g.Captured.AddMinutes(-1), g.Now.AddDays(1), 20, 20, 60, 7, 7200,
        "Replacement notice", "synthetic", "Explicit replacement", g.Captured.AddMinutes(-2), "authority", 120)) })
{
    var g = new OfflineFixture(); change(g);
    await Fails(() => g.Submit(), "zone_offline_configuration_unavailable", 409);
}
var wrongNotice = new OfflineFixture();
await Fails(() => wrongNotice.Submit(wrongNotice.Input with { NoticeSha256 = new string('A', 64) }), "zone_offline_notice_mismatch", 409);
foreach (var change in new Action<OfflineFixture>[] {
    g => g.Assignments.Clear(), g => OfflineFixture.Set(g.Assignment, "EffectiveToUtc", g.Captured),
    g => OfflineFixture.Set(g.Assignment, "IsEmployeeOverride", true),
    g => OfflineFixture.Set(g.Assignment, "ApprovedAtUtc", g.Window.StartsAtUtc.AddSeconds(1)),
    g => OfflineFixture.Set(g.Assignment.ZoneVersion, "DirectAuthorityEvidenceReference", null),
    g => OfflineFixture.Set(g.Assignment.ZoneVersion.ZoneType, "Code", "AMBER"),
    g => OfflineFixture.Set(g.Assignment.ZoneVersion, "EntryRule", ZoneEntryRule.Prohibited) })
{
    var g = new OfflineFixture(); change(g);
    await Fails(() => g.Submit(), "zone_offline_assignment_unavailable", 409);
}
var red = new OfflineFixture(); OfflineFixture.Set(red.Assignment.ZoneVersion.ZoneType, "Code", "RED");
OfflineFixture.Set(red.Assignment.ZoneVersion, "EntryRule", ZoneEntryRule.Prohibited);
red.EnablePolicy(); Check((await red.Submit()).EvidenceStatus == "unverified_employee_report" && red.Events.Count == 1,
    "approved Red claim persists only unverified metadata, never entry permission or verified event");
foreach (var policy in new[] {
    f.Policy with { AuthorityEvidenceReference = "" }, f.Policy with { MaximumDelaySeconds = 0 },
    f.Policy with { ReportRetentionDays = 0 }, f.Policy with { MaximumDelaySeconds = 86401, ReportRetentionDays = 1 },
    f.Policy with { ApprovedAtUtc = f.Window.StartsAtUtc.AddSeconds(1) } })
{
    var options = new ZoneOfflineObservationOptions { Enabled = true, Policies = [policy] };
    await Fails(() => f.Service(options).SubmitAsync(f.Window.Id, f.Input, "claim-key-001", f.Context, ct), "zone_offline_policy_unavailable", 503);
}
var delayed = f.Policy with { MaximumDelaySeconds = 599 };
await Fails(() => f.Service(new() { Enabled = true, Policies = [null!] }).SubmitAsync(f.Window.Id, f.Input, "claim-key-001", f.Context, ct), "zone_offline_policy_unavailable", 503);
await Fails(() => f.Service(new() { Enabled = true, Policies = [delayed] }).SubmitAsync(f.Window.Id, f.Input, "claim-key-001", f.Context, ct), "zone_offline_delay_exceeded", 409);
var retention = new OfflineFixture(); retention.Now = retention.Captured.AddDays(1); retention.EnablePolicy();
OfflineFixture.Set(retention.Session, "ExpiresAt", retention.Now.AddDays(1));
await Fails(() => retention.Submit(), "zone_offline_retention_elapsed", 409);
var duplicate = new OfflineFixture(); duplicate.PermanentKeys.Add("claim-key-001");
await Fails(() => duplicate.Submit(), "zone_observation_already_received", 409);
Check(duplicate.KeyEmployee == duplicate.EmployeeId && duplicate.Key == "claim-key-001" && duplicate.PrivateWrites == 0,
    "permanent-key guard binds server employee and exact key before any evidence storage");

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
foreach (var field in new[] { "employeeId", "accountId", "registeredDeviceId", "source", "isOffline", "latitude", "longitude", "accuracyMeters", "privateReference" })
{
    var json = JsonSerializer.SerializeToNode(f.Input, jsonOptions)!.AsObject(); json[field] = "client-controlled";
    try { JsonSerializer.Deserialize<ZoneOfflineObservationRequest>(json, jsonOptions); throw new Exception("Accepted " + field); }
    catch (JsonException) { Check(true, "offline body rejects authority/GPS/private field " + field); }
}
foreach (var field in new[] { "assignmentId", "configurationVersionId", "sessionKey", "deviceCapturedAtUtc", "eventKindHint" })
{
    var json = JsonSerializer.SerializeToNode(f.Input, jsonOptions)!.AsObject(); json.Remove(field);
    try { JsonSerializer.Deserialize<ZoneOfflineObservationRequest>(json, jsonOptions); throw new Exception("Missing " + field); }
    catch (JsonException) { Check(true, "offline original metadata is explicit: " + field); }
}
var controller = typeof(ZoneOfflineObservationsController);
Check(controller.GetCustomAttribute<AuthorizeAttribute>() is not null && controller.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore == true,
    "offline validation API is authenticated and no-store");
var action = controller.GetMethod("Submit")!;
Check(controller.GetCustomAttribute<RouteAttribute>()?.Template == "api/v1/me/attendance/zones/monitoring-windows/{windowId:guid}/offline-observations" &&
    action.GetCustomAttribute<HttpPostAttribute>() is not null && action.GetCustomAttribute<AllowAnonymousAttribute>() is null,
    "offline validation has its own POST and cannot weaken live samples");
Check(action.GetParameters().Any(x => x.GetCustomAttribute<FromHeaderAttribute>()?.Name == "Idempotency-Key") &&
    action.GetCustomAttributes<ProducesResponseTypeAttribute>().Any(x => x.StatusCode == 200 && x.Type == typeof(ZoneOfflineObservationReceipt)),
    "actual intake exposes typed unverified receipt and original key only in header");
Check(controller.GetMethod("Reconcile")!.GetCustomAttribute<HttpGetAttribute>()?.Template == "reconciliation",
    "original-key reconciliation is read-only GET under authenticated own route");

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(
    "Server=b06-checks.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true").AddInterceptors(guard).Options);
var repository = new ZoneRepository(db);
string Sql(string name, params object?[] args) => ((IQueryable)typeof(ZoneRepository).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(repository, args)!).ToQueryString();
var historical = Sql("HistoricalDutyQuery", f.EmployeeId, f.Duty.SessionKey, f.Captured);
Check(historical.Contains(f.EmployeeId.ToString()) && historical.Contains(f.Duty.SessionKey.ToString()) && historical.Contains("[EmployeeId] =") && historical.Contains("[SessionKey] ="),
    "historical SQL scopes exact employee and work-session key");
Check(historical.Contains("[StartsAtUtc] <=") && historical.Contains("[EndsAtUtc] >") && historical.Contains("[EndsAtUtc] IS NULL"),
    "historical SQL accepts latest closed duty only inside actual inclusive/exclusive interval");
Check(historical.Contains("NOT EXISTS") && historical.Contains("[Revision] >") && historical.Contains("PunchAssessments") && historical.Contains("RawPunchEvents"),
    "historical SQL requires latest retained session and accepted latest opening assessment");
Check(historical.Contains("FaceVerificationAttempts") && historical.Contains("[LivenessPassed]") && historical.Contains("[MatchPassed]") &&
    historical.Contains("[CompletedAtUtc] IS NOT NULL") && historical.Contains("[ConsumedByPunchId] ="), "historical SQL fails closed without completed consumed face evidence");
Check(historical.Contains("[ReceivedAtUtc] =") && historical.Contains("[BusinessDate] =") && historical.Contains("[SiteId] =") &&
    historical.Contains("[Action] = 2"), "historical raw source binds date/site/time and denies already accepted checkout at capture");
var keySql = Sql("EventKeyQuery", f.EmployeeId, "Case-Key_001");
Check(keySql.Contains(f.EmployeeId.ToString()) && keySql.Contains("[IdempotencyKey] =") && keySql.Contains("Case-Key_001") && !keySql.Contains("LOWER("),
    "permanent key SQL preserves exact indexed employee/key lookup");
var ownSql = Sql("OwnOfflineEventQuery", f.EmployeeId, f.Account.Id, f.Window.Id, "Case-Key_001");
Check(ownSql.Contains(f.EmployeeId.ToString()) && ownSql.Contains(f.Account.Id.ToString()) && ownSql.Contains(f.Window.Id.ToString()) &&
    ownSql.Contains("[UserAccountId] =") && ownSql.Contains("[DutyMonitoringWindowId] =") && ownSql.Contains("[Source] = 3") && ownSql.Contains("[IsOffline]"),
    "own reconciliation SQL scopes server employee/account/original window and offline source");
var model = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ZoneEvent))!;
Check(model.FindProperty(nameof(ZoneEvent.IdempotencyKey))!.GetCollation() == "Latin1_General_100_BIN2" &&
    model.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "IdempotencyKey" })),
    "existing zone key mapping is case-sensitive and permanently unique");
Check(model.FindProperty(nameof(ZoneEvent.WrittenClaimPrivateReference)) is { IsNullable: true } claimProperty && claimProperty.GetMaxLength() == 500 &&
    model.FindProperty(nameof(ZoneEvent.WrittenClaimExpiresAtUtc))!.IsNullable && model.GetCheckConstraints().Any(x => x.Name == "CK_ZoneEvents_WrittenClaim" &&
    x.Sql.Contains("[Source] = 3") && x.Sql.Contains("[Latitude] IS NULL") && x.Sql.Contains("[WrittenClaimExpiresAtUtc] > [ReceivedAtUtc]")),
    "owned two-column mapping discriminates paired expiring no-GPS employee claims while preserving legacy rows");
foreach (var badScope in new object?[][] { [Guid.Empty, f.Duty.SessionKey, f.Captured], [f.EmployeeId, Guid.Empty, f.Captured] })
{
    try { Sql("HistoricalDutyQuery", badScope); throw new Exception("Accepted unscoped history"); }
    catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { Check(true, "missing historical scope rejected before SQL"); }
}
Check(guard.Attempts == 0 && !db.ChangeTracker.Entries().Any(), "SQL translation opened no connections and staged no entities");
Console.WriteLine($"PASS: {checks} B06 actual offline intake/retry/reconcile/validation/API/SQL checks; synthetic protected I/O, zero SQL connections/hosts/providers.");
return 0;

sealed class OfflineFixture : ICurrentAuthentication, IAccountAccessReader, ISchedulingJournalRepository, IPrivateFileStore, IZoneClaimStore, IZoneClaimRetentionStatus
{
    public readonly Guid EmployeeId = Guid.NewGuid(), SiteId = Guid.NewGuid(), RoleId = Guid.NewGuid();
    public DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    public readonly DateTimeOffset Captured;
    public readonly AuthRequestContext Context = new("127.0.0.1", "b06-offline-check");
    public UserAccount Account; public RegisteredDevice Device; public UserSession Session; public AuthenticatedAccount Identity;
    public ZoneMonitoringConfigurationVersion Configuration; public ZoneAssignment Assignment; public DutyMonitoringWindow Window; public WorkSession Duty;
    public ZoneOfflineObservationRequest Input; public ZoneOfflineObservationPolicy Policy; public ZoneOfflineObservationOptions Options = new();
    public List<ZoneMonitoringConfigurationVersion> Configurations = []; public List<ZoneAssignmentRecord> Assignments = [];
    public List<PermissionGrant> Grants = []; public List<IdempotencyReceipt> Receipts = []; public List<AuditEvent> Audits = [];
    public HashSet<string> PermanentKeys = new(StringComparer.Ordinal);
    public List<ZoneEvent> Events = [];
    public List<ZoneEventResponse> Responses = [];
    public Dictionary<string, ZoneClaimPayload> Claims = new(StringComparer.Ordinal);
    public Dictionary<string, (byte[] Bytes, string Purpose)> PrivateObjects = new(StringComparer.Ordinal);
    public bool IsConfigured { get; set; } = true;
    public bool RetentionAvailable = true;
    public bool IsAvailable(DateTimeOffset now) => RetentionAvailable;
    public Action? OnClaimStored;
    public Action? OnFinalGrantRead;
    private AttendanceOperation Operation = null!;
    private IZoneRepository Repository = null!;
    private IRosterRepository Roster = null!;
    private IAttendanceCaptureRepository Capture = null!;
    public bool CurrentSession = true, WindowMissing, DutyMissing;
    public Guid HistoricalSite, HistoricalEmployee, HistoricalSession, KeyEmployee;
    public DateTimeOffset HistoricalAt, EligibilityAt; public DateOnly SiteDate; public string? Key;
    public int Saves, Adds, PrivateWrites, ClaimWrites, WindowReads, KeyReads;
    public OfflineFixture()
    {
        Captured = Now.AddMinutes(-10); HistoricalSite = SiteId;
        Account = (UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!; Set(Account, "Id", Guid.NewGuid()); Set(Account, "EmployeeId", EmployeeId);
        Device = new(Account.Id, "b06-device", "android", "synthetic", Now.AddDays(-1)); Device.MarkVerified(Now.AddDays(-1));
        Session = (UserSession)Activator.CreateInstance(typeof(UserSession), true)!;
        Set(Session, "Id", Guid.NewGuid()); Set(Session, "UserAccountId", Account.Id); Set(Session, "Client", SessionClient.Mobile);
        Set(Session, "RegisteredDeviceId", (Guid?)Device.Id); Set(Session, "SecurityVersion", 1); Set(Session, "CreatedAt", Now.AddHours(-1)); Set(Session, "ExpiresAt", Now.AddDays(1));
        Identity = new(Account.Id, EmployeeId, Session.Id, 1, SessionClient.Mobile, false); Self();
        var zone = new OperationalZone("B06", "Operational area", SiteId); var type = new ZoneType("OPERATIONAL", "Operational", "green", ZoneEntryRule.Permitted);
        var version = new ZoneVersion(zone.Id, 1, type, ZoneBoundaryKind.Circle, Now.AddDays(-1), Now.AddDays(2),
            "Public instruction", "CONFIDENTIAL_REASON", "synthetic", Now.AddDays(-1), null, 24m, 67m, 100m,
            privateRestrictionReasonReference: "private-restriction", directAuthorityEvidenceReference: "authority");
        Set(version, "Zone", zone); Set(version, "ZoneType", type);
        Assignment = new(version.Id, ZonePopulationKind.Site, Now.AddDays(-1), Now.AddDays(2), null, "synthetic", Now.AddDays(-1),
            siteId: SiteId, directAuthorityEvidenceReference: "authority"); Set(Assignment, "ZoneVersion", version); Assignments.Add(new(Assignment, null));
        Configuration = new(SiteId, 1, Now.AddDays(-1), Now.AddDays(2), 20, 20, 60, 7, 7200, "Exact retained visible notice",
            "synthetic", "Explicit monitoring", Now.AddDays(-1), "authority", 120); Configurations.Add(Configuration);
        Duty = new(Guid.NewGuid(), 2, EmployeeId, ZoneProjection.Date(Now), Now.AddHours(-1), Now.AddMinutes(-2), Guid.NewGuid(), null,
            Now, null, correctionId: Guid.NewGuid(), siteId: SiteId); Set(Duty, "CheckInPunchId", (Guid?)Guid.NewGuid());
        Window = new(EmployeeId, Assignment, Configuration, Duty.CheckInPunchId!.Value, Duty.SessionKey, Device.Id, Now.AddMinutes(-20), Now.AddMinutes(-5));
        Input = new(Assignment.Id, Configuration.Id, Duty.SessionKey, ZoneProjection.NoticeSha(Configuration.VisibleNotice), Captured,
            ZoneEventKind.Entered, "I believe I approached the area during the retained duty.");
        Policy = new(Configuration.Id, Now.AddDays(-1), "authority", 600, 1);
    }
    public void Self() => Grants = [new(ZonePermissions.Self, false, RoleId, AccessScopeKind.Self, EmployeeId) {
        AuthorityTimingKnown = true, ValidFromUtc = Now.AddDays(-1) }];
    public void EnablePolicy() => Options = new() { Enabled = true, Policies = [Policy] };
    public Task<ZoneOfflineObservationReceipt> Submit(ZoneOfflineObservationRequest? input = null, string key = "claim-key-001") => Service().SubmitAsync(Window.Id, input ?? Input, key, Context, CancellationToken.None);
    public ZoneOfflineObservationService Service(ZoneOfflineObservationOptions? options = null)
    {
        var authz = OfflineProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => typeof(OfflineFixture).GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SYNTHETIC"), _ => throw new NotSupportedException(m.Name) });
        var auth = OfflineProxy.Make<IAuthenticationRepository>((m, a) => m.Name switch {
            "GetSessionAsync" => Task.FromResult<UserSession?>(Session), "GetDeviceAsync" => Task.FromResult<RegisteredDevice?>(Device), _ => throw new NotSupportedException(m.Name) });
        var repository = OfflineProxy.Make<IZoneRepository>((m, a) => m.Name switch {
            "WindowAsync" => FindWindow(), "HistoricalDutyAsync" => Historical((Guid)a![0]!, (Guid)a[1]!, (DateTimeOffset)a[2]!),
            "HasEventKeyAsync" => HasKey((Guid)a![0]!, (string)a[1]!),
            "OwnOfflineEventAsync" => OwnEvent((Guid)a![0]!, (Guid)a[1]!, (Guid)a[2]!, (string)a[3]!),
            "EventAsync" => FindEvent((Guid)a![0]!), "EventsAsync" => EventRows(),
            "ConfigurationsAsync" => Task.FromResult<IReadOnlyList<ZoneMonitoringConfigurationVersion>>(Configurations),
            "ApplicableAssignmentsAsync" => Task.FromResult<IReadOnlyList<ZoneAssignmentRecord>>(Assignments),
            "Add" => Add(a![0]!), "SaveChangesAsync" => SaveChangesAsync(CancellationToken.None), _ => throw new Exception("Unexpected live/zone mutation: " + m.Name) });
        var roster = OfflineProxy.Make<IRosterRepository>((m, a) => m.Name switch {
            "EffectiveSiteAsync" => Site((DateOnly)a![1]!), "ValidateEligibilityAsync" => Eligibility((DateTimeOffset)a![4]!),
            "ResourcesAsync" => Task.FromResult<IReadOnlyList<SchedulingResourceScope>>([new([new(EmployeeId: EmployeeId, SiteId: SiteId)])]),
            _ => throw new NotSupportedException(m.Name) });
        var capture = OfflineProxy.Make<IAttendanceCaptureRepository>((m, a) => m.Name == "IdentityAsync" ? Task.FromResult(new CaptureIdentity(Account, Device)) : throw new NotSupportedException(m.Name));
        Operation = new(this, this, authz, this, auth, new OfflineClock(this), this); Repository = repository; Roster = roster; Capture = capture;
        return new(Operation, repository, roster, capture, options ?? Options,
            this, this, this, auth, new OfflineClock(this), this);
    }
    public ZoneMonitoringService Monitoring()
    {
        _ = Service();
        return new(Operation, Repository, Roster, Capture,
            OfflineProxy.Make<IZoneLocationStore>((m, a) => throw new Exception("Offline claim touched live GPS store")), new(),
            OfflineProxy.Make<IZoneRetentionStatus>((m, a) => false),
            OfflineProxy.Make<INotificationOutbox>((m, a) => throw new Exception("Offline claim staged notification")), this,
            OfflineProxy.Make<IAttendanceCursorProtector>((m, a) => throw new Exception("Unexpected cursor operation")), new OfflineClock(this));
    }
    private Task<DutyMonitoringWindow?> FindWindow() { WindowReads++; return Task.FromResult(WindowMissing ? null : Window); }
    private Task<ZoneDuty?> Historical(Guid employee, Guid session, DateTimeOffset at)
    { HistoricalEmployee = employee; HistoricalSession = session; HistoricalAt = at; return Task.FromResult<ZoneDuty?>(DutyMissing ? null : new(Duty, Duty.CheckInPunchId ?? Guid.Empty)); }
    private Task<bool> HasKey(Guid employee, string key) { KeyReads++; KeyEmployee = employee; Key = key; return Task.FromResult(PermanentKeys.Contains(key) || Events.Any(x => x.EmployeeId == employee && x.IdempotencyKey == key)); }
    private Task<ZoneEventRecord?> OwnEvent(Guid employee, Guid account, Guid window, string key)
    {
        var row = Events.SingleOrDefault(x => x.EmployeeId == employee && x.UserAccountId == account && x.DutyMonitoringWindowId == window &&
            x.IdempotencyKey == key && x.Source == ZoneEventSource.EmployeeReport && x.IsOffline);
        return Task.FromResult(row is null ? null : new ZoneEventRecord(row, SiteId, Responses.LastOrDefault(x => x.ZoneEventId == row.Id)));
    }
    private Task<ZoneEventRecord?> FindEvent(Guid id)
    { var row = Events.SingleOrDefault(x => x.Id == id); return Task.FromResult(row is null ? null : new ZoneEventRecord(row, SiteId, Responses.LastOrDefault(x => x.ZoneEventId == row.Id))); }
    private Task<IReadOnlyList<ZoneEventRecord>> EventRows() => Task.FromResult<IReadOnlyList<ZoneEventRecord>>(Events
        .Select(row => new ZoneEventRecord(row, SiteId, Responses.LastOrDefault(x => x.ZoneEventId == row.Id))).ToArray());
    private Task<Guid?> Site(DateOnly date) { SiteDate = date; return Task.FromResult<Guid?>(HistoricalSite); }
    private Task Eligibility(DateTimeOffset at) { EligibilityAt = at; return Task.CompletedTask; }
    private object? Add(object row) { Adds++; if (row is ZoneEvent report) Events.Add(report); else if (row is ZoneEventResponse response) Responses.Add(response);
        else throw new Exception("Unexpected offline mutation " + row.GetType()); return null; }
    public Task<T> Transaction<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct) => call(ct);
    public AuthenticatedAccount Require() => Identity;
    public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    { if (ClaimWrites > 0) OnFinalGrantRead?.Invoke(); return Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants); }
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount identity, CancellationToken ct) => Task.FromResult(CurrentSession);
    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct) =>
        Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key));
    public void AddAudit(AuditEvent row) => Audits.Add(row); public void AddReceipt(IdempotencyReceipt row) => Receipts.Add(row);
    public Task SaveChangesAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
    {
        PrivateWrites++; var key = Guid.NewGuid().ToString("N"); PrivateObjects.Add(key, (bytes.ToArray(), purpose));
        return Task.FromResult(new PrivateFileReference(key, "synthetic", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    { var item = PrivateObjects[reference.StorageKey]; if (item.Purpose != purpose) throw new Exception("Private purpose mismatch"); return Task.FromResult(item.Bytes.ToArray()); }
    public Task<string> StoreAsync(ZoneClaimPayload payload, CancellationToken ct)
    { ClaimWrites++; var key = Guid.NewGuid().ToString("N"); Claims.Add(key, payload); OnClaimStored?.Invoke(); return Task.FromResult(key); }
    public Task<ZoneClaimPayload> ReadAsync(Guid id, Guid employee, Guid window, DateTimeOffset expiry, string reference, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> DeleteExpiredAsync(Guid id, Guid employee, Guid window, DateTimeOffset expiry, string reference, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<ZoneClaimIndexPage> ScanExpiredAsync(DateTimeOffset now, string? cursor, int maximum, CancellationToken ct) => throw new NotSupportedException();
    public static void Set(object value, string name, object? data) => value.GetType().GetProperty(name)!.SetValue(value, data);
}
sealed class OfflineClock(OfflineFixture fixture) : TimeProvider { public override DateTimeOffset GetUtcNow() => fixture.Now; }
public class OfflineProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    public static T Make<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    { var proxy = Create<T, OfflineProxy>(); ((OfflineProxy)(object)proxy).Handler = handler; return proxy; }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("B06 database connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("B06 database connections forbidden"); }
}
