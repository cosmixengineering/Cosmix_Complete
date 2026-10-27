#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

var checks = 0; var failures = new List<string>();
void Check(bool condition, string name)
{ checks++; if (!condition) { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
async Task Scenario(string name, Func<Task> action)
{ try { await action(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message); } }
async Task Reject(string name, Func<Task> action, string code, int status = 409)
{
    try { await action(); Check(false, name + " unexpectedly succeeded"); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, name + ": expected " + code + ", got " + e.Code); }
}
Action ExpectUnchangedEffects(WorkflowFixture f, string name)
{
    var rows = f.Rows.ToArray(); var audits = f.Audits.ToArray();
    var receipts = f.Receipts.ToArray(); var notifications = f.Notifications.ToArray();
    return () => {
        Check(f.Rows.SequenceEqual(rows), name + " preserves ledger entries");
        Check(f.Audits.SequenceEqual(audits), name + " preserves committed audits");
        Check(f.Receipts.SequenceEqual(receipts), name + " preserves committed receipts");
        Check(f.Notifications.SequenceEqual(notifications), name + " preserves committed outbox");
    };
}
void RevokeAuthority(WorkflowFixture f, string mode)
{
    switch (mode)
    {
        case "grant": f.HasPermission = false; break;
        case "scope": f.Scope = AccessScopeKind.Self; break;
        case "role-assignment": f.FixedAdmin = false; break;
        case "admin-role": f.RoleCode = Role.CreateSystem(SystemRoleKind.SuperAdmin, Guid.NewGuid(), f.Clock.Now).Code; break;
        case "session": f.Current = false; break;
        case "session-expiry": f.Clock.Now = f.SessionExpiresAt; break;
        default: throw new InvalidOperationException("Unknown authority change " + mode);
    }
}
string OperationPurpose(WorkflowFixture f, string command, string kind) =>
    $"attendance:operation:{f.Actor.AccountId:N}:attendance.leave.benefit.{command}:{kind}";
foreach (var cash in new[] { false, true })
    await Scenario("direct award " + cash, async () => {
        var f = new WorkflowFixture(cash); var response = await f.Award();
        Check(response.Status == "awarded" && response.Minutes == 240 && response.StandardDayMinutes == 480 && response.EncashmentEligible == cash,
            "Actual AwardAsync applies configured minutes and cash eligibility " + cash);
        Check(f.Rows.Count == 1 && f.Rows[0].Entry.Id == response.Id && f.Rows[0].Entry.MinutesDelta == 240 &&
            f.Rows[0].Entry.EntryType == LeaveLedgerEntryType.Entitlement, "Award creates one positive entitlement");
        var proof = await f.Evidence.AwardAsync(f.Rows[0], default);
        Check(proof.ActorAccountId == f.Actor.AccountId && proof.Kpi.Score.Id == f.Final.Id && proof.Policy.VersionId == f.Policy.Id,
            "Service-created protected proof verifies independently and binds actor/KPI/policy");
        Check(f.Audits.Count == 2 && f.Receipts.Count == 1 && f.Audits.All(x => x.ProtectedChangesReference is not null),
            "Typed award evidence and operation audit commit with receipt");
        Check(f.Notifications.Count == 1 && f.Notifications[0] == new NotificationIntent(response.Id, f.Recipient!.Value, NotificationTemplates.Decision, response.Id),
            "Award stages one Decision notification for the synthetic employee recipient and immutable entry");
        var repeat = await f.Award();
        Check(repeat == response && f.Rows.Count == 1 && f.Receipts.Count == 1 && f.Notifications.Count == 1,
            "Same-key replay returns original result without another credit or notification");
        await Reject("Same key changed payload", () => f.Service.AwardAsync(f.Employee, f.Request with { Reason = "Changed reason" }, "benefit-award-001", f.Context, default), "idempotency_key_reused");
        f.HasPermission = false;
        await Reject("Replay after grant removal", () => f.Award(), "access_denied", 403);
        Check(f.Rows.Count == 1 && f.Notifications.Count == 1, "Denied replay cannot restage effects");
    });
foreach (var cultureName in new[] { "ar-SA", "th-TH" })
    await Scenario("culture-independent award and replay " + cultureName, async () => {
        var f = new WorkflowFixture();
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            var award = await f.Award();
            Check(f.Rows.Single().Entry.IdempotencyKey == "leave-benefit:2025-07-01",
                cultureName + " actual award persists the invariant natural key");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName == "ar-SA" ? "th-TH" : "ar-SA");
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            Check((await f.Evidence.AwardAsync(f.Rows.Single(), default)).Id == award.Id,
                cultureName + " saved award validates after changing calendar culture");
            Check(await f.Award() == award, cultureName + " same-key replay returns the retained award after culture change");
            await Reject(cultureName + " different key cannot duplicate source after culture change",
                () => f.Award("benefit-culture-duplicate"), "leave_benefit_source_already_awarded");
            Check(f.Rows.Count == 1 && f.Notifications.Count == 1 && f.Receipts.Count == 1,
                cultureName + " culture changes do not duplicate entitlement, receipt or notification");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    });

await Scenario("different business calendars", async () => {
    var f = new WorkflowFixture(sourceFrom: new DateOnly(2025, 1, 1)); var award = await f.Award();
    Check(award.SourceFrom == new DateOnly(2025, 1, 1) && award.SourceToExclusive == new DateOnly(2026, 1, 1) &&
        award.LeaveYearFrom == new DateOnly(2026, 7, 1) && award.LeaveYearToExclusive == new DateOnly(2027, 7, 1),
        "January KPI year maps to configured July leave year using explicit offset");
    Check((await f.Evidence.ReadAsync(f.Employee, award.LeaveYearFrom, award.LeaveYearToExclusive, award.AvailableFrom, default)).Count == 1,
        "Evidence reader accepts the same mixed-calendar service award");
});
await Scenario("duplicate source across versions", async () => {
    var f = new WorkflowFixture(); await f.Award(); f.PublishPolicy(true);
    await Reject("Different request key and policy version", () => f.Award("benefit-award-002"), "leave_benefit_source_already_awarded");
    Check(f.Rows.Count == 1 && f.Notifications.Count == 1, "Source-year identity prevents policy-version double grant");
});
await Scenario("retained source", async () => {
    var f = new WorkflowFixture(); var awarded = await f.Award();
    f.Final = f.Final with { Id = Guid.NewGuid(), Revision = 2, SupersedesId = f.Final.Id, ExactScore = 1m };
    f.PublishPolicy(true);
    var listed = await f.Service.ListAsync(f.Employee, new(2026, 7, 1), false, f.Context, default);
    Check(listed.Count == 1 && listed[0] == awarded, "Later KPI/policy changes do not silently rewrite retained award");
});
await Scenario("no recipient", async () => {
    var f = new WorkflowFixture { Recipient = null }; var award = await f.Award();
    Check(award.Status == "awarded" && f.Rows.Count == 1 && f.Notifications.Count == 0,
        "No active account recipient does not fabricate a recipient or discard entitlement");
});
foreach (var mode in new[] { "permission", "ordinary-leave-permission", "scope", "mfa", "mobile", "custom-role", "superadmin", "self", "missing-employee", "session" })
    await Scenario("authorization " + mode, async () => {
        var f = new WorkflowFixture(); var code = "access_denied"; var status = 403;
        switch (mode) {
            case "permission": f.HasPermission = false; break;
            case "ordinary-leave-permission": f.Permission = LeavePermissions.Review; break;
            case "scope": f.Scope = AccessScopeKind.Self; break;
            case "mfa": f.Actor = f.Actor with { MfaSatisfied = false }; break;
            case "mobile": f.Actor = f.Actor with { Client = SessionClient.Mobile }; break;
            case "custom-role": f.FixedAdmin = false; break;
            case "superadmin": f.RoleCode = Role.CreateSystem(SystemRoleKind.SuperAdmin, Guid.NewGuid(), f.Clock.Now).Code; break;
            case "self": f.Actor = f.Actor with { EmployeeId = f.Employee }; break;
            case "missing-employee": f.Actor = f.Actor with { EmployeeId = Guid.Empty }; break;
            case "session": f.Current = false; code = "invalid_authentication"; status = 401; break;
        }
        await Reject(mode, () => f.Award(), code, status);
        Check(f.Rows.Count == 0 && f.Notifications.Count == 0 && f.Receipts.Count == 0, mode + " produces no business effect");
    });
foreach (var mode in new[] { "inactive", "locked", "stale-kpi", "low-kpi", "foreign-policy", "missing-assignment" })
    await Scenario("eligibility " + mode, async () => {
        var f = new WorkflowFixture(); var request = f.Request; string code;
        switch (mode) {
            case "inactive": f.EmployeeActive = false; code = "leave_employee_inactive"; break;
            case "locked": f.Locked = true; code = "leave_benefit_period_locked"; break;
            case "stale-kpi": request = request with { ExpectedKpiFinalizationId = Guid.NewGuid() }; code = "leave_benefit_kpi_version_changed"; break;
            case "low-kpi": f.SetScore(79.999999m); request = f.Request; code = "leave_benefit_kpi_ineligible"; break;
            case "foreign-policy": request = request with { PolicyVersionId = Guid.NewGuid() }; code = "leave_benefit_policy_unavailable"; break;
            default: f.Assignments = []; code = "leave_benefit_ineligible"; break;
        }
        await Reject(mode, () => f.Service.AwardAsync(f.Employee, request, "benefit-invalid-001", f.Context, default), code);
        Check(f.Rows.Count == 0 && f.Notifications.Count == 0 && f.Receipts.Count == 0, mode + " leaves entitlement unchanged");
    });
foreach (var mode in new[] { "authority", "source", "month-lock", "midnight" })
    await Scenario("recheck after protected write " + mode, async () => {
        var f = new WorkflowFixture();
        f.AfterBenefitWrite = () => {
            if (mode == "authority") f.HasPermission = false;
            if (mode == "source") f.Final = f.Final with { Id = Guid.NewGuid() };
            if (mode == "month-lock") f.Locked = true;
            if (mode == "midnight") f.Clock.Now = f.Clock.Now.AddDays(1);
        };
        var code = mode switch { "authority" => "access_denied", "source" => "leave_benefit_kpi_version_changed",
            "month-lock" => "leave_benefit_period_locked", _ => "leave_benefit_date_changed" };
        await Reject(mode, () => f.Award(), code, mode == "authority" ? 403 : 409);
        Check(f.Rows.Count == 0 && f.Notifications.Count == 0 && f.Receipts.Count == 0, mode + " cannot commit stale award");
    });
await Scenario("authorization after notification staging", async () => {
    var f = new WorkflowFixture(); f.AfterNotification = () => f.HasPermission = false;
    await Reject("Grant revoked during outbox staging", () => f.Award(), "access_denied", 403);
    Check(f.Rows.Count == 0 && f.Notifications.Count == 0 && f.Receipts.Count == 0, "Late authority failure rolls back award, outbox and receipt together");
});
foreach (var command in new[] { "award", "revoke" })
    foreach (var mode in new[] { "session", "session-expiry" })
        await Scenario(command + " session change during benefit proof write " + mode, async () => {
            var f = new WorkflowFixture();
            var award = command == "revoke" ? await f.Award() : null;
            f.SessionExpiresAt = f.Clock.Now.AddMinutes(1);
            var name = command + " benefit proof write " + mode;
            var unchanged = ExpectUnchangedEffects(f, name); var reached = false;
            f.AfterBenefitWrite = () => { reached = true; RevokeAuthority(f, mode); };
            // The real administrator query validates the session, so this earlier guard is already expected to deny.
            await Reject(name, async () => { if (award is null) await f.Award(); else await f.Revoke(award); }, "access_denied", 403);
            Check(reached, name + " reaches the protected proof I/O boundary");
            unchanged();
        });
foreach (var command in new[] { "award", "revoke" })
    foreach (var kind in new[] { "result", "audit" })
        foreach (var mode in new[] { "grant", "scope", "role-assignment", "admin-role", "session", "session-expiry" })
            await Scenario(command + " authority change during operation " + kind + " write " + mode, async () => {
                var f = new WorkflowFixture();
                var award = command == "revoke" ? await f.Award() : null;
                f.SessionExpiresAt = f.Clock.Now.AddMinutes(1);
                var name = command + " operation " + kind + " write " + mode;
                var unchanged = ExpectUnchangedEffects(f, name); var reached = false; var staged = false;
                var rows = f.Rows.Count; var audits = f.Audits.Count;
                var receipts = f.Receipts.Count; var notifications = f.Notifications.Count;
                f.AfterPrivateWrite.Add(OperationPurpose(f, command, kind), () => {
                    reached = true;
                    staged = f.Rows.Count == rows + 1 && f.Audits.Count == audits + 1 &&
                        f.Receipts.Count == receipts && f.Notifications.Count == notifications + 1;
                    RevokeAuthority(f, mode);
                });
                var session = mode is "session" or "session-expiry";
                await Reject(name, async () => { if (award is null) await f.Award(); else await f.Revoke(award); },
                    session ? "invalid_authentication" : "access_denied", session ? 401 : 403);
                Check(reached && staged, name + " changes authority after service effects are staged but before receipt commit");
                unchanged();
            });
foreach (var command in new[] { "award", "revoke" })
    foreach (var mode in new[] { "grant", "scope", "role-assignment", "admin-role", "session", "session-expiry" })
        await Scenario(command + " authority change during replay read " + mode, async () => {
            var f = new WorkflowFixture(); var award = await f.Award();
            if (command == "revoke") await f.Revoke(award);
            f.SessionExpiresAt = f.Clock.Now.AddMinutes(1);
            var name = command + " replay result read " + mode;
            var unchanged = ExpectUnchangedEffects(f, name); var reached = false;
            f.AfterPrivateRead.Add(OperationPurpose(f, command, "result"), () => { reached = true; RevokeAuthority(f, mode); });
            var session = mode is "session" or "session-expiry";
            await Reject(name, async () => { if (command == "award") await f.Award(); else await f.Revoke(award); },
                session ? "invalid_authentication" : "access_denied", session ? 401 : 403);
            Check(reached, name + " changes authority inside the saved receipt read");
            unchanged();
        });
await Scenario("unused revocation", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); var original = f.Rows[0].Entry;
    var reversed = await f.Revoke(award);
    Check(reversed.Status == "revoked" && reversed.Id == award.Id && original.MinutesDelta == 240 &&
        f.Rows.Count == 2 && f.Rows[1].Entry.MinutesDelta == -240 && f.Rows[1].Entry.ReversesEntryId == award.Id,
        "Unused award revocation appends an exact reversal and preserves original");
    Check((await f.Evidence.ReadAsync(f.Employee, new(2026, 7, 1), new(2027, 7, 1), new(2026, 10, 19), default)).Count == 0,
        "Service-created reversal is independently verified and removes usable credit");
    Check(f.Notifications.Count == 2 && f.Notifications[1].EventId == f.Rows[1].Entry.Id && f.Notifications[1].ResourceId == f.Rows[1].Entry.Id &&
        f.Notifications[1].RecipientAccountId == f.Recipient, "Revocation notification binds its own immutable reversal entry");
    Check(await f.Revoke(award) == reversed && f.Rows.Count == 2 && f.Notifications.Count == 2, "Revocation replay has no duplicate effect");
    await Reject("Another revocation", () => f.Revoke(award, "benefit-revoke-002"), "leave_benefit_already_revoked");
    await Reject("Revoked source cannot be granted again", () => f.Award("benefit-award-003"), "leave_benefit_source_already_awarded");
});
foreach (var kind in new[] { LeaveLedgerEntryType.Utilization, LeaveLedgerEntryType.Reservation, LeaveLedgerEntryType.Encashment })
    await Scenario("revocation consumption " + kind, async () => {
        var f = new WorkflowFixture(); var award = await f.Award(); f.Consumption(kind, released: false);
        await Reject("History " + kind, () => f.Revoke(award), "leave_benefit_consumed_adjustment_review_required");
        Check(f.Rows.Count == 1 && f.Notifications.Count == 1, "Outstanding " + kind + " history blocks revocation");
    });
foreach (var kind in new[] { LeaveLedgerEntryType.Utilization, LeaveLedgerEntryType.Reservation })
    await Scenario("fully released usage " + kind, async () => {
        var f = new WorkflowFixture(); var award = await f.Award(); f.Consumption(kind, released: true);
        var result = await f.Revoke(award);
        Check(result.Status == "revoked" && f.Rows.Count == 2 && f.Uses.Count == 2,
            "Exact release permits revocation without rewriting historical " + kind);
        Check((await f.Evidence.ReadAsync(f.Employee, new(2026, 7, 1), new(2027, 7, 1), new(2026, 10, 19), default)).Count == 0,
            "Released-history revocation retains independently verifiable evidence");
        Check(await f.Revoke(award) == result && f.Rows.Count == 2, "Released-history retry does not duplicate reversal");
    });
await Scenario("consumption races protected storage", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); f.Consumption(LeaveLedgerEntryType.Utilization, true);
    f.AfterBenefitWrite = () => f.Consumption(LeaveLedgerEntryType.Utilization, false);
    await Reject("New consumption", () => f.Revoke(award), "leave_benefit_consumed_adjustment_review_required");
    Check(f.Rows.Count == 1 && f.Notifications.Count == 1, "New consumption cannot be lost behind evidence IO");
});
await Scenario("stale revocation fingerprint", async () => {
    var f = new WorkflowFixture(); var award = await f.Award();
    await Reject("Changed award fingerprint", () => f.Revoke(award with { EvidenceFingerprint = new string('0', 64) }), "leave_benefit_source_changed");
    Check(f.Rows.Count == 1 && f.Notifications.Count == 1, "Stale fingerprint cannot revoke");
});
await Scenario("future release cannot create unreadable revocation", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); f.Consumption(LeaveLedgerEntryType.Utilization, true);
    typeof(LeaveLedgerEntry).GetProperty(nameof(LeaveLedgerEntry.RecordedAt))!.SetValue(f.Uses.Last(), f.Clock.Now.AddMinutes(1));
    var unchanged = ExpectUnchangedEffects(f, "future release");
    await Reject("Future release", () => f.Revoke(award), "leave_benefit_usage_changed");
    unchanged();
});
foreach (var mode in new[] { "duplicate", "over-limit" })
    await Scenario("award must retain readable assignment evidence " + mode, async () => {
        var f = new WorkflowFixture();
        f.Assignments = mode == "duplicate" ? [f.Assignments[0], f.Assignments[0]] :
            Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToArray();
        var unchanged = ExpectUnchangedEffects(f, "assignment " + mode);
        await Reject("Invalid award assignments " + mode, () => f.Award(), "leave_benefit_ineligible");
        unchanged();
    });
await Scenario("consumed correction and exact release", async () => {
    var f = new WorkflowFixture(cash: true); var award = await f.Award();
    var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    var request = await f.CorrectionRequest(award, [new(use.Id, 120)]);
    var result = await f.Correct(award, request);
    Check(result.Status == "corrected" && result.Correction is { RemovedUnusedMinutes: 120, ConsumedMinutes: 120,
        ProtectedConsumedMinutes: 120, Status: "pending_admin_review" }, "Unused half removed, consumed half retained for Admin review");
    Check(f.Rows.Count == 2 && f.Rows[0].Entry.MinutesDelta == 240 && f.Rows[1].Entry.MinutesDelta == -240,
        "Correction is append-only and never rewrites the original award");
    var credits = await f.Evidence.ReadAsync(f.Employee, new(2026, 7, 1), new(2027, 7, 1), new(2026, 10, 19), default);
    Check(credits.Count == 1 && credits[0].Minutes == 120 && !credits[0].Encashable && credits[0].ConsumptionOffset,
        "Consumed protection is an explicit non-spendable offset");
    var usage = new LeaveBalanceUsage(120, 120, [new(120, 480, LeaveKind.Sick)]) { Credits = credits };
    Check(!LeaveTreatmentRules.ExceedsDays(usage, LeaveKind.Sick, 480, 480, 1, 1) &&
        LeaveTreatmentRules.ExceedsDays(usage, LeaveKind.Sick, 481, 480, 1, 1), "No future base quota cut or new earned capacity");
    Check(JsonSerializer.Serialize(await f.Correct(award, request)) == JsonSerializer.Serialize(result) && f.Rows.Count == 2,
        "Correction retry has identical serialized evidence and one durable effect");
    await Reject("Changed retry", () => f.Correct(award, request with { Reason = "Different correction" }), "idempotency_key_reused");
    await Reject("Different correction key", () => f.Correct(award, request, "correction-again"), "leave_benefit_already_revoked");
    f.Release(use);
    Check((await f.Evidence.ReadAsync(f.Employee, new(2026, 7, 1), new(2027, 7, 1), new(2026, 10, 19), default)).Count == 0,
        "Cancelled source cannot turn consumed protection into newly spendable credit");
    var history = await f.Service.ListAsync(f.Employee, new(2026, 7, 1), false, f.Context, default);
    Check(history.Single().Correction is { ConsumedMinutes: 120, ProtectedConsumedMinutes: 0, Status: "pending_admin_review" },
        "Admin history preserves original review obligation after factual cancellation");
});
foreach (var kind in new[] { LeaveLedgerEntryType.Utilization, LeaveLedgerEntryType.Encashment })
    await Scenario("full consumed correction " + kind, async () => {
        var f = new WorkflowFixture(cash: true); var award = await f.Award(); var use = f.Consumption(kind, false, 240);
        var result = await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 240)]));
        Check(result.Correction is { RemovedUnusedMinutes: 0, ConsumedMinutes: 240 }, "Full consumption retained " + kind);
        Check(f.Uses.Count == 1 && f.Uses[0] == use && f.Notifications.Count == 2, "No consumption/payment mutation " + kind);
    });
await Scenario("unused correction", async () => {
    var f = new WorkflowFixture(); var award = await f.Award();
    var result = await f.Correct(award, await f.CorrectionRequest(award, []));
    Check(result.Correction is { RemovedUnusedMinutes: 240, ConsumedMinutes: 0, Status: "corrected_unused" }, "Entire unused award removed");
});
await Scenario("encashment correction after completed year", async () => {
    var f = new WorkflowFixture(cash: true); var award = await f.Award();
    f.Clock.Now = new(2027, 7, 2, 8, 0, 0, TimeSpan.Zero); f.SessionExpiresAt = f.Clock.Now.AddDays(1);
    var use = f.Consumption(LeaveLedgerEntryType.Encashment, false, 120);
    var result = await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
    Check(result.Correction is { ConsumedMinutes: 120, Status: "pending_admin_review" },
        "Completed unlocked year supports real encashment timing without a payment/refund");
});
await Scenario("leave-only award cannot be attributed to cash", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Encashment, false, 120);
    await Reject("Cash attribution", () => f.Correct(award, new(award.EvidenceFingerprint,
        LeaveBenefitUsage.Fingerprint(f.UsageSnapshot()), [new(use.Id, 120)], "Synthetic review")), "leave_benefit_allocation_source_invalid");
});
await Scenario("one source cannot fund two corrected awards", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); var second = await f.SecondRetainedAward();
    var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
    var request = await f.CorrectionRequest(second, [new(use.Id, 1)]);
    var unchanged = ExpectUnchangedEffects(f, "allocation reuse");
    await Reject("Previously allocated source", () => f.Correct(second, request, "second-correction"), "leave_benefit_usage_already_allocated");
    unchanged();
});
foreach (var mode in new[] { "wrong-type", "wrong-policy", "wrong-amount", "duplicate", "orphan" })
    await Scenario("invalid release cannot establish unused credit " + mode, async () => {
        var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, true);
        var release = f.Uses.Last();
        if (mode == "duplicate") f.Release(use);
        else if (mode == "orphan") f.Uses.Remove(use);
        else typeof(LeaveLedgerEntry).GetProperty(mode == "wrong-type" ? nameof(LeaveLedgerEntry.LeaveTypeId) :
            mode == "wrong-policy" ? nameof(LeaveLedgerEntry.PolicyVersionId) : nameof(LeaveLedgerEntry.MinutesDelta))!.SetValue(release,
                mode == "wrong-amount" ? (object)2 : Guid.NewGuid());
        await Reject("Invalid pair", () => f.Revoke(award), "leave_benefit_usage_conflict");
        Check(f.Rows.Count == 1, "Malformed release cannot withdraw entitlement");
    });
foreach (var mode in new[] { "missing", "over-use", "over-award", "duplicate", "reservation", "released", "old-use", "stale", "locked" })
    await Scenario("correction rejects " + mode, async () => {
        var f = new WorkflowFixture(); var award = await f.Award();
        var use = f.Consumption(mode == "reservation" ? LeaveLedgerEntryType.Reservation : LeaveLedgerEntryType.Utilization, mode == "released", 300);
        var allocation = new LeaveBenefitAllocation(mode == "missing" ? Guid.NewGuid() : use.Id, mode == "over-use" ? 301 : mode == "over-award" ? 241 : 120);
        var request = await f.CorrectionRequest(award, mode == "duplicate" ? [allocation, allocation] : [allocation]);
        var code = mode switch {
            "missing" or "released" => "leave_benefit_allocation_source_changed",
            "over-use" or "old-use" => "leave_benefit_allocation_source_invalid",
            "over-award" => "leave_benefit_allocation_exceeds_award", "duplicate" => "invalid_leave_benefit_allocation",
            "reservation" => "leave_benefit_pending_reservation", "stale" => "leave_benefit_usage_changed",
            _ => "leave_benefit_period_locked" };
        if (mode == "old-use") {
            typeof(LeaveLedgerEntry).GetProperty(nameof(LeaveLedgerEntry.RecordedAt))!.SetValue(use, f.Clock.Now.AddDays(-1));
            request = await f.CorrectionRequest(award, [allocation]);
        }
        if (mode == "stale") request = request with { ExpectedUsageFingerprint = new string('F', 64) };
        if (mode == "locked") f.Locked = true;
        var unchanged = ExpectUnchangedEffects(f, mode);
        await Reject(mode, () => f.Correct(award, request), code, mode == "duplicate" ? 400 : 409);
        unchanged();
    });
foreach (var mode in new[] { "grant", "scope", "role-assignment", "admin-role", "session" })
    foreach (var moment in new[] { "before", "storage", "replay" })
        await Scenario("correction authority " + mode + " " + moment, async () => {
            var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
            var request = await f.CorrectionRequest(award, [new(use.Id, 120)]);
            if (moment == "replay") await f.Correct(award, request);
            var unchanged = ExpectUnchangedEffects(f, moment);
            if (moment == "storage") f.AfterBenefitWrite = () => RevokeAuthority(f, mode); else RevokeAuthority(f, mode);
            // Mid-proof IO reaches the earlier administrator guard; entry/replay reaches the session guard.
            var sessionFirst = mode == "session" && moment != "storage";
            await Reject(mode, () => f.Correct(award, request), sessionFirst ? "invalid_authentication" : "access_denied", sessionFirst ? 401 : 403);
            unchanged();
        });
await Scenario("correction usage races evidence IO", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    var request = await f.CorrectionRequest(award, [new(use.Id, 120)]); var unchanged = ExpectUnchangedEffects(f, "usage race");
    f.AfterBenefitWrite = () => f.Release(use);
    await Reject("Changed usage", () => f.Correct(award, request), "leave_benefit_usage_changed"); unchanged();
});
foreach (var stage in new[] { "proof", "result", "audit", "notification" })
    await Scenario("correction storage/outbox rollback " + stage, async () => {
        var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
        var request = await f.CorrectionRequest(award, [new(use.Id, 120)]); var unchanged = ExpectUnchangedEffects(f, stage);
        Action failure = () => throw new IOException("Synthetic controlled failure");
        if (stage == "proof") f.AfterBenefitWrite = failure;
        else if (stage == "notification") f.AfterNotification = failure;
        else f.AfterPrivateWrite.Add(OperationPurpose(f, "correct", stage), failure);
        try { await f.Correct(award, request); Check(false, "Controlled failure must propagate"); }
        catch (IOException e) { Check(e.Message == "Synthetic controlled failure", "Correct failure propagated"); }
        unchanged();
    });
await Scenario("null allocation item", async () => {
    var f = new WorkflowFixture(); var award = await f.Award();
    await Reject("Null allocation", () => f.Correct(award, new(award.EvidenceFingerprint, new string('A', 64), [null!], "Synthetic review")),
        "invalid_leave_benefit_correction", 400);
});
await Scenario("self list isolation", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); f.Actor = f.Actor with { EmployeeId = f.Employee, Client = SessionClient.Mobile, MfaSatisfied = false };
    var own = await f.Service.ListAsync(Guid.NewGuid(), new(2026, 7, 1), true, f.Context, default);
    Check(own.Count == 1 && own[0].Id == award.Id, "Self list binds authenticated employee despite caller-supplied employee");
    f.Actor = f.Actor with { EmployeeId = Guid.NewGuid() };
    Check((await f.Service.ListAsync(f.Employee, new(2026, 7, 1), true, f.Context, default)).Count == 0, "Another employee cannot read award via self route");
});
await Scenario("waiver disabled until explicit dated policy", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
    var request = await f.ResolutionRequest(award); var unchanged = ExpectUnchangedEffects(f, "disabled waiver");
    await Reject("Unconfigured waiver", () => f.Resolve(award, request), "leave_benefit_waiver_not_configured");
    unchanged();
    Check((await f.Service.CorrectionImpactAsync(f.Employee, award.Id, f.Context, default)).Resolution!.Status == "pending_admin_review",
        "Unconfigured obligation stays pending");
});
await Scenario("waiver resolves obligation without touching credit or consumption", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
    f.EnableWaiver(); var request = await f.ResolutionRequest(award);
    var rows = f.Rows.ToArray(); var uses = f.Uses.ToArray(); var noticeCount = f.Notifications.Count;
    var result = await f.Resolve(award, request);
    Check(result.Correction is { Status: "waived", ConsumedMinutes: 120, ProtectedConsumedMinutes: 120, Waiver.WaivedMinutes: 120 },
        "Exact reviewed obligation waived while consumed offset remains");
    Check(f.Rows.SequenceEqual(rows) && f.Uses.SequenceEqual(uses), "Waiver never mutates or appends financial ledger/consumption");
    Check(f.Notifications.Count == noticeCount + 1, "Waiver stages one notification");
    var credits = await f.Evidence.ReadAsync(f.Employee, award.LeaveYearFrom, award.LeaveYearToExclusive, award.AvailableFrom, default);
    Check(credits.Single() is { Minutes: 120, ConsumptionOffset: true, Encashable: false }, "Waiver does not cut untouched quota or add spendable credit");
    Check(JsonSerializer.Serialize(await f.Resolve(award, request)) == JsonSerializer.Serialize(result), "Same-key waiver replay returns retained result");
    Check(f.Notifications.Count == noticeCount + 1, "Replay never duplicates waiver notification");
    await Reject("Changed waiver retry", () => f.Resolve(award, request with { Reason = "Changed reason" }), "idempotency_key_reused");
    await Reject("Separate duplicate waiver", () => f.Resolve(award, request, "benefit-waiver-other"), "leave_benefit_resolution_changed");
    Check((await f.Service.ListAsync(f.Employee, award.LeaveYearFrom, false, f.Context, default)).Single().Correction!.Status == "waived",
        "History reads retained waiver status");
    f.Release(use);
    Check((await f.Evidence.ReadAsync(f.Employee, award.LeaveYearFrom, award.LeaveYearToExclusive, award.AvailableFrom, default)).Count == 0,
        "Cancellation after waiver cannot release new spendable credit");
    var after = (await f.Service.CorrectionImpactAsync(f.Employee, award.Id, f.Context, default)).Resolution!;
    Check(after.Status == "waived" && after.ProtectedConsumedMinutes == 0 && after.WaivedMinutes == 120,
        "Source cancellation retains immutable waived obligation");
});
foreach (var mode in new[] { "correction", "usage", "version", "policy", "disabled", "self", "decision", "unused" })
    await Scenario("waiver fails closed " + mode, async () => {
        var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
        await f.Correct(award, await f.CorrectionRequest(award, mode == "unused" ? [] : [new(use.Id, 120)]));
        f.EnableWaiver(mode != "disabled"); var request = await f.ResolutionRequest(award);
        var code = "leave_benefit_resolution_changed"; var status = 409;
        switch (mode) {
            case "correction": request = request with { ExpectedCorrectionFingerprint = new string('F', 64) }; break;
            case "usage": f.Release(use); code = "leave_benefit_usage_changed"; break;
            case "version": request = request with { ExpectedResolutionVersion = new string('F', 64) }; break;
            case "policy": request = request with { PolicyVersionId = Guid.NewGuid() }; code = "leave_benefit_policy_unavailable"; break;
            case "disabled": code = "leave_benefit_waiver_not_configured"; break;
            case "self": f.Actor = f.Actor with { EmployeeId = f.Employee }; code = "access_denied"; status = 403; break;
            case "decision": request = request with { Decision = (LeaveBenefitResolutionDecision)2 }; code = "invalid_leave_benefit_resolution"; status = 400; break;
            case "unused": code = "leave_benefit_consumed_obligation_required"; break;
        }
        var unchanged = ExpectUnchangedEffects(f, mode);
        await Reject(mode, () => f.Resolve(award, request), code, status); unchanged();
    });
foreach (var mode in new[] { "usage", "authority", "policy", "storage", "receipt", "replay-authority" })
    await Scenario("waiver IO boundary " + mode, async () => {
        var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
        await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
        f.EnableWaiver(); var request = await f.ResolutionRequest(award);
        if (mode == "replay-authority") await f.Resolve(award, request);
        var unchanged = ExpectUnchangedEffects(f, mode);
        if (mode == "replay-authority") f.AfterPrivateRead.Add(OperationPurpose(f, "correction.resolve", "result"), () => f.HasPermission = false);
        else if (mode == "receipt") f.AfterPrivateWrite.Add(OperationPurpose(f, "correction.resolve", "result"), () => f.HasPermission = false);
        else f.AfterBenefitWrite = () => {
            if (mode == "usage") f.Release(use);
            else if (mode == "authority") f.HasPermission = false;
            else if (mode == "policy") f.EnableWaiver(false);
            else throw new IOException("Synthetic waiver storage failure");
        };
        if (mode == "storage") {
            try { await f.Resolve(award, request); Check(false, "Waiver storage failure propagates"); }
            catch (IOException) { Check(true, "Waiver storage failure propagates"); }
        }
        else await Reject(mode, () => f.Resolve(award, request), mode == "usage" ? "leave_benefit_usage_changed" :
            mode == "policy" ? "leave_benefit_waiver_not_configured" : "access_denied", mode is "usage" or "policy" ? 409 : 403);
        unchanged();
    });
await Scenario("mixed consumption released before Admin resolution", async () => {
    var f = new WorkflowFixture(cash: true); var award = await f.Award();
    var leave = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    var cash = f.Consumption(LeaveLedgerEntryType.Encashment, false, 60);
    var correctionRequest = await f.CorrectionRequest(award, [new(leave.Id, 120), new(cash.Id, 60)]);
    var corrected = await f.Correct(award, correctionRequest);
    Check(corrected.Correction is { RemovedUnusedMinutes: 60, ConsumedMinutes: 180, ProtectedConsumedMinutes: 180 },
        "Mixed consumption retains only its exact allocated total");
    f.Release(leave);
    var impact = await f.Service.CorrectionImpactAsync(f.Employee, award.Id, f.Context, default);
    Check(impact.Resolution is { ConsumedMinutes: 180, ProtectedConsumedMinutes: 60, Status: "pending_admin_review" },
        "Release before review removes only the leave offset, preserving the original obligation");
    f.EnableWaiver(); var request = await f.ResolutionRequest(award);
    var rows = f.Rows.ToArray(); var uses = f.Uses.ToArray();
    var resolved = await f.Resolve(award, request);
    Check(resolved.Correction is { RemovedUnusedMinutes: 60, ConsumedMinutes: 180, ProtectedConsumedMinutes: 60,
        Status: "waived", Waiver.WaivedMinutes: 180 }, "Admin resolves the historical obligation after partial cancellation");
    var credits = await f.Evidence.ReadAsync(f.Employee, award.LeaveYearFrom, award.LeaveYearToExclusive, award.AvailableFrom, default);
    Check(credits.Single() is { Minutes: 60, ConsumptionOffset: true, Encashable: false },
        "Only the unreleased encashment remains protected after waiver");
    Check(f.Rows.SequenceEqual(rows) && f.Uses.SequenceEqual(uses), "Mixed resolution leaves the original financial history intact");
    var audits = f.Audits.ToArray(); var receipts = f.Receipts.ToArray(); var notifications = f.Notifications.ToArray();
    Check(await f.Award() == award, "Award retry after correction and waiver returns its original receipt");
    Check(JsonSerializer.Serialize(await f.Correct(award, correctionRequest)) == JsonSerializer.Serialize(corrected),
        "Correction retry after release and waiver returns its original receipt");
    Check(JsonSerializer.Serialize(await f.Resolve(award, request)) == JsonSerializer.Serialize(resolved),
        "Resolution retry after mixed-source cancellation returns its original receipt");
    Check(f.Rows.SequenceEqual(rows) && f.Uses.SequenceEqual(uses), "Historical retries preserve the financial history");
    Check(f.Receipts.SequenceEqual(receipts), "Historical retries preserve original receipts");
    Check(f.Notifications.SequenceEqual(notifications), "Historical retries do not repeat notifications");
    Check(f.Audits.Take(audits.Length).SequenceEqual(audits) && f.Audits.Skip(audits.Length).Select(x => x.EventType).SequenceEqual(new[] {
        "attendance.leave.benefit.award.replay", "attendance.leave.benefit.correct.replay", "attendance.leave.benefit.correction.resolve.replay" }),
        "Historical retries append only the three expected replay audit events");
    Check((await f.Service.ListAsync(f.Employee, award.LeaveYearFrom, false, f.Context, default)).Single().Correction is
        { Status: "waived", ProtectedConsumedMinutes: 60 }, "Receipt replay cannot regress current history to an earlier lifecycle state");
});
await Scenario("fully released obligation still requires explicit Admin resolution", async () => {
    var f = new WorkflowFixture(); var award = await f.Award();
    var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 240);
    await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 240)]));
    f.Release(use); f.EnableWaiver();
    var request = await f.ResolutionRequest(award); var result = await f.Resolve(award, request);
    Check(result.Correction is { ConsumedMinutes: 240, ProtectedConsumedMinutes: 0, Status: "waived", Waiver.WaivedMinutes: 240 },
        "Fully cancelled consumption can still resolve the retained Admin obligation");
    Check((await f.Evidence.ReadAsync(f.Employee, award.LeaveYearFrom, award.LeaveYearToExclusive, award.AvailableFrom, default)).Count == 0,
        "Waiving a fully released obligation cannot restore spendable credit");
    var unchanged = ExpectUnchangedEffects(f, "resolved reversal guards");
    await Reject("Revoke corrected and resolved award", () => f.Revoke(award), "leave_benefit_already_revoked");
    await Reject("Reaward resolved source", () => f.Award("resolved-source-again"), "leave_benefit_source_already_awarded");
    unchanged();
});
await Scenario("pending encashment completes before correction and waiver", async () => {
    var f = new WorkflowFixture(cash: true); var award = await f.Award();
    var reservation = f.Consumption(LeaveLedgerEntryType.Reservation, false, 120);
    var stale = await f.CorrectionRequest(award, []);
    var unchanged = ExpectUnchangedEffects(f, "reserved award correction");
    await Reject("Pending reservation", () => f.Correct(award, stale), "leave_benefit_pending_reservation"); unchanged();
    f.Release(reservation); var cash = f.Consumption(LeaveLedgerEntryType.Encashment, false, 120);
    await Reject("Reservation completion invalidates old impact", () => f.Correct(award, stale), "leave_benefit_usage_changed");
    var corrected = await f.Correct(award, await f.CorrectionRequest(award, [new(cash.Id, 120)]));
    Check(corrected.Correction is { RemovedUnusedMinutes: 120, ConsumedMinutes: 120, ProtectedConsumedMinutes: 120 },
        "Completed encashment is attributed once without counting its released reservation");
    f.EnableWaiver(); await f.Resolve(award, await f.ResolutionRequest(award));
    Check(f.Uses.Count == 3 && f.Rows.Count == 2 && f.Receipts.Count == 3 && f.Notifications.Count == 3,
        "Reservation completion lifecycle has exactly one award, correction and resolution effect");
});
foreach (var mode in new[] { "missing", "changed" })
    await Scenario("resolved consumption remains immutable " + mode, async () => {
        var f = new WorkflowFixture(); var award = await f.Award();
        var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
        await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
        f.EnableWaiver(); var request = await f.ResolutionRequest(award); await f.Resolve(award, request);
        if (mode == "missing") f.Uses.Remove(use);
        else typeof(LeaveLedgerEntry).GetProperty(nameof(LeaveLedgerEntry.MinutesDelta))!.SetValue(use, -121);
        var unchanged = ExpectUnchangedEffects(f, "altered resolved source " + mode);
        await Reject("Resolved history rejects " + mode, () => f.Service.ListAsync(f.Employee, award.LeaveYearFrom, false, f.Context, default),
            "leave_benefit_correction_source_changed");
        await Reject("Resolved replay rejects " + mode, () => f.Resolve(award, request), "leave_benefit_correction_source_changed");
        unchanged();
    });
await Scenario("waiver replay requires its durable resolution fact", async () => {
    var f = new WorkflowFixture(); var award = await f.Award(); var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
    f.EnableWaiver(); var request = await f.ResolutionRequest(award); await f.Resolve(award, request);
    f.Audits.RemoveAll(x => x.TargetType == LeaveBenefitEvidence.ResolutionTargetType);
    var unchanged = ExpectUnchangedEffects(f, "missing waiver fact replay");
    await Reject("Missing waiver fact", () => f.Resolve(award, request), "leave_benefit_resolution_evidence_conflict");
    unchanged();
});
async Task<Func<Task>> BenefitCommand(WorkflowFixture f, string command)
{
    if (command == "award") return async () => { await f.Award(); };
    var award = await f.Award();
    if (command == "revoke") return async () => { await f.Revoke(award); };
    if (command == "list") return async () => { await f.Service.ListAsync(f.Employee, award.LeaveYearFrom, false, f.Context, default); };
    if (command == "impact") return async () => { await f.Service.CorrectionImpactAsync(f.Employee, award.Id, f.Context, default); };
    var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    var correction = await f.CorrectionRequest(award, [new(use.Id, 120)]);
    if (command == "correct") return async () => { await f.Correct(award, correction); };
    await f.Correct(award, correction);
    f.EnableWaiver();
    var resolution = await f.ResolutionRequest(award);
    return async () => { await f.Resolve(award, resolution); };
}
string OperationName(string command) => command == "resolve" ? "correction.resolve" : command;
foreach (var command in new[] { "award", "revoke", "correct", "resolve", "list", "impact" })
    await Scenario("separate explicit grant with fixed Admin " + command, async () => {
        var f = new WorkflowFixture();
        var action = await BenefitCommand(f, command);
        f.SeparateBenefitRole = true;
        var audits = f.Audits.Count;
        await action();
        Check(f.Audits.Skip(audits).Any(x => x.TargetType == "Attendance" && x.ActorRoleCode == "BENEFIT_REVIEW"),
            command + " operation audit records the explicit custom permission basis");
        Check(f.Audits.Where(x => x.TargetType is "LeaveBenefit" or "LeaveBenefitResolution").All(x => x.ActorRoleCode == "ADMIN"),
            command + " typed facts retain independently verified fixed ADMIN authority");
        var proof = await f.Evidence.AwardAsync(f.Rows[0], default);
        Check(proof.ActorRole == "ADMIN", command + " retained award remains valid without relabeling its role");
        var retained = f.Files.ToDictionary(x => x.Key, x => Convert.ToHexString(SHA256.HashData(x.Value.Bytes)));
        await f.Service.ListAsync(f.Employee, proof.YearFrom, false, f.Context, default);
        Check(retained.All(x => Convert.ToHexString(SHA256.HashData(f.Files[x.Key].Bytes)) == x.Value),
            command + " history leaves protected evidence bytes unchanged");
        if (command is "list" or "impact") return;
        var effectCounts = (f.Rows.Count, f.Receipts.Count, f.Notifications.Count);
        await action();
        Check(effectCounts == (f.Rows.Count, f.Receipts.Count, f.Notifications.Count),
            command + " custom grant replay retains one business effect");
    });
foreach (var command in new[] { "award", "revoke", "correct", "resolve", "list", "impact" })
foreach (var mode in new[] { "custom-only", "non-admin", "admin-name-only", "expired-fixed", "scheduled-fixed", "revoked-fixed",
    "inactive-fixed", "missing-grant", "expired-grant", "scope", "mfa", "mobile", "self" })
    await Scenario("separate grant denied " + command + " " + mode, async () => {
        var f = new WorkflowFixture();
        var action = await BenefitCommand(f, command);
        f.SeparateBenefitRole = true;
        switch (mode) {
            case "custom-only": f.FixedAdmin = false; break;
            case "non-admin": f.RoleCode = "SUPERADMIN"; break;
            case "admin-name-only": f.FixedAdmin = false; f.OtherSystemAdministrator = true; break;
            case "expired-fixed": f.FixedUntil = f.Clock.Now; f.OtherSystemAdministrator = true; break;
            case "scheduled-fixed": f.FixedFrom = f.Clock.Now.AddMinutes(1); f.OtherSystemAdministrator = true; break;
            case "revoked-fixed": f.FixedRevokedAt = f.Clock.Now; f.OtherSystemAdministrator = true; break;
            case "inactive-fixed": f.FixedRoleActive = false; f.OtherSystemAdministrator = true; break;
            case "missing-grant": f.HasPermission = false; break;
            case "expired-grant": f.GrantUntil = f.Clock.Now; break;
            case "scope": f.Scope = AccessScopeKind.Employee; break;
            case "mfa": f.Actor = f.Actor with { MfaSatisfied = false }; break;
            case "mobile": f.Actor = f.Actor with { Client = SessionClient.Mobile }; break;
            case "self": f.Actor = f.Actor with { EmployeeId = f.Employee }; break;
        }
        if (mode == "self" && command == "list") {
            await action();
            Check(true, "Management list permits own history without permitting own decisions");
            return;
        }
        var unchanged = ExpectUnchangedEffects(f, command + mode);
        await Reject(command + mode, action, "access_denied", 403);
        unchanged();
    });
foreach (var command in new[] { "award", "revoke", "correct", "resolve" })
foreach (var stage in new[] { "proof", "result", "audit", "notification", "replay" })
foreach (var loss in new[] { "fixed", "grant" })
    await Scenario("separate authority IO recheck " + command + " " + stage + " " + loss, async () => {
        var f = new WorkflowFixture();
        var action = await BenefitCommand(f, command);
        f.SeparateBenefitRole = true;
        if (stage == "replay") await action();
        Action revoke = () => {
            if (loss == "fixed") f.FixedUntil = f.Clock.Now;
            else f.HasPermission = false;
        };
        if (stage == "proof") f.AfterBenefitWrite = revoke;
        else if (stage == "notification") f.AfterNotification = revoke;
        else if (stage == "replay") f.AfterPrivateRead.Add(OperationPurpose(f, OperationName(command), "result"), revoke);
        else f.AfterPrivateWrite.Add(OperationPurpose(f, OperationName(command), stage), revoke);
        var unchanged = ExpectUnchangedEffects(f, command + stage + loss);
        await Reject(command + stage + loss, action, "access_denied", 403);
        unchanged();
    });
foreach (var command in new[] { "list", "impact" })
foreach (var loss in new[] { "fixed", "grant" })
    await Scenario("separate authority read recheck " + command + " " + loss, async () => {
        var f = new WorkflowFixture();
        var action = await BenefitCommand(f, command);
        f.SeparateBenefitRole = true;
        f.AfterPrivateRead.Add(LeaveBenefitEvidence.Purpose(f.Employee, f.Rows[0].Entry.Id), () => {
            if (loss == "fixed") { f.FixedUntil = f.Clock.Now; f.OtherSystemAdministrator = true; }
            else f.HasPermission = false;
        });
        var unchanged = ExpectUnchangedEffects(f, command + loss);
        await Reject(command + loss, action, "access_denied", 403);
        unchanged();
    });
await Scenario("fixed Admin located beyond first assignment page", async () => {
    var f = new WorkflowFixture { SeparateBenefitRole = true, PriorAssignmentCount = 101 };
    Check((await f.Award()).Status == "awarded", "Historical/custom assignment pages cannot hide a valid fixed Admin");
});
foreach (var command in new[] { "award", "revoke", "correct", "resolve" })
foreach (var stage in new[] { "commit", "replay" })
foreach (var timing in new[] { "expiry", "revocation" })
foreach (var exactBoundary in new[] { false, true })
    await Scenario("fixed Admin final lookup timing " + command + " " + stage + " " + timing + " " + exactBoundary, async () => {
        var f = new WorkflowFixture();
        var action = await BenefitCommand(f, command);
        f.SeparateBenefitRole = true;
        if (stage == "replay") await action();
        var boundary = f.Clock.Now.AddMinutes(1);
        if (timing == "expiry") f.FixedUntil = boundary;
        else f.FixedRevokedAt = boundary;
        var lookupReached = false;
        Action arm = () => f.AfterFixedRoleLookup = () => {
            lookupReached = true;
            f.Clock.Now = exactBoundary ? boundary : boundary.AddTicks(-1);
        };
        if (stage == "commit") f.AfterPrivateWrite.Add(OperationPurpose(f, OperationName(command), "audit"), arm);
        else f.AfterPrivateRead.Add(OperationPurpose(f, OperationName(command), "result"), arm);
        var unchanged = ExpectUnchangedEffects(f, command + stage + timing);
        if (exactBoundary) {
            await Reject("Fixed Admin ended during final lookup " + command + stage + timing, action, "access_denied", 403);
            unchanged();
        }
        else {
            await action();
            Check(true, command + stage + timing + " still accepts one tick before the exclusive boundary");
        }
        Check(lookupReached && f.Clock.Now == (exactBoundary ? boundary : boundary.AddTicks(-1)),
            command + stage + timing + " clock changes inside final fixed-role lookup with custom grant still valid");
    });
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} leave benefit workflow checks. Real service, authorization and KPI validator; synthetic repositories/outbox; no SQL or host.");
return failures.Count == 0 ? 0 : 1;

public class WorkflowProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, WorkflowProxy>(); ((WorkflowProxy)(object)value).Handler = handler; return value; }
}
public sealed class WorkflowClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public sealed class WorkflowFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Category = Guid.NewGuid(), Role = Guid.NewGuid();
    public readonly Guid BenefitRole = Guid.NewGuid();
    public bool SeparateBenefitRole, OtherSystemAdministrator, FixedRoleActive = true;
    public int PriorAssignmentCount;
    public DateTimeOffset FixedFrom = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset? FixedUntil, FixedRevokedAt, GrantUntil;
    public Guid? Recipient = Guid.NewGuid();
    public readonly DateOnly SourceFrom;
    public readonly WorkflowClock Clock = new();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool Current = true, FixedAdmin = true, HasPermission = true, EmployeeActive = true, Locked;
    public DateTimeOffset SessionCreatedAt = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);
    public DateTimeOffset SessionExpiresAt = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);
    public string RoleCode = Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), DateTimeOffset.UtcNow).Code, Permission = LeaveBenefitPermissions.Review;
    public AccessScopeKind Scope = AccessScopeKind.All;
    public IReadOnlyList<Guid> Assignments = [Guid.NewGuid()];
    public Action? AfterBenefitWrite, AfterNotification, AfterFixedRoleLookup;
    public readonly Dictionary<string, Action> AfterPrivateWrite = new(StringComparer.Ordinal);
    public readonly Dictionary<string, Action> AfterPrivateRead = new(StringComparer.Ordinal);
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-benefit-workflow");
    public readonly List<LeaveBenefitRow> Rows = [];
    public readonly List<LeaveLedgerEntry> Uses = [];
    public readonly List<AuditEvent> Audits = [];
    public readonly List<IdempotencyReceipt> Receipts = [];
    public readonly List<NotificationIntent> Notifications = [];
    public readonly Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public readonly LeaveType Type = new("SYNTHETIC", "Synthetic benefit leave", true, false, true, LeaveKind.Sick);
    public PolicyVersion Policy = null!;
    public KpiSource Source = null!;
    public FinalizedKpiScore Final = null!;
    public LeaveBenefitService Service;
    public LeaveBenefitEvidence Evidence;
    public AwardLeaveBenefitRequest Request => new(Type.Id, Policy.Id, SourceFrom, Final.Id, "Synthetic award");
    public WorkflowFixture(bool cash = false, DateOnly? sourceFrom = null)
    {
        SourceFrom = sourceFrom ?? new DateOnly(2025, 7, 1); PublishPolicy(cash); SetScore(80m);
        var current = WorkflowProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = WorkflowProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            HasPermission && (GrantUntil is null || (DateTimeOffset)a[1]! < GrantUntil)
                ? [new(Permission, false, SeparateBenefitRole ? BenefitRole : Role, Scope, Scope == AccessScopeKind.All ? null : Actor.EmployeeId),
                    new(LeavePermissions.Self, false, SeparateBenefitRole ? BenefitRole : Role, AccessScopeKind.Self, Actor.EmployeeId)] : []));
        var authorization = WorkflowProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult((Guid)a[0]! == Actor.AccountId && (Guid)a[1]! == Role &&
                FixedCurrent((DateTimeOffset)a[2]!)),
            "IsCurrentAdministratorAsync" => Task.FromResult((FixedCurrent((DateTimeOffset)a[1]!) || OtherSystemAdministrator) && SessionCurrent((DateTimeOffset)a[1]!) &&
                ((AuthenticatedAccount)a[0]!).Client == SessionClient.Web && ((AuthenticatedAccount)a[0]!).MfaSatisfied),
            "GetAssignmentRoleCodeAsync" => AssignmentRoleCode((Guid)a[0]!),
            "ListAssignmentsAsync" when (Guid)a[0]! == Actor.AccountId => Task.FromResult(AssignmentPage((AccessListRequest)a[1]!)),
            _ => throw new InvalidOperationException("Unexpected authorization " + m.Name)
        });
        var store = WorkflowProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "ReadAsync" => ReadFile((PrivateFileReference)a[0]!, (string)a[1]!),
            "WriteAsync" => WriteFile((byte[])a[0]!, (string)a[1]!),
            _ => throw new InvalidOperationException("Unexpected storage " + m.Name)
        });
        var journal = WorkflowProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent(Clock.GetUtcNow())), "SaveChangesAsync" => Task.CompletedTask,
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            _ => throw new InvalidOperationException("Unexpected journal " + m.Name)
        });
        var auth = WorkflowProxy.Make<IAuthenticationRepository>((m, a) => throw new InvalidOperationException("No device/provider calls allowed"));
        var operation = new AttendanceOperation(current, access, authorization, journal, auth, Clock, store);
        var benefits = WorkflowProxy.Make<ILeaveBenefitRepository>((m, a) => m.Name switch {
            "YearAsync" => Task.FromResult<IReadOnlyList<LeaveBenefitRow>>(Rows.Where(x => x.Entry.EmployeeId == (Guid)a[0]! && x.Entry.LeaveYearStart == (DateOnly)a[1]! && x.Entry.LeaveYearEnd == (DateOnly)a[2]!).ToArray()),
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Entry.EmployeeId == (Guid)a[0]! && x.Entry.Id == (Guid)a[1]!)),
            "SourceExistsAsync" => Task.FromResult(Rows.Any(x => x.Entry.EmployeeId == (Guid)a[0]! && x.Entry.IdempotencyKey == (string)a[1]!)),
            "RecipientAsync" when (Guid)a[0]! == Employee => Task.FromResult(Recipient),
            "UsageAsync" when (Guid)a[0]! == Employee => Task.FromResult(UsageSnapshot()),
            "ResolutionFactsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(Audits.Where(x => x.EmployeeScopeId == (Guid)a[0]! &&
                x.TargetType == LeaveBenefitEvidence.ResolutionTargetType && x.TargetId == ((Guid)a[1]!).ToString()).ToArray()),
            "AddResolution" => Add(Audits, (AuditEvent)a[0]!),
            "ResolutionReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == ((Guid)a[0]!).ToString() &&
                x.Operation == LeaveBenefitService.ResolutionOperation && x.Key == (string)a[1]!)),
            "Add" => AddBenefit((LeaveLedgerEntry)a[0]!, (AuditEvent)a[1]!), "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new InvalidOperationException("Unexpected benefits " + m.Name)
        });
        Evidence = new(operation, benefits);
        var leaves = WorkflowProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "EmployeeExistsAsync" => Task.FromResult((Guid)a[0]! == Employee && EmployeeActive),
            "AnyLockedAsync" => Task.FromResult(Locked),
            "TypeAsync" => Task.FromResult<LeaveType?>((Guid)a[0]! == Type.Id ? Type : null),
            "PolicyAsync" => Task.FromResult<LeavePolicyEvidence?>((Guid)a[0]! == Employee && (Guid)a[1]! == Type.Id && (Guid)a[2]! == Policy.Id
                ? new(Policy, Category, new(2020, 1, 1), true, [], new(0, 0, [])) { AssignmentIds = Assignments } : null),
            _ => throw new InvalidOperationException("Unexpected leave " + m.Name)
        });
        var ledger = WorkflowProxy.Make<ILeaveEncashmentRepository>((m, a) => m.Name == "LedgerAsync" && (Guid)a[0]! == Employee
            ? Task.FromResult<IReadOnlyList<LeaveLedgerEntry>>(Uses.Where(x => x.LeaveYearStart == (DateOnly)a[1]! && x.LeaveYearEnd == (DateOnly)a[2]!).ToArray())
            : throw new InvalidOperationException("Unexpected ledger " + m.Name));
        var finalized = WorkflowProxy.Make<IFinalizedKpiReader>((m, a) => (Guid)a[0]! == Employee && (DateOnly)a[1]! == SourceFrom
            ? Task.FromResult<FinalizedKpiScore?>(Final) : throw new InvalidOperationException("Incorrect KPI identity"));
        var requests = WorkflowProxy.Make<IKpiRepository>((m, a) => m.Name == "PendingAsync" && (Guid)a[0]! == Employee
            ? Task.FromResult<IReadOnlyList<ApprovalRequest>>([]) : throw new InvalidOperationException("Unexpected KPI write"));
        var disputes = WorkflowProxy.Make<IKpiDisputeRepository>((m, a) => m.Name == "YearAsync" && (Guid)a[0]! == Employee && (DateOnly)a[1]! == SourceFrom
            ? Task.FromResult<IReadOnlyList<ApprovalRequest>>([]) : throw new InvalidOperationException("Unexpected dispute read"));
        var sources = WorkflowProxy.Make<IKpiSourceReader>((m, a) => m.Name == "ReadAsync" && (Guid)a[0]! == Employee && (DateOnly)a[1]! == SourceFrom.AddYears(1).AddDays(-1)
            ? Task.FromResult(Source) : throw new InvalidOperationException("Incorrect KPI source interval"));
        var kpis = new LeaveBenefitKpiReader(operation, finalized, disputes, new(disputes, operation, store), requests, sources);
        var outbox = WorkflowProxy.Make<INotificationOutbox>((m, a) => {
            if (m.Name != "StageAsync") throw new InvalidOperationException("Unexpected notification operation");
            Notifications.Add((NotificationIntent)a[0]!); AfterNotification?.Invoke(); return Task.FromResult(true);
        });
        Service = new(operation, leaves, benefits, Evidence, kpis, ledger, store, access, authorization, Clock, outbox);
    }
    public Task<LeaveBenefitView> Award(string key = "benefit-award-001") => Service.AwardAsync(Employee, Request, key, Context, default);
    public Task<LeaveBenefitView> Revoke(LeaveBenefitView award, string key = "benefit-revoke-001") =>
        Service.RevokeAsync(Employee, award.Id, new(award.EvidenceFingerprint, "Synthetic unused award revocation"), key, Context, default);
    public async Task<CorrectLeaveBenefitRequest> CorrectionRequest(LeaveBenefitView award, IReadOnlyList<LeaveBenefitAllocation> allocations) =>
        new(award.EvidenceFingerprint, (await Service.CorrectionImpactAsync(Employee, award.Id, Context, default)).UsageFingerprint, allocations, "Synthetic Admin correction");
    public Task<LeaveBenefitView> Correct(LeaveBenefitView award, CorrectLeaveBenefitRequest request, string key = "benefit-correct-001") =>
        Service.CorrectAsync(Employee, award.Id, request, key, Context, default);
    public async Task<ResolveLeaveBenefitCorrectionRequest> ResolutionRequest(LeaveBenefitView award)
    {
        var impact = await Service.CorrectionImpactAsync(Employee, award.Id, Context, default);
        return new(Policy.Id, LeaveBenefitResolutionDecision.Waive, impact.Resolution!.CorrectionFingerprint,
            impact.UsageFingerprint, impact.Resolution.Version, "Synthetic documented waiver");
    }
    public Task<LeaveBenefitView> Resolve(LeaveBenefitView award, ResolveLeaveBenefitCorrectionRequest request, string key = "benefit-waiver-001") =>
        Service.ResolveCorrectionAsync(Employee, award.Id, request, key, Context, default);
    public void EnableWaiver(bool enabled = true)
    {
        var settings = LeavePolicySettings.Parse(Policy.RulesJson, Policy.RulesSchemaVersion);
        settings = settings with { Benefits = settings.Benefits! with {
            ConsumedCorrectionResolution = new(1, enabled, "Synthetic approved waiver authority") } };
        typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.RulesJson))!.SetValue(Policy, JsonSerializer.Serialize(settings));
    }
    public IReadOnlyList<LeaveBenefitUsageEntry> UsageSnapshot() => Uses.Select(x =>
        new LeaveBenefitUsageEntry(x.Id, x.EmployeeId, x.LeaveTypeId, x.PolicyVersionId, x.LeaveYearStart, x.LeaveYearEnd,
            x.EntryType, x.MinutesDelta, x.LeaveRequestId, x.ReversesEntryId, x.RecordedAt, 480)).ToArray();
    public async Task<LeaveBenefitView> SecondRetainedAward()
    {
        var original = await Evidence.AwardAsync(Rows[0], default);
        var from = original.Kpi.Score.From.AddDays(1);
        var row = new LeaveLedgerEntry(Employee, Type.Id, original.YearFrom, original.YearTo, LeaveLedgerEntryType.Entitlement,
            original.Minutes, null, null, Policy.Id, "Synthetic distinct retained source", LeaveBenefitEvidence.SourceKey(from), Actor.AccountId.ToString(), Clock.Now);
        var proof = original with { Id = row.Id, Reason = row.Reason, ActorRole = "Admin", Kpi = original.Kpi with {
            Score = original.Kpi.Score with { Id = Guid.NewGuid(), From = from, ToExclusive = from.AddYears(1) }
        } };
        var reference = AttendanceOperation.Reference(await WriteFile(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveBenefitEvidence.Purpose(Employee, row.Id)));
        Rows.Add(new(row, new AuditEvent(LeaveBenefitEvidence.AwardEvent, Actor.AccountId.ToString(), "Admin", LeaveBenefitEvidence.TargetType,
            row.Id.ToString(), Clock.Now, Context.CorrelationId, employeeScopeId: Employee, protectedChangesReference: reference)));
        _ = await Evidence.AwardAsync(Rows.Last(), default);
        return LeaveBenefitEvidence.View(proof, false);
    }
    private bool SessionCurrent(DateTimeOffset now) => Current && SessionCreatedAt <= now && now < SessionExpiresAt;
    private Task<string?> AssignmentRoleCode(Guid assignmentId)
    {
        if (assignmentId == BenefitRole) return Task.FromResult<string?>("BENEFIT_REVIEW");
        var role = RoleCode;
        var callback = AfterFixedRoleLookup;
        AfterFixedRoleLookup = null;
        callback?.Invoke();
        return Task.FromResult<string?>(role);
    }
    private bool FixedCurrent(DateTimeOffset now) => FixedAdmin && FixedRoleActive && FixedFrom <= now &&
        (FixedUntil is null || now < FixedUntil) && (FixedRevokedAt is null || now < FixedRevokedAt);
    private PagedResult<AccessAssignmentResponse> AssignmentPage(AccessListRequest request)
    {
        var rows = Enumerable.Range(0, PriorAssignmentCount).Select(_ => new AccessAssignmentResponse(Guid.NewGuid(),
            Guid.NewGuid(), "HISTORICAL", FixedFrom, FixedFrom.AddDays(1), null, [], "")).ToList();
        rows.Add(new(Role, Role, RoleCode, FixedFrom, FixedUntil, FixedRevokedAt, [], ""));
        return new(rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray(),
            rows.Count, request.Page, request.PageSize);
    }
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var rows = Rows.Count; var audits = Audits.Count; var receipts = Receipts.Count; var notifications = Notifications.Count;
        try { return await action(ct); }
        catch { Rows.RemoveRange(rows, Rows.Count - rows); Audits.RemoveRange(audits, Audits.Count - audits);
            Receipts.RemoveRange(receipts, Receipts.Count - receipts); Notifications.RemoveRange(notifications, Notifications.Count - notifications); throw; }
    }
    public void PublishPolicy(bool cash)
    {
        var settings = new LeavePolicySettings(18, 6, 12, 4, 480, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Sick]) {
            SettingsVersion = 4, YearStartMonth = 7, YearStartDay = 1, Benefits = new(LeaveKind.Sick, 4, 80m, 240, 1, 3660, cash),
            Encashment = cash ? new([LeaveKind.Sick], 4, 80m, 4800, 30, 1m, 2, MidpointRounding.ToEven, "PKR", EncashmentValuationDate.LastDayOfLeaveYear, 366) : null
        };
        var policy = new Policy("SYN-" + Guid.NewGuid().ToString("N"), "Synthetic leave policy", PolicyCategory.Leave);
        Policy = new(policy.Id, 1, new(2026, 7, 1), new(2027, 7, 1), "leave-settings-v4", JsonSerializer.Serialize(settings), null, null,
            Actor.AccountId.ToString(), Clock.Now.AddDays(-10), PolicyAuthority.DirectHr);
        typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.Policy))!.SetValue(Policy, policy);
    }
    public void SetScore(decimal score)
    {
        var to = SourceFrom.AddYears(1); var policy = Guid.NewGuid(); var assignment = Guid.NewGuid();
        var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["SYNTHETIC"], new("Synthetic", KpiPermissions.Decide, false, []),
            new KpiRules(score, SourceFrom.Month, 2, AdvancedRounding.AwayFromZero, 0, 100, KpiLateBasis.PercentagePoints, 0.8m, 0.5m,
                KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive });
        var days = Enumerable.Range(0, to.DayNumber - SourceFrom.DayNumber).Select(i => new KpiDayInput(SourceFrom.AddDays(i), Guid.NewGuid(), new string('A', 64), false, null, null, null, null)).ToArray();
        var input = new KpiInput(rules, SourceFrom, to, days, []); var calculation = AttendanceKpiCalculation.Calculate(input);
        Source = new(Employee, SourceFrom, to, policy, calculation, new string('B', 64), [new([new(EmployeeId: Employee)])], [assignment]) {
            SourceManifest = new(AttendanceKpiCalculation.Version, input, days.Select(x => new KpiSavedDay(x.Date, x.SummaryId, 1,
                "synthetic-v1", x.SourceFingerprint, Guid.NewGuid(), Guid.NewGuid(), policy, [assignment], new string('A', 64))).ToArray(), null)
        };
        Final = new(1, Guid.NewGuid(), Employee, SourceFrom, to, 1, null, Guid.NewGuid(), Guid.NewGuid(), policy, score,
            calculation.DisplayScore!.Value, calculation.Fingerprint, Source.Fingerprint, Guid.NewGuid(), Clock.Now.AddDays(-5), Source.Scopes);
    }
    public LeaveLedgerEntry Consumption(LeaveLedgerEntryType kind, bool released, int minutes = 1)
    {
        var row = new LeaveLedgerEntry(Employee, Type.Id, new(2026, 7, 1), new(2027, 7, 1), kind, -minutes,
            kind == LeaveLedgerEntryType.Utilization ? Guid.NewGuid() : null, null,
            Policy.Id, "Synthetic historical use", "synthetic-use", Actor.AccountId.ToString(), Clock.Now); Uses.Add(row);
        if (released) Release(row);
        return row;
    }
    public void Release(LeaveLedgerEntry row) => Uses.Add(new(Employee, row.LeaveTypeId, row.LeaveYearStart, row.LeaveYearEnd, LeaveLedgerEntryType.Release, -row.MinutesDelta,
        row.LeaveRequestId, row.Id, Policy.Id, "Synthetic release", "synthetic-release", Actor.AccountId.ToString(), Clock.Now));
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private object? AddBenefit(LeaveLedgerEntry entry, AuditEvent audit) { Rows.Add(new(entry, audit)); Audits.Add(audit); return null; }
    private Task<PrivateFileReference> WriteFile(byte[] bytes, string purpose)
    {
        var key = Guid.NewGuid().ToString("N"); Files.Add(key, (bytes.ToArray(), purpose));
        if (purpose.StartsWith("leave:benefit:", StringComparison.Ordinal)) { var callback = AfterBenefitWrite; AfterBenefitWrite = null; callback?.Invoke(); }
        if (AfterPrivateWrite.Remove(purpose, out var afterWrite)) afterWrite();
        return Task.FromResult(new PrivateFileReference(key, "synthetic", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    private Task<byte[]> ReadFile(PrivateFileReference reference, string purpose)
    {
        if (!Files.TryGetValue(reference.StorageKey, out var saved) || saved.Purpose != purpose) throw AttendanceOperation.Conflict("private_evidence_unavailable");
        var bytes = saved.Bytes.ToArray();
        if (AfterPrivateRead.Remove(purpose, out var afterRead)) afterRead();
        return Task.FromResult(bytes);
    }
}
